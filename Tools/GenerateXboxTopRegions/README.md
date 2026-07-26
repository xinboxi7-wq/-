# Xbox top-control extraction helper

Runs only during development. It performs Canny edge detection and contour
selection inside independent LB/RB/LT/RT search ROIs in the original
`Assets/controller.png`, then exports stage-space path candidates plus image
evidence. It does not add an OpenCV dependency to ControllerLab.

```powershell
python Tools/GenerateXboxTopRegions/generate_xbox_top_regions.py `
  --image Assets/controller.png --output audit/xbox-top-region-extraction
```

The generated paths are evidence, not a blind overwrite mechanism: top buttons
are partly hidden by the photographed front shell. The WPF trigger calibration
tool searches only persisted transforms against the same source-image edges and
saves a reversible user override.
