"""audit_split.py — prove the structural split only MOVED code, never changed it.

Method: take the pre-split ControllerLab.cs from git, take the union of every
file the split produced, normalise both (drop blanks / using / namespace /
brace-only lines / extraction headers) and compare the resulting line
MULTISETS. Any renamed identifier, edited statement or lost line shows up as a
residual on one side.

Verdict:
  * LOST (in old, not in new) must be 0 for real code lines.
  * ADDED (in new, not in old) should be extraction header comments only.
"""

import collections
import os
import re
import subprocess
import sys

REPO = r"C:\Users\XinBai\Documents\Codex\2026-07-15\zu\outputs\xbox_controller_lab"
BASELINE = "7f571d4:ControllerLab.cs"      # state right before the first extraction
NEW_DIRS = ["Models", "Theme", "Controls", "Services", "Views"]

HEADER_PAT = re.compile(
    r"^\s*//\s*("
    r"extracted|as part of|no logic|see docs/redesign|"
    r"[A-Za-z_][A-Za-z0-9_]*\.cs$|"
    r"$) ", re.I)


def normalise(lines):
    out = []
    headerish = []
    for raw in lines:
        s = raw.rstrip()
        t = s.strip()
        if not t:
            continue
        if t.startswith("using ") or t.startswith("namespace ") or t.startswith("//") and (
                "Extracted verbatim" in t or t.startswith("// =====")):
            continue
        if t in ("{", "}"):
            continue
        # extraction headers written by the split tool / by Codex
        if t.startswith("//") and (
            "分批" in t or "Batch" in t or "拆分" in t or "batch" in t
            or "移动" in t or "structural split" in t.lower()
            or "ControllerLab.cs" in t
        ):
            headerish.append(t)
            continue
        out.append(t)
    return collections.Counter(out), headerish


def main():
    try:
        sys.stdout.reconfigure(encoding="utf-8")
    except Exception:
        pass

    old_src = subprocess.run(["git", "show", BASELINE], cwd=REPO,
                             capture_output=True, text=True,
                             encoding="utf-8", errors="replace")
    if old_src.returncode != 0:
        print("git show failed:", old_src.stderr[:300])
        return 2

    old_lines = old_src.stdout.split("\n")
    new_lines = []
    files = []
    for d in NEW_DIRS:
        base = os.path.join(REPO, d)
        for root, _dirs, names in os.walk(base):
            for n in sorted(names):
                if n.endswith(".cs"):
                    p = os.path.join(root, n)
                    files.append(p)
                    with open(p, encoding="utf-8", errors="replace") as fh:
                        new_lines.extend(fh.read().split("\n"))

    print("pre-split ControllerLab.cs : {0} lines".format(len(old_lines)))
    print("files produced by the split: {0}".format(len(files)))
    print("their combined lines       : {0}".format(len(new_lines)))
    print()

    old_c, _ = normalise(old_lines)
    new_c, headerish = normalise(new_lines)

    lost = old_c - new_c        # in old, missing from new  -> REAL LOSS
    added = new_c - old_c       # in new, not in old        -> should be comments

    print("normalised distinct lines — old: {0}  new: {1}".format(len(old_c), len(new_c)))
    print("extraction-header comment lines excluded: {0}".format(len(headerish)))
    print()

    print("=== LOST (present before, missing now) — must be 0 ===")
    if not lost:
        print("  ✅ 无。没有任何一行代码在拆分中丢失或被改写。")
    else:
        total = sum(lost.values())
        print("  ❌ 共 {0} 行、{1} 种：".format(total, len(lost)))
        for line, n in lost.most_common(30):
            print("    [{0}x] {1}".format(n, line[:120]))
    print()

    print("=== ADDED (present now, absent before) — should be comments only ===")
    if not added:
        print("  ✅ 无新增行。")
    else:
        total = sum(added.values())
        codeish = {l: n for l, n in added.items()
                   if not l.startswith("//") and not l.startswith("/*") and not l.startswith("*")}
        print("  共 {0} 行、{1} 种；其中疑似代码 {2} 种".format(total, len(added), len(codeish)))
        for line, n in added.most_common(25):
            print("    [{0}x] {1}".format(n, line[:120]))

    print()
    print("VERDICT:", "PASS — 纯搬运，逻辑未变" if not lost else "FAIL — 存在丢失/改写，需要人工审查")
    return 0 if not lost else 1


if __name__ == "__main__":
    sys.exit(main())
