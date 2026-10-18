"""Aggregate completed local load tests without treating aborted runs as capacity."""
import argparse
import csv
import json
from pathlib import Path
import statistics


def available_stat(values, aggregate):
    values = list(values)
    return aggregate(values) if values and all(value >= 0 for value in values) else None


def summarize(root):
    result = []
    for config_path in sorted(root.glob("*/config.json")):
        folder = config_path.parent
        config = json.loads(config_path.read_text(encoding="utf-8-sig"))
        row = dict(case=folder.name, mode=config["mode"], rooms=config["rooms"],
                   humanPeers=config["rooms"] * config["playersPerRoom"],
                   comPerRoom=6-config["playersPerRoom"] if config.get("fillCom") else 0,
                   seconds=config["seconds"], healthy=False,
                   assemblySha256=config.get("assemblySha256", ""),
                   syntheticDurableActors=config.get("syntheticDurableActors", True),
                   spawnRegressionCheck=config.get("spawnRegressionCheck", False),
                   frameBudgetScope=config.get("frameBudgetScope", "legacy windows include scene/round transitions"),
                   metricsVersion=config.get("metricsVersion", 1),
                   privateMemoryMeaning="VmData (Linux virtual data)" if "cpuStealTicks" in config["memory"] else "PrivateUsage (Windows)")
        path = folder / "summary.json"
        if not path.exists() and (folder / "flow.json").exists():
            flow = json.loads((folder / "flow.json").read_text(encoding="utf-8-sig"))
            row.update(outcome="flow passed" if flow["passed"] else "flow failed", flowPassed=flow["passed"],
                       assessment=flow["scope"], scenes=",".join(sorted({s for r in flow["roomResults"] for s in r["scenes"]})),
                       errorEventCount=sum(len(r["errors"]) for r in flow["roomResults"]),
                       warningEventCount=sum(len(r["warnings"]) for r in flow["roomResults"]))
            result.append(row); continue
        if not path.exists():
            failure = folder / "failure.json"
            row["outcome"] = json.loads(failure.read_text(encoding="utf-8-sig"))["error"] if failure.exists() else "not complete"
            result.append(row); continue
        data = json.loads(path.read_text(encoding="utf-8-sig")); rooms = data["roomResults"]
        host = [json.loads(line) for line in (folder / "host.jsonl").read_text(encoding="utf-8-sig").splitlines()]
        errors = set()
        warnings = set()
        error_events = warning_events = 0
        for room in rooms:
            errors.update(room["errors"])
            events = folder / room["room"] / "events.jsonl"
            if events.exists():
                for line in events.read_text(encoding="utf-8-sig").splitlines():
                    try: event=json.loads(line)
                    except ValueError: continue
                    if event.get("level") in ("Error", "Exception", "Assert"):
                        errors.add(event["message"]); error_events += 1
                    if event.get("level") == "Warning":
                        warnings.add(event["message"]); warning_events += 1
        row.update(outcome=data["outcome"], scenes=",".join(sorted({s for r in rooms for s in r["scenes"]})),
                   excludedTransitionWindows=sum(r.get("excludedTransitionWindows",0) for r in rooms),
                   minimumWindowsPerRoom=min(r["windows"] for r in rooms),
                   minFps=min(r["minFps"] for r in rooms), maxP95Ms=max(r["maxP95Ms"] for r in rooms),
                   maxP99Ms=max(r["maxP99Ms"] for r in rooms), worstFrameMs=max(r["worstFrameMs"] for r in rooms),
                   maxSlowFramePercent=max(r["slowFramePercent"] for r in rooms),
                   serverPeakRssMiB=data["serverPeakRssMiB"], serverPeakPrivateMiB=data["serverPeakPrivateMiB"],
                   clientPeakRssMiB=data["clientPeakRssMiB"], minimumAvailableMiB=data["minimumAvailableMiB"],
                   totalOutgoingKiBps=sum(r["meanOutgoingKiBps"] for r in rooms),
                   networkMetricsAvailable=all(r["meanOutgoingKiBps"] > 0 for r in rooms),
                   totalGcKiBps=available_stat((r["meanGcKiBps"] for r in rooms), sum),
                   gcMetricsAvailable=all(r["meanGcKiBps"] >= 0 for r in rooms),
                   errorCount=len(errors), errors=" | ".join(sorted(errors)),
                   warningCount=len(warnings), warnings=" | ".join(sorted(warnings)),
                   errorEventCount=error_events, warningEventCount=warning_events,
                   coopPresentationMsPerFrame=available_stat((r["markers"].get("SB6.Coop.Presentation",-1) for r in rooms), statistics.mean))
        elapsed = host[-1]["seconds"] - host[0]["seconds"]
        delta_cpu = sum(s["cpuSeconds"] for s in host[-1]["servers"]) - sum(s["cpuSeconds"] for s in host[0]["servers"])
        row["serverCpuPercentOneCore"] = delta_cpu / elapsed * 100 if elapsed else 0
        row["hostCpuPercentMean"] = data["meanHostCpuPercent"]
        row["clientCpuPercentOneCore"] = (host[-1]["clients"]["cpuSeconds"] - host[0]["clients"]["cpuSeconds"]) / elapsed * 100 if elapsed else 0
        row["serverStartRssMiB"] = sum(s["rssMiB"] for s in host[0]["servers"])
        row["serverEndRssMiB"] = sum(s["rssMiB"] for s in host[-1]["servers"])
        row["serverRssDeltaMiB"] = row["serverEndRssMiB"] - row["serverStartRssMiB"]
        for label, marker in (("comUpdateMsPerFrame", "SB6.BattlePerf.COM.Update"),
                              ("comThinkMsPerFrame", "SB6.BattlePerf.COM.Think"),
                              ("playerFixedUpdateMsPerFrame", "SB6.BattlePerf.Player.FixedUpdate")):
            row[label] = available_stat((r["markers"].get(marker, -1) for r in rooms), statistics.mean)
        before, after = host[0]["memory"], host[-1]["memory"]
        if "cpuStealTicks" in before:
            ticks = after["cpuTotalTicks"] - before["cpuTotalTicks"]
            row["cpuStealPercent"] = (after["cpuStealTicks"]-before["cpuStealTicks"]) / ticks * 100 if ticks else 0
        if "lo" in before.get("network", {}):
            row["loopbackTxKiBps"] = (after["network"]["lo"]["txBytes"]-before["network"]["lo"]["txBytes"]) / elapsed / 1024 if elapsed else 0
        row["frameBudgetMet"] = data["outcome"] == "complete" and row["minimumWindowsPerRoom"] >= 5 and row["minFps"] >= 58 and row["maxP99Ms"] <= 33.5 and row["maxSlowFramePercent"] < 1
        row["healthy"] = row["frameBudgetMet"] and row["errorCount"] == 0
        row["assessment"] = ("pass" if row["healthy"] else
                             "insufficient active gameplay windows" if row["minimumWindowsPerRoom"] < 5 else
                             "frame budget exceeded" if not row["frameBudgetMet"] else "functional errors")
        result.append(row)
    return result


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("root",type=Path)
    args=parser.parse_args()
    rows=summarize(args.root)
    (args.root / "results.json").write_text(json.dumps(rows,indent=2),encoding="utf-8")
    with (args.root / "results.csv").open("w",encoding="utf-8",newline="") as stream:
        columns=list(dict.fromkeys(key for row in rows for key in row))
        writer=csv.DictWriter(stream,fieldnames=columns); writer.writeheader(); writer.writerows(rows)
    for row in rows:
        print(row["case"], row["outcome"], "healthy="+str(row["healthy"]),
              "p99="+str(row.get("maxP99Ms","-")), "RSS="+str(round(row.get("serverPeakRssMiB",0))))
