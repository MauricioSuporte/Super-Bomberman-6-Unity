"""R01 remote-player cost: baseline (pre-R01) vs R01 vs R01 with its A/B switches off.

Input: online run folders from r01_compare_run.py (no --capture), grouped per configuration.
Rules fixed BEFORE the measured runs (07/11/2026):
  - a run is valid only if the server and both clients exit 0, the scripted window was seen on client 1, and the server logged
    at least one non-transition BattleMode_15 window with 2 players; invalid runs are listed, never silently dropped;
  - process CPU (% of one core) for server, client-1 (owner of the walking P1) and client-2 (observer: renders the remote
    player) over the scripted window, from GetProcessTimes; server [ServerMetrics] cpu, KiB/s in/out and BattlePerf markers
    over the non-transition BattleMode_15 windows (whole match, 10 s windows);
  - per configuration: mean, min, max, n. A difference to the baseline is "not resolved" when the min-max ranges overlap,
    otherwise it is reported as a measured difference with its size. No pass/fail threshold is applied to CPU;
  - attribution: baseline -> R01 is the total change of the build (all R01 code plus anything else in the diff);
    R01 -> R01 switches off isolates only pose enforcement and the owner mount gate. The input queue and the stun fix have no
    switch and cannot be isolated; the process totals include Unity rendering, Mirror and the OS scheduler.
Instrument amendment (07/11/2026, after session R01-measure-20261107; its GetProcessTimes figures are kept and reported as
unresolved): the clients use ~1 s of CPU per 70 s, a few 15.6 ms scheduler ticks per window, so GetProcessTimes cannot
resolve them. Runs that recorded QueryProcessCycleTime also report exact cycles per second (Mcycles/s) per process over the
same scripted window; the same overlap rule applies. No other rule changed.
Capture-off session (fixed 07/11/2026 BEFORE session R01-measure-20261107d; builds baseline-nodiag2 and r01-nodiag, both with
GameplayLatencyDiagnostics.json "enabled": false, same harness, same Packages, same scenario/duration as before):
  - 5 repetitions per configuration, interleaved baseline / r01 / r01 switches off; same overlap rule (min-max of the 5);
  - with --require-capture-off a run is valid only if each of the 3 processes logged "[R01Frame] installed ... motionCapture=False",
    no process logged "[GameplayLatency] Capture: " and the run folder holds no *-motion.jsonl;
  - additionally reported per process from the harness rows "[R01Frame]" (runs with -r01-frame-stats) fully inside the scripted
    window, whose exact UTC bounds come from client 1's "[R01Compare] script_start/script_done ... u=" lines: main-thread frame
    work time (EarlyUpdate start to the end of PostLateUpdate: the frame-rate wait is excluded) as frame-weighted mean, mean
    per-second p95 and max; frames per second; managed allocations (KiB/s, "GC Allocated In Frame") and gen-0 collections.
    The rows are written by the same harness code in both builds, so their own cost is common to both;
  - the experience criterion for this session is the absolute frame cost: a measured (non-overlapping) difference in work time
    is reported in ms per frame and as a share of the 16.7 ms frame budget at 60 fps; no pass/fail threshold is applied.
"""
import argparse
import json
from pathlib import Path
import statistics

PROCESSES = ("server", "client-1", "client-2")


def server_windows(folder):
    windows = []
    log = Path(folder) / "server.log"
    for line in log.read_text(encoding="utf-8", errors="replace").splitlines():
        if line.startswith("[ServerMetrics] "):
            try:
                row = json.loads(line[len("[ServerMetrics] "):])
            except ValueError:
                continue
            if row.get("scene") == "BattleMode_15" and not row.get("includesTransition") and row.get("players") == 2:
                windows.append(row)
    return windows


def process_cpu(folder, start, end):
    samples = json.loads((Path(folder) / "cpu-samples.json").read_text(encoding="utf-8"))
    inside = [s for s in samples if start <= s["t"] <= end]
    result = {}
    for name in PROCESSES:
        points = [(s["t"], s.get(name)) for s in inside if s.get(name) is not None]
        if len(points) >= 2 and points[-1][0] > points[0][0]:
            result[name + ".cpuPercentScripted"] = 100.0 * (points[-1][1] - points[0][1]) / (points[-1][0] - points[0][0])
        cycles = [(s["t"], s.get(name + "#cycles")) for s in inside if s.get(name + "#cycles") is not None]
        if len(cycles) >= 2 and cycles[-1][0] > cycles[0][0]:
            result[name + ".mcyclesPerSecondScripted"] = (cycles[-1][1] - cycles[0][1]) / (cycles[-1][0] - cycles[0][0]) / 1e6
    return result


def utc_markers(folder):
    # Exact UTC (harness "u", ms) of the scripted window, from client 1's own log.
    markers = {}
    log = Path(folder) / "client-1.log"
    if log.is_file():
        for line in log.read_text(encoding="utf-8", errors="replace").splitlines():
            for kind in ("script_start", "script_done"):
                if line.startswith("[R01Compare] " + kind + " ") and " u=" in line and kind not in markers:
                    markers[kind] = float(line.rsplit(" u=", 1)[1].strip())
    return markers


def frame_rows(log):
    rows, installed = [], None
    if log.is_file():
        for line in log.read_text(encoding="utf-8", errors="replace").splitlines():
            if line.startswith("[R01Frame] installed"):
                installed = line
            elif line.startswith("[R01Frame] {"):
                try:
                    rows.append(json.loads(line[len("[R01Frame] "):]))
                except ValueError:
                    pass
    return rows, installed


def frame_metrics(folder, markers):
    result = {}
    if "script_start" not in markers or "script_done" not in markers:
        return result
    for name in PROCESSES:
        rows, _ = frame_rows(Path(folder) / (name + ".log"))
        inside = [r for r in rows if r["u"] - 1000 * r["seconds"] >= markers["script_start"] and r["u"] <= markers["script_done"]]
        if not inside:
            continue
        frames = sum(r["frames"] for r in inside)
        seconds = sum(r["seconds"] for r in inside)
        result[name + ".frame.workMeanMs"] = sum(r["workMeanMs"] * r["frames"] for r in inside) / frames
        result[name + ".frame.workP95Ms"] = statistics.fmean(r["workP95Ms"] for r in inside)
        result[name + ".frame.workMaxMs"] = max(r["workMaxMs"] for r in inside)
        result[name + ".frame.framesPerSecond"] = frames / seconds
        if all(r.get("gcAllocKiB") is not None for r in inside):
            result[name + ".frame.gcAllocKiBPerSecond"] = sum(r["gcAllocKiB"] for r in inside) / seconds
        result[name + ".frame.gcCollectionsPerMinute"] = 60 * sum(r["gcCollections"] for r in inside) / seconds
        result[name + ".frame.secondsCovered"] = seconds
    return result


def run_metrics(folder, require_capture_off=False):
    folder = Path(folder)
    config = json.loads((folder / "config.json").read_text(encoding="utf-8"))
    problems = []
    exits = config.get("exits", {})
    for name in PROCESSES:
        if exits.get(name) != 0:
            problems.append(f"{name} exit {exits.get(name)}")
    window = config.get("scriptWindowWallClock", {})
    if "script_start" not in window or "script_done" not in window:
        problems.append("scripted window not seen on client 1")
    windows = server_windows(folder)
    if not windows:
        problems.append("no non-transition BattleMode_15 server window with 2 players")
    cpu = process_cpu(folder, window["script_start"], window["script_done"]) if not problems else {}
    metrics = dict(cpu)
    if windows:
        for key in ("cpuPercentOneCore", "incomingKiBPerSecond", "outgoingKiBPerSecond", "p95FrameMs", "gcAllocatedKiBPerSecond"):
            metrics["serverMetrics." + key] = statistics.fmean(w[key] for w in windows)
        names = windows[0]["markerNames"]
        for i, name in enumerate(names):
            metrics["serverMarkerMs." + name.removeprefix("SB6.BattlePerf.")] = statistics.fmean(w["averageMarkerMs"][i] for w in windows)
    capture, reported = {}, {}
    for name in PROCESSES:  # whether GameplayLatencyDiagnostics wrote a capture / reported itself enabled in that process
        log = folder / (name + ".log")
        capture[name] = log.is_file() and "[GameplayLatency] Capture: " in log.read_text(encoding="utf-8", errors="replace")
        installed = frame_rows(log)[1]
        reported[name] = None if installed is None else ("motionCapture=True" in installed)
    files = sorted(p.name for p in folder.glob("*-motion.jsonl"))
    if require_capture_off:
        for name in PROCESSES:
            if capture[name]:
                problems.append(f"{name} wrote a motion capture")
            if reported[name] is not False:
                problems.append(f"{name} did not report motionCapture=False (reported {reported[name]})")
        if files:
            problems.append("motion capture files present: " + ", ".join(files))
    markers = utc_markers(folder)
    if not problems:
        metrics.update(frame_metrics(folder, markers))
    return {"folder": str(folder), "assemblySha256": config.get("assemblySha256"), "motionCaptureActive": capture,
            "motionCaptureReportedEnabled": reported, "motionCaptureFiles": files, "scriptWindowUtc": markers, "clientExtra": config.get("clientExtra"),
            "seconds": config.get("seconds"), "serverWindows": len(windows),
            "scriptSeconds": (window["script_done"] - window["script_start"]) if "script_done" in window and "script_start" in window else None,
            "problems": problems, "metrics": metrics}


def summary(runs):
    valid = [r for r in runs if not r["problems"]]
    keys = sorted({k for r in valid for k in r["metrics"]})
    return {key: {"n": len(values), "mean": statistics.fmean(values), "min": min(values), "max": max(values),
                  "stdev": statistics.stdev(values) if len(values) > 1 else None}
            for key in keys for values in [[r["metrics"][key] for r in valid if key in r["metrics"]]] if values}


def difference(reference, other):
    result = {}
    for key in sorted(set(reference) & set(other)):
        a, b = reference[key], other[key]
        overlap = a["min"] <= b["max"] and b["min"] <= a["max"]
        result[key] = {"meanDelta": b["mean"] - a["mean"],
                       "meanDeltaPercent": 100 * (b["mean"] - a["mean"]) / a["mean"] if a["mean"] else None,
                       "rangesOverlap": overlap, "classification": "not resolved (ranges overlap)" if overlap else "measured difference"}
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--baseline", nargs="+", type=Path, required=True)
    parser.add_argument("--r01", nargs="+", type=Path, required=True)
    parser.add_argument("--r01-switches-off", nargs="+", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--require-capture-off", action="store_true")
    args = parser.parse_args()
    off = args.require_capture_off
    groups = {"baseline": [run_metrics(f, off) for f in args.baseline], "r01": [run_metrics(f, off) for f in args.r01],
              "r01SwitchesOff": [run_metrics(f, off) for f in args.r01_switches_off]}
    summaries = {name: summary(runs) for name, runs in groups.items()}
    report = {"rules": __doc__.split("Rules", 1)[1].strip(), "runs": groups, "summary": summaries,
              "r01MinusBaseline": difference(summaries["baseline"], summaries["r01"]),
              "r01MinusSwitchesOff": difference(summaries["r01SwitchesOff"], summaries["r01"]),
              "validRuns": {name: sum(not r["problems"] for r in runs) for name, runs in groups.items()},
              "captureOffRequired": off}
    budget = 1000.0 / 60
    report["frameWorkBudgetShare"] = {key: {"meanDeltaMs": d["meanDelta"], "shareOf16_7msFramePercent": 100 * d["meanDelta"] / budget,
                                            "classification": d["classification"]}
                                      for key, d in report["r01MinusBaseline"].items() if key.endswith(".frame.workMeanMs")}
    args.output.write_text(json.dumps(report, indent=2, allow_nan=False) + "\n", encoding="utf-8")
    print(json.dumps(report["validRuns"]))


if __name__ == "__main__":
    main()
