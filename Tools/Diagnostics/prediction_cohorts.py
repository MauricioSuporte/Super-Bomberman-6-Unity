"""Select P0 cohorts using same-machine UTC brackets, never UTC latency subtraction."""
from datetime import datetime
import json
import re

# R01Frame.u = .NET UTC milliseconds - 63900000000000.
FRAME_TO_UNIX_MS = 63900000000000 - 62135596800000


def windows(log, guard_seconds=2):
    lines = log.splitlines()
    frames = []
    markers = {}
    for index, line in enumerate(lines):
        match = re.search(r"\[R01Frame\] (\{.*\})", line)
        if match:
            frames.append((index, (json.loads(match[1])["u"] + FRAME_TO_UNIX_MS) / 1000))
        for name, marker in (("walking", "[P0] cohort walking"), ("mounted", "[P0] cohort mounted"),
                             ("end", "[P0] reproduction completed")):
            if marker in line:
                if name in markers: raise ValueError("Duplicate P0 marker: " + name)
                markers[name] = index
    if set(markers) != {"walking", "mounted", "end"}: raise ValueError("Missing P0 cohort markers")
    if any(right[1] < left[1] for left, right in zip(frames, frames[1:])):
        raise ValueError("Frame UTC clock moved backwards; cohort selection is unsafe")
    brackets = {}
    for name, index in markers.items():
        before = [t for i, t in frames if i < index]
        after = [t for i, t in frames if i > index]
        if not before or (not after and name != "end"): raise ValueError("Missing frame-clock bracket: " + name)
        # Offline quits immediately after completion. Only the preceding bound is
        # used for the conservative end; do not invent a following timestamp.
        left, right = before[-1], after[0] if after else None
        if right is not None and not 0 <= right - left <= 5: raise ValueError("Invalid frame-clock bracket: " + name)
        brackets[name] = [left, right]
    result = {}
    for name, following in (("walking", "mounted"), ("mounted", "end")):
        begin, end = brackets[name][1] + guard_seconds, brackets[following][0] - guard_seconds
        if end <= begin: raise ValueError("Empty P0 cohort: " + name)
        result[name] = {"beginUnix": begin, "endUnix": end}
    return {"windows": result, "markerBracketsUnix": brackets, "boundaryGuardSeconds": guard_seconds,
            "scope": "same-machine UTC selection only; latency durations use the capture's local realtime clock"}


def select(records, cohort):
    for row in records:
        if row.get("kind") == "capture_start":
            yield row; continue
        try:
            utc = datetime.fromisoformat(row["utc"].replace("Z", "+00:00")).timestamp()
        except (KeyError, ValueError, TypeError):
            raise ValueError("Missing/invalid UTC for cohort selection") from None
        if any(w["beginUnix"] <= utc <= w["endUnix"] for w in cohort["windows"].values()):
            yield row
