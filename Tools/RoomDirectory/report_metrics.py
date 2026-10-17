"""Summarize recorded Unity samples without guessing room capacity."""
import argparse
from collections import defaultdict
import json
from pathlib import Path
import statistics


def summarize(paths):
    groups = defaultdict(list)
    for path in paths:
        for line in path.read_text(encoding="utf-8-sig", errors="strict").splitlines():
            try:
                sample = json.loads(line)
                if "cpuPercentOneCore" in sample:
                    groups[(sample["room"], sample["scene"], sample["players"])].append(sample)
            except (ValueError, KeyError):
                continue
    result = []
    for (room, scene, players), samples in sorted(groups.items()):
        markers = defaultdict(list)
        for sample in samples:
            for name, value in zip(sample.get("markerNames", []), sample.get("averageMarkerMs", [])):
                if value >= 0:
                    markers[name].append(value)
        result.append(dict(room=room, scene=scene, players=players, samples=len(samples),
            averageCpuPercentOneCore=round(statistics.mean(s["cpuPercentOneCore"] for s in samples), 2),
            peakCpuPercentOneCore=round(max(s["cpuPercentOneCore"] for s in samples), 2),
            peakResidentMiB=round(max(s["residentMiB"] for s in samples), 2),
            lowestFps=round(min(s["fps"] for s in samples), 2),
            worstFrameMs=round(max(s["maxFrameMs"] for s in samples), 2),
            markersByAverageMs=sorted([
                dict(name=name, averageMs=round(statistics.mean(values), 4))
                for name, values in markers.items()], key=lambda item: -item["averageMs"])))
    return result


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("logs", type=Path)
    args = parser.parse_args()
    print(json.dumps(summarize(args.logs.glob("*/metrics.jsonl*")), indent=2))
