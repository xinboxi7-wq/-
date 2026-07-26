#!/usr/bin/env python3
"""Generate the Xbox DPadUp contour directly from Assets/controller.png.

This is a development-only extractor.  It never runs inside the WPF program.
The stage mapping matches XboxRegionManager exactly: source 1586x992 is
uniformly placed into a 1536x1024 logical stage with a 32px top gutter.

The four bands are search ROIs, not hand-authored Geometry.  A dynamic-program
selects the strongest continuous image edge in each band, then writes the
detected contour as reversible move/line/close commands.
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path

import cv2
import numpy as np

SOURCE_WIDTH = 1586
SOURCE_HEIGHT = 992
LOGICAL_WIDTH = 1536
SOURCE_SCALE = LOGICAL_WIDTH / SOURCE_WIDTH
SOURCE_TOP = 32.0


def stage_point(raw_point: np.ndarray) -> list[float]:
    return [round(float(raw_point[0]) * SOURCE_SCALE, 3), round(float(raw_point[1]) * SOURCE_SCALE + SOURCE_TOP, 3)]


def best_horizontal(magnitude: np.ndarray, x0: int, x1: int, y0: int, y1: int, preferred_y: float) -> np.ndarray:
    """Find one smooth high-gradient y coordinate for every x in the band."""
    width = x1 - x0 + 1
    height = y1 - y0 + 1
    local = magnitude[y0 : y1 + 1, x0 : x1 + 1].astype(np.float32)
    local /= max(1.0, float(local.max()))
    y_values = np.arange(y0, y1 + 1, dtype=np.float32)
    # Target guidance only resolves double edges; image magnitude remains dominant.
    target_cost = 0.007 * np.square(y_values - preferred_y)
    score = local - target_cost[:, None]
    cost = np.full((height, width), -1e9, dtype=np.float32)
    back = np.zeros((height, width), dtype=np.int16)
    cost[:, 0] = score[:, 0]
    for x in range(1, width):
        for y in range(height):
            low, high = max(0, y - 3), min(height, y + 4)
            candidates = cost[low:high, x - 1] - 0.12 * np.square(np.arange(low, high) - y)
            best = int(np.argmax(candidates)) + low
            cost[y, x] = score[y, x] + candidates[best - low]
            back[y, x] = best
    y = int(np.argmax(cost[:, -1]))
    result = np.zeros((width, 2), dtype=np.float32)
    for x in range(width - 1, -1, -1):
        result[x] = (x0 + x, y0 + y)
        if x:
            y = int(back[y, x])
    return result


def best_vertical(magnitude: np.ndarray, x0: int, x1: int, y0: int, y1: int, preferred_x: float) -> np.ndarray:
    """Find one smooth high-gradient x coordinate for every y in the band."""
    # Transpose and reuse the horizontal dynamic program.
    transposed = magnitude.T
    path = best_horizontal(transposed, y0, y1, x0, x1, preferred_x)
    return np.column_stack((path[:, 1], path[:, 0])).astype(np.float32)


def rdp(points: np.ndarray, epsilon: float) -> np.ndarray:
    """Small deterministic Ramer-Douglas-Peucker simplifier for a closed path."""
    if len(points) < 3:
        return points
    start, end = points[0], points[-1]
    line = end - start
    length = float(np.linalg.norm(line))
    if length < 1e-6:
        distances = np.linalg.norm(points - start, axis=1)
    else:
        offsets = points - start
        distances = np.abs(line[0] * offsets[:, 1] - line[1] * offsets[:, 0]) / length
    index = int(np.argmax(distances))
    if distances[index] <= epsilon:
        return np.vstack((start, end))
    return np.vstack((rdp(points[: index + 1], epsilon)[:-1], rdp(points[index:], epsilon)))


def extract_up(image: np.ndarray) -> tuple[np.ndarray, np.ndarray]:
    gray = cv2.cvtColor(image, cv2.COLOR_BGR2GRAY)
    blurred = cv2.GaussianBlur(gray, (3, 3), 0)
    gx = cv2.Scharr(blurred, cv2.CV_32F, 1, 0)
    gy = cv2.Scharr(blurred, cv2.CV_32F, 0, 1)
    magnitude = cv2.magnitude(gx, gy)

    # All coordinates are source-pixel ROI bands around DPadUp. They cover only
    # the expected physical seam neighbourhood, not a guessed button polygon.
    # These bands deliberately exclude the outer DPad bezel/highlight. They
    # bracket the dark plastic separation line on the key itself.
    top = best_horizontal(magnitude, 598, 659, 398, 428, 408)
    right = best_vertical(magnitude, 653, 663, 421, 472, 657)
    bottom = best_horizontal(magnitude, 597, 660, 461, 480, 470)
    left = best_vertical(magnitude, 596, 606, 421, 472, 599)

    def join_horizontal(path: np.ndarray, start: np.ndarray, end: np.ndarray) -> np.ndarray:
        """Keep only the image edge between the independently detected sides."""
        low, high = sorted((float(start[0]), float(end[0])))
        trimmed = path[(path[:, 0] >= low) & (path[:, 0] <= high)]
        if len(trimmed) < 2:
            trimmed = np.vstack((start, end))
        else:
            trimmed[0] = start
            trimmed[-1] = end
        return trimmed

    top = join_horizontal(top, left[0], right[0])
    bottom = join_horizontal(bottom, left[-1], right[-1])

    # Build clockwise, then simplify the detected polyline while retaining its
    # four independently found corners and the image-derived curvature.
    contour = np.vstack((left[::-1], top, right, bottom[::-1]))
    dedup = [contour[0]]
    for point in contour[1:]:
        if np.linalg.norm(point - dedup[-1]) > 0.25:
            dedup.append(point)
    contour = np.asarray(dedup, dtype=np.float32)
    closed = np.vstack((contour, contour[0]))
    simplified = rdp(closed, 0.65)[:-1]
    return contour, simplified


def commands_from_contour(contour: np.ndarray) -> list[dict]:
    commands = []
    for index, point in enumerate(contour):
        x, y = stage_point(point)
        commands.append({"op": "move" if index == 0 else "line", "x": x, "y": y})
    commands.append({"op": "close"})
    return commands


def render_audit(image: np.ndarray, raw_contour: np.ndarray, simplified: np.ndarray, output: Path) -> None:
    view = image.copy()
    all_points = np.round(raw_contour).astype(np.int32).reshape((-1, 1, 2))
    simple_points = np.round(simplified).astype(np.int32).reshape((-1, 1, 2))
    cv2.polylines(view, [all_points], True, (0, 80, 255), 1, cv2.LINE_AA)       # detected edge
    cv2.polylines(view, [simple_points], True, (70, 255, 70), 1, cv2.LINE_AA)  # generated overlay
    for point in simplified:
        cv2.circle(view, tuple(np.round(point).astype(int)), 2, (70, 255, 255), -1, cv2.LINE_AA)
    cv2.imwrite(str(output), view)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--image", default="Assets/controller.png")
    parser.add_argument("--output", default="Tools/GenerateXboxDPadRegions/xboxDpadUp.generated.json")
    parser.add_argument("--audit", default="Tools/GenerateXboxDPadRegions/xboxDpadUp.extraction-audit.png")
    args = parser.parse_args()
    image = cv2.imread(args.image, cv2.IMREAD_COLOR)
    if image is None or image.shape[1] != SOURCE_WIDTH or image.shape[0] != SOURCE_HEIGHT:
        raise SystemExit("Expected the unmodified 1586x992 Assets/controller.png source image.")
    contour, simplified = extract_up(image)
    output = Path(args.output)
    output.parent.mkdir(parents=True, exist_ok=True)
    payload = {
        "sourceImage": "controller.png",
        "sourceImageSize": [SOURCE_WIDTH, SOURCE_HEIGHT],
        "logicalStage": [1536, 1024],
        "sourceScale": SOURCE_SCALE,
        "sourceTop": SOURCE_TOP,
        "region": "dpad-up",
        "extraction": "scharr-gradient + continuous seam paths + RDP(0.65px)",
        "rawContourPointCount": int(len(contour)),
        "pathCommands": commands_from_contour(simplified),
    }
    output.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")
    render_audit(image, contour, simplified, Path(args.audit))
    print(json.dumps({"output": str(output), "audit": str(args.audit), "rawPoints": len(contour), "pathPoints": len(simplified)}, ensure_ascii=False))


if __name__ == "__main__":
    main()
