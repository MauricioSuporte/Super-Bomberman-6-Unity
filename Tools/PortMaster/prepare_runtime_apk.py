"""Prepare the Unity data container used by ua_host64 from a signed Android build."""
import argparse
import copy
from pathlib import Path
import re
import struct
from zipfile import ZIP_STORED, ZipFile

import UnityPy


def keep_music_compressed(data, clip_name):
    clips = [obj for obj in UnityPy.load(data).objects if obj.type.name == "AudioClip"]
    if len(clips) != 1:
        raise ValueError(f"Expected one AudioClip for {clip_name}")
    obj = clips[0]
    clip = obj.read()
    if clip.m_Name != clip_name or clip.m_CompressionFormat != 1:
        raise ValueError(f"Unexpected encoded music format for {clip_name}")
    if clip.m_LoadType in (1, 2):
        return data
    # The Unity 6 Android AudioClip stores LoadType immediately after its aligned name.
    name = clip_name.encode("utf-8")
    start = obj.byte_start
    offset = start + 4 + ((len(name) + 3) // 4) * 4
    if data[start:start + 4 + len(name)] != struct.pack("<I", len(name)) + name:
        raise ValueError(f"Unsupported AudioClip layout for {clip_name}")
    if clip.m_LoadType != 0 or data[offset:offset + 4] != bytes(4):
        raise ValueError(f"Unexpected LoadType for {clip_name}")
    updated = data[:offset] + struct.pack("<I", 1) + data[offset + 4:]
    verified = next(obj for obj in UnityPy.load(updated).objects if obj.type.name == "AudioClip").read()
    if verified.m_LoadType != 1 or verified.m_Resource != clip.m_Resource:
        raise ValueError(f"Could not verify modified AudioClip {clip_name}")
    return updated


def prepare(source, destination):
    project = Path(__file__).resolve().parents[2]
    meta = project / "Assets/Resources/GameplayLatencyDiagnostics.json.meta"
    guid = re.search(r"^guid: (\w+)$", meta.read_text(encoding="utf-8-sig"), re.M)[1]
    settings_entry = "assets/bin/Data/" + guid
    music = {}
    for path in (project / "Assets/Resources/Sounds/BattleModeMusics").glob("*.wav.meta"):
        asset_guid = re.search(r"^guid: (\w+)$", path.read_text(encoding="utf-8-sig"), re.M)[1]
        music["assets/bin/Data/" + asset_guid] = path.name.removesuffix(".wav.meta")
    scene_data = re.compile(r"assets/bin/Data/(?:globalgamemanagers|level\d+|sharedassets\d+)(?:\.assets)?(?:\.split\d+)?$")
    with ZipFile(source) as original, ZipFile(destination, "x") as output:
        original.getinfo(settings_entry)
        for name in music:
            original.getinfo(name)
        for entry in original.infolist():
            info = copy.copy(entry)
            data = original.read(entry)
            if entry.filename in music:
                data = keep_music_compressed(data, music[entry.filename])
            if entry.filename == settings_entry:
                # Unity stores the byte length alongside the TextAsset. Consume one
                # whitespace byte so offsets and the serialized length stay valid.
                old = b'"enabled": true'
                if data.count(old) == 1:
                    data = data.replace(old, b'"enabled":false')
                elif not re.search(rb'"enabled"\s*:\s*false', data):
                    raise ValueError("Unexpected movement diagnostics settings")
            if scene_data.fullmatch(entry.filename):
                info.compress_type = ZIP_STORED
            output.writestr(info, data)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path)
    parser.add_argument("destination", type=Path)
    args = parser.parse_args()
    prepare(args.source, args.destination)
