#!/usr/bin/env python3
"""Rasterize actual MaximumUI DrawList exports and verify Gallery coverage.

Requires Inkscape and Pillow. The HTML file only selects generated screenshots;
it does not reimplement any Gallery controls or business plugin interactions.
"""
from __future__ import annotations

import argparse
from concurrent.futures import ThreadPoolExecutor
import json
import math
from pathlib import Path
import shutil
import subprocess
import sys
import xml.etree.ElementTree as ET


def interval_coverage(intervals: list[tuple[float, float]], end: float, tolerance: float = 1.0) -> bool:
    reached = 0.0
    for start, stop in sorted(intervals):
        if start > reached + tolerance:
            return False
        reached = max(reached, stop)
        if reached >= end - tolerance:
            return True
    return reached >= end - tolerance


def validate_manifest(manifest: dict) -> list[str]:
    errors = list(manifest.get("errors", []))
    previews = manifest.get("previews", [])
    pages = manifest.get("galleryPages", [])
    if not previews or not pages:
        errors.append("No production Gallery snapshots were exported.")
        return errors
    for page in pages:
        for palette, size in (("Metro Light", "standard"), ("Metro Dark", "standard"),
                              ("Metro Light", "minimum"), ("Metro Light", "large-text")):
            if not any(p.get("page") == page and p["palette"] == palette
                       and p["size"] == size and p["state"] == "default" for p in previews):
                errors.append(f"Missing {page}: {palette}, {size}, default.")
    names = [preview["file"] for preview in previews]
    if len(names) != len(set(names)):
        errors.append("Duplicate snapshot names overwrite a scene.")
    for expected in manifest.get("galleryCases", []):
        if not any(all(preview.get(key) == value for key, value in expected.items()) for preview in previews):
            errors.append(f"Missing catalog case: {expected['file']}.")
    for preview in previews:
        for area in preview.get("scrollAreas", []):
            if area["Virtual"] and area.get("RowHeight", 0) > 0:
                budget = min(area["ItemCount"], math.ceil(area["ViewHeight"] / area["RowHeight"]) + 4)
                if area["BuiltRows"] > budget:
                    errors.append(f"Virtual list exceeded visible-row budget: {preview['file']} {area['Key']} ({area['BuiltRows']} > {budget}).")
        if not preview.get("page") or preview.get("scrollOf") or preview.get("popups") or preview.get("paintState"):
            continue
        for area in preview.get("scrollAreas", []):
            if area["Maximum"] <= 1 or area["ViewHeight"] < 24:
                continue
            samples = [area]
            for other in previews:
                if other.get("scrollOf") != preview["file"]:
                    continue
                samples.extend(s for s in other.get("scrollAreas", []) if s["Path"] == area["Path"])
            # A scroll offset is useful evidence only if the viewport was
            # actually visible through all its ancestor scroll clips.
            samples = [s for s in samples if s.get("VisibleHeight", 0) >= min(24, s["ViewHeight"])]
            if area["Virtual"]:
                fractions = [s["Offset"] / area["Maximum"] for s in samples]
                if not (any(f <= .01 for f in fractions) and any(.4 <= f <= .6 for f in fractions)
                        and any(f >= .99 for f in fractions)):
                    errors.append(f"Virtual list lacks top/middle/bottom: {preview['file']} {area['Key']}.")
            elif not interval_coverage([(s["VisibleContentStart"], s["VisibleContentStart"] + s["VisibleHeight"]) for s in samples], area["ContentHeight"]):
                errors.append(f"Scroll content not fully covered: {preview['file']} {area['Key']}.")
    return errors


def intersect(a: tuple[float, ...], b: tuple[float, ...]) -> tuple[float, float, float, float]:
    x, y = max(a[0], b[0]), max(a[1], b[1])
    return x, y, max(0.0, min(a[0] + a[2], b[0] + b[2]) - x), max(0.0, min(a[1] + a[3], b[1] + b[3]) - y)


def verify_rendered_text(root: ET.Element, query: str, scene: str) -> list[dict]:
    """Independently check actual SVG glyph ink, measured by the PNG renderer.

    Inkscape returns un-clipped text-object bounds. We compare these with the
    production content box and then apply actual ancestor visibility/layers for
    overlap checks. Font-metric estimates alone cannot pass this gate.
    """
    bounds = {}
    for row in query.splitlines():
        fields = row.split(",")
        if len(fields) == 5:
            try:
                bounds[fields[0]] = tuple(float(value) for value in fields[1:])
            except ValueError:
                pass
    issues, shown_text = [], []
    for element in root.findall(".//{http://www.w3.org/2000/svg}text"):
        if "data-content-box" not in element.attrib:
            raise ValueError(f"Text lacks production geometry metadata: {scene}")
        text = "".join(element.itertext())
        if not text.strip():
            continue
        if element.attrib["id"] not in bounds:
            raise ValueError(f"PNG renderer did not measure text {element.attrib['id']}: {scene}")
        ink = bounds[element.attrib["id"]]
        content = tuple(float(value) for value in element.attrib["data-content-box"].split(","))
        visible = tuple(float(value) for value in element.attrib["data-visible-box"].split(","))
        if visible[2] < 1 or visible[3] < 1:
            continue
        path = element.get("data-widget-path")
        if element.get("data-can-clip") != "true":
            horizontal = ink[0] < content[0] - 1 or ink[0] + ink[2] > content[0] + content[2] + 1
            vertical = ink[1] < content[1] - 1 or ink[1] + ink[3] > content[1] + content[3] + 1
            if horizontal or vertical:
                issues.append(dict(Scene=scene, Kind="rendered-text-overflow" if horizontal else "rendered-text-height",
                                   Path=path, Detail=f"Inkscape glyph bounds {ink} exceed content box {content}: {text}"))
        shown = intersect(ink, visible)
        if shown[2] > 1 and shown[3] > 1:
            shown_text.append((shown, path, text, element.get("data-layer", "0")))
    for index, (ink, path, text, layer) in enumerate(shown_text):
        for other, other_path, other_text, other_layer in shown_text[index + 1:]:
            if layer != other_layer:
                continue
            overlap = intersect(ink, other)
            if overlap[2] > 2 and overlap[3] > 2:
                issues.append(dict(Scene=scene, Kind="rendered-text-overlap", Path=path,
                                   Detail=f"{text} overlaps {other_path} ({other_text})"))
    return issues


def rasterize(path: Path, preview: dict, inkscape: str) -> tuple[str, list[dict]]:
    from PIL import Image
    source = path / preview["file"]
    tree = ET.parse(source)
    root = tree.getroot()
    if root.tag != "{http://www.w3.org/2000/svg}svg":
        raise ValueError(f"Not an SVG: {source}")
    expected = preview["viewport"]
    if abs(float(root.attrib["width"]) - expected["width"]) > .01 or abs(float(root.attrib["height"]) - expected["height"]) > .01:
        raise ValueError(f"Snapshot dimensions differ from the production viewport: {source}")
    target = source.with_suffix(".png")
    rendered = subprocess.run([inkscape, str(source), "--export-type=png", "--export-area-page",
                               "--export-dpi=96", "--export-filename=" + str(target), "--query-all"], check=True,
                              stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True, timeout=90)
    with Image.open(target) as image:
        image.verify()
    with Image.open(target) as image:
        if abs(image.width - expected["width"]) > 1 or abs(image.height - expected["height"]) > 1:
            raise ValueError(f"Unexpected raster dimensions: {target}")
    return target.name, verify_rendered_text(root, rendered.stdout, source.stem)


def contact_sheets(path: Path, previews: list[dict]) -> list[str]:
    from PIL import Image, ImageDraw, ImageFont
    primary = [p for p in previews if p.get("page") and p["palette"] == "Metro Light"
               and p["size"] == "standard" and p["state"] == "default" and not p.get("scrollOf")]
    outputs = []
    try:
        font = ImageFont.truetype("DejaVuSans.ttf", 18)
    except OSError:
        font = ImageFont.load_default()
    for start in range(0, len(primary), 4):
        group = primary[start:start + 4]
        width = 1064
        cell_width, cell_height = 500, 420
        sheet = Image.new("RGB", (width, 2 * cell_height + 32), "#ededed")
        draw = ImageDraw.Draw(sheet)
        for index, preview in enumerate(group):
            x, y = 24 + index % 2 * 524, 12 + index // 2 * cell_height
            draw.text((x, y), preview["title"], fill="#202020", font=font)
            with Image.open(path / Path(preview["file"]).with_suffix(".png")) as image:
                image = image.convert("RGB")
                image.thumbnail((cell_width, cell_height - 48), Image.Resampling.LANCZOS)
                sheet.paste(image, (x, y + 32))
        target = path / f"gallery-contact-{start // 4 + 1}.png"
        sheet.save(target)
        outputs.append(target.name)
    return outputs


def write_index(path: Path, manifest: dict, quality: dict) -> None:
    data = json.dumps(manifest["previews"], ensure_ascii=False).replace("<", "\\u003c")
    title = "MaximumUI Gallery"
    content = """<!doctype html>
<html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>MaximumUI Gallery</title>
<style>
*{box-sizing:border-box}body{margin:0;background:#fff;color:#202020;font:14px/1.45 'Segoe UI',sans-serif}
header{height:52px;border-bottom:1px solid #ccc;display:flex;align-items:center;padding:0 24px;gap:24px}h1{font-size:20px;font-weight:400;margin:0}
main{display:grid;grid-template-columns:192px minmax(0,1fr);min-height:calc(100vh - 52px)}nav{padding:12px 8px;background:#f3f3f3}
button{font:inherit;border:0;background:none;text-align:left;width:100%;padding:10px 12px;cursor:pointer;border-left:3px solid transparent}button:hover{background:#e5e5e5}button[aria-current=true]{background:#ddd;border-color:#0078d4}button:focus-visible,a:focus-visible,select:focus-visible{outline:2px solid #202020;outline-offset:2px}
section{padding:16px 24px;min-width:0}.toolbar{display:flex;align-items:center;gap:16px;flex-wrap:wrap;margin-bottom:16px}select{font:inherit;padding:6px;border:1px solid #666;background:#fff;min-width:280px}a{color:#005a9e;text-decoration:none}a:hover{text-decoration:underline}
img{display:block;max-width:100%;height:auto;border:1px solid #bbb}details{margin-top:20px;color:#555}pre{white-space:pre-wrap;font:12px/1.5 monospace}summary{cursor:pointer}#status{font-size:12px;color:#555;margin-left:auto}
@media(max-width:800px){main{grid-template-columns:140px minmax(0,1fr)}section{padding:12px}header{padding:0 12px}select{min-width:0;max-width:100%}}
</style>
<header><h1>MaximumUI Gallery</h1><a href="quality-report.json">Validation report</a><span id="status"></span></header>
<main><nav aria-label="Pages"></nav><section><div class="toolbar"><label>View <select aria-label="Snapshot" id="variant"></select></label><a id="svg">SVG</a><a id="png">PNG</a></div><img id="preview" alt=""><details><summary>Rendering details</summary><p>These are snapshots of the production Gallery, LayoutEngine and WidgetPainter. Native Unity hosting, input and GPU rendering are not executed.</p><pre id="detail"></pre></details></section></main>
<script>
const previews=__DATA__;const groups=new Map();for(const p of previews){const id=p.page||p.title;if(!groups.has(id))groups.set(id,[]);groups.get(id).push(p)}
const nav=document.querySelector('nav'),select=document.querySelector('#variant'),image=document.querySelector('#preview');
function show(p){image.src=p.file.replace(/\\.svg$/,'.png');image.alt=p.title+' '+p.palette+' '+p.state;document.querySelector('#svg').href=p.file;document.querySelector('#png').href=image.src;document.querySelector('#detail').textContent=JSON.stringify(p,null,2)}
function choose(id){for(const b of nav.children)b.setAttribute('aria-current',b.dataset.id===id?'true':'false');select.replaceChildren();const list=groups.get(id);for(let i=0;i<list.length;i++){const p=list[i],o=document.createElement('option');o.value=i;o.textContent=p.palette+' / '+p.size+' / '+p.state+(p.scrollOf?' / '+p.detail.split(';').pop().trim():'');select.append(o)}select.onchange=()=>show(list[+select.value]);show(list[0]);history.replaceState(null,'','#'+encodeURIComponent(id))}
for(const [id,list] of groups){const b=document.createElement('button');b.dataset.id=id;b.textContent=list[0].title;b.onclick=()=>choose(id);nav.append(b)}document.querySelector('#status').textContent=previews.length+' snapshots';const requested=decodeURIComponent(location.hash.slice(1));choose(groups.has(requested)?requested:groups.keys().next().value);
</script></html>
""".replace("__DATA__", data)
    (path / "index.html").write_text(content, encoding="utf-8")
    (path / "quality-report.json").write_text(json.dumps(quality, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    report = [f"# {title} render verification", "", manifest.get("disclosure", ""), "",
              f"- Production pages: {len(manifest.get('galleryPages', []))}",
              f"- Rendered snapshots: {len(manifest['previews'])}",
              f"- Structural/coverage failures: {len(quality['errors'])}",
              f"- Text review items: {len(quality['textIssues'])}",
              f"- Typography: {manifest.get('typography', '')}", "",
              manifest.get("fontNotes", ""), "",
              "Open `index.html` to select every page, theme, size, state and scroll position.", "",
              "Regular scroll content is covered from top to bottom at each theme, size and Gallery state. Coverage counts only the portion visible through ancestor clips. Virtual lists are sampled at top, middle and bottom; exporting every data row would defeat virtualization.", "",
              "Open dropdowns and menus use production PopupGeometry, PopupPresentation and ChoiceCache. Modal content comes from actual Gallery declarations and invoked DialogService buttons.", ""]
    if quality["errors"]:
        report.extend(["## Failures", ""] + ["- " + error for error in quality["errors"]])
    (path / "README.md").write_text("\n".join(report) + "\n", encoding="utf-8")


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directory", type=Path)
    parser.add_argument("--jobs", type=int, default=3)
    parser.add_argument("--strict", action="store_true", help="Fail on text overflow, overlap or missing glyphs as well as structural/coverage failures.")
    args = parser.parse_args(argv)
    path = args.directory.resolve()
    manifest = json.loads((path / "preview-manifest.json").read_text(encoding="utf-8"))
    errors = validate_manifest(manifest)
    inkscape = shutil.which("inkscape")
    if not inkscape:
        raise SystemExit("Inkscape is required. Install inkscape, fonts-noto-cjk and python3-pil.")
    previews = manifest["previews"]
    with ThreadPoolExecutor(max_workers=max(1, min(8, args.jobs))) as pool:
        outputs = list(pool.map(lambda preview: rasterize(path, preview, inkscape), previews))
    sheets = contact_sheets(path, previews)
    quality = {"pages": manifest.get("galleryPages", []), "snapshotCount": len(previews),
               "pngCount": len(outputs), "contactSheets": sheets, "errors": errors,
               "textIssues": manifest.get("issues", []) + [issue for _, issues in outputs for issue in issues],
               "measuredTextIssues": manifest.get("issues", []),
               "renderedTextIssues": [issue for _, issues in outputs for issue in issues],
               "typography": manifest.get("typography"), "primaryFont": manifest.get("primaryFont"),
               "fontNotes": manifest.get("fontNotes"),
               "missingGlyphs": manifest.get("missingGlyphs", 0),
               "catalogStateCount": len(manifest.get("galleryStates", [])),
               "catalogCaseCount": len(manifest.get("galleryCases", [])),
               "declaredWidgetTypes": sorted({kind for preview in previews if preview.get("page") for kind in preview.get("declaredWidgetTypes", [])}),
               "widgetTypes": sorted({kind for preview in previews if preview.get("page") for kind in preview.get("widgetTypes", [])}),
               "visibleWidgetTypes": sorted({kind for preview in previews if preview.get("page") for kind in preview.get("visibleWidgetTypes", [])}),
               "boundary": "Production C# composition, layout and drawing; Pango/SVG/Inkscape rasterization; no Unity host or live game."}
    write_index(path, manifest, quality)
    print(f"Rendered {len(outputs)} PNGs and {len(sheets)} contact sheets; {len(errors)} structural/coverage failures, {len(quality['textIssues'])} text review items.")
    print(path / "index.html")
    return 1 if errors or args.strict and quality["textIssues"] else 0


if __name__ == "__main__":
    raise SystemExit(main())
