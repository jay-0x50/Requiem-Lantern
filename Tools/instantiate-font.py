#!/usr/bin/env python3
"""Create Unity's static Noto Sans KR Regular from the preserved variable font.

Install once from the repository root:
    python -m pip install --index-url https://pypi.org/simple --target Tools/.fonttools fonttools==4.61.1
Then run:
    python Tools/instantiate-font.py

The original font and adjacent OFL.txt are never modified. Fixed source timestamps
and a pinned fontTools version make this conversion reproducible.
"""

from hashlib import sha256
import json
from pathlib import Path
import sys

TOOLS = Path(__file__).resolve().parent
ROOT = TOOLS.parent
sys.path.insert(0, str(TOOLS / ".fonttools"))

try:
    import fontTools
    from fontTools.ttLib import TTFont
    from fontTools.varLib.instancer import instantiateVariableFont
except ImportError as error:
    raise SystemExit("Install fonttools==4.61.1 as described in this script's header.") from error


def digest(path):
    return sha256(path.read_bytes()).hexdigest()


def main():
    folder = ROOT / "RequiemLantern/Assets/Resources/Fonts"
    source = folder / "NotoSansKR.ttf"
    output = folder / "NotoSansKRRegular.ttf"
    license_file = folder / "OFL.txt"
    original_hash = digest(source)
    license_hash = digest(license_file)

    with TTFont(source, recalcTimestamp=False) as variable:
        if "fvar" not in variable or {a.axisTag for a in variable["fvar"].axes} != {"wght"}:
            raise ValueError("Expected the original font with one wght variation axis.")
        source_cmap = variable.getBestCmap()
        source_glyphs = variable.getGlyphOrder()
        regular = instantiateVariableFont(
            variable, {"wght": 400}, inplace=False, updateFontNames=True
        )
        regular.recalcTimestamp = False
        if "fvar" in regular or regular["OS/2"].usWeightClass != 400:
            raise ValueError("The font did not become a static Regular 400 instance.")
        regular.save(output)
        regular.close()

    with TTFont(output) as verified:
        if "fvar" in verified or verified["OS/2"].usWeightClass != 400:
            raise ValueError("Saved font is not a static Regular 400 instance.")
        if verified.getBestCmap() != source_cmap or verified.getGlyphOrder() != source_glyphs:
            raise ValueError("The output changed source character coverage or glyph order.")
        result = {
            "output": str(output.relative_to(ROOT)),
            "fontTools": fontTools.__version__,
            "fvar_present": "fvar" in verified,
            "weight_class": verified["OS/2"].usWeightClass,
            "family": verified["name"].getDebugName(1),
            "subfamily": verified["name"].getDebugName(2),
            "postscript_name": verified["name"].getDebugName(6),
            "glyphs": len(verified.getGlyphOrder()),
            "characters": len(verified.getBestCmap()),
            "bytes": output.stat().st_size,
            "sha256": digest(output),
        }

    if digest(source) != original_hash or digest(license_file) != license_hash:
        raise ValueError("The original font or OFL license unexpectedly changed.")
    result["original_and_license_unchanged"] = True
    print(json.dumps(result, indent=2, ensure_ascii=False))


if __name__ == "__main__":
    main()
