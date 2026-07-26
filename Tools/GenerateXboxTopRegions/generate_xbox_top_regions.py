"""Generate calibration evidence for Xbox LB/RB/LT/RT from controller.png.

This is a development-only tool.  It keeps image analysis out of the WPF
runtime and emits candidates/audits in the same 1536x1024 logical coordinate
space used by XboxRegions.json.  The WPF calibration window consumes the same
edge-distance principle for its transform recommendation.
"""
import argparse
import json
from pathlib import Path

import cv2
import numpy as np

RAW_WIDTH = 1586
LOGICAL_WIDTH = 1536.0
SOURCE_SCALE = LOGICAL_WIDTH / RAW_WIDTH
SOURCE_TOP = 32.0

# ROIs are only search limits. They are deliberately wider than the final
# paths because the photographed top controls are partly occluded by the shell.
ROIS = {
    "lb": (390, 70, 585, 160),
    "lt": (380, 42, 585, 168),
    "rb": (960, 70, 1140, 170),
    "rt": (945, 42, 1145, 180),
}


def stage_point(x, y):
    return {"x": round(x * SOURCE_SCALE, 3), "y": round(y * SOURCE_SCALE + SOURCE_TOP, 3)}


def contour_score(contour, w, h):
    area = cv2.contourArea(contour)
    if area < max(35, w * h * 0.008):
        return -1e9
    x, y, cw, ch = cv2.boundingRect(contour)
    extent = area / max(1.0, cw * ch)
    # Prefer a broad, shallow top-control contour instead of texture speckles.
    aspect = cw / max(1.0, ch)
    return area * (0.6 + extent) + min(4.0, aspect) * 120.0


def commands_from_contour(contour, left, top):
    epsilon = max(1.3, cv2.arcLength(contour, True) * 0.018)
    polygon = cv2.approxPolyDP(contour, epsilon, True).reshape(-1, 2)
    if len(polygon) < 3:
        return []
    commands = [{"op": "move", **stage_point(left + int(polygon[0][0]), top + int(polygon[0][1]))}]
    for point in polygon[1:]:
        commands.append({"op": "line", **stage_point(left + int(point[0]), top + int(point[1]))})
    commands.append({"op": "close"})
    return commands


def extract(image, region_id, roi):
    x0, y0, x1, y1 = roi
    crop = image[y0:y1, x0:x1]
    gray = cv2.cvtColor(crop, cv2.COLOR_BGR2GRAY)
    blurred = cv2.GaussianBlur(gray, (3, 3), 0)
    edges = cv2.Canny(blurred, 28, 92, L2gradient=True)
    edges = cv2.morphologyEx(edges, cv2.MORPH_CLOSE, np.ones((3, 3), np.uint8), iterations=1)
    contours, _ = cv2.findContours(edges, cv2.RETR_LIST, cv2.CHAIN_APPROX_NONE)
    best = max(contours, key=lambda c: contour_score(c, crop.shape[1], crop.shape[0]), default=None)
    overlay = crop.copy()
    if best is not None:
        cv2.drawContours(overlay, [best], -1, (0, 255, 255), 1, cv2.LINE_AA)
    cv2.putText(overlay, region_id, (6, 16), cv2.FONT_HERSHEY_SIMPLEX, 0.5, (0, 255, 255), 1, cv2.LINE_AA)
    return edges, overlay, commands_from_contour(best, x0, y0) if best is not None else []


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--image", required=True)
    parser.add_argument("--output", required=True)
    args = parser.parse_args()
    image = cv2.imread(args.image, cv2.IMREAD_COLOR)
    if image is None:
        raise SystemExit("could not read image")
    if image.shape[1] != RAW_WIDTH:
        raise SystemExit("expected original controller.png width 1586")
    out = Path(args.output)
    out.mkdir(parents=True, exist_ok=True)
    regions = []
    for region_id, roi in ROIS.items():
        edges, overlay, commands = extract(image, region_id, roi)
        cv2.imwrite(str(out / f"{region_id}-edges.png"), edges)
        cv2.imwrite(str(out / f"{region_id}-candidate.png"), overlay)
        regions.append({"id": region_id, "sourceRoi": list(roi), "pathCommands": commands})
    document = {
        "sourceImage": Path(args.image).name,
        "sourceImageWidth": int(image.shape[1]),
        "sourceImageHeight": int(image.shape[0]),
        "logicalWidth": 1536,
        "logicalHeight": 1024,
        "sourceScale": SOURCE_SCALE,
        "sourceTop": SOURCE_TOP,
        "regions": regions,
        "note": "Candidates are image-derived edge evidence. Review/merge only after visual calibration because shell occlusion can leave partial contours."
    }
    (out / "xboxTopRegions.generated.json").write_text(json.dumps(document, ensure_ascii=False, indent=2), encoding="utf-8")


if __name__ == "__main__":
    main()
