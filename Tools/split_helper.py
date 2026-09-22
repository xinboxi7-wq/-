"""split_helper.py — mechanical extraction of top-level types out of ControllerLab.cs

Part of the ControllerLab structural split (Batch 1: Models/ + Theme/).
See docs/redesign/ControllerLab-结构拆分施工图.md.

Design rules (deliberately conservative):
  * Only TOP-LEVEL types are touched — declarations indented exactly 4 spaces.
    Nested types (8+ spaces, e.g. MainWindow.RawInputRegistration) are private
    implementation details and must NOT move.
  * The extracted text is moved VERBATIM. No renaming, no reformatting, no
    re-indenting: the body is already indented for a namespace member, so it is
    wrapped in the same namespace and keeps its 4-space indentation.
  * Leading attributes / XML doc comments / line comments directly above a
    declaration move with it.
  * DRY RUN by default: pass --apply to actually write files.

Usage:
    python split_helper.py                     # dry run, prints the plan
    python split_helper.py --apply             # perform the extraction
"""

import os
import re
import sys

SRC = "ControllerLab.cs"
DRY_RUN = "--apply" not in sys.argv

# Batch 1: pure data / enums -> Models/, Palette -> Theme/
BATCH1 = {
    "Models": [
        "ControllerFamily", "GuidedStage", "StickPlotTraceMode",
        "ControllerReport", "ControllerSettings", "InputSnapshot",
        "DualSenseTouchPoint", "DualSenseTouchDebugInfo", "DualSenseOverlayState",
        "DualSenseRegionsDocument", "DualSenseRegionDefinition", "DualSensePathCommand",
        "DualSenseEllipseDefinition", "DualSenseMotionRangeDefinition",
        "DualSenseTouchSensorDefinition", "DualSenseLogicalPoint",
        "DualSenseVisualStylesDocument", "DualSenseVisualStyleDefinition",
        "DualSenseRegionsOverride", "DualSenseCalibrationHandle",
        "DualSenseCalibrationSnapshot",
    ],
    "Theme": ["Palette"],
}

TYPE_DECL = re.compile(
    r"^    (?:public|internal|private|protected)?\s*"
    r"(?:static\s+|sealed\s+|abstract\s+|partial\s+)*"
    r"(?:class|struct|enum|interface)\s+([A-Za-z_][A-Za-z0-9_]*)"
)


def strip_noise(line):
    """Remove string/char/comment content so braces can be counted safely."""
    out = []
    i, n = 0, len(line)
    while i < n:
        c = line[i]
        if c == '"':
            out.append(' ')
            i += 1
            while i < n:
                if line[i] == "\\":
                    i += 2
                    continue
                if line[i] == '"':
                    break
                i += 1
            i += 1
        elif c == "'":
            out.append(' ')
            i += 1
            while i < n:
                if line[i] == "\\":
                    i += 2
                    continue
                if line[i] == "'":
                    break
                i += 1
            i += 1
        elif c == "/" and i + 1 < n and line[i + 1] == "/":
            break  # line comment: rest of line is not code
        elif c == "/" and i + 1 < n and line[i + 1] == "*":
            out.append(' ')
            i += 2
            while i + 1 < n and not (line[i] == "*" and line[i + 1] == "/"):
                i += 1
            i += 2
        else:
            out.append(c)
            i += 1
    return "".join(out)


def find_types(lines):
    """Return [(name, start_idx, end_idx)] by brace matching.

    Only types declared at namespace level (exactly 4-space indent) are reported;
    nested types are skipped by virtue of the depth == 1 guard.
    """
    results = []
    depth = 0          # brace depth of the current *scope*
    pending = None     # (name, start_idx) of a type currently being scanned
    p_depth = 0        # brace depth accumulated inside that type
    p_open = False     # has the type's own opening brace been seen yet?

    for idx, line in enumerate(lines):
        code = strip_noise(line)
        opens = code.count("{")
        closes = code.count("}")

        if pending is None:
            if depth == 1:
                m = TYPE_DECL.match(line)
                if m:
                    pending = (m.group(1), idx)
                    p_depth = opens - closes
                    p_open = opens > 0
                    if p_open and p_depth <= 0:
                        results.append((pending[0], pending[1], idx))
                        pending = None
                    continue
            depth += opens - closes
            continue

        # scanning the body of a pending type
        p_depth += opens - closes
        if opens:
            p_open = True
        if p_open and p_depth <= 0:
            results.append((pending[0], pending[1], idx))
            pending = None
            depth = 1   # back at namespace level

    return results


def header(dest, name, origin_start, origin_end):
    return (
        "// " + name + "\n"
        "//\n"
        "// Extracted verbatim from ControllerLab.cs (lines {0}-{1}) on 2026-09-22\n"
        "// as part of the ControllerLab structural split. No logic was changed.\n"
        "// See docs/redesign/ControllerLab-结构拆分施工图.md\n"
    ).format(origin_start, origin_end)


def main():
    with open(SRC, encoding="utf-8") as fh:
        lines = fh.read().split("\n")

    types = find_types(lines)
    by_name = {n: (s, e) for n, s, e in types}
    print("found {0} top-level types in {1} ({2} lines)".format(len(types), SRC, len(lines)))

    usings = [l for l in lines if l.startswith("using ")]
    namespace = next(l for l in lines if l.startswith("namespace ")).strip()
    print("usings: {0} | namespace: {1}".format(len(usings), namespace))

    plan = []
    for folder, names in BATCH1.items():
        for name in names:
            if name not in by_name:
                print("  !! MISSING: {0} (not found as a top-level type)".format(name))
                continue
            s, e = by_name[name]
            # pull in directly-preceding attributes / doc comments / line comments
            top = s
            while top - 1 > 0 and re.match(r"^    (\s*\[|///|//)", lines[top - 1]) is not None:
                top -= 1
            plan.append((folder, name, top, e))

    plan.sort(key=lambda p: p[2], reverse=True)  # remove from the bottom up

    print("\nplan ({0} types):".format(len(plan)))
    for folder, name, top, e in plan:
        print("  {0:<10} {1:<32} lines {2:>6}-{3:<6} ({4} lines)".format(
            folder + "/", name + ".cs", top + 1, e + 1, e - top + 1))

    if DRY_RUN:
        print("\nDRY RUN — nothing written. Re-run with --apply to perform the split.")
        return 0

    for folder, name, top, e in plan:
        body = "\n".join(lines[top:e + 1])
        os.makedirs(folder, exist_ok=True)
        path = os.path.join(folder, name + ".cs")
        content = (
            header(folder, name, top + 1, e + 1)
            + "\n".join(usings)
            + "\n\n"
            + namespace + "\n{\n"
            + body
            + "\n}\n"
        )
        with open(path, "w", encoding="utf-8", newline="") as fh:
            fh.write(content)
        print("wrote {0}".format(path))
        del lines[top:e + 1]

    with open(SRC, "w", encoding="utf-8", newline="") as fh:
        fh.write("\n".join(lines))
    print("\n{0} is now {1} lines".format(SRC, len(lines)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
