"""Validate Xbox trigger alpha mattes and derive their edge/glow helper masks.

The two source-sized production mattes are the trigger silhouettes.  This
development helper never rebuilds a Bezier path and never changes their alpha;
it only derives the thin inner edge and bounded outer-glow rings used by WPF.
"""
from pathlib import Path

import cv2
import numpy as np


ROOT = Path(__file__).resolve().parents[2]
STAGE_W = 1536.0
SOURCE_W = 1586
SOURCE_H = 992
SCALE = STAGE_W / SOURCE_W
TOP = 32.0


def write_alpha(path, alpha):
    rgba = np.zeros((SOURCE_H, SOURCE_W, 4), np.uint8)
    rgba[:, :, :3] = 255
    rgba[:, :, 3] = alpha
    cv2.imwrite(str(path), rgba)


def main():
    assets = ROOT / "Assets"
    audit = ROOT / "audit" / "xbox-trigger-mask-extraction"
    photo = cv2.imread(str(assets / "controller.png"), cv2.IMREAD_COLOR)
    audit.mkdir(parents=True, exist_ok=True)
    report = {}
    for name in ("lt", "rt"):
        # The production silhouette is already the source-aligned alpha matte.
        # This step only derives a non-expanding inner edge and a separately
        # bounded glow ring. It never recreates a Bezier path or alters alpha.
        rgba = cv2.imread(str(assets / f"xbox_{name}_mask.png"), cv2.IMREAD_UNCHANGED)
        if rgba is None or rgba.shape[:2] != (SOURCE_H, SOURCE_W) or rgba.shape[2] != 4:
            raise RuntimeError(f"Missing source-sized xbox_{name}_mask.png")
        alpha = rgba[:, :, 3]
        solid = (alpha > 32).astype(np.uint8) * 255
        edge = cv2.subtract(solid, cv2.erode(solid, np.ones((3, 3), np.uint8), iterations=1))
        glow = cv2.subtract(cv2.dilate(solid, np.ones((5, 5), np.uint8), iterations=1), solid)
        write_alpha(assets / f"xbox_{name}_mask_edge.png", edge)
        write_alpha(assets / f"xbox_{name}_mask_glow.png", glow)
        ys, xs = np.where(alpha > 32)
        report[name] = {
            "rawBounds": [int(xs.min()), int(ys.min()), int(xs.max() - xs.min() + 1), int(ys.max() - ys.min() + 1)],
            "edge": "inside-only 1 source-pixel alpha edge",
            "glow": "outside-only 2 source-pixel alpha ring",
        }

        x0 = max(0, int(xs.min()) - 20); y0 = max(0, int(ys.min()) - 20)
        x1 = min(SOURCE_W, int(xs.max()) + 21); y1 = min(SOURCE_H, int(ys.max()) + 21)
        crop = photo[y0:y1, x0:x1].copy()
        tint = (76, 226, 76) if name == "lt" else (235, 160, 30)
        selected = alpha[y0:y1, x0:x1] > 32
        crop[selected] = (0.70 * crop[selected] + 0.30 * np.array(tint)).astype(np.uint8)
        contour, _ = cv2.findContours((alpha[y0:y1, x0:x1] > 32).astype(np.uint8), cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_NONE)
        cv2.drawContours(crop, contour, -1, (255, 90, 220), 1, cv2.LINE_AA)
        cv2.imwrite(str(audit / f"{name}-baked-mask-overlay.png"), cv2.resize(crop, None, fx=3, fy=3, interpolation=cv2.INTER_NEAREST))
    (audit / "baked-mask-report.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
