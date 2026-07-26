"""Measure the current LT Bezier against nearby source-image edges.

Development-only audit helper. It never edits xboxRegions.json. Coordinates
are converted from the shared 1536x1024 stage back to controller.png pixels.
"""
import json
import math
from pathlib import Path

import cv2
import numpy as np


ROOT = Path(__file__).resolve().parents[2]
IMAGE = ROOT / "Assets" / "controller.png"
REGIONS = ROOT / "Assets" / "xboxRegions.json"
OUTPUT = ROOT / "audit" / "xbox-trigger-geometry" / "bezier-edge-audit"
RAW_WIDTH = 1586
STAGE_WIDTH = 1536.0
SCALE = STAGE_WIDTH / RAW_WIDTH
TOP = 32.0


def stage_to_raw(point):
    return np.array([point[0] / SCALE, (point[1] - TOP) / SCALE], dtype=np.float64)


def cubic(p0, c1, c2, p1, t):
    u = 1.0 - t
    return u**3 * p0 + 3.0 * u * u * t * c1 + 3.0 * u * t * t * c2 + t**3 * p1


def cubic_tangent(p0, c1, c2, p1, t):
    u = 1.0 - t
    return 3.0 * u * u * (c1 - p0) + 6.0 * u * t * (c2 - c1) + 3.0 * t * t * (p1 - c2)


def nearest_edge(edges, point, normal, radius=8.0):
    best = None
    best_distance = 1e9
    # Search mainly across the curve normal. A narrow tangent allowance helps
    # bridge anti-aliased and partially occluded source pixels.
    tangent = np.array([-normal[1], normal[0]])
    for normal_distance in np.arange(-radius, radius + 0.01, 0.25):
        for tangent_distance in (-1.5, -0.75, 0.0, 0.75, 1.5):
            candidate = point + normal * normal_distance + tangent * tangent_distance
            x, y = int(round(candidate[0])), int(round(candidate[1]))
            if 0 <= y < edges.shape[0] and 0 <= x < edges.shape[1] and edges[y, x]:
                distance = abs(normal_distance) + abs(tangent_distance) * 0.15
                if distance < best_distance:
                    best_distance = distance
                    best = np.array([x, y], dtype=np.float64)
    return best


def adjusted_commands(source, adjustments):
    result = json.loads(json.dumps(source))
    for adjustment in adjustments or []:
        command = result[adjustment["commandIndex"]]
        role = adjustment.get("role", "P").lower()
        prefix = "" if role == "p" else role
        command[f"{prefix}x" if prefix else "x"] += adjustment["dx"]
        command[f"{prefix}y" if prefix else "y"] += adjustment["dy"]
    return result


def transform_point(point, mirror=False, offset_x=0.0, offset_y=0.0):
    transformed = point.copy()
    if mirror:
        transformed[0] = STAGE_WIDTH - transformed[0]
    transformed[0] += offset_x
    transformed[1] += offset_y
    return transformed


def analyze(image, edges, region_id, commands, mirror, offset_x, offset_y, crop_rect):
    previous = np.array([commands[0]["x"], commands[0]["y"]], dtype=np.float64)
    samples = []
    segment_reports = []
    for index, command in enumerate(commands[1:], start=1):
        if command["op"] != "cubic":
            continue
        endpoint = np.array([command["x"], command["y"]], dtype=np.float64)
        c1 = np.array([command["c1x"], command["c1y"]], dtype=np.float64)
        c2 = np.array([command["c2x"], command["c2y"]], dtype=np.float64)
        distances = []
        vectors = []
        for t in np.linspace(0.03, 0.97, 40):
            point_stage = transform_point(cubic(previous, c1, c2, endpoint, t), mirror, offset_x, offset_y)
            tangent_stage = cubic_tangent(previous, c1, c2, endpoint, t)
            if mirror:
                tangent_stage[0] *= -1.0
            point = stage_to_raw(point_stage)
            tangent = np.array([tangent_stage[0], tangent_stage[1]], dtype=np.float64)
            tangent_length = np.linalg.norm(tangent)
            if tangent_length < 1e-6:
                continue
            tangent /= tangent_length
            normal = np.array([-tangent[1], tangent[0]])
            edge = nearest_edge(edges, point, normal)
            if edge is None:
                continue
            vector = edge - point
            signed = float(np.dot(vector, normal))
            distances.append(abs(signed) * SCALE)
            vectors.append(vector * SCALE)
            samples.append((index, point, edge))
        if distances:
            mean_vector = np.mean(np.array(vectors), axis=0)
            segment_reports.append(
                {
                    "segment": index,
                    "meanDistanceLogicalPx": round(float(np.mean(distances)), 3),
                    "maxDistanceLogicalPx": round(float(np.max(distances)), 3),
                    "meanDxLogicalPx": round(float(mean_vector[0]), 3),
                    "meanDyLogicalPx": round(float(mean_vector[1]), 3),
                    "samples": len(distances),
                }
            )
        previous = endpoint

    x0, y0, x1, y1 = crop_rect
    overlay = image[y0:y1, x0:x1].copy()
    for _, point, edge in samples:
        p = tuple(np.round(point - [x0, y0]).astype(int))
        e = tuple(np.round(edge - [x0, y0]).astype(int))
        cv2.line(overlay, p, e, (0, 210, 255), 1, cv2.LINE_AA)
        cv2.circle(overlay, p, 1, (255, 0, 255), -1, cv2.LINE_AA)
        cv2.circle(overlay, e, 1, (0, 255, 255), -1, cv2.LINE_AA)

    lower_targets = []
    for segment, _, edge in samples:
        if segment in (5, 6):
            lower_targets.append([edge[0] * SCALE, edge[1] * SCALE + TOP])
    if len(lower_targets) >= 12:
        lower_targets = np.asarray(lower_targets, dtype=np.float64)
        keep = np.ones(len(lower_targets), dtype=bool)
        coefficients = None
        for _ in range(4):
            coefficients = np.polyfit(lower_targets[keep, 0], lower_targets[keep, 1], 3)
            residual = np.abs(np.polyval(coefficients, lower_targets[:, 0]) - lower_targets[:, 1])
            keep = residual < max(1.8, float(np.percentile(residual, 70)))
        report_points = {}
        for x in (375.768, 437.750, 500.0, 567.300):
            display_x = STAGE_WIDTH - x + offset_x if mirror else x
            report_points[f"x={display_x:.3f}"] = round(float(np.polyval(coefficients, display_x)), 3)
        derivative = np.polyder(coefficients)
        slopes = {}
        for x in (375.768, 437.750, 500.0, 567.300):
            display_x = STAGE_WIDTH - x + offset_x if mirror else x
            slopes[f"x={display_x:.3f}"] = round(float(np.polyval(derivative, display_x)), 4)
        print(region_id.upper(), "ROBUST_LOWER_EDGE", report_points, "slopes", slopes, "inliers", int(keep.sum()))

    cv2.imwrite(str(OUTPUT / f"{region_id}-nearest-edge-overlay.png"), cv2.resize(overlay, None, fx=4, fy=4, interpolation=cv2.INTER_NEAREST))
    (OUTPUT / f"{region_id}-edge-report.json").write_text(
        json.dumps(segment_reports, ensure_ascii=False, indent=2), encoding="utf-8"
    )
    print(region_id.upper())
    print(json.dumps(segment_reports, ensure_ascii=False, indent=2))


def main():
    image = cv2.imread(str(IMAGE), cv2.IMREAD_COLOR)
    document = json.loads(REGIONS.read_text(encoding="utf-8"))
    lt = next(region for region in document["regions"] if region["id"] == "lt")
    rt = next(region for region in document["regions"] if region["id"] == "rt")

    gray = cv2.cvtColor(image, cv2.COLOR_BGR2GRAY)
    gray = cv2.GaussianBlur(gray, (3, 3), 0)
    edges = cv2.Canny(gray, 24, 72, L2gradient=True)
    OUTPUT.mkdir(parents=True, exist_ok=True)

    analyze(image, edges, "lt", lt["pathCommands"], False, 0.0, 0.0, (340, 40, 590, 180))
    analyze(
        image,
        edges,
        "rt",
        adjusted_commands(lt["pathCommands"], rt.get("pathPointAdjustments")),
        True,
        rt.get("offsetX", 0.0),
        rt.get("offsetY", 0.0),
        (990, 40, 1245, 190),
    )


if __name__ == "__main__":
    main()
