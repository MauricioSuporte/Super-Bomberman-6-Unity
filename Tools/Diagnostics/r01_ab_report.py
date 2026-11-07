"""R01: per-run and per-configuration report for the remote-player presentation A/B.

For each smoke folder (server-motion.jsonl, client-N-motion.jsonl, server.log) it measures, per observer:
- visible root steps flagged by the observer (`root_backstep` rows above 0.1 u), each placed on the DENSE server path
  (`-online-dense-server-reference`, one server row per frame): both endpoints are mapped to the server time tau of the
  authoritative state they show (nearest to the presented time, never in the future):
    path_forward = tau moved forward (stale root catching up, or the server's own reversal shown in order): no back-step,
    presentation = tau moved back: the observer showed an earlier authoritative state again (a real back-step),
    off_path     = an endpoint is on no server segment (the observer showed a state the server never had),
    ambiguous_reversal = the server path doubles back within ~2 frames and both readings fit (not a pass, not a failure),
    unverifiable = the server reference has a capture gap (missing frames) there; a run with any is inconclusive;
- holds of the remote presentation clock (count, reasons, max gap = accumulated delay, recovery durations);
- presented delay against Mirror's clock (mirror - ptime) and alignment of root, pose and mount on one time;
- action edges sent by each owner vs edges the server applied (an applied edge with no sent press = duplicate);
- pause windows and mounted deaths (mount-visuals): back-steps, foot body + visible mount, mount visible while dead.
Nothing is dropped silently; counts that cannot be measured are reported as unavailable. This never accepts R01.
"""

import argparse
import bisect
import json
import math
import re
import statistics
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from analyze_remote_player import GAP_S, classify, consecutive_frames, fields, load, num  # noqa: E402

VISIBLE = 0.1
FOOT = {"Up", "Down", "Left", "Right", "Ball"}
BUTTONS = {16: "A", 32: "B", 64: "C", 128: "Select"}


def server_tracks(rows):
    tracks = {}
    for r in rows:
        if r.get("kind") != "player_visual_sample" or r.get("role") not in ("server", "host") or not isinstance(r.get("position"), dict):
            continue
        p = num(fields(r).get("ptime"))
        if p is not None and r.get("netId"):
            tracks.setdefault(r["netId"], []).append((p, r["position"]["x"], r["position"]["y"], r.get("frame")))
    for t in tracks.values():
        t.sort(key=lambda x: x[0])
    return tracks


def capture_gaps(track):
    capture = simulation = 0
    for a, b in zip(track, track[1:]):
        if b[0] - a[0] > GAP_S:
            if consecutive_frames(a[3], b[3]):
                simulation += 1
            else:
                capture += 1
    return capture, simulation


def ref_at(track, keys, t):
    """Server position at server time t, or None when t falls in a capture gap or outside the track."""
    i = bisect.bisect_left(keys, t)
    if i < len(track) and keys[i] == t:
        return track[i][1], track[i][2]
    if i == 0 or i >= len(track):
        return None
    a, b = track[i - 1], track[i]
    if b[0] - a[0] > GAP_S and not consecutive_frames(a[3], b[3]):
        return None
    k = 0.0 if b[0] == a[0] else (t - a[0]) / (b[0] - a[0])
    return a[1] + (b[1] - a[1]) * k, a[2] + (b[2] - a[2]) * k


def stats(values):
    if not values:
        return None
    values = sorted(values)
    return dict(n=len(values), median=round(statistics.median(values), 1), p95=round(values[min(len(values) - 1, int(0.95 * len(values)))], 1), max=round(values[-1], 1))


def observer_report(obs, track):
    keys = [t[0] for t in track]
    out = dict(classes=dict(path_forward=0, presentation=0, off_path=0, ambiguous_reversal=0, unverifiable=0, unclassified=0), examples=[])
    for r in obs:
        if r.get("kind") != "root_backstep":
            continue
        f = fields(r)
        if num(f.get("units"), 0) <= VISIBLE:
            continue
        c = classify(r, track, keys)
        out["classes"][c] += 1
        if c != "path_forward" and len(out["examples"]) < 4:
            out["examples"].append(dict(cls=c, units=f.get("units"), ptime=f.get("ptime"), pprev=f.get("pprev"), holding=f.get("holding")))
    summaries = [fields(r) for r in obs if r.get("kind") == "root_motion_summary"]
    out["movingFrames"] = int(sum(num(s.get("moving_frames"), 0) for s in summaries))
    out["misalignedFrames"] = int(sum(num(s.get("misaligned"), 0) for s in summaries)) if any("misaligned" in s for s in summaries) else None
    ends = [((r.get("detail") or "").split(";")[0], fields(r)) for r in obs if r.get("kind") == "timeline_slew_end"]
    reasons = {}
    for reason, _ in ends:
        reasons[reason] = reasons.get(reason, 0) + 1
    out["holds"] = dict(count=len(ends), reasons=reasons,
                        maxGapMs=stats([num(e.get("max_gap_ms"), 0) for _, e in ends]),
                        recoveryMs=stats([num(e.get("duration_ms"), 0) for reason, e in ends if reason == "caught_up"]),
                        totalHoldMs=round(sum(num(e.get("duration_ms"), 0) for _, e in ends), 1))
    lag = []
    for r in obs:
        if r.get("kind") == "player_visual_sample" and isinstance(r.get("timeline"), (int, float)):
            p = num(fields(r).get("ptime"))
            if p is not None:
                lag.append((r["timeline"] - p) * 1000)
    out["mirrorMinusPresentedMs"] = stats(lag)
    # pose and mount presenters on one time: same frame, same entity, same ptime
    pose = {(r.get("frame"), r.get("entity")): fields(r).get("ptime") for r in obs if r.get("kind") == "player_visual_sample"}
    mount = [(r.get("frame"), r.get("entity"), fields(r).get("ptime")) for r in obs if r.get("kind") == "mount_visual_sample"]
    pairs = [(pose[(fr, en)], pt) for fr, en, pt in mount if (fr, en) in pose]
    out["poseMountSameFrame"] = dict(pairs=len(pairs), mismatched=sum(1 for a, b in pairs if a != b))
    return out


def foot_mount(rows):
    edges = [(r, fields(r)) for r in rows if r.get("kind") == "mount_body_edge"]
    bad = []
    for r, f in edges:
        body = set(filter(None, (f.get("body") or "").split(",")))
        if body & FOOT and f.get("mount"):
            bad.append(dict(frame=r.get("frame"), player=r.get("player"), local=r.get("localPlayer"), body=f.get("body"), mount=f.get("mount")))
    return bad


def paused_windows(rows):
    """Client-side pause intervals [start, end] on the shared UTC clock, from the probe's state string."""
    marks = sorted((r["_t"], "paused=True" in (r.get("detail") or "")) for r in rows
                   if r.get("kind") == "motion_sample" and r.get("_t") is not None)
    windows, start = [], None
    for t, paused in marks:
        if paused and start is None:
            start = t
        elif not paused and start is not None:
            windows.append((start, t)); start = None
    return windows


def actions(server_rows, client_rows):
    """Edges each owner sent (input_send pressed bits) vs edges the server applied (network_action_edge)."""
    out = {}
    for name, rows in client_rows.items():
        local = [r for r in rows if r.get("localPlayer") and r.get("netId")]
        if not local:
            continue
        net = local[0]["netId"]
        sent = {b: 0 for b in BUTTONS}
        for r in rows:
            if r.get("kind") == "input_send" and r.get("localPlayer"):
                for b in BUTTONS:
                    if (r.get("pressed") or 0) & b:
                        sent[b] += 1
        applied = {b: 0 for b in BUTTONS}
        for r in server_rows:
            if r.get("kind") == "network_action_edge" and r.get("netId") == net:
                for b in BUTTONS:
                    if (r.get("pressed") or 0) & b:
                        applied[b] += 1
        drops = max((r.get("dropped") or 0 for r in rows), default=0)
        out[name] = {BUTTONS[b]: dict(sent=sent[b], applied=applied[b], duplicated=max(0, applied[b] - sent[b])) for b in BUTTONS}
        out[name]["clientCaptureDropped"] = drops
    return out


def run_report(folder):
    folder = Path(folder)
    server = load(folder / "server-motion.jsonl")
    clients = {p.stem: load(p) for p in sorted(folder.glob("client-*-motion.jsonl"))}
    tracks = server_tracks(server)
    rep = dict(folder=folder.name, serverCaptureDropped=max((r.get("dropped") or 0 for r in server), default=0), observers=[], tracks={})
    for net, track in tracks.items():
        cap, sim = capture_gaps(track)
        rep["tracks"][net] = dict(rows=len(track), captureGaps=cap, simulationGaps=sim)
    totals = dict(path_forward=0, presentation=0, off_path=0, ambiguous_reversal=0, unverifiable=0, unclassified=0, movingFrames=0, holds=0, misaligned=0, poseMountMismatch=0)
    for name, rows in clients.items():
        by_net = {}
        for r in rows:
            if r.get("role") == "client" and not r.get("localPlayer") and r.get("netId"):
                by_net.setdefault(r["netId"], []).append(r)
        for net, obs in sorted(by_net.items()):
            track = tracks.get(net, [])
            o = observer_report(obs, track)
            if o["movingFrames"] == 0 and not any(o["classes"].values()):
                continue
            o.update(client=name, netId=net)
            rep["observers"].append(o)
            for k in ("path_forward", "presentation", "off_path", "ambiguous_reversal", "unverifiable", "unclassified"):
                totals[k] += o["classes"][k]
            totals["movingFrames"] += o["movingFrames"]
            totals["holds"] += o["holds"]["count"]
            totals["misaligned"] += o["misalignedFrames"] or 0
            totals["poseMountMismatch"] += o["poseMountSameFrame"]["mismatched"]
    bad = totals["presentation"] + totals["off_path"]
    totals["presentationPer1000MovingFrames"] = round(1000 * bad / totals["movingFrames"], 3) if totals["movingFrames"] else None
    rep["totals"] = totals
    rep["footBodyWithMount"] = {name: foot_mount(rows) for name, rows in clients.items()}
    rep["actions"] = actions(server, clients)
    pauses = {}
    for name, rows in clients.items():
        wins = paused_windows(rows)
        if not wins:
            continue
        steps = [r for r in rows if r.get("kind") == "root_backstep" and r.get("_t") is not None and num(fields(r).get("units"), 0) > VISIBLE
                 and any(a <= r["_t"] <= b + 1.0 for a, b in wins)]
        renderer_edges = [r for r in rows if r.get("kind") == "player_renderer_edge" and not r.get("localPlayer") and r.get("_t") is not None
                          and any(a + 0.1 <= r["_t"] <= b for a, b in wins)]
        pauses[name] = dict(windows=[round(b - a, 2) for a, b in wins], visibleBackStepsInOrAfter=len(steps),
                            observedRendererChangesWhilePaused=len(renderer_edges))
    rep["pause"] = pauses or None
    dead = {}
    for name, rows in clients.items():
        # Observed players that are dead (probe state) must not show a mount: count mount samples inside dead intervals.
        dead_at = {}
        for r in rows:
            if r.get("kind") == "motion_sample" and not r.get("localPlayer") and r.get("_t") is not None and "dead=True" in (r.get("detail") or ""):
                dead_at.setdefault(r.get("entity"), []).append(r["_t"])
        visible = 0
        for r in rows:
            if r.get("kind") == "mount_visual_sample" and not r.get("localPlayer") and r.get("_t") is not None and fields(r).get("mount"):
                times = dead_at.get(r.get("entity"), [])
                if times and min(times) + 0.3 <= r["_t"] <= max(times):
                    visible += 1
        dead[name] = dict(observedDeadSamples=sum(len(v) for v in dead_at.values()), mountVisibleWhileDead=visible)
    rep["deadObserved"] = dead
    log = folder / "server.log"
    lines = log.read_text(encoding="utf-8", errors="replace").splitlines() if log.exists() else []
    rep["serverLog"] = [l.strip()[:200] for l in lines if re.search(r"\[LoadTest\] (visual (pause mounted|mounted death)|server-stall schedule)", l)]
    unverifiable = totals["unverifiable"] + totals["unclassified"] + totals["ambiguous_reversal"]
    rep["verdict"] = "inconclusive" if unverifiable else "measured"
    return rep


def aggregate(groups):
    out = {}
    for label, reports in groups.items():
        per_run = [r["totals"] for r in reports]
        def series(key):
            return [t[key] for t in per_run]
        out[label] = dict(runs=len(reports), inconclusiveRuns=sum(1 for r in reports if r["verdict"] != "measured"),
                          presentation=series("presentation"), offPath=series("off_path"), ambiguous=series("ambiguous_reversal"), pathForward=series("path_forward"), unverifiable=series("unverifiable"),
                          presentationPer1000=series("presentationPer1000MovingFrames"), holds=series("holds"),
                          misaligned=series("misaligned"), poseMountMismatch=series("poseMountMismatch"),
                          maxHoldGapMs=[max([o["holds"]["maxGapMs"]["max"] for o in r["observers"] if o["holds"]["maxGapMs"]] or [0]) for r in reports],
                          maxRecoveryMs=[max([o["holds"]["recoveryMs"]["max"] for o in r["observers"] if o["holds"]["recoveryMs"]] or [0]) for r in reports],
                          mirrorMinusPresentedP95Ms=[max([o["mirrorMinusPresentedMs"]["p95"] for o in r["observers"] if o["mirrorMinusPresentedMs"]] or [0]) for r in reports])
    return out


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--group", action="append", nargs="+", metavar=("LABEL", "FOLDER"), required=True,
                        help="a configuration label followed by its smoke folders; repeat per configuration")
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    if args.output.exists():
        parser.error("--output exists; refusing to overwrite a previous report")
    groups = {g[0]: [run_report(f) for f in g[1:]] for g in args.group}
    report = dict(aggregate=aggregate(groups), runs={k: v for k, v in groups.items()})
    args.output.write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps(report["aggregate"], indent=2))


if __name__ == "__main__":
    main()
