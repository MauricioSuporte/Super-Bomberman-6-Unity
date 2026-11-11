"""R08 approved environmental motion and poses; CPU observations, not perceptual acceptance."""
import argparse
import json
from pathlib import Path
from Tools.Diagnostics.analyze_bosses import analyze
from Tools.Diagnostics.analyze_projectiles import rows, detail
FAMILIES = {"RocketPenguin", "DestructibleRocket", "SnowBall", "Barrel", "Vulcan", "Volcano", "GateBubble", "BubbleFloat", "Platform", "AmbientBubble", "BurstBubble"}

def audio_report(folder):
    native = rows(folder / "server-motion.jsonl")
    expected = {}
    for row in native:
        if row.get("kind") == "r08_loop_native":
            state = detail(row); expected[(state["rev"], state["id"])] = state
    reports = {}
    for client in (1, 2):
        source = rows(folder / f"client-{client}-motion.jsonl")
        actual = [detail(r) for r in source if r.get("kind") == "r08_loop_view"]
        mismatches = []
        for state in actual:
            approved = expected.get((state["rev"], state["id"]))
            if approved is None:
                mismatches.append("no authority reference")
            elif state.get("clip") != "1" or any(state.get(k) != approved.get(k) for k in ("loop", "paused")):
                mismatches.append("presenter clip/loop/pause differs")
        coverage = dict(loop=any(s.get("loop") == "1" for s in actual),
                        tail=any(s.get("loop") == "0" for s in actual),
                        pause=any(s.get("paused") == "1" for s in actual),
                        ended=any(r.get("kind") == "r08_loop_snapshot_view" and detail(r).get("count") == "0" for r in source))
        reports[f"client-{client}"] = dict(samples=len(actual), coverage=coverage, mismatches=mismatches,
            status="FAILED" if mismatches else "PASSED" if expected and all(coverage.values()) else "INCONCLUSIVE",
            limitations=["AudioSource state, not audible output/device latency; phase drift is not scored here."])
    return reports

def require_scenario_coverage(result, folder):
    required = {"Stage_3-2": {"Vulcan", "Volcano"}, "Stage_3-6": {"DestructibleRocket"},
                "Stage_3-3": {"AmbientBubble", "BurstBubble", "GateBubble"},
                "Stage_3-4": {"Barrel"}, "Stage_3-5": {"SnowBall", "RocketPenguin"}}
    expected = next((families for scene, families in required.items() if scene in str(folder)), set())
    for report in result["reports"].values():
        missing = sorted(f for f in expected if (report["families"].get(f, {}).get("visibleSamples", 0) == 0 or
                    report["families"].get(f, {}).get("movingEntities", 0) == 0))
        report["missingScenarioFamilies"] = missing
        if missing:
            report["status"] = "INCONCLUSIVE" if report["status"] != "FAILED" else "FAILED"
            report["problems"].append("Scenario families unmeasured: " + ", ".join(missing))

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("folder", type=Path)
    args = parser.parse_args()
    result = analyze(args.folder, "r08", FAMILIES)
    require_scenario_coverage(result, args.folder)
    for report in result["reports"].values():
        for bucket in report["families"].values():
            bucket["poseMismatch"] = bucket.pop("spriteMismatch")
    if "Stage_3-5" in str(args.folder):
        result["audio"] = audio_report(args.folder)
    (args.folder / "r08-analysis.json").write_text(json.dumps(result, indent=2), encoding="utf-8")
    for name, report in result["reports"].items():
        print(name, report["status"], report["problems"], list(report["families"]))
    scored = list(result["reports"].values()) + list(result.get("audio", {}).values())
    raise SystemExit(1 if any(r["status"] == "FAILED" for r in scored) else
                     2 if any(r["status"] == "INCONCLUSIVE" for r in scored) else 0)
