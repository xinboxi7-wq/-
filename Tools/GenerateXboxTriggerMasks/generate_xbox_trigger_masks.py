"""Extract the photographed Xbox LT/RT silhouettes into alpha-mask assets.

Development-only.  The WPF application consumes the resulting PNG masks and
does not reconstruct a trigger outline from Bezier or box geometry.
"""
import argparse
import json
from pathlib import Path

import cv2
import numpy as np


RAW_WIDTH = 1586
RAW_HEIGHT = 992

# Search limits only; the mask contour itself is extracted from image edges.
ROIS = {
    "lt": (340, 40, 610, 195),
    "rt": (965, 40, 1235, 195),
}


def contour_score(contour, roi_width, roi_height):
    area = cv2.contourArea(contour)
    if area < roi_width * roi_height * 0.012:
        return -1e9
    x, y, width, height = cv2.boundingRect(contour)
    extent = area / max(1.0, width * height)
    aspect = width / max(1.0, height)
    if height < 24 or aspect < 1.4:
        return -1e9
    return area * (0.75 + extent) + min(5.0, aspect) * 165.0


TRACE_GUIDES = {
    "lt": {"top": (34, 105, 240, 22), "bottom": (34, 134, 240, 93)},
    "rt": {"top": (16, 22, 240, 120), "bottom": (16, 95, 240, 151)},
}


def trace_edge(strength, guide):
    start_x, start_y, end_x, end_y = guide
    xs = np.arange(start_x, end_x + 1)
    guide_y = np.linspace(start_y, end_y, len(xs))
    band = 18
    offsets = np.arange(-band, band + 1)
    candidate_y = np.clip(np.rint(guide_y[:, None] + offsets[None, :]).astype(np.int32), 0, strength.shape[0] - 1)
    emission = -strength[candidate_y, xs[:, None]].astype(np.float32) / 52.0
    emission += 0.62 * np.abs(offsets[None, :])
    costs = np.full((len(xs), len(offsets)), np.inf, np.float32)
    parents = np.zeros((len(xs), len(offsets)), np.int16)
    costs[0] = emission[0]
    for index in range(1, len(xs)):
        for current in range(len(offsets)):
            low = max(0, current - 4)
            high = min(len(offsets), current + 5)
            previous = costs[index - 1, low:high] + 1.05 * (offsets[low:high] - offsets[current]) ** 2
            choice = int(np.argmin(previous)) + low
            costs[index, current] = emission[index, current] + previous[choice - low]
            parents[index, current] = choice
    selected = np.zeros(len(xs), np.int16)
    selected[-1] = int(np.argmin(costs[-1]))
    for index in range(len(xs) - 1, 0, -1):
        selected[index - 1] = parents[index, selected[index]]
    return np.column_stack((xs, candidate_y[np.arange(len(xs)), selected]))


def trace_silhouette(gray, region_id):
    sobel_x = cv2.Sobel(gray, cv2.CV_16S, 1, 0, ksize=3)
    sobel_y = cv2.Sobel(gray, cv2.CV_16S, 0, 1, ksize=3)
    strength = cv2.GaussianBlur(cv2.convertScaleAbs(np.abs(sobel_x) + np.abs(sobel_y)), (3, 3), 0)
    guides = TRACE_GUIDES[region_id]
    top = trace_edge(strength, guides["top"])
    bottom = trace_edge(strength, guides["bottom"])
    polygon = np.vstack((top, bottom[::-1])).astype(np.int32)
    alpha = np.zeros(gray.shape, np.uint8)
    cv2.fillPoly(alpha, [polygon], 255, lineType=cv2.LINE_AA)
    return cv2.GaussianBlur(alpha, (3, 3), 0), polygon


def extract_alpha(image, region_id, roi):
    x0, y0, x1, y1 = roi
    crop = image[y0:y1, x0:x1]
    gray = cv2.cvtColor(crop, cv2.COLOR_BGR2GRAY)
    smooth = cv2.GaussianBlur(gray, (3, 3), 0)
    edges = cv2.Canny(smooth, 24, 76, L2gradient=True)
    edges = cv2.morphologyEx(edges, cv2.MORPH_CLOSE, np.ones((3, 3), np.uint8), iterations=2)
    # Trace upper/lower physical seams from Sobel/Canny evidence and rasterize
    # the resulting matte.  The output is an alpha image, not a runtime path.
    local, polygon = trace_silhouette(gray, region_id)
    keep = np.where(local > 32, 255, 0).astype(np.uint8)

    alpha = np.zeros(image.shape[:2], np.uint8)
    alpha[y0:y1, x0:x1] = local
    return alpha, edges, polygon.reshape(-1, 1, 2) + np.array([[[x0, y0]]], dtype=polygon.dtype)


def write_alpha_png(path, alpha):
    rgba = np.zeros((alpha.shape[0], alpha.shape[1], 4), np.uint8)
    rgba[:, :, :3] = 255
    rgba[:, :, 3] = alpha
    cv2.imwrite(str(path), rgba)


def stage_bounds(alpha):
    ys, xs = np.where(alpha > 32)
    if len(xs) == 0:
        return None
    scale = 1536.0 / RAW_WIDTH
    return {
        "raw": {"x": int(xs.min()), "y": int(ys.min()), "width": int(xs.max() - xs.min() + 1), "height": int(ys.max() - ys.min() + 1)},
        "stage": {
            "x": round(float(xs.min()) * scale, 3),
            "y": round(float(ys.min()) * scale + 32.0, 3),
            "width": round(float(xs.max() - xs.min() + 1) * scale, 3),
            "height": round(float(ys.max() - ys.min() + 1) * scale, 3),
        },
    }


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--image", required=True)
    parser.add_argument("--assets", required=True)
    parser.add_argument("--audit", required=True)
    args = parser.parse_args()

    image = cv2.imread(args.image, cv2.IMREAD_COLOR)
    if image is None or image.shape[:2] != (RAW_HEIGHT, RAW_WIDTH):
        raise SystemExit("Expected original controller.png at 1586x992")

    assets = Path(args.assets)
    audit = Path(args.audit)
    assets.mkdir(parents=True, exist_ok=True)
    audit.mkdir(parents=True, exist_ok=True)
    report = {"sourceImage": Path(args.image).name, "sourceSize": [RAW_WIDTH, RAW_HEIGHT], "masks": {}}
    for name, roi in ROIS.items():
        alpha, edges, contour = extract_alpha(image, name, roi)
        edge_alpha = cv2.subtract(cv2.dilate(alpha, np.ones((5, 5), np.uint8), iterations=1), cv2.erode(alpha, np.ones((3, 3), np.uint8), iterations=1))
        write_alpha_png(assets / f"xbox_{name}_mask.png", alpha)
        write_alpha_png(assets / f"xbox_{name}_mask_edge.png", edge_alpha)

        x0, y0, x1, y1 = roi
        crop = image[y0:y1, x0:x1].copy()
        tinted = crop.copy()
        color = (74, 220, 74) if name == "lt" else (235, 150, 30)
        overlay = alpha[y0:y1, x0:x1] > 32
        tinted[overlay] = (0.70 * tinted[overlay] + 0.30 * np.array(color)).astype(np.uint8)
        cv2.drawContours(tinted, [contour - np.array([[[x0, y0]]], dtype=contour.dtype)], -1, (255, 90, 220), 1, cv2.LINE_AA)
        cv2.imwrite(str(audit / f"{name}-mask-overlay.png"), cv2.resize(tinted, None, fx=3, fy=3, interpolation=cv2.INTER_NEAREST))
        cv2.imwrite(str(audit / f"{name}-edges.png"), cv2.resize(edges, None, fx=3, fy=3, interpolation=cv2.INTER_NEAREST))
        report["masks"][name] = {"roi": list(roi), "bounds": stage_bounds(alpha)}

    (audit / "mask-report.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps(report, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
