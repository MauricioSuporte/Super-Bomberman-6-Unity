"""R02 co-op world presentation report (Development captures from smoke_full_clients.py --coop-world).

families: capture-on runs (server + 2 clients motion logs). Per sprite family:
  server  coop_family_shape  error of the frames actually sent, reconstructed like the client (linear between two visible
                             samples, cut on an enemy discontinuity, otherwise hold) against the native per-frame pose on
                             the authority, which runs the same scene code as offline; native step/stall statistics;
          coop_family_cost   capture CPU, sampled/changed frames, frame payload bytes, R02 position-only entries/bytes/CPU;
  clients coop_present_window per sprite id (joined to families through coop_family_ids): presented/interpolated/moving
                             frames, stalls (1-3 frame holds between moving frames), step sizes, delay from the server state
                             to presentation, present CPU, stream starvation (presentation clock past the newest sample of a
                             continuous stream before the next one arrived), late samples and frames held while paused.
  A run is invalid when a process dropped diagnostic rows, hit the capture limit, or a log is missing.

cost: capture-off runs (--cycle-samples, -r01-frame-stats, -online-motion-logs-off). Pre-registered window: from the fixture
  line "coop-world started" to "coop-world paused index=1" (30 s of steady native world, harness UTC clock "u"). Per process:
  cycles/s, frame work (EarlyUpdate..PostLateUpdate), GC allocation and collections, fps; server KiB/s in/out from the
  [ServerMetrics] windows fully inside the window. A run is invalid if any process wrote or reported a motion capture.

Rules (pre-registered in Docs/multiplayer/progresso-predicao-online.md before any R02 measurement)
  shape loss: p95 > 1 px or max > 2 px (PPU 16) on moving visible frames in >= 2 runs; fixed: p95 <= 1 px and max <= 2 px.
  starvation: fraction of continuous-stream arrivals that found the clock already past the previous newest sample.
  a difference between configurations counts only when the run ranges (min-max) do not overlap.
  invalid or inconclusive runs are reported, never merged into a pass.
"""
import argparse
import json
import math
import statistics
import sys
from collections import Counter
from datetime import datetime, timezone
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from r01_cost_report import difference, frame_rows, summary  # noqa: E402

PROCESSES = ("server", "client-1", "client-2")
PIXEL_BIN = 0.1
U_EPOCH_MS = 63900000000000  # harness clock: .NET ticks / 10000 minus this offset
CLIENT_FIELDS = ("presented", "interpolated", "moving", "stalls", "stepSumPx", "stepMaxPx", "delaySumMs", "delayMaxMs", "cpuUs",
                 "arrivals", "continuous", "starved", "starvedMs", "late", "marginSumMs", "marginMinMs", "pausedHeld", "pausedMoving",
                 # v=3 (supplementary, declared after the pilot and before any measurement session): positional subset
                 "moveContinuous", "moveStarved", "moveStarvedMs",
                 # v=4 (R02 part 2, declared before any part-2 measurement): approved holds (the next sample repeats the
                 # pose), delay split into network/timeline lag and presentation hold, arrival age; then sparse delay bins.
                 "approvedHolds", "approvedHoldMs", "lagSumMs", "lagMaxMs", "holdSumMs", "holdMaxMs", "ageSumMs", "ageMaxMs")
V2_FIELDS = 18
MAX_FIELDS = ("stepMaxPx", "delayMaxMs", "lagMaxMs", "holdMaxMs", "ageMaxMs")
DELAY_BIN_MS = 10
DELAY_BINS = 61  # the last bin collects every delay >= 600 ms


def parse_delay_bins(text):
    bins = [0] * DELAY_BINS
    for item in (text or "").split("/"):
        if "=" in item:
            index, count = item.split("=", 1)
            bins[min(DELAY_BINS - 1, int(index))] += int(count)
    return bins


def delay_percentile(bins, q):
    """Upper edge (ms) of the bin holding the q-quantile; None without samples; inf when it falls in the overflow bin."""
    total = sum(bins)
    if not total:
        return None
    target, seen = q * total, 0
    for index, count in enumerate(bins):
        seen += count
        if seen >= target:
            return float("inf") if index == DELAY_BINS - 1 else (index + 1) * DELAY_BIN_MS
    return float("inf")


def parse_detail(detail):
    values = {}
    for part in (detail or "").split(";"):
        if "=" in part:
            key, value = part.split("=", 1)
            values[key] = value
    return values


def load_rows(path):
    rows = []
    for line in Path(path).read_text(encoding="utf-8-sig").splitlines():
        line = line.strip()
        if line:
            rows.append(json.loads(line))
    return rows


def family_ids(rows):
    mapping = {}
    for row in rows:
        if row.get("kind") != "coop_family_ids":
            continue
        for item in parse_detail(row.get("detail")).get("ids", "").split("|"):
            if ":" in item:
                key, family = item.split(":", 1)
                mapping[int(key)] = family
    return mapping


def percentile_from_bins(bins, q):
    total = sum(bins)
    if not total:
        return None
    target, seen = q * total, 0
    for index, count in enumerate(bins):
        seen += count
        if seen >= target:
            return (index + 1) * PIXEL_BIN  # upper edge of the bin; the last bin collects everything >= 8 px
    return len(bins) * PIXEL_BIN


def server_families(rows):
    families = {}
    for row in rows:
        kind = row.get("kind")
        if kind not in ("coop_family_shape", "coop_family_cost"):
            continue
        d = parse_detail(row.get("detail"))
        f = families.setdefault(d["family"], {"shapeFrames": 0, "errorSumPx": 0.0, "errorMaxPx": 0.0, "over1": 0, "over2": 0, "holdStarts": 0,
                                              "holdFrames": 0, "holdSumPx": 0.0, "holdMaxPx": 0.0, "extraFrames": 0, "nativeMoving": 0,
                                              "nativeStepSumPx": 0.0, "nativeStepMaxPx": 0.0, "nativeStalls": 0, "bins": [],
                                              "sampled": 0, "changed": 0, "framePayloadBytes": 0, "captureCpuMs": 0.0,
                                              "motionEntries": 0, "motionAnchors": 0, "motionBytes": 0, "motionCpuMs": 0.0, "repositions": 0, "costReports": 0})
        if kind == "coop_family_shape":
            f["shapeFrames"] += int(d["frames"]); f["errorSumPx"] += float(d["error_sum_px"]); f["errorMaxPx"] = max(f["errorMaxPx"], float(d["error_max_px"]))
            f["over1"] += int(d["over1"]); f["over2"] += int(d["over2"]); f["holdStarts"] += int(d["hold_starts"]); f["holdFrames"] += int(d["hold_frames"])
            f["holdSumPx"] += float(d["hold_sum_px"]); f["holdMaxPx"] = max(f["holdMaxPx"], float(d["hold_max_px"])); f["extraFrames"] += int(d["extra_frames"])
            f["nativeMoving"] += int(d["native_moving"]); f["nativeStepSumPx"] += float(d["native_step_sum_px"])
            f["nativeStepMaxPx"] = max(f["nativeStepMaxPx"], float(d["native_step_max_px"])); f["nativeStalls"] += int(d.get("native_stalls", 0))
            bins = [int(x) for x in d.get("bins", "").split(",") if x != ""]
            if len(f["bins"]) < len(bins):
                f["bins"] += [0] * (len(bins) - len(f["bins"]))
            for i, count in enumerate(bins):
                f["bins"][i] += count
        else:
            f["costReports"] += 1
            f["sampled"] += int(d["sampled"]); f["changed"] += int(d["changed"]); f["framePayloadBytes"] += int(d["frame_payload_bytes"])
            f["captureCpuMs"] += float(d["capture_cpu_ms"]); f["motionEntries"] += int(d.get("motion_entries", 0))
            f["motionAnchors"] += int(d.get("motion_anchors", 0)); f["motionBytes"] += int(d.get("motion_bytes", 0)); f["motionCpuMs"] += float(d.get("motion_cpu_ms", 0))
            f["repositions"] += int(d.get("repositions", 0))
    for f in families.values():
        n = f["shapeFrames"]
        f["errorMeanPx"] = f["errorSumPx"] / n if n else None
        f["errorP95Px"] = percentile_from_bins(f["bins"], .95)
        f["over1Fraction"] = f["over1"] / n if n else None
        f["holdMeanPx"] = f["holdSumPx"] / f["holdFrames"] if f["holdFrames"] else None
        f["nativeStepMeanPx"] = f["nativeStepSumPx"] / f["nativeMoving"] if f["nativeMoving"] else None
        f["nativeStallFraction"] = f["nativeStalls"] / f["nativeMoving"] if f["nativeMoving"] else None
        f["shapeLoss"] = None if not n else bool((f["errorP95Px"] or 0) > 1 or f["errorMaxPx"] > 2)
    return families


def client_families(rows, mapping):
    families, seconds = {}, None
    times = [row["realtime"] for row in rows if row.get("kind") == "coop_present_window"]
    if len(times) >= 2:
        seconds = times[-1] - times[0] + 1
    for row in rows:
        if row.get("kind") != "coop_present_window":
            continue
        d = parse_detail(row.get("detail"))
        for item in d.get("ids", "").split("|"):
            parts = item.split(":")
            if len(parts) < 1 + V2_FIELDS:
                continue
            bins = parse_delay_bins(parts[1 + len(CLIENT_FIELDS)]) if len(parts) > 1 + len(CLIENT_FIELDS) else None
            if len(parts) < 1 + len(CLIENT_FIELDS):
                parts = parts + ["nan"] * (1 + len(CLIENT_FIELDS) - len(parts))
            family = mapping.get(int(parts[0]), "UnmappedId")
            for target in (family, "*all*"):
                f = families.setdefault(target, {key: 0.0 for key in CLIENT_FIELDS})
                f.setdefault("delayBins", [0] * DELAY_BINS)
                if bins is not None:
                    f["delayBins"] = [a + b for a, b in zip(f["delayBins"], bins)]
                    f["v4"] = True
                accumulate_client(f, parts)
    for f in families.values():
        finish_client(f)
    return families, seconds


def accumulate_client(f, parts):
    for key, raw in zip(CLIENT_FIELDS, parts[1:]):
        value = float(raw)
        if key in MAX_FIELDS:
            f[key] = max(f[key], value)
        elif key == "marginMinMs":
            f[key] = value if f.get("_marginSeen") is None else min(f[key], value)
            f["_marginSeen"] = True
        else:
            f[key] += value


def finish_client(f):
    f.pop("_marginSeen", None)
    v4 = f.pop("v4", False)
    f["delayP95Ms"] = delay_percentile(f["delayBins"], .95) if v4 else None
    f["delayP50Ms"] = delay_percentile(f["delayBins"], .50) if v4 else None
    f["lagMeanMs"] = f["lagSumMs"] / f["moving"] if v4 and f["moving"] else None
    f["holdMeanMs"] = f["holdSumMs"] / f["moving"] if v4 and f["moving"] else None
    f["ageMeanMs"] = f["ageSumMs"] / f["arrivals"] if v4 and f["arrivals"] else None
    f["approvedHoldMeanMs"] = f["approvedHoldMs"] / f["approvedHolds"] if v4 and f["approvedHolds"] else None
    if not v4:
        for key in ("approvedHolds", "approvedHoldMs", "lagSumMs", "lagMaxMs", "holdSumMs", "holdMaxMs", "ageSumMs", "ageMaxMs"):
            f[key] = None
    f["interpolatedFraction"] = f["interpolated"] / f["presented"] if f["presented"] else None
    f["stallFraction"] = f["stalls"] / f["moving"] if f["moving"] else None
    f["stepMeanPx"] = f["stepSumPx"] / f["moving"] if f["moving"] else None
    f["delayMeanMs"] = f["delaySumMs"] / f["moving"] if f["moving"] else None
    f["starvedFraction"] = f["starved"] / f["continuous"] if f["continuous"] else None
    f["moveStarvedFraction"] = f["moveStarved"] / f["moveContinuous"] if f["moveContinuous"] and f["moveContinuous"] == f["moveContinuous"] else None
    f["moveStarvedMeanMs"] = f["moveStarvedMs"] / f["moveStarved"] if f["moveStarved"] and f["moveStarved"] == f["moveStarved"] else None
    f["lateFraction"] = f["late"] / f["arrivals"] if f["arrivals"] else None
    f["marginMeanMs"] = f["marginSumMs"] / f["arrivals"] if f["arrivals"] else None
    f["presentCpuUsPerFrame"] = f["cpuUs"] / f["presented"] if f["presented"] else None


def capture_problems(rows, name):
    problems = []
    if not rows:
        problems.append(name + ": empty capture")
        return problems
    if any(row.get("kind") == "capture_limit" for row in rows):
        problems.append(name + ": capture limit reached")
    dropped = max((row.get("dropped") or 0) for row in rows)
    if dropped:
        problems.append(f"{name}: {dropped} diagnostic rows dropped by the event budget")
    return problems


def analyze_run(folder):
    folder = Path(folder)
    config = json.loads((folder / "config.json").read_text(encoding="utf-8"))
    problems, data = [], {}
    if (folder / "failure.json").is_file():
        problems.append("smoke failed: " + json.loads((folder / "failure.json").read_text(encoding="utf-8")).get("error", "?"))
    for name in PROCESSES:
        path = folder / (name + "-motion.jsonl")
        if not path.is_file():
            problems.append(name + ": missing motion capture")
            data[name] = []
            continue
        data[name] = load_rows(path)
        problems += capture_problems(data[name], name)
    mapping = family_ids(data.get("server", []))
    server = server_families(data.get("server", []))
    clients = {}
    for name in ("client-1", "client-2"):
        families, seconds = client_families(data.get(name, []), mapping)
        clients[name] = {"seconds": seconds, "families": families}
    legacy = "-coop-r02-legacy" in " ".join(config.get("extraArgs", []))
    return {"folder": str(folder), "scene": config.get("scene"), "rendered": config.get("rendered"), "legacyStream": legacy,
            "assemblySha256": config.get("assemblySha256"), "problems": problems, "mappedIds": len(mapping),
            "server": server, "clients": clients}


def family_table(runs):
    """Per family: per-run values of the key metrics (valid runs only) and their mean/min/max."""
    keys_server = ("errorP95Px", "errorMaxPx", "errorMeanPx", "over1Fraction", "holdStarts", "holdMaxPx", "nativeStepMeanPx", "nativeStallFraction",
                   "shapeFrames", "framePayloadBytes", "motionBytes", "motionEntries", "motionAnchors", "repositions", "captureCpuMs", "motionCpuMs", "changed", "sampled")
    keys_client = ("starvedFraction", "moveStarvedFraction", "moveStarvedMeanMs", "stallFraction", "delayMeanMs", "delayMaxMs", "stepMeanPx", "stepMaxPx", "interpolatedFraction",
                   "lateFraction", "marginMeanMs", "presentCpuUsPerFrame", "moving", "pausedHeld", "pausedMoving",
                   "delayP95Ms", "delayP50Ms", "lagMeanMs", "lagMaxMs", "holdMeanMs", "holdMaxMs", "ageMeanMs", "ageMaxMs",
                   "approvedHolds", "approvedHoldMeanMs", "moveStarved")
    table = {}
    for run in runs:
        if run["problems"]:
            continue
        for family, values in run["server"].items():
            row = table.setdefault(family, {})
            for key in keys_server:
                if values.get(key) is not None:
                    row.setdefault("server." + key, []).append(values[key])
        for client, block in run["clients"].items():
            for family, values in block["families"].items():
                row = table.setdefault(family, {})
                for key in keys_client:
                    if values.get(key) is not None:
                        row.setdefault("clients." + key, []).append(values[key])
    out = {}
    for family, row in sorted(table.items()):
        out[family] = {key: {"values": vals, "mean": statistics.fmean(vals), "min": min(vals), "max": max(vals)} for key, vals in row.items()}
    return out


def compare_tables(reference, other):
    result = {}
    for family in sorted(set(reference) & set(other)):
        rows = {}
        for key in sorted(set(reference[family]) & set(other[family])):
            a, b = reference[family][key], other[family][key]
            overlap = a["min"] <= b["max"] and b["min"] <= a["max"]
            rows[key] = {"reference": a["mean"], "other": b["mean"], "delta": b["mean"] - a["mean"],
                         "classification": "not resolved (ranges overlap)" if overlap else "measured difference"}
        result[family] = rows
    return result


def shape_verdict(table, minimum_runs=2):
    verdict = {}
    for family, row in table.items():
        p95 = row.get("server.errorP95Px", {}).get("values", [])
        peak = row.get("server.errorMaxPx", {}).get("values", [])
        losing = sum(1 for a, b in zip(p95, peak) if a > 1 or b > 2)
        verdict[family] = {"runsWithShapeData": len(p95), "runsLosingShape": losing,
                           "shapeLoss": losing >= minimum_runs, "fixed": bool(p95) and losing == 0}
    return verdict


# ---- capture-off cost ----

def iso_to_u(text):
    moment = datetime.fromisoformat(text.replace("Z", "+00:00")[:26] + ("+00:00" if text.endswith("Z") else ""))
    unix_ms = moment.replace(tzinfo=timezone.utc).timestamp() * 1000
    return unix_ms + 62135596800000 - U_EPOCH_MS


def fixture_markers(folder):
    markers = {}
    log = Path(folder) / "server.log"
    for line in log.read_text(encoding="utf-8", errors="replace").splitlines() if log.is_file() else []:
        for key, token in (("start", "[LoadTest] coop-world started"), ("pause", "[LoadTest] coop-world paused index=1")):
            if line.startswith(token) and " u=" in line and key not in markers:
                markers[key] = float(line.rsplit(" u=", 1)[1].split()[0])
    return markers


def server_metric_windows(folder, start, end, scene):
    rows = []
    log = Path(folder) / "server.log"
    for line in log.read_text(encoding="utf-8", errors="replace").splitlines():
        if not line.startswith("[ServerMetrics] "):
            continue
        try:
            row = json.loads(line[len("[ServerMetrics] "):])
        except ValueError:
            continue
        if row.get("scene") != scene or row.get("includesTransition") or row.get("players") != 2:
            continue
        stop = iso_to_u(row["utc"])
        if stop - 10000 >= start and stop <= end:  # 10 s windows ending at "utc", fully inside
            rows.append(row)
    return rows


def steady_cost_window(markers, seconds):
    """Select a fixed steady tail, excluding optional fixture setup before it."""
    if not math.isfinite(seconds) or seconds <= 0:
        raise ValueError("steady duration must be finite and positive")
    start = markers["pause"] - seconds * 1000
    if start < markers["start"]:
        raise ValueError("fixture window is shorter than the requested steady duration")
    return dict(markers, fixtureStart=markers["start"], start=start, steadySeconds=seconds)


def cost_run(folder, steady_seconds=None):
    folder = Path(folder)
    config = json.loads((folder / "config.json").read_text(encoding="utf-8"))
    problems, metrics = [], {}
    if (folder / "failure.json").is_file():
        problems.append("smoke failed: " + json.loads((folder / "failure.json").read_text(encoding="utf-8")).get("error", "?"))
    markers = fixture_markers(folder)
    if "start" not in markers or "pause" not in markers:
        problems.append("fixture window markers missing")
    elif steady_seconds is not None:
        try:
            markers = steady_cost_window(markers, steady_seconds)
        except ValueError as error:
            problems.append(str(error))
    reported = {}
    for name in PROCESSES:
        log = folder / (name + ".log")
        text = log.read_text(encoding="utf-8", errors="replace") if log.is_file() else ""
        if "[GameplayLatency] Capture: " in text:
            problems.append(name + " wrote a motion capture")
        installed = frame_rows(log)[1]
        reported[name] = installed
        if installed is None or "motionCapture=False" not in installed:
            problems.append(f"{name} did not report motionCapture=False ({installed})")
    files = sorted(p.name for p in folder.glob("*-motion.jsonl"))
    if files:
        problems.append("motion capture files present: " + ", ".join(files))
    if not problems:
        start, end = markers["start"], markers["pause"]
        samples = json.loads((folder / "cpu-samples.json").read_text(encoding="utf-8"))
        inside = [s for s in samples if start <= s["u"] <= end]
        for name in PROCESSES:
            cycles = [(s["u"], s[name + "#cycles"]) for s in inside if s.get(name + "#cycles") is not None]
            if len(cycles) >= 2 and cycles[-1][0] > cycles[0][0]:
                metrics[name + ".mcyclesPerSecond"] = (cycles[-1][1] - cycles[0][1]) / ((cycles[-1][0] - cycles[0][0]) / 1000) / 1e6
            rows, _ = frame_rows(folder / (name + ".log"))
            frames = [r for r in rows if r["u"] - 1000 * r["seconds"] >= start and r["u"] <= end]
            if frames:
                count = sum(r["frames"] for r in frames); seconds = sum(r["seconds"] for r in frames)
                metrics[name + ".frame.workMeanMs"] = sum(r["workMeanMs"] * r["frames"] for r in frames) / count
                metrics[name + ".frame.workP95Ms"] = statistics.fmean(r["workP95Ms"] for r in frames)
                metrics[name + ".frame.workMaxMs"] = max(r["workMaxMs"] for r in frames)
                metrics[name + ".frame.framesPerSecond"] = count / seconds
                metrics[name + ".frame.gcAllocKiBPerSecond"] = sum(r["gcAllocKiB"] for r in frames) / seconds
                metrics[name + ".frame.gcCollectionsPerMinute"] = 60 * sum(r["gcCollections"] for r in frames) / seconds
                metrics[name + ".frame.secondsCovered"] = seconds
        windows = server_metric_windows(folder, start, end, config.get("scene"))
        if not windows:
            problems.append("no [ServerMetrics] window fully inside the fixture window")
        else:
            metrics["server.incomingKiBPerSecond"] = statistics.fmean(w["incomingKiBPerSecond"] for w in windows)
            metrics["server.outgoingKiBPerSecond"] = statistics.fmean(w["outgoingKiBPerSecond"] for w in windows)
            names = windows[0].get("markerNames", [])
            for i, marker in enumerate(names):
                if marker.startswith("SB6.Coop"):
                    metrics["serverMarkerMs." + marker] = statistics.fmean(w["averageMarkerMs"][i] for w in windows)
        if len([k for k in metrics if k.endswith(".mcyclesPerSecond")]) != 3:
            problems.append("cycle samples did not cover the window for the three processes")
    return {"folder": str(folder), "assemblySha256": config.get("assemblySha256"), "extraArgs": config.get("extraArgs"),
            "window": markers, "frameStatsInstalled": reported, "problems": problems, "metrics": metrics}


SPIKE_MS = 500.0


def delay_spikes(folder, threshold=SPIKE_MS):
    """Per client window and sprite id with a delay maximum above the threshold (v=4 windows only): which part
    dominates (presentation hold >= 50% of the delay, otherwise network/timeline lag), with the arrival age and the
    RTT/buffer of that window and the RTT range of the same +-2 s in both clients (shared network events).

    Origin (rule fixed before it was applied to the R02 data): the largest frameMs logged by each process within +-2 s
    is a lower bound of its main-thread stall. "server stall" when the server's is >= 50% of the delay maximum, else
    "client stall" when that client's own is, else "Mirror-observed delay (origin not isolated)": transport and OS
    scheduling of the processes cannot be told apart with these logs, so the delay is not attributed to the network."""
    folder = Path(folder)
    data = {name: load_rows(folder / (name + "-motion.jsonl")) for name in ("client-1", "client-2") if (folder / (name + "-motion.jsonl")).is_file()}
    server_path = folder / "server-motion.jsonl"
    mapping = family_ids(load_rows(server_path)) if server_path.is_file() else {}
    index = len(CLIENT_FIELDS)
    position = {key: i + 1 for i, key in enumerate(CLIENT_FIELDS)}
    spikes = []
    for name, rows in data.items():
        for row in rows:
            if row.get("kind") != "coop_present_window" or not parse_detail(row.get("detail")).get("v") == "4":
                continue
            for item in parse_detail(row.get("detail")).get("ids", "").split("|"):
                parts = item.split(":")
                if len(parts) <= index:
                    continue
                delay = float(parts[position["delayMaxMs"]])
                if delay <= threshold:
                    continue
                hold, lag, age = (float(parts[position[k]]) for k in ("holdMaxMs", "lagMaxMs", "ageMaxMs"))
                spikes.append({"client": name, "utc": row.get("utc"), "realtime": row.get("realtime"), "family": mapping.get(int(parts[0]), "UnmappedId"),
                               "delayMaxMs": delay, "holdMaxMs": hold, "lagMaxMs": lag, "ageMaxMs": age,
                               "dominant": "presentation hold" if hold >= .5 * delay else "network/timeline lag",
                               "rttMs": row.get("rttMs"), "bufferMs": row.get("bufferMs")})
    timelines = {name: [(iso_to_u(r["utc"]), r["rttMs"], r.get("bufferMs")) for r in rows if r.get("utc") and r.get("rttMs") is not None]
                 for name, rows in data.items()} if spikes else {}
    frames = {}
    if spikes:
        sources = dict(data)
        if server_path.is_file():
            sources["server"] = load_rows(server_path)
        frames = {name: [(iso_to_u(r["utc"]), float(r["frameMs"])) for r in rows if r.get("utc") and isinstance(r.get("frameMs"), (int, float))]
                  for name, rows in sources.items()}
    for spike in spikes:
        u = iso_to_u(spike["utc"]) if spike.get("utc") else None
        for name, timeline in frames.items():
            near = [ms for at, ms in timeline if u is not None and abs(at - u) <= 2000]
            spike[name + ".frameMaxMs"] = max(near) if near else None
        half = .5 * spike["delayMaxMs"]
        if (spike.get("server.frameMaxMs") or 0) >= half:
            spike["origin"] = "server stall"
        elif (spike.get(spike["client"] + ".frameMaxMs") or 0) >= half:
            spike["origin"] = "client stall"
        else:
            spike["origin"] = "Mirror-observed delay (origin not isolated)"
        for name, timeline in timelines.items():
            near = [(rtt, buffer) for at, rtt, buffer in timeline if u is not None and abs(at - u) <= 2000]
            spike[name + ".rttRangeMs"] = [min(n[0] for n in near), max(n[0] for n in near)] if near else None
            buffers = [n[1] for n in near if n[1] is not None]
            spike[name + ".bufferRangeMs"] = [min(buffers), max(buffers)] if buffers else None
    return spikes


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = parser.add_subparsers(dest="mode", required=True)
    fam = sub.add_parser("families")
    fam.add_argument("--group", action="append", nargs="+", required=True, metavar=("NAME", "FOLDER"),
                     help="NAME followed by capture-on run folders; the first group is the reference")
    fam.add_argument("--output", type=Path, required=True)
    cost = sub.add_parser("cost")
    cost.add_argument("--group", action="append", nargs="+", required=True, metavar=("NAME", "FOLDER"),
                      help="NAME followed by capture-off run folders; the first group is the reference")
    cost.add_argument("--output", type=Path, required=True)
    cost.add_argument("--steady-seconds", type=float,
                      help="Measure only this fixed tail before the first pause; excludes optional fixture setup")
    spikes = sub.add_parser("spikes")
    spikes.add_argument("folders", nargs="+", type=Path)
    spikes.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    if args.mode == "spikes":
        found = {str(folder): delay_spikes(folder) for folder in args.folders}
        args.output.parent.mkdir(parents=True, exist_ok=True)
        origins = {folder: dict(Counter(item["origin"] for item in items)) for folder, items in found.items()}
        args.output.write_text(json.dumps({"thresholdMs": SPIKE_MS, "origins": origins, "spikes": found}, indent=2), encoding="utf-8")
        print(json.dumps(origins, indent=2))
        return
    groups = {group[0]: group[1:] for group in args.group}
    if args.mode == "families":
        runs = {name: [analyze_run(f) for f in folders] for name, folders in groups.items()}
        tables = {name: family_table(r) for name, r in runs.items()}
        names = list(groups)
        report = {"rules": __doc__.split("Rules", 1)[1].strip(), "runs": runs, "families": tables,
                  "shapeVerdict": {name: shape_verdict(table) for name, table in tables.items()},
                  "validRuns": {name: sum(not r["problems"] for r in rs) for name, rs in runs.items()},
                  "comparisons": {f"{other}-vs-{names[0]}": compare_tables(tables[names[0]], tables[other]) for other in names[1:]}}
    else:
        runs = {name: [cost_run(f, args.steady_seconds) for f in folders] for name, folders in groups.items()}
        summaries = {name: summary(r) for name, r in runs.items()}
        names = list(groups)
        report = {"rules": __doc__.split("Rules", 1)[1].strip(), "runs": runs, "summary": summaries,
                  "steadySeconds": args.steady_seconds,
                  "validRuns": {name: sum(not r["problems"] for r in rs) for name, rs in runs.items()},
                  "differences": {f"{other}-minus-{names[0]}": difference(summaries[names[0]], summaries[other]) for other in names[1:]}}
        budget = 1000.0 / 60
        report["frameWorkBudgetShare"] = {pair: {key: {"meanDeltaMs": d["meanDelta"], "shareOf16_7msPercent": 100 * d["meanDelta"] / budget,
                                                       "classification": d["classification"]}
                                                 for key, d in diffs.items() if key.endswith(".frame.workMeanMs")}
                                          for pair, diffs in report["differences"].items()}
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps({"validRuns": report["validRuns"], "output": str(args.output)}, indent=2))


if __name__ == "__main__":
    main()
