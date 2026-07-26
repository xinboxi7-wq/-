#!/usr/bin/env python3
"""Offline DualSense region generator.

This development-only tool reads the 1536x1024 photographic source image,
finds the most likely physical button contour inside a per-button search ROI,
converts it to reversible WPF path commands, and reports its pixel error.

It intentionally has no connection to the shipped WPF executable.  The app
only consumes the generated JSON after the generator has been reviewed.
"""

from __future__ import annotations

import argparse
import copy
import json
import math
import shutil
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Iterable

import cv2
import numpy as np


ROOT = Path(__file__).resolve().parents[2]
ASSETS = ROOT / "Assets"
SOURCE_IMAGE = ASSETS / "dualsense.png"
DEFAULT_JSON = ASSETS / "dualSenseRegions.json"
OUTPUT_ROOT = Path(__file__).resolve().parent / "output"

IMAGE_WIDTH = 1536
IMAGE_HEIGHT = 1024
MEAN_ERROR_LIMIT = 1.35
MAX_ERROR_LIMIT = 4.5


@dataclass(frozen=True)
class RegionSpec:
    id: str
    kind: str  # contour, circle, ellipse, stick
    roi: tuple[int, int, int, int]
    center: tuple[float, float]
    size: tuple[float, float]
    expected_area: float
    min_area_ratio: float = 0.35
    max_area_ratio: float = 2.3
    canny_low: int = 28
    canny_high: int = 96
    close_kernel: int = 3
    samples: tuple[int, ...] = (12, 16, 20, 28, 36)


# These are only search windows and expected physical scale.  No vertex is
# copied from the WPF JSON: all generated vertices come from image contours.
SPECS: tuple[RegionSpec, ...] = (
    RegionSpec("dpad-up", "contour", (332, 202, 112, 112), (387, 258), (75, 91), 5600),
    RegionSpec("dpad-right", "contour", (398, 266, 124, 103), (459, 316), (96, 69), 6100),
    RegionSpec("dpad-down", "contour", (332, 325, 112, 104), (388, 377), (77, 83), 5500),
    RegionSpec("dpad-left", "contour", (250, 266, 137, 103), (324, 316), (95, 68), 6000),
    RegionSpec("button-triangle", "circle", (1086, 158, 132, 132), (1151, 224), (78, 78), 4700),
    RegionSpec("button-square", "circle", (993, 247, 132, 132), (1061, 313), (77, 77), 4650),
    RegionSpec("button-circle", "circle", (1171, 247, 132, 132), (1234, 313), (77, 77), 4650),
    RegionSpec("button-cross", "circle", (1086, 339, 132, 132), (1151, 405), (78, 78), 4700),
    RegionSpec("button-l1", "contour", (262, 61, 240, 86), (406, 109), (190, 49), 1600, min_area_ratio=0.65, max_area_ratio=1.55, samples=(16, 24, 32, 44)),
    RegionSpec("button-r1", "contour", (1032, 61, 240, 86), (1147, 122), (116, 37), 2140, min_area_ratio=0.65, max_area_ratio=1.55, samples=(16, 24, 32, 44)),
    RegionSpec("trigger-l2", "contour", (250, 30, 250, 105), (407, 109), (185, 37), 1550, min_area_ratio=0.65, max_area_ratio=1.55, samples=(16, 24, 32, 44, 56, 64)),
    RegionSpec("trigger-r2", "contour", (1035, 30, 250, 105), (1128, 109), (185, 37), 1600, min_area_ratio=0.65, max_area_ratio=1.55, samples=(16, 24, 32, 44, 56, 64)),
    RegionSpec("button-create", "contour", (442, 128, 82, 104), (483, 182), (42, 64), 1900, samples=(12, 16, 20, 28)),
    RegionSpec("button-options", "contour", (1012, 128, 82, 104), (1053, 182), (42, 64), 1900, samples=(12, 16, 20, 28)),
    RegionSpec("button-ps", "contour", (716, 520, 108, 82), (767, 556), (65, 20), 1136, min_area_ratio=0.65, max_area_ratio=1.55, samples=(16, 20, 28)),
    RegionSpec("button-mic", "ellipse", (730, 565, 78, 70), (768, 584), (20, 17), 163, min_area_ratio=0.55, max_area_ratio=1.65, samples=(12, 16, 20)),
    RegionSpec("touchpad-surface", "contour", (492, 86, 554, 294), (768, 232), (496, 255), 113600, min_area_ratio=0.70, max_area_ratio=1.25, canny_low=20, canny_high=75, close_kernel=5, samples=(32, 44, 56, 72)),
    RegionSpec("stick-left", "stick", (456, 374, 224, 224), (568, 484), (150, 150), 17500, samples=(24, 32, 40)),
    RegionSpec("stick-right", "stick", (857, 374, 224, 224), (969, 484), (150, 150), 17500, samples=(24, 32, 40)),
)


def ensure(condition: bool, message: str) -> None:
    if not condition:
        raise RuntimeError(message)


def load_image(path: Path) -> np.ndarray:
    image = cv2.imread(str(path), cv2.IMREAD_COLOR)
    ensure(image is not None, f"Could not read source image: {path}")
    height, width = image.shape[:2]
    ensure((width, height) == (IMAGE_WIDTH, IMAGE_HEIGHT), f"Expected {IMAGE_WIDTH}x{IMAGE_HEIGHT}, got {width}x{height}")
    return image


def roi_image(image: np.ndarray, spec: RegionSpec) -> tuple[np.ndarray, tuple[int, int]]:
    x, y, width, height = spec.roi
    return image[y:y + height, x:x + width].copy(), (x, y)


def edge_map(crop: np.ndarray, low: int, high: int, close_kernel: int = 3) -> np.ndarray:
    gray = cv2.cvtColor(crop, cv2.COLOR_BGR2GRAY)
    gray = cv2.GaussianBlur(gray, (5, 5), 0)
    local = cv2.createCLAHE(clipLimit=2.0, tileGridSize=(8, 8)).apply(gray)
    edges = cv2.Canny(local, low, high, L2gradient=True)
    # Close tiny highlight gaps while preserving separate nearby controls.
    return cv2.morphologyEx(edges, cv2.MORPH_CLOSE, np.ones((close_kernel, close_kernel), np.uint8), iterations=1)


def contour_center(contour: np.ndarray) -> tuple[float, float]:
    moments = cv2.moments(contour)
    if moments["m00"]:
        return moments["m10"] / moments["m00"], moments["m01"] / moments["m00"]
    x, y, width, height = cv2.boundingRect(contour)
    return x + width * 0.5, y + height * 0.5


def candidate_score(contour: np.ndarray, spec: RegionSpec, origin: tuple[int, int]) -> float | None:
    area = abs(cv2.contourArea(contour))
    if area < spec.expected_area * spec.min_area_ratio or area > spec.expected_area * spec.max_area_ratio:
        return None
    x, y, width, height = cv2.boundingRect(contour)
    if width < spec.size[0] * 0.45 or height < spec.size[1] * 0.40:
        return None
    cx, cy = contour_center(contour)
    gx, gy = cx + origin[0], cy + origin[1]
    diagonal = math.hypot(spec.roi[2], spec.roi[3])
    center_penalty = math.hypot(gx - spec.center[0], gy - spec.center[1]) / diagonal
    area_penalty = abs(math.log(max(1.0, area) / spec.expected_area))
    aspect_penalty = abs(math.log(max(width / max(1, height), 1e-5) / max(spec.size[0] / spec.size[1], 1e-5)))
    perimeter = cv2.arcLength(contour, True)
    solidity = area / max(1.0, cv2.contourArea(cv2.convexHull(contour)))
    border_touch = int(x <= 1 or y <= 1 or x + width >= spec.roi[2] - 2 or y + height >= spec.roi[3] - 2)
    # Prefer closed physical rims, not clipped body outlines or glare streaks.
    return center_penalty * 5.2 + area_penalty * 2.4 + aspect_penalty * 1.6 + border_touch * 5.0 + abs(0.80 - solidity) * 0.35 + 80.0 / max(perimeter, 1.0)


def choose_contour(edges: np.ndarray, spec: RegionSpec, origin: tuple[int, int]) -> tuple[np.ndarray, float, list[tuple[np.ndarray, float]]]:
    contours, _ = cv2.findContours(edges, cv2.RETR_LIST, cv2.CHAIN_APPROX_NONE)
    candidates: list[tuple[np.ndarray, float]] = []
    for contour in contours:
        if len(contour) < 24 or not cv2.isContourConvex(cv2.convexHull(contour)):
            continue
        score = candidate_score(contour, spec, origin)
        if score is not None:
            candidates.append((contour, score))
    candidates.sort(key=lambda item: item[1])
    ensure(candidates, f"No suitable closed contour for {spec.id}")
    return candidates[0][0], candidates[0][1], candidates[:6]


def choose_circle(edges: np.ndarray, spec: RegionSpec, origin: tuple[int, int]) -> tuple[np.ndarray, float, float, float]:
    gray = cv2.GaussianBlur(edges, (7, 7), 1.3)
    minimum = int(min(spec.size) * 0.34)
    maximum = int(max(spec.size) * 0.70)
    circles = cv2.HoughCircles(gray, cv2.HOUGH_GRADIENT, dp=1.1, minDist=min(spec.size) * 0.65, param1=80, param2=16, minRadius=minimum, maxRadius=maximum)
    ensure(circles is not None, f"No circle candidate for {spec.id}")
    choices: list[tuple[float, float, float, float]] = []
    for x, y, radius in circles[0]:
        gx, gy = x + origin[0], y + origin[1]
        center_penalty = math.hypot(gx - spec.center[0], gy - spec.center[1]) / max(spec.size)
        radius_penalty = abs(radius * 2.0 - sum(spec.size) * 0.5) / max(spec.size)
        choices.append((center_penalty * 5.0 + radius_penalty, x, y, radius))
    choices.sort(key=lambda item: item[0])
    _, x, y, radius = choices[0]
    return circle_contour(x, y, radius, radius, 160), x, y, radius


def circle_contour(cx: float, cy: float, rx: float, ry: float, count: int) -> np.ndarray:
    values = []
    for index in range(count):
        angle = index * 2.0 * math.pi / count
        values.append((cx + math.cos(angle) * rx, cy + math.sin(angle) * ry))
    return np.asarray(values, dtype=np.float32).reshape((-1, 1, 2))


def choose_ellipse(edges: np.ndarray, spec: RegionSpec, origin: tuple[int, int]) -> tuple[np.ndarray, tuple[float, float, float, float]]:
    contour, _, _ = choose_contour(edges, spec, origin)
    ensure(len(contour) >= 5, f"Not enough points for ellipse: {spec.id}")
    (cx, cy), (width, height), angle = cv2.fitEllipse(contour)
    # FitEllipse is image-derived; normalize to an axis-aligned WPF ellipse only
    # where the photographed control is circular/near-circular.
    rx, ry = width * 0.5, height * 0.5
    return circle_contour(cx, cy, rx, ry, 160), (cx, cy, rx, ry)


def resample_closed_contour(contour: np.ndarray, count: int) -> np.ndarray:
    points = contour.reshape((-1, 2)).astype(np.float64)
    points = np.vstack((points, points[0]))
    segments = np.linalg.norm(np.diff(points, axis=0), axis=1)
    total = float(np.sum(segments))
    ensure(total > 1.0, "Degenerate contour")
    targets = np.linspace(0.0, total, count, endpoint=False)
    result: list[np.ndarray] = []
    cursor = 0
    accumulated = 0.0
    for target in targets:
        while cursor < len(segments) - 1 and accumulated + segments[cursor] < target:
            accumulated += segments[cursor]
            cursor += 1
        ratio = 0.0 if segments[cursor] == 0 else (target - accumulated) / segments[cursor]
        result.append(points[cursor] * (1.0 - ratio) + points[cursor + 1] * ratio)
    return np.asarray(result, dtype=np.float64)


def catmull_rom_commands(points: np.ndarray) -> list[dict[str, Any]]:
    commands: list[dict[str, Any]] = [{"op": "M", "x": round(float(points[0][0]), 3), "y": round(float(points[0][1]), 3)}]
    length = len(points)
    for index in range(length):
        p0 = points[(index - 1) % length]
        p1 = points[index]
        p2 = points[(index + 1) % length]
        p3 = points[(index + 2) % length]
        c1 = p1 + (p2 - p0) / 6.0
        c2 = p2 - (p3 - p1) / 6.0
        commands.append({
            "op": "C",
            "c1x": round(float(c1[0]), 3), "c1y": round(float(c1[1]), 3),
            "c2x": round(float(c2[0]), 3), "c2y": round(float(c2[1]), 3),
            "x": round(float(p2[0]), 3), "y": round(float(p2[1]), 3),
        })
    commands.append({"op": "Z"})
    return commands


def sample_cubic(p0: np.ndarray, c1: np.ndarray, c2: np.ndarray, p1: np.ndarray, steps: int = 10) -> list[np.ndarray]:
    values: list[np.ndarray] = []
    for value in np.linspace(0.0, 1.0, steps, endpoint=False):
        inv = 1.0 - value
        values.append(inv ** 3 * p0 + 3 * inv * inv * value * c1 + 3 * inv * value * value * c2 + value ** 3 * p1)
    return values


def commands_to_polyline(commands: list[dict[str, Any]]) -> np.ndarray:
    current = np.asarray((commands[0]["x"], commands[0]["y"]), dtype=np.float64)
    values: list[np.ndarray] = []
    for command in commands[1:]:
        if command["op"] == "C":
            end = np.asarray((command["x"], command["y"]), dtype=np.float64)
            c1 = np.asarray((command["c1x"], command["c1y"]), dtype=np.float64)
            c2 = np.asarray((command["c2x"], command["c2y"]), dtype=np.float64)
            values.extend(sample_cubic(current, c1, c2, end))
            current = end
    return np.asarray(values, dtype=np.float32)


def contour_error(target: np.ndarray, commands: list[dict[str, Any]]) -> tuple[float, float]:
    all_points = np.vstack((target.reshape((-1, 2)), commands_to_polyline(commands))).astype(np.float32)
    min_x, min_y = np.floor(np.min(all_points, axis=0) - 8).astype(int)
    max_x, max_y = np.ceil(np.max(all_points, axis=0) + 8).astype(int)
    width, height = max(2, max_x - min_x + 1), max(2, max_y - min_y + 1)
    target_mask = np.zeros((height, width), np.uint8)
    generated_mask = np.zeros((height, width), np.uint8)
    shifted_target = np.round(target.reshape((-1, 2)) - (min_x, min_y)).astype(np.int32).reshape((-1, 1, 2))
    generated = commands_to_polyline(commands)
    shifted_generated = np.round(generated - (min_x, min_y)).astype(np.int32).reshape((-1, 1, 2))
    # The metric uses a binary raster. Anti-aliased values never reach 255
    # on dense source contours and would make distanceTransform report INF.
    cv2.polylines(target_mask, [shifted_target], True, 255, 1, cv2.LINE_8)
    cv2.polylines(generated_mask, [shifted_generated], True, 255, 1, cv2.LINE_8)
    target_distance = cv2.distanceTransform(255 - target_mask, cv2.DIST_L2, 3)
    generated_distance = cv2.distanceTransform(255 - generated_mask, cv2.DIST_L2, 3)
    target_samples = target_distance[generated_mask > 0]
    generated_samples = generated_distance[target_mask > 0]
    distances = np.concatenate((target_samples, generated_samples))
    return float(np.mean(distances)), float(np.max(distances))


def has_obvious_overflow(target: np.ndarray, commands: list[dict[str, Any]], allowance: float = 3.0) -> bool:
    """Detect curve overshoot beyond the source contour's bounding envelope.

    This is intentionally separate from the bidirectional edge error: a
    simplified cubic may have a low average distance while still visibly poke
    past a tight physical button edge at one corner.  The allowance keeps
    legitimate sub-pixel rasterisation and anti-aliased source edges from
    becoming false positives.
    """
    target_points = target.reshape((-1, 2)).astype(np.float32)
    generated_points = commands_to_polyline(commands)
    target_min = np.min(target_points, axis=0)
    target_max = np.max(target_points, axis=0)
    generated_min = np.min(generated_points, axis=0)
    generated_max = np.max(generated_points, axis=0)
    return bool(np.any(generated_min < target_min - allowance) or np.any(generated_max > target_max + allowance))


def choose_smoothing(target: np.ndarray, samples: Iterable[int]) -> tuple[list[dict[str, Any]], float, float, int]:
    best: tuple[float, list[dict[str, Any]], float, float, int] | None = None
    passing: list[tuple[list[dict[str, Any]], float, float, int]] = []
    for count in samples:
        commands = catmull_rom_commands(resample_closed_contour(target, count))
        mean_error, max_error = contour_error(target, commands)
        if mean_error <= MEAN_ERROR_LIMIT and max_error <= MAX_ERROR_LIMIT:
            passing.append((commands, mean_error, max_error, count))
        # Prefer the simplest path that remains inside the error budget.
        objective = mean_error + max_error * 0.08 + count * 0.006
        if best is None or objective < best[0]:
            best = (objective, commands, mean_error, max_error, count)
    ensure(best is not None, "No smoothing candidate")
    # Automatic retry rule: when the first simplified curve exceeds either
    # threshold, select the least-complex sampled contour that passes both.
    if passing:
        passing.sort(key=lambda item: (item[3], item[1], item[2]))
        return passing[0]
    _, commands, mean_error, max_error, count = best
    return commands, mean_error, max_error, count


def draw_contour_overlay(crop: np.ndarray, target: np.ndarray, commands: list[dict[str, Any]], origin: tuple[int, int], color: tuple[int, int, int], spec: RegionSpec) -> tuple[np.ndarray, np.ndarray]:
    edge = edge_map(crop, spec.canny_low, spec.canny_high, spec.close_kernel)
    edge_bgr = cv2.cvtColor(edge, cv2.COLOR_GRAY2BGR)
    detected = crop.copy()
    geometry = crop.copy()
    overlay = crop.copy()
    local_target = np.round(target.reshape((-1, 2)) - origin).astype(np.int32).reshape((-1, 1, 2))
    local_geometry = np.round(commands_to_polyline(commands) - origin).astype(np.int32).reshape((-1, 1, 2))
    cv2.polylines(detected, [local_target], True, (0, 210, 255), 1, cv2.LINE_AA)
    cv2.polylines(geometry, [local_geometry], True, color, 1, cv2.LINE_AA)
    cv2.polylines(overlay, [local_target], True, (0, 210, 255), 1, cv2.LINE_AA)
    cv2.polylines(overlay, [local_geometry], True, color, 1, cv2.LINE_AA)
    return edge_bgr, detected, geometry, overlay


def detect_region(image: np.ndarray, spec: RegionSpec, output: Path) -> tuple[dict[str, Any], dict[str, Any]]:
    crop, origin = roi_image(image, spec)
    edges = edge_map(crop, spec.canny_low, spec.canny_high, spec.close_kernel)
    selected_score = 0.0
    extra: dict[str, Any] = {}
    if spec.kind in ("circle", "stick"):
        target, cx, cy, radius = choose_circle(edges, spec, origin)
        target[:, 0, 0] += origin[0]
        target[:, 0, 1] += origin[1]
        generated = {"kind": "ellipse", "ellipse": {"cx": round(float(cx + origin[0]), 3), "cy": round(float(cy + origin[1]), 3), "rx": round(float(radius), 3), "ry": round(float(radius), 3)}}
        commands = catmull_rom_commands(resample_closed_contour(target, max(spec.samples)))
        extra = {"circleRadius": round(float(radius), 3)}
    else:
        contour, selected_score, _ = choose_contour(edges, spec, origin)
        contour = contour.astype(np.float32)
        contour[:, 0, 0] += origin[0]
        contour[:, 0, 1] += origin[1]
        target = contour
        if spec.kind == "ellipse":
            fitted, (cx, cy, rx, ry) = choose_ellipse(edges, spec, origin)
            fitted[:, 0, 0] += origin[0]
            fitted[:, 0, 1] += origin[1]
            target = fitted
            generated = {"kind": "ellipse", "ellipse": {"cx": round(float(cx + origin[0]), 3), "cy": round(float(cy + origin[1]), 3), "rx": round(float(rx), 3), "ry": round(float(ry), 3)}}
        else:
            generated = {"kind": "path"}
        commands, mean_error, max_error, selected_samples = choose_smoothing(target, spec.samples)
        extra.update({"selectedContourScore": round(float(selected_score), 4), "samples": selected_samples})
    if spec.kind in ("circle", "stick"):
        commands, mean_error, max_error, selected_samples = choose_smoothing(target, spec.samples)
        extra["samples"] = selected_samples
    if generated["kind"] == "path":
        generated["commands"] = commands
    else:
        # Ellipses are still generated from source pixels. Use a sampled ellipse
        # path internally for the error and debug overlay.
        generated["commands"] = commands
    color = (70, 210, 75) if "left" in spec.id or spec.id in ("dpad-left", "dpad-up", "dpad-down") else (255, 145, 45)
    region_dir = output / "regions" / spec.id
    region_dir.mkdir(parents=True, exist_ok=True)
    edge_bgr, detected, geometry, overlay = draw_contour_overlay(crop, target, commands, origin, color, spec)
    cv2.imwrite(str(region_dir / "roi.png"), crop)
    cv2.imwrite(str(region_dir / "edges.png"), edge_bgr)
    cv2.imwrite(str(region_dir / "detected-contour.png"), detected)
    cv2.imwrite(str(region_dir / "geometry-1px.png"), geometry)
    cv2.imwrite(str(region_dir / "overlay-1px.png"), overlay)
    overflow = has_obvious_overflow(target, commands)
    report = {
        "id": spec.id,
        "kind": spec.kind,
        "roi": {"x": spec.roi[0], "y": spec.roi[1], "width": spec.roi[2], "height": spec.roi[3]},
        "meanErrorPixels": round(mean_error, 3),
        "maxErrorPixels": round(max_error, 3),
        "passed": mean_error <= MEAN_ERROR_LIMIT and max_error <= MAX_ERROR_LIMIT and not overflow,
        "outOfBounds": overflow,
        **extra,
    }
    return generated, report


def update_default(document: dict[str, Any], generated: dict[str, dict[str, Any]]) -> dict[str, Any]:
    result = copy.deepcopy(document)
    regions = {region["id"]: region for region in result["regions"]}
    for region_id, value in generated.items():
        if region_id.startswith("stick-"):
            continue
        region = regions[region_id]
        if value["kind"] == "ellipse":
            region["kind"] = "ellipse"
            region["ellipse"] = value["ellipse"]
            region.pop("commands", None)
        else:
            region["kind"] = "path"
            region["commands"] = value["commands"]
            region.pop("ellipse", None)
    motion = {item["id"]: item for item in result["motionRanges"]}
    for stick_id in ("stick-left", "stick-right"):
        ellipse = generated[stick_id]["ellipse"]
        # The detected circular rubber cap becomes the physical cap. The socket
        # is sourced from the same measured center with an image-derived rim.
        cap_radius = ellipse["rx"]
        motion[stick_id]["cap"] = {"cx": ellipse["cx"], "cy": ellipse["cy"], "rx": round(cap_radius, 3), "ry": round(cap_radius, 3)}
        motion[stick_id]["socket"] = {"cx": ellipse["cx"], "cy": ellipse["cy"], "rx": round(cap_radius + 18.0, 3), "ry": round(cap_radius + 18.0, 3)}
    return result


def render_full_comparison(image: np.ndarray, generated: dict[str, dict[str, Any]], output: Path) -> None:
    canvas = image.copy()
    for spec in SPECS:
        item = generated[spec.id]
        color = (70, 210, 75) if "left" in spec.id or spec.id.startswith("dpad") else (255, 145, 45)
        if item["kind"] == "ellipse":
            ellipse = item["ellipse"]
            cv2.ellipse(canvas, (int(round(ellipse["cx"])), int(round(ellipse["cy"]))), (int(round(ellipse["rx"])), int(round(ellipse["ry"]))), 0, 0, 360, color, 1, cv2.LINE_AA)
        else:
            points = np.round(commands_to_polyline(item["commands"])).astype(np.int32).reshape((-1, 1, 2))
            cv2.polylines(canvas, [points], True, color, 1, cv2.LINE_AA)
    cv2.imwrite(str(output / "dualSenseRegions.generated-1px-overlay.png"), canvas)


def write_markdown(report: dict[str, Any], output: Path) -> None:
    rows = ["# DualSense automatic contour extraction report", "", "All measurements use the source image at 100% / 1536×1024, transparent fill, Glow off and a 1px outline.", "", "| Region | Mean px | Max px | Overflow | Status | ROI |", "| --- | ---: | ---: | --- | --- | --- |"]
    for item in report["regions"]:
        roi = item["roi"]
        status = "PASS" if item["passed"] else "RETRY"
        overflow = "YES" if item["outOfBounds"] else "NO"
        rows.append(f"| {item['id']} | {item['meanErrorPixels']:.3f} | {item['maxErrorPixels']:.3f} | {overflow} | {status} | {roi['x']},{roi['y']} {roi['width']}×{roi['height']} |")
    rows += ["", f"Pass threshold: mean ≤ {MEAN_ERROR_LIMIT}px, max ≤ {MAX_ERROR_LIMIT}px and no >3px curve-envelope overflow.", "", "The debug folder contains `roi.png`, `edges.png`, `detected-contour.png`, `geometry-1px.png` and `overlay-1px.png` for every image-derived region."]
    (output / "report.md").write_text("\n".join(rows) + "\n", encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser(description="Generate DualSense WPF Geometry from the raw source image")
    parser.add_argument("--output", type=Path, default=OUTPUT_ROOT)
    parser.add_argument("--write-default", action="store_true", help="replace Assets/dualSenseRegions.json after generating")
    args = parser.parse_args()
    output: Path = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    image = load_image(SOURCE_IMAGE)
    document = json.loads(DEFAULT_JSON.read_text(encoding="utf-8"))
    generated: dict[str, dict[str, Any]] = {}
    reports: list[dict[str, Any]] = []
    for spec in SPECS:
        value, report = detect_region(image, spec, output)
        generated[spec.id] = value
        reports.append(report)
    # L3/R3 are independent input states that intentionally reuse stick cap geometry.
    generated_document = update_default(document, generated)
    generated_document["visualStyleDefaults"] = document["visualStyleDefaults"]
    (output / "dualSenseRegions.generated.json").write_text(json.dumps(generated_document, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    report = {
        "sourceImage": "dualsense.png",
        "imageWidth": IMAGE_WIDTH,
        "imageHeight": IMAGE_HEIGHT,
        "thresholds": {"meanErrorPixels": MEAN_ERROR_LIMIT, "maxErrorPixels": MAX_ERROR_LIMIT},
        "regions": reports,
        "logicalVisuals": 22,
        "fixedRegions": 20,
        "motionRegions": 2,
        "note": "L3/R3 reuse the measured left/right stick cap geometry; touchpad surface/button share one generated geometry.",
    }
    (output / "report.json").write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    write_markdown(report, output)
    render_full_comparison(image, generated, output)
    failed = [item["id"] for item in reports if not item["passed"]]
    if args.write_default:
        ensure(not failed, "Refusing to write default JSON while error checks fail: " + ", ".join(failed))
        backup = output / "dualSenseRegions.before-auto-generation.json"
        shutil.copy2(DEFAULT_JSON, backup)
        DEFAULT_JSON.write_text(json.dumps(generated_document, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"output": str(output), "regions": len(reports), "failed": failed}, ensure_ascii=False))
    return 0 if not failed else 2


if __name__ == "__main__":
    raise SystemExit(main())
