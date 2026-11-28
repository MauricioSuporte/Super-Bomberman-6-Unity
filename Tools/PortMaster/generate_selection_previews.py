from pathlib import Path
from uuid import uuid5, NAMESPACE_URL
import re
from PIL import Image

root = Path(__file__).resolve().parents[2]
resources = root / "Assets/Resources"
dest = resources / "SelectionPreviews"
source = (root / "Assets/Scripts/Skin/PlayerBomberSkinController.cs").read_text(encoding="utf-8-sig")

def guid(path):
    return uuid5(NAMESPACE_URL, path.relative_to(resources).as_posix()).hex

def generate(sheet):
    character = sheet.parent.name
    match = re.search(r"int\[\] " + character + r"EndStageFrames\s*=\s*\{([^}]+)", source)
    desired = set(range(5)) | {int(n) for n in re.findall(r"\d+", match[1])}
    if sheet.stem == "Bomberman2":
        desired.update((108, 109, 110))
    meta = Path(str(sheet) + ".meta").read_text(encoding="utf-8-sig")
    block = re.compile(r"^    - serializedVersion: \d+\n      name: .*?^      weights: .*?$", re.M | re.S)
    frames = []
    for item in block.finditer(meta, meta.index("    sprites:\n")):
        body = item[0]
        key = int(re.search(r"      name: .*_(\d+)", body)[1])
        if key in desired:
            rect = tuple(int(re.search(r"^        " + field + r": (\d+)", body, re.M)[1]) for field in ("x","y","width","height"))
            frames.append((body, rect))
    if not frames:
        raise ValueError(sheet)
    height = max(rect[3] for _, rect in frames)
    width = 8 * max(rect[2] for _, rect in frames)
    atlas = Image.new("RGBA", (width, height * ((len(frames)+7)//8)))
    entries = []
    with Image.open(sheet) as original:
        for index, (body, (x,y,w,h)) in enumerate(frames):
            dx, dy = index % 8 * (width//8), index // 8 * height
            atlas.paste(original.crop((x,original.height-y-h,x+w,original.height-y)).convert("RGBA"), (dx,dy))
            entries.append(re.sub(r"        x: \d+\n        y: \d+", f"        x: {dx}\n        y: {atlas.height-dy-h}", body, count=1))
    target = dest / sheet.name
    atlas.save(target)
    meta = re.sub(r"(?m)^guid: \w+", "guid: "+guid(target), meta, count=1)
    meta = re.sub(r"(    sprites:\n).*?(\n    outline:)", lambda m:m[1]+"\n".join(entries)+m[2], meta, count=1, flags=re.S)
    def keep_name(name):
        return int(name.rsplit("_",1)[1]) in desired
    meta = re.sub(r"  - first:\n      213: -?\d+\n    second: (\w+_\d+)\n", lambda m:m[0] if keep_name(m[1]) else "", meta)
    meta = re.sub(r"^      (\w+_\d+): -?\d+\n", lambda m:m[0] if keep_name(m[1]) else "", meta, flags=re.M)
    meta = re.sub(r"    textureCompression: \d+", "    textureCompression: 0",meta)
    meta = re.sub(r"    overridden: \d+", "    overridden: 0",meta)
    Path(str(target)+".meta").write_text(meta,encoding="utf-8",newline="\n")

if __name__ == "__main__":
    dest.mkdir(exist_ok=True)
    template = (resources/"Sprites.meta").read_text(encoding="utf-8-sig")
    Path(str(dest)+".meta").write_text(re.sub(r"guid: \w+", "guid: "+guid(dest),template),encoding="utf-8",newline="\n")
    for sheet in sorted((resources/"Sprites/Bombers").glob("*/Generated/*/*.png")):
        generate(sheet)
