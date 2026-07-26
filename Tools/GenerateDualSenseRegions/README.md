# GenerateDualSenseRegions

Development-only, offline contour generator for the DS5 WPF overlay. It reads
the raw `Assets\dualsense.png` image at its fixed `1536 × 1024` logical size,
extracts a real image contour inside each dedicated search ROI, emits reversible
WPF path commands, and validates the result before it is allowed to replace the
default JSON.

It is **not** a runtime dependency. The shipped WPF application continues to
load only `Assets\dualSenseRegions.json`.

## Run

```powershell
python .\Tools\GenerateDualSenseRegions\generate_dualsense_regions.py `
  --output .\Tools\GenerateDualSenseRegions\output
```

After reviewing the generated 1px evidence, promote only a clean run:

```powershell
python .\Tools\GenerateDualSenseRegions\generate_dualsense_regions.py `
  --output .\Tools\GenerateDualSenseRegions\output `
  --write-default
```

`--write-default` refuses to update `Assets\dualSenseRegions.json` if any
region exceeds the bidirectional contour error limits or has more than 3px of
cubic-curve envelope overflow. It saves the prior default as
`output\dualSenseRegions.before-auto-generation.json`.

## Output

- `dualSenseRegions.generated.json` — complete WPF configuration generated from the image.
- `dualSenseRegions.generated-1px-overlay.png` — full-source no-Glow comparison.
- `report.json` and `report.md` — mean/max pixel error, ROI and overflow status.
- `regions\<id>\roi.png`, `edges.png`, `detected-contour.png`,
  `geometry-1px.png`, `overlay-1px.png` — per-region evidence.

There are 19 uniquely measured physical contours. They cover all 22 logical
visual states: L3/R3 reuse their measured stick-cap contours as independent
input states, and touchpad surface/button share the one measured touchpad
contour with independent state channels.
