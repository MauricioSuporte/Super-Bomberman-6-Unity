"""R01: compare what an observer presents for a remote player against the server.

Inputs are the motion JSONL files written by Development builds (`-online-motion-logs`):
`player_visual_sample` (root position, visible renderers) and `mount_visual_sample`
(body offset, mount type). Server and clients run on one machine, so rows are joined on
the shared UTC clock and on the entity netId. Only client rows with localPlayer=false are
observer views.

An observer is *expected* to trail the server by its interpolation delay. That delay is
taken from `timeline - ptime` (ptime = the presentation time the presenters sample; logged
by newer builds) or else from the logged `bufferMs`. A constant lag near that value is
healthy; a stall (observer frozen while the server moved) or lag drift between windows is
not. Entities that do not move enough give no lag estimate and are reported as
inconclusive, never as 0 ms. Limits are provisional (no baseline exists) and this report
never declares R01 accepted.
"""

import argparse
import bisect
import json
import math
import re
import statistics
from datetime import datetime, timezone
from pathlib import Path

DEFAULTS = dict(max_lag_ms=600, lag_step_ms=20, min_samples=20, window_s=3.0, min_window_samples=15,
                min_travel=1.5, clip=2.0, teleport_units=1.0, lag_tolerance_ms=150, fallback_lag_limit_ms=250,
                drift_limit_ms=150, rms_limit=0.35, stall_s=0.5, stall_margin_s=0.3, arc_margin_s=0.6, tail_margin_s=0.3, stall_server_move=0.5, pose_tolerance_ms=250,
                min_pose_matches=3, min_state_ms=300, mount_p95_limit=0.5,
                ptime_window_s=0.35, ptime_gap_s=0.15, ptime_strict_rate=0.02, ptime_pos_p95=0.5, ptime_lag_disagree_ms=100, unverifiable_rate=0.25,
                backstep_visible_units=0.1)
NOT_MEASURED = [
    "perceptual/GPU acceptance and human input latency",
    "offline-vs-online comparison (needs an equivalent offline capture)",
    "transitions the fixture does not drive: death while mounted, pause, round change",
    "CPU and bytes cost (see metrics.jsonl and R02 baselines)",
    "per-tick correlation: observer rows carry no input/authoritative tick (owner-only field)",
]


def parse_utc(text):
    match = re.match(r"(\d{4})-(\d\d)-(\d\d)T(\d\d):(\d\d):(\d\d)(?:\.(\d+))?", text or "")
    if not match:
        return None
    y, mo, d, h, mi, s, frac = match.groups()
    base = datetime(int(y), int(mo), int(d), int(h), int(mi), int(s), tzinfo=timezone.utc).timestamp()
    return base + (float("0." + frac) if frac else 0.0)


def load(path):
    rows = []
    for line in Path(path).read_text(encoding="utf-8-sig").splitlines():
        try:
            row = json.loads(line)
        except json.JSONDecodeError:
            continue  # truncated last line of a writer that was still flushing
        row["_t"] = parse_utc(row.get("utc"))
        rows.append(row)
    return rows


def fields(row):
    return dict(p.split("=", 1) for p in (row.get("detail") or "").split(";") if "=" in p)


def interpolate(points, t, times=None):
    times = times if times is not None else [p[0] for p in points]
    j = bisect.bisect_right(times, t) - 1
    if 0 <= j < len(points) and times[j] == t:
        return points[j][1], points[j][2]  # exact sample (dense captures can repeat a time: the last row wins)
    i = bisect.bisect_left(times, t)
    if i == 0 or i >= len(points):
        return None  # never extrapolate the reference
    (t0, x0, y0), (t1, x1, y1) = points[i - 1], points[i]
    if t1 - t0 > 0.5 or t1 == t0:
        return None
    a = (t - t0) / (t1 - t0)
    return x0 + (x1 - x0) * a, y0 + (y1 - y0) * a


def consecutive_frames(a, b):
    """True when two server rows come from adjacent server frames: a time gap between them is the server not simulating
    (a hitch or stall), so linear interpolation between them is exactly what an observer can be shown, not a capture loss.
    Only dense captures (-online-dense-server-reference, one row per frame) can satisfy this."""
    fa, fb = a.get("frame") if isinstance(a, dict) else a, b.get("frame") if isinstance(b, dict) else b
    return isinstance(fa, int) and isinstance(fb, int) and 0 < fb - fa <= 1


def percentile(values, q):
    values = sorted(values)
    return values[min(len(values) - 1, int(math.ceil(q * len(values))) - 1)] if values else None


def lag_search(server, observer, cfg, lo=None, hi=None, min_n=None):
    """Lag minimising the clipped mean error of observer(t) against server(t - lag)."""
    obs = [p for p in observer if (lo is None or lo <= p[0] < hi)]
    min_n = min_n or cfg["min_samples"]
    best = None
    for lag_ms in range(0, cfg["max_lag_ms"] + 1, cfg["lag_step_ms"]):
        errors = []
        for t, x, y in obs:
            ref = interpolate(server, t - lag_ms / 1000)
            if ref:
                errors.append(math.hypot(x - ref[0], y - ref[1]))
        if len(errors) < min_n:
            continue
        cost = sum(min(e, cfg["clip"]) for e in errors) / len(errors)
        if best is None or cost < best[0] - 1e-9:
            best = (cost, lag_ms, errors)
    return best


def travel(points, lo, hi, step_limit):
    total, prev = 0.0, None
    for t, x, y in points:
        if lo <= t <= hi:
            if prev is not None and (t - prev[0]) <= 0.5:
                d = math.hypot(x - prev[1], y - prev[2])
                if d <= step_limit:
                    total += d
            prev = (t, x, y)
    return total


def root_report(server, observer, cfg):
    if not server or not observer:
        return dict(conclusive=False, reason="no root samples")
    lo, hi = observer[0][0], observer[-1][0]
    moved = travel(server, lo, hi, cfg["teleport_units"])
    if moved < cfg["min_travel"]:
        return dict(conclusive=False, reason=f"server moved only {moved:.2f} units; lag not identifiable")
    best = lag_search(server, observer, cfg)
    if best is None:
        return dict(conclusive=False, reason="not enough overlapping root samples")
    cost, lag, errors = best
    rep = dict(conclusive=True, lagMs=lag, rms=round(math.sqrt(sum(e * e for e in errors) / len(errors)), 4),
               medianError=round(statistics.median(errors), 4), p95=round(percentile(errors, .95), 4),
               max=round(max(errors), 4), samples=len(errors), serverTravel=round(moved, 2))
    windows, t = [], lo
    while t + cfg["window_s"] <= hi + 1e-9:
        end = t + cfg["window_s"]
        if travel(server, t - 0.6, end, cfg["teleport_units"]) >= cfg["min_travel"]:
            w = lag_search(server, observer, cfg, t, end, cfg["min_window_samples"])
            if w:
                windows.append(w[1])
        t = end
    rep["windowLagsMs"] = windows
    rep["driftMs"] = (max(windows) - min(windows)) if len(windows) >= 2 else None
    # Stalls: observer frozen for >= stall_s while the (lag-shifted) server moved.
    stalls, i = [], 0
    while i < len(observer):
        j = i
        while j + 1 < len(observer) and math.hypot(observer[j + 1][1] - observer[i][1], observer[j + 1][2] - observer[i][2]) < 0.01 \
                and observer[j + 1][0] - observer[j][0] <= 0.5:
            j += 1
        span = observer[j][0] - observer[i][0]
        margin = cfg["stall_margin_s"]  # motion starting/ending at the span edges is not a freeze
        if span >= cfg["stall_s"] + 2 * margin:
            a = interpolate(server, observer[i][0] - lag / 1000 + margin)
            b = interpolate(server, observer[j][0] - lag / 1000 - margin)
            if a and b and math.hypot(b[0] - a[0], b[1] - a[1]) > cfg["stall_server_move"]:
                stalls.append(round(span * 1000))
        i = j + 1
    rep["stalls"] = len(stalls)
    rep["longestStallMs"] = max(stalls) if stalls else 0
    count = 0
    for (t0, x0, y0), (t1, x1, y1) in zip(observer, observer[1:]):
        if t1 - t0 > 0.5 or math.hypot(x1 - x0, y1 - y0) <= cfg["teleport_units"]:
            continue
        s0, s1 = interpolate(server, t0 - lag / 1000), interpolate(server, t1 - lag / 1000)
        if s0 and s1 and math.hypot(s1[0] - s0[0], s1[1] - s0[1]) < cfg["teleport_units"] / 2:
            count += 1
    rep["discontinuities"] = count
    return rep


def expected_lag(observer_rows):
    """(ms, source): the interpolation delay the observer is designed to show."""
    ahead = []
    for r in observer_rows:
        if r.get("kind") != "player_visual_sample":
            continue
        try:
            ahead.append((float(r["timeline"]) - float(fields(r)["ptime"])) * 1000)
        except (KeyError, ValueError, TypeError):
            pass
    ahead = [a for a in ahead if 0 <= a < 2000]
    if len(ahead) >= 10 and statistics.median(ahead) >= 5:  # ~0 means `timeline` is already the presentation time
        return statistics.median(ahead), "timeline-ptime"
    buf = [r["bufferMs"] for r in observer_rows if isinstance(r.get("bufferMs"), (int, float)) and r["bufferMs"] > 0]
    if len(buf) >= 10:
        return statistics.median(buf), "bufferMs"
    return None, "unknown"


def states(rows):
    seq = []
    for r in rows:
        if r.get("kind") == "player_visual_sample" and r["_t"] is not None:
            s = fields(r).get("renderers", "")
            if not seq or seq[-1][1] != s:
                seq.append((r["_t"], s))
    return seq


def sustained_states(rows):
    """Names the observer showed for at least two consecutive samples; single-sample states are sampling aliasing."""
    names, run, prev = set(), 0, None
    for r in rows:
        if r.get("kind") != "player_visual_sample" or r["_t"] is None:
            continue
        cur = fields(r).get("renderers", "")
        run = run + 1 if cur == prev else 1
        prev = cur
        if run >= 2 and cur:
            names.add(cur)
    return names


def pose_report(server_rows, observer_rows, cfg):
    s, o = states(server_rows), states(observer_rows)
    sustained = sustained_states(observer_rows)
    durations = {}
    for (t0, name), (t1, _) in zip(s, s[1:]):
        durations[name] = max(durations.get(name, 0), (t1 - t0) * 1000)
    seen = {name for _, name in o}
    delays = []
    for t_o, name in o[1:]:
        cands = [t for t, n in s[1:] if n == name and abs(t_o - t) <= 1.0]
        if cands:
            delays.append((t_o - min(cands, key=lambda t: abs(t_o - t))) * 1000)
    rep = dict(serverStates=len(s), observerStates=len(o),
               missingLongStates=sorted(n for n, d in durations.items() if d >= cfg["min_state_ms"] and n not in seen),
               observerOnlyStates=sorted(n for n in (seen & sustained) - {name for _, name in s} if n),
               singleSampleOnlyStates=sorted(n for n in (seen - sustained) - {name for _, name in s} if n), matches=len(delays), conclusive=len(delays) >= cfg["min_pose_matches"])
    if delays:
        rep.update(delayMsMedian=round(statistics.median(delays), 1), delayMsP90=round(percentile(delays, .9), 1),
                   delayMsMax=round(max(delays), 1), earlierThanServer=sum(1 for d in delays if d < -150))
    return rep


def mount_report(server_rows, observer_rows, lag_ms, cfg):
    def pts(rows):
        return [(r["_t"], r["delta"]["x"], r["delta"]["y"]) for r in rows
                if r.get("kind") == "mount_visual_sample" and r["_t"] and isinstance(r.get("delta"), dict)]

    def generations(rows):
        seq = []
        for r in rows:
            f = fields(r)
            if r.get("kind") == "mount_visual_sample" and f.get("type") not in (None, "", "None") and "rev" in f:
                key = (f["type"], f["rev"])
                if not seq or seq[-1] != key:
                    seq.append(key)
        return seq
    rep, server, observer = dict(), pts(server_rows), pts(observer_rows)
    errors = []
    for t, x, y in observer:
        ref = interpolate(server, t - (lag_ms or 0) / 1000)
        if ref:
            errors.append(math.hypot(x - ref[0], y - ref[1]))
    rep.update(samples=len(errors), conclusive=len(errors) >= cfg["min_samples"] // 2)
    if rep["conclusive"]:
        rep.update(medianError=round(statistics.median(errors), 4), p95Error=round(percentile(errors, .95), 4))
    gs, go = generations(server_rows), generations(observer_rows)
    if gs or go:
        known = set(gs)

        def plausible(key):
            # frameRevision is one counter per player: the server bumps it on every (un)mount and logs nothing while
            # unmounted, while observers still show the old type with the intermediate revision. Only a type the server
            # never had, or a revision outside the server's overall range, is a fault.
            revs = [int(r) for _, r in gs if str(r).isdigit()]
            return key[0] in {t for t, _ in gs} and bool(revs) and str(key[1]).isdigit() and min(revs) <= int(key[1]) <= max(revs)
        rep["generations"] = dict(server=len(gs), observer=len(go),
                                  observerOnly=sorted(k for k in set(go) - known if not plausible(k))[:5],
                                  intermediate=sorted(k for k in set(go) - known if plausible(k))[:8],
                                  serverNotSeen=len(known - set(go)))
    else:
        rep["generations"] = "not logged (build predates rev= instrumentation)"
    return rep


def ptime_of(row):
    try:
        return float(fields(row)["ptime"])
    except (KeyError, ValueError, TypeError):
        return None


def presentation_report(srv_rows, obs_rows, cfg, skip=lambda r: False):
    """Compare by entity, generation and PRESENTATION time (ptime), not by wall clock.
    Every observer sample is classified; nothing is dropped silently:
    agree / ambiguous (server changed state inside the window, 10 Hz logging cannot resolve it) /
    singleSample (observer state lasted one sample) / intermediateRevision / strict mismatch (a real disagreement)."""
    def timed(rows, kind):
        out = [(ptime_of(r), r) for r in rows if r.get("kind") == kind and r.get("_t") is not None]
        return sorted([(p, r) for p, r in out if p is not None], key=lambda x: x[0])
    sp, op = timed(srv_rows, "player_visual_sample"), timed(obs_rows, "player_visual_sample")
    op_file = [(ptime_of(r), r) for r in obs_rows if r.get("kind") == "player_visual_sample" and ptime_of(r) is not None]  # written order
    rep = dict(available=bool(sp and op))
    if not rep["available"]:
        return rep
    skeys = [p for p, _ in sp]
    clock = sorted((r["_t"], p) for p, r in sp)
    clock_pts = [(t, pp, 0) for t, pp in clock]
    clock_keys = [c[0] for c in clock_pts]
    delays, perr = [], []
    track = [(p, r["position"]["x"], r["position"]["y"]) for p, r in sp if isinstance(r.get("position"), dict)]
    track_dropped = [r.get("dropped", 0) for p, r in sp if isinstance(r.get("position"), dict)]
    track_frames = [r.get("frame") for p, r in sp if isinstance(r.get("position"), dict)]
    track_keys = [t[0] for t in track]
    sparse = dict(n=0, logDropped=0, serverGap=0, byRegion=[], simulationGapsBridged=0)
    for p, r in op:
        if skip(r):
            continue
        ref = interpolate(clock_pts, r["_t"], clock_keys)
        if ref:
            delays.append((ref[0] - p) * 1000)
        pos = interpolate(track, p, track_keys)
        if pos and isinstance(r.get("position"), dict):
            k = bisect.bisect_left(track_keys, p)
            if 0 < k < len(track) and track[k][0] - track[k - 1][0] > cfg["ptime_gap_s"] and consecutive_frames(track_frames[k - 1], track_frames[k]):
                sparse["simulationGapsBridged"] += 1  # dense capture: no server state exists inside this gap
            elif 0 < k < len(track) and track[k][0] - track[k - 1][0] > cfg["ptime_gap_s"]:
                # The server reference is sparser than the tolerance: interpolating across it measures the reference, not the
                # observer. Counted and attributed (the capture's own drop counter vs a real server gap), never silently removed.
                sparse["n"] += 1
                sparse["logDropped" if track_dropped[k] > track_dropped[k - 1] else "serverGap"] += 1
                if len(sparse["byRegion"]) < 12 and (not sparse["byRegion"] or p - sparse["byRegion"][-1] > 1.0):
                    sparse["byRegion"].append(round(p, 2))
                continue
            perr.append(math.hypot(r["position"]["x"] - pos[0], r["position"]["y"] - pos[1]))
    if delays:
        rep["presentationDelayMs"] = dict(median=round(statistics.median(delays), 1), p10=round(percentile(delays, .1), 1), p90=round(percentile(delays, .9), 1), n=len(delays))
    if perr:
        rep["positionAtSamePtime"] = dict(median=round(statistics.median(perr), 4), p95=round(percentile(perr, .95), 4), n=len(perr),
                                          sparseReference=dict(sparse, rate=round(sparse["n"] / max(1, sparse["n"] + len(perr)), 4)))
    win_s = cfg["ptime_window_s"]
    pose = dict(checked=0, agree=0, ambiguous=0, singleSample=0, strictMismatch=0, examples=[])
    tail = [0]
    last = skeys[-1] - 0.2  # the server stopped logging: nothing to compare against (counted, not dropped silently)
    for i, (p, r) in enumerate(op):  # arcs are NOT skipped here: ascend/descend poses and offsets are what R01 must check
        if p > last:
            tail[0] += 1
            continue
        name = fields(r).get("renderers", "")
        j = bisect.bisect_left(skeys, p)
        near = {fields(sp[k][1]).get("renderers", "") for k in (j - 1, j, j + 1) if 0 <= k < len(sp)}
        if not near:
            continue
        pose["checked"] += 1
        if name in near:
            pose["agree"] += 1
            continue
        win = {fields(sp[k][1]).get("renderers", "") for k in range(max(0, j - 4), min(len(sp), j + 4)) if abs(sp[k][0] - p) <= win_s}
        prev_same = i > 0 and fields(op[i - 1][1]).get("renderers", "") == name
        next_same = i + 1 < len(op) and fields(op[i + 1][1]).get("renderers", "") == name
        gap = (sp[j][0] - sp[j - 1][0]) if 0 < j < len(sp) else 0.0
        if not prev_same and not next_same:
            pose["singleSample"] += 1
        elif len(win) > 1 or gap > cfg["ptime_gap_s"]:
            pose["ambiguous"] += 1
        else:
            pose["strictMismatch"] += 1
            if len(pose["examples"]) < 5:
                pose["examples"].append(dict(ptime=round(p, 3), observer=name, server=sorted(win)))
    rep["pose"] = pose
    back = [(a[0] - b[0]) * 1000 for a, b in zip([x for x in op_file], [x for x in op_file][1:]) if b[0] < a[0] - 0.02]
    rep["ptimeRegressions"] = dict(count=len(back), maxBackMs=round(max(back), 1) if back else 0.0)
    # Does a clock step back reach the presented position? The observer follows ptime, so while the entity moves the avatar steps
    # back by about speed*back. backStepUnits > 0 = the observer moved against the server's direction of travel at that moment.
    effects = []
    for ra, rb in zip(op_file, op_file[1:]):
        if not rb[0] < ra[0] - 0.02:
            continue
        pa, pb = ra[1].get("position"), rb[1].get("position")
        s_a, s_b = interpolate(track, ra[0]), interpolate(track, rb[0])
        if not (isinstance(pa, dict) and isinstance(pb, dict) and s_a and s_b):
            effects.append(dict(ptime=round(ra[0], 2), backMs=round((ra[0] - rb[0]) * 1000, 1), measurable=False))
            continue
        fx, fy = s_a[0] - s_b[0], s_a[1] - s_b[1]
        length = math.hypot(fx, fy)
        if length < 0.05:
            effects.append(dict(ptime=round(ra[0], 2), backMs=round((ra[0] - rb[0]) * 1000, 1), measurable=True, serverTravel=round(length, 3), backStepUnits=0.0))
            continue
        back_units = -((pb["x"] - pa["x"]) * fx + (pb["y"] - pa["y"]) * fy) / length
        effects.append(dict(ptime=round(ra[0], 2), backMs=round((ra[0] - rb[0]) * 1000, 1), measurable=True, serverTravel=round(length, 3), backStepUnits=round(back_units, 3)))
    moving = [e for e in effects if e.get("serverTravel", 0) >= 0.05]
    rep["ptimeRegressions"].update(effects=effects[:12], movingAtRegression=len(moving), visibleBackSteps=sum(1 for e in moving if e["backStepUnits"] > 0.1),
                                   maxBackStepUnits=max([e["backStepUnits"] for e in moving], default=0.0), unmeasurable=sum(1 for e in effects if not e["measurable"]))
    sm = [(ptime_of(r), fields(r).get("rev"), r["delta"]["x"], r["delta"]["y"]) for r in srv_rows
          if r.get("kind") == "mount_visual_sample" and ptime_of(r) is not None and isinstance(r.get("delta"), dict) and r.get("_t") is not None]
    sm.sort(key=lambda a: a[0])
    skm, srevs = [a[0] for a in sm], {a[1] for a in sm}
    mount = dict(checked=0, agree=0, ambiguous=0, intermediateRevision=0, intermediateRevs=[], strictMismatch=0, examples=[])
    for r in obs_rows:
        if r.get("kind") != "mount_visual_sample" or ptime_of(r) is None or not isinstance(r.get("delta"), dict):
            continue
        f = fields(r)
        if f.get("type") in (None, "", "None") or "rev" not in f:
            continue
        p = ptime_of(r)
        if p > last:
            tail[0] += 1
            continue
        j = bisect.bisect_left(skm, p)
        near = [sm[k] for k in (j - 1, j) if 0 <= k < len(sm)]
        if not near:
            continue
        mount["checked"] += 1
        if f["rev"] not in srevs:
            mount["intermediateRevision"] += 1
            if f["rev"] not in mount["intermediateRevs"] and len(mount["intermediateRevs"]) < 8:
                mount["intermediateRevs"].append(f["rev"])
            continue
        same = [a for a in near if a[1] == f["rev"]]
        if not same:
            mount["ambiguous"] += 1  # generation boundary: the server row for this revision is outside the bracket
            continue
        err = min(math.hypot(r["delta"]["x"] - a[2], r["delta"]["y"] - a[3]) for a in same)
        if err <= 0.05:
            mount["agree"] += 1
            continue
        win = [a for a in sm if abs(a[0] - p) <= win_s and a[1] == f["rev"]]
        gap = (sm[j][0] - sm[j - 1][0]) if 0 < j < len(sm) else 0.0
        if gap <= cfg["ptime_gap_s"] and win and all(math.hypot(a[2] - win[0][2], a[3] - win[0][3]) < 0.05 for a in win):
            mount["strictMismatch"] += 1
            if len(mount["examples"]) < 5:
                mount["examples"].append(dict(ptime=round(p, 3), observer=[round(r["delta"]["x"], 3), round(r["delta"]["y"], 3)], server=[round(win[0][2], 3), round(win[0][3], 3)]))
        else:
            mount["ambiguous"] += 1  # offset flips inside the window, or the server logged nothing around this ptime
    rep["mount"] = mount
    checked = pose["checked"] + mount["checked"]
    strict = pose["strictMismatch"] + mount["strictMismatch"]
    unverifiable = pose["ambiguous"] + pose["singleSample"] + mount["ambiguous"] + mount["intermediateRevision"]
    rep.update(tailExcluded=tail[0], checked=checked, strictMismatches=strict, strictRate=round(strict / checked, 4) if checked else None,
               unverifiable=unverifiable, unverifiableRate=round(unverifiable / checked, 4) if checked else None)
    return rep


def verify_steps(srv_rows, obs_rows, cfg, skip=lambda r: False):
    """Large observer steps are checked against the server at the SAME presentation time. A fast but real server
    excursion explains the step; a sparse server log (gap) makes it unverifiable; only a step the dense server
    reference does not show is a discontinuity of the observer."""
    def track(rows, order):
        out = [(ptime_of(r), r["position"]["x"], r["position"]["y"], r.get("frame")) for r in rows
               if r.get("kind") == "player_visual_sample" and isinstance(r.get("position"), dict)]
        out = [t for t in out if t[0] is not None]
        return sorted(out) if order else out  # observer rows stay in written order: its clock can step back
    srv, obs = track(srv_rows, True), track([r for r in obs_rows if not skip(r)], False)
    skeys = [t[0] for t in srv]
    srv3 = [(t[0], t[1], t[2]) for t in srv]
    res = dict(steps=0, explained=0, unexplained=[], unverifiable=[], borderline=[])
    for a, b in zip(obs, obs[1:]):
        step = math.hypot(b[1] - a[1], b[2] - a[2])
        if step <= cfg["teleport_units"] or not 0 < b[0] - a[0] <= 0.5:
            continue
        res["steps"] += 1
        lo, hi = bisect.bisect_right(skeys, a[0]) - 1, bisect.bisect_left(skeys, b[0])  # server rows bracketing the WHOLE step
        window = srv[lo:hi + 1] if lo >= 0 and hi < len(srv) else []
        dense = len(window) >= 2 and all(y[0] - x[0] <= cfg["ptime_gap_s"] or consecutive_frames(x[3], y[3]) for x, y in zip(window, window[1:]))
        if not dense:
            res["unverifiable"].append(dict(ptime=round(a[0], 2), step=round(step, 2)))
            continue
        pa, pb = interpolate(srv3, a[0], skeys), interpolate(srv3, b[0], skeys)
        server_step = math.hypot(pb[0] - pa[0], pb[1] - pa[1]) if pa and pb else 0.0
        # The server reference is itself sampled (<= ptime_gap_s apart) and linearly interpolated; at a sharp excursion it can
        # under-read the peak by about (server speed x sampling interval). A shortfall inside that bound cannot be told from a
        # real discontinuity, so it is listed as borderline (inconclusive), never as explained and never as a failure.
        speed = max((math.hypot(y[1] - x[1], y[2] - x[2]) / (y[0] - x[0]) for x, y in zip(window, window[1:]) if y[0] > x[0]), default=0.0)
        interval = max((y[0] - x[0] for x, y in zip(window, window[1:])), default=0.0)
        if server_step >= step - 0.5:
            res["explained"] += 1
        elif step - server_step <= 0.5 + speed * interval:
            res["borderline"].append(dict(ptime=round(a[0], 2), observer=round(step, 2), server=round(server_step, 2), serverSpeed=round(speed, 1), samplingBound=round(speed * interval, 2)))
        else:
            res["unexplained"].append(dict(ptime=round(a[0], 2), observer=round(step, 2), server=round(server_step, 2)))
    return res


GAP_S = DEFAULTS["ptime_gap_s"]


def num(text, default=None):
    try:
        return float(text)
    except (TypeError, ValueError):
        return default


PATH_TOL = 0.06      # units: an interpolating observer lies on the server polyline (one row per server frame)
FUTURE_SLACK = 0.02  # s: a presented point cannot show a server state later than its presented time
WINDOW_BACK, WINDOW_AHEAD = 0.6, 0.3


AMBIGUITY_S = 0.035  # about two server frames: closer interpretations than this cannot be told apart


def path_times(track, keys, q, target):
    """Server times tau of the authoritative states the presented point q can show: the server polyline segments near
    `target` that q lies on, never later than its presented time. Returns (candidates, status) with status ok /
    off_path (q is on no segment) / unverifiable (a capture gap inside the window)."""
    lo = bisect.bisect_left(keys, target - WINDOW_BACK)
    hi = bisect.bisect_right(keys, target + WINDOW_AHEAD)
    seg = track[max(0, lo - 1):min(len(track), hi + 1)]
    if len(seg) < 2:
        return [], "unverifiable"
    candidates = []
    for a, b in zip(seg, seg[1:]):
        if b[0] - a[0] > GAP_S and not consecutive_frames(a[3], b[3]):
            return [], "unverifiable"
        ax, ay, bx, by = a[1], a[2], b[1], b[2]
        vx, vy = bx - ax, by - ay
        length2 = vx * vx + vy * vy
        if length2 < 1e-9:  # server standing still: any time inside the segment shows this state
            if math.hypot(q[0] - ax, q[1] - ay) <= PATH_TOL:
                candidates.append(min(max(target, a[0]), b[0]))
            continue
        k = max(0.0, min(1.0, ((q[0] - ax) * vx + (q[1] - ay) * vy) / length2))
        if math.hypot(q[0] - (ax + vx * k), q[1] - (ay + vy * k)) <= PATH_TOL:
            candidates.append(a[0] + (b[0] - a[0]) * k)
    if not candidates:
        return [], "off_path"
    past = [c for c in candidates if c <= target + FUTURE_SLACK]
    return past or candidates, "ok"


def classify(row, track, keys):
    """A visible root step is a back-step only if the presented root moved to an EARLIER authoritative state (tau went
    back along the server path). A stale root catching up along the path, or the server's own reversal shown in order,
    moves tau forward and is not a back-step, even when it moves against the server's direction at that instant.
    Each endpoint is read as the least stale state it can show; when the server path doubles back so tightly that a
    forward and a backward reading are within AMBIGUITY_S of each other, the step is ambiguous_reversal (not a pass,
    not a failure: frame resolution cannot decide it)."""
    f = fields(row)
    ptime, pprev = num(f.get("ptime")), num(f.get("pprev"))
    pos, d = row.get("position") or {}, row.get("delta") or {}
    if ptime is None or pprev is None or "x" not in pos:
        return "unclassified"
    after = (pos["x"], pos.get("y", 0.0))
    before = (after[0] - d.get("x", 0.0), after[1] - d.get("y", 0.0))
    ca, sa = path_times(track, keys, before, pprev)
    cb, sb = path_times(track, keys, after, ptime)
    if "unverifiable" in (sa, sb):
        return "unverifiable"
    if "off_path" in (sa, sb):
        return "off_path"
    forward = [abs(pprev - ta) + abs(ptime - tb) for ta in ca for tb in cb if tb >= ta - 0.005]
    back = [abs(pprev - ta) + abs(ptime - tb) for ta in ca for tb in cb if tb < ta - 0.005]
    if not back:
        return "path_forward"
    if not forward:
        return "presentation"
    if abs(min(back) - min(forward)) < AMBIGUITY_S:
        return "ambiguous_reversal"
    return "presentation" if min(back) < min(forward) else "path_forward"


def dense_server_track(srv_rows):
    """(ptime, x, y, frame) per server row, sorted; usable for path classification only when the capture is dense."""
    out = []
    for r in srv_rows:
        if r.get("kind") == "player_visual_sample" and isinstance(r.get("position"), dict):
            p = num(fields(r).get("ptime"))
            if p is not None:
                out.append((p, r["position"]["x"], r["position"]["y"], r.get("frame")))
    out.sort(key=lambda x: x[0])
    dense = len(out) > 1 and sum(1 for a, b in zip(out, out[1:]) if consecutive_frames(a[3], b[3])) >= 0.9 * (len(out) - 1)
    return out, dense


def path_classes(srv_rows, obs_rows, visible):
    """Visible observer root steps placed on the dense server path (see classify). None when not classifiable."""
    track, dense = dense_server_track(srv_rows)
    steps = [r for r in obs_rows if r.get("kind") == "root_backstep" and num(fields(r).get("units"), 0) > visible]
    if not dense or not steps or any("pprev" not in fields(r) for r in steps):
        return None
    keys = [t[0] for t in track]
    out = dict(path_forward=0, presentation=0, off_path=0, ambiguous_reversal=0, unverifiable=0, unclassified=0, examples=[])
    for r in steps:
        c = classify(r, track, keys)
        out[c] += 1
        if c != "path_forward" and len(out["examples"]) < 3:
            out["examples"].append(dict(cls=c, ptime=fields(r).get("ptime"), units=fields(r).get("units")))
    return out


def backstep_report(obs_rows, visible_units=DEFAULTS["backstep_visible_units"]):
    """Per-frame root check written by the observer itself (`root_backstep` edge rows, `root_motion_summary` every 5 s):
    a presented root step against the direction of the snapshot segment being sampled, measured the same way with the
    remote presentation clock on or off. `timeline_slew_end` rows describe the clock's holds. Builds without these rows
    report available=False (not measured, never a pass)."""
    summaries = [fields(r) for r in obs_rows if r.get("kind") == "root_motion_summary"]
    steps = [(r, fields(r)) for r in obs_rows if r.get("kind") == "root_backstep"]
    holds = [fields(r) for r in obs_rows if r.get("kind") == "timeline_slew_end"]
    rep = dict(available=bool(summaries or steps))
    if not rep["available"]:
        return rep
    def num(f, key, cast=float):
        try:
            return cast(f[key])
        except (KeyError, ValueError, TypeError):
            return cast(0)
    units = [num(f, "units") for _, f in steps]
    clocks = {f.get("slew") for f in summaries} | {f.get("slew") for _, f in steps}
    # Mirror's own clock (row field `timeline`) going back is the condition being corrected; counting it on both sides of
    # an A/B shows whether the run reproduced it at all (no regressions = nothing to compare, not a pass for the clock).
    mirror = [r.get("timeline") for r in obs_rows if r.get("kind") == "player_visual_sample" and isinstance(r.get("timeline"), (int, float))]
    regressions = [(a - b) * 1000 for a, b in zip(mirror, mirror[1:]) if b < a - 0.02]
    rep["mirrorRegressions"] = dict(count=len(regressions), maxBackMs=round(max(regressions, default=0.0), 1))
    rep.update(movingFrames=sum(num(f, "moving_frames", int) for f in summaries),
               pixelSteps=sum(1 for u in units if u >= 1 / 16), visibleSteps=sum(1 for u in units if u > visible_units),
               maxBackUnits=round(max(units, default=0.0), 3), stepsWhileHolding=sum(1 for _, f in steps if f.get("holding") == "True"),
               clock="on" if clocks == {"True"} else "off" if clocks == {"False"} else "none (removed 07/11)" if clocks <= {None} else "mixed/unknown",
               examples=[dict(ptime=f.get("ptime"), mirror=f.get("mirror"), units=num(f, "units"), holding=f.get("holding"))
                         for _, f in sorted(steps, key=lambda s: -num(s[1], "units"))[:5]],
               holds=dict(count=len(holds), maxGapMs=round(max((num(h, "max_gap_ms") for h in holds), default=0.0), 1),
                          maxDurationMs=round(max((num(h, "duration_ms") for h in holds), default=0.0), 1), reasons={}))
    for r in obs_rows:
        if r.get("kind") == "timeline_slew_end":
            reason = (r.get("detail") or "").split(";", 1)[0]
            rep["holds"]["reasons"][reason] = rep["holds"]["reasons"].get(reason, 0) + 1
    return rep


def arc_times(*row_sets):
    """Times at which a specialised arc (riding / Pink jump) owns the player's root; other presenters, not R01."""
    times = []
    for rows in row_sets:
        for r in rows:
            f = fields(r)
            if r.get("kind") == "mount_visual_sample" and r["_t"] and (f.get("riding") == "True" or f.get("pink") == "True"):
                times.append(r["_t"])
    return sorted(times)


def outside_arcs(points, times, margin):
    if not times:
        return points
    keep = []
    for p in points:
        i = bisect.bisect_left(times, p[0])
        near = (i < len(times) and times[i] - p[0] <= margin) or (i > 0 and p[0] - times[i - 1] <= margin)
        if not near:
            keep.append(p)
    return keep


def analyze(server_rows, client_rows_by_name, **overrides):
    cfg = dict(DEFAULTS, **overrides)
    report = dict(thresholdsProvisional=cfg, notMeasured=NOT_MEASURED, observers=[], skipped=[], failures=[], inconclusive=[], coverageNotes=[], candidates=[])
    server_players = {}
    for r in server_rows:
        if r.get("role") in ("server", "host") and r.get("netId"):
            server_players.setdefault(r["netId"], []).append(r)
    for name, rows in sorted(client_rows_by_name.items()):
        by_net = {}
        for r in rows:
            if r.get("role") == "client" and not r.get("localPlayer") and r.get("netId"):
                by_net.setdefault(r["netId"], []).append(r)
        for net_id, obs_rows in sorted(by_net.items()):
            srv_rows = server_players.get(net_id, [])
            stamps = [r["_t"] for r in srv_rows if r.get("_t") is not None]
            if stamps:  # rows after the server stopped logging have no reference (end-of-run artefacts)
                obs_rows = [r for r in obs_rows if r.get("_t") is not None and r["_t"] <= max(stamps) + cfg["tail_margin_s"]]
            tag = f"{name}/{net_id}"
            def root(rows_):
                return [(r["_t"], r["position"]["x"], r["position"]["y"]) for r in rows_
                        if r.get("kind") == "player_visual_sample" and r["_t"] and isinstance(r.get("position"), dict)]
            arcs = arc_times(srv_rows, obs_rows)
            srv, obs = outside_arcs(root(srv_rows), arcs, cfg["arc_margin_s"]), outside_arcs(root(obs_rows), arcs, cfg["arc_margin_s"])
            if len(obs) < cfg["min_samples"] or not srv:
                report["skipped"].append(f"{tag}: {len(obs)} observer root samples (not a continuously observed player)")
                continue
            entry = dict(client=name, netId=net_id, arcSamplesExcluded=len(arcs), root=root_report(srv, obs, cfg))
            exp, source = expected_lag(obs_rows)
            entry["expectedLagMs"] = None if exp is None else round(exp, 1)
            entry["expectedLagSource"] = source
            report["observers"].append(entry)
            lag = entry["root"].get("lagMs")
            entry["pose"] = pose_report(srv_rows, obs_rows, cfg)
            entry["mount"] = mount_report(srv_rows, obs_rows, lag, cfg)
            near_arc = lambda r: bool(arcs) and r.get("_t") is not None and (lambda i: (i < len(arcs) and arcs[i] - r["_t"] <= cfg["arc_margin_s"]) or (i > 0 and r["_t"] - arcs[i - 1] <= cfg["arc_margin_s"]))(bisect.bisect_left(arcs, r["_t"]))
            entry["presentation"] = pres = presentation_report(srv_rows, obs_rows, cfg, near_arc)
            if not pres["available"]:
                report["inconclusive"].append(f"{tag}: no ptime in the logs; presentation-time alignment not measurable")
            else:
                report["coverageNotes"].append(f"{tag}: checked {pres['checked']} samples by ptime; strict mismatches {pres['strictMismatches']}, "
                                               f"unverifiable {pres['unverifiable']} (pose ambiguous {pres['pose']['ambiguous']}, pose single-sample {pres['pose']['singleSample']}, "
                                               f"mount ambiguous {pres['mount']['ambiguous']}, mount intermediate revisions {pres['mount']['intermediateRevision']} {pres['mount']['intermediateRevs']}); end-of-run samples excluded {pres['tailExcluded']}")
                reg = pres["ptimeRegressions"]
                if reg["count"]:
                    report["candidates"].append(f"{tag}: presentation clock (ptime) went backwards {reg['count']} time(s), up to {reg['maxBackMs']} ms; "
                                                f"while the entity moved ({reg.get('movingAtRegression', 0)} of them) the presented position stepped back by up to {reg.get('maxBackStepUnits', 0)} units "
                                                f"({reg.get('visibleBackSteps', 0)} steps over 0.1 u); {reg.get('unmeasurable', 0)} not measurable")
                if entry["pose"]["singleSampleOnlyStates"]:
                    report["candidates"].append(f"{tag}: observer showed one-sample state(s) the server never logged: {entry['pose']['singleSampleOnlyStates']}")
                if pres["strictRate"] is not None and pres["strictRate"] > cfg["ptime_strict_rate"]:
                    report["failures"].append(f"{tag}: {pres['strictMismatches']} observer samples disagree with the server at the same presentation time ({pres['strictRate']:.1%}); examples {pres['pose']['examples'][:2] + pres['mount']['examples'][:2]}")
                if pres["unverifiableRate"] is not None and pres["unverifiableRate"] > cfg["unverifiable_rate"]:
                    report["inconclusive"].append(f"{tag}: {pres['unverifiableRate']:.0%} of samples cannot be verified at this sampling rate")
                sp_ref = (pres.get("positionAtSamePtime") or {}).get("sparseReference")
                if sp_ref and sp_ref["n"]:
                    report["coverageNotes"].append(f"{tag}: {sp_ref['n']} position samples ({sp_ref['rate']:.1%}) have a server reference sparser than {cfg['ptime_gap_s']} s and are not in the position p95: "
                                                   f"{sp_ref['logDropped']} where the capture's own budget dropped rows, {sp_ref['serverGap']} other server gaps; first regions (ptime) {sp_ref['byRegion']}")
                    if sp_ref["rate"] > cfg["unverifiable_rate"]:
                        report["inconclusive"].append(f"{tag}: {sp_ref['rate']:.0%} of position samples have no dense server reference")
                if pres.get("positionAtSamePtime") and pres["positionAtSamePtime"]["p95"] > cfg["ptime_pos_p95"]:
                    report["failures"].append(f"{tag}: position at the same presentation time off by p95 {pres['positionAtSamePtime']['p95']}")
                if pres.get("presentationDelayMs") and entry["root"].get("lagMs") is not None and abs(pres["presentationDelayMs"]["median"] - entry["root"]["lagMs"]) > cfg["ptime_lag_disagree_ms"]:
                    report["failures"].append(f"{tag}: presentation delay {pres['presentationDelayMs']['median']} ms disagrees with the root lag {entry['root']['lagMs']} ms")
            bs = entry["rootBackSteps"] = backstep_report(obs_rows, cfg["backstep_visible_units"])
            if bs["available"]:
                report["coverageNotes"].append(f"{tag}: per-frame root check over {bs['movingFrames']} moving frames: {bs['pixelSteps']} back-step(s) >= 1 px, "
                                               f"{bs['visibleSteps']} > {cfg['backstep_visible_units']} u, max {bs['maxBackUnits']} u (presentation clock {bs['clock']}); "
                                               f"holds {bs['holds']['count']} (max gap {bs['holds']['maxGapMs']} ms, max {bs['holds']['maxDurationMs']} ms, ends {bs['holds']['reasons']}); "
                                               f"Mirror clock regressions {bs['mirrorRegressions']['count']} (max {bs['mirrorRegressions']['maxBackMs']} ms)")
                pc = entry["rootPathClasses"] = path_classes(srv_rows, obs_rows, cfg["backstep_visible_units"])
                if bs["visibleSteps"] and pc is None:
                    report["failures"].append(f"{tag}: presented root stepped back against the sampled reference {bs['visibleSteps']} time(s) by more than "
                                              f"{cfg['backstep_visible_units']} u while moving (max {bs['maxBackUnits']} u; no dense server path to classify them); examples {bs['examples'][:3]}")
                elif pc is not None:
                    report["coverageNotes"].append(f"{tag}: {bs['visibleSteps']} flagged step(s) placed on the dense server path: {pc['path_forward']} forward along it "
                                                   f"(stale root catching up / server reversal shown in order), {pc['presentation']} back to an earlier state, "
                                                   f"{pc['off_path']} off the path, {pc['ambiguous_reversal']} ambiguous at a 2-frame server reversal, {pc['unverifiable']} unverifiable")
                    if pc["presentation"] or pc["off_path"]:
                        report["failures"].append(f"{tag}: presented root went back along the authoritative path {pc['presentation']} time(s) and off it {pc['off_path']} "
                                                  f"time(s) by more than {cfg['backstep_visible_units']} u; examples {pc['examples']}")
                    if pc["ambiguous_reversal"]:
                        report["inconclusive"].append(f"{tag}: {pc['ambiguous_reversal']} flagged root step(s) at a server reversal tighter than two frames; frame resolution cannot classify them")
                    if pc["unverifiable"] or pc["unclassified"]:
                        report["inconclusive"].append(f"{tag}: {pc['unverifiable'] + pc['unclassified']} flagged root step(s) fall in a server capture gap; not classifiable")
            pose = entry["pose"]
            if pose["missingLongStates"]:
                report["failures"].append(f"{tag}: observer never showed {pose['missingLongStates']}")
            if pose["observerOnlyStates"]:
                report["failures"].append(f"{tag}: observer showed states the server never had: {pose['observerOnlyStates']}")
            if isinstance(entry["mount"]["generations"], dict) and entry["mount"]["generations"]["observerOnly"]:
                report["failures"].append(f"{tag}: observer showed mount generations the server never had: {entry['mount']['generations']['observerOnly']}")
            # mount.p95Error (UTC-interpolated) is informational: the offset mirrors with facing, so interpolation across a
            # flip invents offsets. Offsets are judged by presentation time in `presentation.mount` instead.
            root_ = entry["root"]
            if not root_["conclusive"]:
                report["inconclusive"].append(f"{tag}: {root_.get('reason')}")
                continue
            limit = cfg["fallback_lag_limit_ms"] if exp is None else exp + cfg["lag_tolerance_ms"]
            if lag > limit:
                report["failures"].append(f"{tag}: lag {lag} ms exceeds expected interpolation delay ({source}: {entry['expectedLagMs']} ms) + tolerance")
            if root_["driftMs"] is not None and root_["driftMs"] > cfg["drift_limit_ms"]:
                delay = (entry.get("presentation") or {}).get("presentationDelayMs")
                perr = ((entry.get("presentation") or {}).get("positionAtSamePtime") or {}).get("p95")
                corroborated = not delay or (delay["p90"] - delay["p10"]) > cfg["drift_limit_ms"] or (perr or 0) > cfg["ptime_pos_p95"]
                msg = f"{tag}: lag drifts {root_['driftMs']} ms between windows {root_['windowLagsMs']}"
                if corroborated:
                    report["failures"].append(msg)
                else:
                    report["inconclusive"].append(msg + f"; NOT corroborated by presentation time (delay spread {delay['p10']}..{delay['p90']} ms, position p95 {perr}); likely a lag-search artefact on sparse server data, unresolved")
            if root_["stalls"]:
                report["failures"].append(f"{tag}: {root_['stalls']} stall(s), longest {root_['longestStallMs']} ms, while the server moved")
            steps = entry["steps"] = verify_steps(srv_rows, obs_rows, cfg, near_arc)
            if steps["unexplained"]:
                report["failures"].append(f"{tag}: observer step the dense server reference does not show: {steps['unexplained'][:3]}")
            if steps["borderline"]:
                report["inconclusive"].append(f"{tag}: {len(steps['borderline'])} large observer step(s) exceed the server reference by less than its own sampling bound at a fast excursion; borderline, not a pass: {steps['borderline'][:3]}")
            if steps["unverifiable"]:
                report["inconclusive"].append(f"{tag}: {len(steps['unverifiable'])} large observer step(s) fall where the server log is sparse (gap); unverifiable: {steps['unverifiable'][:3]}")
            if root_["discontinuities"] and not steps["steps"]:
                report["failures"].append(f"{tag}: {root_['discontinuities']} observer root discontinuities")
            if root_["rms"] > cfg["rms_limit"]:
                report["failures"].append(f"{tag}: root RMS {root_['rms']} > {cfg['rms_limit']} (max {root_['max']})")
            if pose["conclusive"] and pose["delayMsMedian"] > lag + cfg["pose_tolerance_ms"]:
                report["failures"].append(f"{tag}: pose median delay {pose['delayMsMedian']} ms vs root lag {lag} ms")
    conclusive = [o for o in report["observers"] if o["root"].get("conclusive")]
    if report["failures"]:
        report["status"] = "failures"
    elif not conclusive:
        report["status"] = "inconclusive"
        report["inconclusive"].append("no observer with an identifiable root lag (players did not move enough)")
    elif report["inconclusive"]:
        report["status"] = "inconclusive"  # something that should have been measured could not be; never report that as a pass
    else:
        report["status"] = "within-provisional-limits"
    report["measuredOk"] = report["status"] == "within-provisional-limits"
    report["verdict"] = {"within-provisional-limits": "measurements within provisional limits; R01 is NOT accepted by this report alone",
                         "failures": "measurement failures; inspect failures before changing runtime",
                         "inconclusive": "not enough movement or data to measure; this is not a pass"}[report["status"]]
    return report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--server", type=Path, required=True)
    parser.add_argument("--client", type=Path, nargs="+", required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    if args.output.exists():
        parser.error("--output exists; refusing to overwrite a previous analysis")
    report = analyze(load(args.server), {p.stem: load(p) for p in args.client})
    args.output.write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps(dict(status=report["status"], failures=report["failures"], inconclusive=report["inconclusive"]), indent=2))
    raise SystemExit({"within-provisional-limits": 0, "failures": 2, "inconclusive": 3}[report["status"]])


if __name__ == "__main__":
    main()
