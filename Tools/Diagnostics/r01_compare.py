"""R01 online x offline equivalence report (Logs/R01-comparacao.json).

Input: run folders written by r01_compare_run.py --capture (R01ComparisonHarness samples, one JSONL per process).
  offline run: offline-compare.jsonl (role offline, players 1 and 2)
  online run:  server-compare.jsonl (server), client-1-compare.jsonl (owner of P1, observer of P2),
               client-2-compare.jsonl (observer of P1, owner of P2)

Criteria fixed BEFORE the measured runs (07/11/2026); thresholds are constants below and must not be tuned to results.
  A setup/phase/loadout: same scene, 2 players, P1 spawn |d| <= 0.01 u per axis, same speed, bombs, radius, Sudden Death Off,
    same destructible-tile fingerprint, P1 not mounted and alive at script start; rendered processes run at 60 fps / vSync 0.
  B input: offline and owner both played the 12 scripted segments (R,L,U,D x3) in order, every hold 2.4 s +- 0.034 s (2 frames).
  C presentation: P1 collapsed body-renderer sequence in the window equals the offline one for server, owner and observer;
    no P1/P2 renderer combination that never occurs offline (catches double poses); P2 (idle) shows no motion.
  D motion: P1 motion runs (|displacement| >= 0.05 u, gaps <= 0.1 s merged): same count and direction as offline;
    per run |d displacement| <= 0.25 u and |d duration| <= 0.1 s.
  E final P1 position |d| <= 0.25 u.
  F reported without a threshold: input->motion onset (offline vs owner, own clock) and the server/observer delay behind the
    owner (shared UTC clock), plus the analyze_prediction.py summary of the motion logs.
Offline runs are also compared with the first valid offline run under C/D/E; if offline does not reproduce itself, the
comparison is inconclusive (never a pass).
Validity amendment (07/11/2026, after the tooling pilot, before the measured runs; no threshold changed): input edges are
taken on rendered frames, so a long frame over an edge really lengthens a hold. A run whose only problems are B is listed
as "input not equivalent", excluded from pairing and replaced by another attempt (at most 6 attempts per mode). The verdict
needs REQUIRED_PAIRS valid offline and online runs, paired in order; every excluded run stays in the report.
Second amendment (07/11/2026, after session R01-measure-20261107 FAILED under the rules above; that result is kept; the
verdict comes from a new session; no numeric tolerance changed):
  - "2 players" is checked from the samples (P1 and P2 sampled from the window start on, in every process), not from the
    setup row, which is written on the first frame P1 exists, sometimes before P2 has spawned on that process;
  - the double-pose check is explicit: no sample of P1 or P2 with two or more visible body renderers, in any role;
  - P2 (idle) must show the same collapsed renderer sequence on owner and observer as on the authoritative server. Its
    states must be ones seen offline or "Afk2": InactivityAnimation picks Afk2 instead of Afk at random (chanceAltAnimation
    0.3) on the simulating process, so the offline runs cannot be expected to contain the same variant;
  - P1 (scripted, no idle emote in the window) still must match offline exactly (sequence and states).
"""
import argparse
import json
import math
from pathlib import Path
import statistics
import sys

sys.path.insert(0, str(Path(__file__).resolve().parent))
import analyze_prediction  # noqa: E402

EXPECTED_ACTIONS = ["MoveRight", "MoveLeft", "MoveUp", "MoveDown"] * 3
HOLD_SECONDS, HOLD_TOLERANCE = 2.4, 0.034
SETTLE_MS, REMOTE_ALLOWANCE_MS = 3000.0, 500.0
SPAWN_TOLERANCE = 0.01
STEP_EPSILON, MERGE_GAP_MS, MIN_RUN = 0.0005, 100.0, 0.05
DISPLACEMENT_TOLERANCE, DURATION_TOLERANCE, FINAL_TOLERANCE = 0.25, 0.1, 0.25
REQUIRED_PAIRS = 3
RANDOM_IDLE_VARIANTS = {"Afk2"}
PRESENCE_SLACK_MS = 100.0
SETUP_KEYS = ("scene", "speed", "bombs", "radius", "suddenDeath", "destructibles")


def read_rows(path):
    rows, lines = [], Path(path).read_text(encoding="utf-8-sig").splitlines()
    for number, line in enumerate(lines, 1):
        if not line.strip():
            continue
        try:
            rows.append(json.loads(line))
        except json.JSONDecodeError:
            # A process killed mid-write can leave one torn last line; anything else is corrupt.
            if number != len(lines):
                raise ValueError(f"{path}:{number}: invalid JSON")
    return rows


def detail(row):
    return dict(part.split("=", 1) for part in row.get("d", "").split(";") if "=" in part)


def logs(rows, kind):
    return [r for r in rows if r.get("k") == kind]


def samples(rows, role, player):
    return sorted((r for r in rows if r.get("k") == "s" and r.get("role") == role and r.get("p") == player), key=lambda r: r["u"])


def setup(rows, role, player):
    found = [detail(r) for r in logs(rows, "setup") if detail(r).get("role") == role and detail(r).get("player") == str(player)]
    battle = [d for d in found if d.get("scene", "").startswith("BattleMode_")]
    return battle[-1] if battle else (found[-1] if found else None)


def segments(rows):
    starts, ends = logs(rows, "segment_start"), logs(rows, "segment_end")
    result = []
    for start in starts:
        d = detail(start)
        end = next((e for e in ends if detail(e).get("index") == d.get("index")), None)
        result.append({"index": int(d["index"]), "action": d.get("action"), "u": start["u"],
                       "hold": float(detail(end)["st"]) - float(d["st"]) if end else None})
    return result


def window(rows):
    start, done = logs(rows, "script_start"), logs(rows, "script_done")
    if not start or not done:
        return None
    return start[0]["u"], done[0]["u"] + SETTLE_MS


def within(track, lo, hi):
    return [s for s in track if lo <= s["u"] <= hi]


def motion_runs(track):
    runs, current = [], None
    for previous, sample in zip(track, track[1:]):
        if math.hypot(sample["x"] - previous["x"], sample["y"] - previous["y"]) <= STEP_EPSILON:
            continue
        if current and previous["u"] - current["end"]["u"] <= MERGE_GAP_MS:
            current["end"] = sample
        else:
            current = {"start": previous, "end": sample}
            runs.append(current)
    result = []
    for run in runs:
        dx, dy = run["end"]["x"] - run["start"]["x"], run["end"]["y"] - run["start"]["y"]
        if math.hypot(dx, dy) < MIN_RUN:
            continue
        direction = ("R" if dx > 0 else "L") if abs(dx) >= abs(dy) else ("U" if dy > 0 else "D")
        result.append({"direction": direction, "dx": dx, "dy": dy, "startU": run["start"]["u"],
                       "seconds": (run["end"]["u"] - run["start"]["u"]) / 1000.0})
    return result


def collapse(track):
    sequence = []
    for sample in track:
        if not sequence or sequence[-1] != sample["r"]:
            sequence.append(sample["r"])
    return sequence


def view(rows, role, player, lo, hi):
    track = within(samples(rows, role, player), lo, hi)
    return {"track": track, "runs": motion_runs(track), "renderers": collapse(track),
            "final": (track[-1]["x"], track[-1]["y"]) if track else None}


def compare_views(reference, other):
    """C1 (renderer sequence), D and E for P1 of one role against the reference view."""
    problems = []
    if not other["track"]:
        return ["no samples in window"], {}
    if other["renderers"] != reference["renderers"]:
        problems.append(f"C renderer sequence differs ({len(other['renderers'])} vs {len(reference['renderers'])} states)")
    ra, rb = reference["runs"], other["runs"]
    if [r["direction"] for r in rb] != [r["direction"] for r in ra]:
        problems.append("D motion runs differ: " + "".join(r["direction"] for r in rb) + " vs " + "".join(r["direction"] for r in ra))
    displacement, duration = [], []
    for a, b in zip(ra, rb):
        displacement.append(math.hypot(b["dx"] - a["dx"], b["dy"] - a["dy"]))
        duration.append(abs(b["seconds"] - a["seconds"]))
    if displacement and max(displacement) > DISPLACEMENT_TOLERANCE:
        problems.append(f"D displacement delta {max(displacement):.3f} u > {DISPLACEMENT_TOLERANCE}")
    if duration and max(duration) > DURATION_TOLERANCE:
        problems.append(f"D duration delta {max(duration):.3f} s > {DURATION_TOLERANCE}")
    final = math.hypot(other["final"][0] - reference["final"][0], other["final"][1] - reference["final"][1])
    if final > FINAL_TOLERANCE:
        problems.append(f"E final position delta {final:.3f} u > {FINAL_TOLERANCE}")
    return problems, {"runs": len(rb), "maxDisplacementDelta": max(displacement, default=None),
                      "maxDurationDelta": max(duration, default=None), "finalDelta": final}


def check_setup(reference, other, rendered):
    problems = []
    if reference is None or other is None:
        return ["A setup row missing"]
    for key in SETUP_KEYS:
        if reference.get(key) != other.get(key):
            problems.append(f"A {key}: {other.get(key)} vs {reference.get(key)}")
    for axis in ("spawnX", "spawnY"):
        if abs(float(other[axis]) - float(reference[axis])) > SPAWN_TOLERANCE:
            problems.append(f"A {axis}: {other[axis]} vs {reference[axis]}")
    if other.get("suddenDeath") != "Off":
        problems.append(f"A suddenDeath={other.get('suddenDeath')}")
    if rendered and (other.get("fps") != "60" or other.get("vsync") != "0"):
        problems.append(f"A pacing fps={other.get('fps')} vsync={other.get('vsync')}")
    return problems


def check_input(rows, label):
    found = segments(rows)
    problems = []
    if [s["action"] for s in found] != EXPECTED_ACTIONS:
        problems.append(f"B {label} segments {[s['action'] for s in found]}")
    bad = [s["hold"] for s in found if s["hold"] is None or abs(s["hold"] - HOLD_SECONDS) > HOLD_TOLERANCE]
    if bad:
        problems.append(f"B {label} holds outside {HOLD_SECONDS}+-{HOLD_TOLERANCE}: {bad}")
    holds = [s["hold"] for s in found if s["hold"] is not None]
    return problems, {"segments": len(found), "holdMin": min(holds, default=None), "holdMax": max(holds, default=None)}


def presence(p1, p2, lo, label):
    """A: both players exist in this process for the whole window."""
    problems = []
    for name, view_ in (("P1", p1), ("P2", p2)):
        if not view_["track"] or view_["track"][0]["u"] > lo + PRESENCE_SLACK_MS:
            problems.append(f"A {label}: {name} not sampled from the window start (2 players required)")
    return problems


def double_poses(view_):
    return sum(1 for sample in view_["track"] if "," in sample["r"])


def start_state(view_, label):
    if not view_["track"]:
        return [f"A {label}: no P1 sample at script start"]
    first = view_["track"][0]
    return [f"A {label}: P1 mounted/dead at script start"] if first.get("m") or first.get("dead") else []


def onsets(rows, runs):
    """Script input edge -> first P1 motion run start on the same process clock (ms)."""
    result = []
    starts = segments(rows)
    for i, segment in enumerate(starts):
        until = starts[i + 1]["u"] if i + 1 < len(starts) else float("inf")
        run = next((r for r in runs if segment["u"] - 20 <= r["startU"] < until), None)
        if run:
            result.append(run["startU"] - segment["u"])
    return result


def stats(values):
    values = [v for v in values if v is not None]
    if not values:
        return None
    ordered = sorted(values)
    return {"n": len(values), "min": ordered[0], "median": statistics.median(ordered), "max": ordered[-1],
            "p95": ordered[math.ceil(len(ordered) * 0.95) - 1]}


def offline_bundle(folder):
    try:
        rows = read_rows(Path(folder) / "offline-compare.jsonl")
    except OSError as error:
        return {"folder": str(folder), "problems": [f"capture missing: {error}"]}
    span = window(rows)
    if span is None:
        return {"folder": str(folder), "problems": ["B offline script did not run to completion"]}
    lo, hi = span
    bundle = {"folder": str(folder), "rows": rows, "p1": view(rows, "offline", 1, lo, hi), "p2": view(rows, "offline", 2, lo, hi),
              "setup": setup(rows, "offline", 1)}
    problems, info = check_input(rows, "offline")
    problems += check_setup(bundle["setup"], bundle["setup"], True) + start_state(bundle["p1"], "offline") + \
        presence(bundle["p1"], bundle["p2"], lo, "offline")
    for player in ("p1", "p2"):
        if double_poses(bundle[player]):
            problems.append(f"C offline {player.upper()} double pose in {double_poses(bundle[player])} samples")
    bundle.update(problems=problems, input=info, onsetsMs=onsets(rows, bundle["p1"]["runs"]))
    return bundle


def online_bundle(folder, offline):
    folder = Path(folder)
    try:
        server, owner, observer = (read_rows(folder / name) for name in ("server-compare.jsonl", "client-1-compare.jsonl", "client-2-compare.jsonl"))
    except OSError as error:
        return {"folder": str(folder), "problems": [f"capture missing: {error}"]}
    span = window(owner)
    if span is None:
        return {"folder": str(folder), "problems": ["B owner script did not run to completion"]}
    lo, hi = span
    roles = {"server": view(server, "server", 1, lo, hi + REMOTE_ALLOWANCE_MS), "owner": view(owner, "owner", 1, lo, hi),
             "observer": view(observer, "observer", 1, lo, hi + REMOTE_ALLOWANCE_MS)}
    idle = {"server": view(server, "server", 2, lo, hi + REMOTE_ALLOWANCE_MS), "owner": view(observer, "owner", 2, lo, hi),
            "observer": view(owner, "observer", 2, lo, hi + REMOTE_ALLOWANCE_MS)}
    setups = {"server": setup(server, "server", 1), "owner": setup(owner, "owner", 1), "observer": setup(observer, "observer", 1)}
    problems, info = check_input(owner, "owner")
    result = {"folder": str(folder), "input": info, "roles": {}}
    allowed = {1: {s["r"] for s in offline["p1"]["track"]},
               2: {s["r"] for s in samples(offline["rows"], "offline", 2)} | RANDOM_IDLE_VARIANTS}
    for role, view_ in roles.items():
        role_problems = check_setup(offline["setup"], setups[role], role != "server") + start_state(view_, role) + \
            presence(view_, idle[role], lo, role)
        compared, metrics = compare_views(offline["p1"], view_)
        role_problems += compared
        for player, track in ((1, view_["track"]), (2, idle[role]["track"])):
            unseen = sorted({s["r"] for s in track} - allowed[player])
            if unseen:
                role_problems.append(f"C P{player} renderer states never seen offline: {unseen[:4]}")
        for player, v in ((1, view_), (2, idle[role])):
            if double_poses(v):
                role_problems.append(f"C P{player} double pose in {double_poses(v)} samples")
        if role != "server" and idle[role]["renderers"] != idle["server"]["renderers"]:
            role_problems.append(f"C P2 renderer sequence differs from the server: {idle[role]['renderers'][:6]} vs {idle['server']['renderers'][:6]}")
        metrics["p2Renderers"] = idle[role]["renderers"]
        if idle[role]["runs"]:
            role_problems.append(f"C idle P2 moved: {len(idle[role]['runs'])} runs")
        if role != "owner":
            owner_runs = roles["owner"]["runs"]
            metrics["delayBehindOwnerMs"] = stats([b["startU"] - a["startU"] for a, b in zip(owner_runs, view_["runs"])])
        result["roles"][role] = {"problems": role_problems, **metrics}
        problems += [f"{role}: {p}" for p in role_problems]
    result["ownerOnsetsMs"] = onsets(owner, roles["owner"]["runs"])
    result["problems"] = problems
    return result


def input_only(problems):
    return bool(problems) and all(p.startswith("B ") or ": B " in p for p in problems)


def owner_input_problems(folder):
    try:
        rows = read_rows(Path(folder) / "client-1-compare.jsonl")
    except OSError:
        return []  # not an input problem: online_bundle reports the missing capture as a failure
    return check_input(rows, "owner")[0] if window(rows) else ["B owner script did not run to completion"]


def report(offline_folders, online_folders, motion_online=(), motion_offline=(), required=None):
    required = REQUIRED_PAIRS if required is None else required
    all_offline = [offline_bundle(f) for f in offline_folders]
    excluded = [{"folder": b["folder"], "problems": b["problems"]} for b in all_offline if input_only(b["problems"])]
    offline = [b for b in all_offline if not input_only(b["problems"])]
    consistency = []
    for i, bundle in enumerate(offline):
        if i and "p1" in bundle and "p1" in offline[0]:
            problems, metrics = compare_views(offline[0]["p1"], bundle["p1"])
            consistency.append({"folder": bundle["folder"], "problems": problems, **metrics})
    online = []
    for folder in online_folders:
        owner_b = owner_input_problems(folder)
        if owner_b:
            excluded.append({"folder": str(folder), "problems": owner_b})
            continue
        pair = offline[len(online)] if len(online) < len(offline) else None
        online.append(online_bundle(folder, pair) if pair and "p1" in pair else {"folder": str(folder), "problems": ["no paired offline run"]})
    offline_ok = all(not b["problems"] for b in offline) and all(not c["problems"] for c in consistency) and len(offline) >= max(2, required)
    online_ok = len(online) >= required and all(not b["problems"] for b in online) and len(online) <= len(offline)
    verdict = ("demonstrated" if offline_ok and online_ok else
               "inconclusive: offline runs do not reproduce each other" if offline and all(not b["problems"] for b in offline) and
               any(c["problems"] for c in consistency) else
               f"inconclusive: fewer than {required} valid runs per mode" if (len(offline) < max(2, required) or len(online) < required) and
               all(not b["problems"] for b in offline + online) else "failed")
    roles = ("server", "owner", "observer")
    variation = {role: {key: stats([b["roles"][role].get(key) for b in online if role in b.get("roles", {})])
                        for key in ("maxDisplacementDelta", "maxDurationDelta", "finalDelta")} for role in roles}
    for role in ("server", "observer"):
        variation[role]["delayBehindOwnerMedianMs"] = stats([(b["roles"][role].get("delayBehindOwnerMs") or {}).get("median")
                                                             for b in online if role in b.get("roles", {})])
    prediction = None
    if motion_online and motion_offline:
        prediction = {"online": analyze_prediction.summarize(analyze_prediction.read_captures(motion_online)),
                      "offline": analyze_prediction.summarize(analyze_prediction.read_captures(motion_offline))}
    return {
        "verdict": verdict,
        "criteria": __doc__.split("Criteria", 1)[1].strip(),
        "thresholds": {"holdSeconds": HOLD_SECONDS, "holdTolerance": HOLD_TOLERANCE, "spawnTolerance": SPAWN_TOLERANCE,
                       "minRun": MIN_RUN, "mergeGapMs": MERGE_GAP_MS, "displacementTolerance": DISPLACEMENT_TOLERANCE,
                       "durationTolerance": DURATION_TOLERANCE, "finalTolerance": FINAL_TOLERANCE,
                       "settleMs": SETTLE_MS, "remoteAllowanceMs": REMOTE_ALLOWANCE_MS},
        "offline": [{k: v for k, v in b.items() if k in ("folder", "problems", "input", "setup")} |
                    {"runs": "".join(r["direction"] for r in b["p1"]["runs"]) if "p1" in b else None,
                     "onsetMs": stats(b.get("onsetsMs", []))} for b in offline],
        "offlineConsistency": consistency,
        "excludedInputNotEquivalent": excluded,
        "requiredValidRunsPerMode": required,
        "online": [{k: v for k, v in b.items() if k != "ownerOnsetsMs"} | {"ownerOnsetMs": stats(b.get("ownerOnsetsMs", []))} for b in online],
        "variationAcrossRuns": variation,
        "onsetMs": {"offline": stats([v for b in offline for v in b.get("onsetsMs", [])]),
                    "owner": stats([v for b in online for v in b.get("ownerOnsetsMs", [])])},
        "analyzePrediction": {
            "summary": prediction,
            "whatItMeasures": "input_to_motion latency (median/p95) and event counts per capture/role/context only",
            "whatItDoesNotMeasure": "setup/phase/loadout equivalence, the input sequence, renderer/pose state, positions or motion "
                                    "runs; those criteria (A-E) are checked by this report from the harness samples",
        },
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--offline", nargs="+", required=True, type=Path)
    parser.add_argument("--online", nargs="+", required=True, type=Path)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    motion_offline = [str(p) for f in args.offline for p in sorted(f.glob("*-motion.jsonl"))]
    motion_online = [str(p) for f in args.online for p in sorted(f.glob("*-motion.jsonl"))]
    result = report(args.offline, args.online, motion_online, motion_offline)
    result["inputs"] = {"offline": [str(f) for f in args.offline], "online": [str(f) for f in args.online],
                        "motionOffline": motion_offline, "motionOnline": motion_online}
    args.output.write_text(json.dumps(result, ensure_ascii=False, indent=2, allow_nan=False) + "\n", encoding="utf-8")
    print(result["verdict"])
    return 0 if result["verdict"] == "demonstrated" else 1


if __name__ == "__main__":
    sys.exit(main())
