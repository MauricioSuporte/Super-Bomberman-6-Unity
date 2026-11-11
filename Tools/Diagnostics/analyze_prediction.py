"""Summarize motion captures without mixing builds or treating blocked input as lag."""

import argparse
from collections import Counter
import json
import math
from pathlib import Path
import re
import statistics


def summarize(records, minimum_percentile_samples=20):
    groups = {}
    build, protocol, device = "unknown", None, "unknown"
    current_capture = None
    for row in records:
        capture = row.get("_capture", "memory")
        if capture != current_capture:
            build, protocol, device = "unknown", None, "unknown"
            current_capture = capture
        kind = row.get("kind", "unknown")
        if kind == "capture_start":
            build, protocol = row.get("build", "unknown"), row.get("protocol")
            device = row.get("device") or "unknown"
            continue
        detail = row.get("detail", "") or ""
        input_context = detail.split(";at_response:", 1)[0].removeprefix("at_input:")
        mounted = re.search(r"\bmounted=(True|False)\b", input_context, re.IGNORECASE)
        block = re.search(r"\bprediction_block=([^\s;]+)", input_context)
        context = "mounted" if mounted and mounted[1].lower() == "true" else "walking" if mounted else "unknown"
        boat = re.search(r"\bboat=(True|False)\b", input_context, re.IGNORECASE)
        if boat and boat[1].lower() == "true":
            context = "boat_mounted" if context == "mounted" else "boat" if mounted else "boat_unknown_mount"
        pusher = re.search(r"\bpusher=(True|False)\b", input_context, re.IGNORECASE)
        if pusher and pusher[1].lower() == "true":
            context = "pusher_" + context
        enemy_force = re.search(r"\benemy_force=(Hurricane|Magnet|Bubble)\b", input_context)
        if enemy_force:
            context = "force_" + enemy_force[1].lower() + "_" + context
        conveyor = re.search(r"\bconveyor=([^;]+)", input_context)
        conveyor_context = conveyor[1].rsplit(":", 1)[0] if conveyor else "unknown"
        if kind.startswith("revenge_launch_"):
            context = "revenge_cart"
        if kind == "coop_family_cost":
            context = "world_presentation_cost"
        key = (build, protocol, row.get("role", "unknown"), row.get("scene", ""), context,
               row.get("device") or device, row.get("player", 0), capture, conveyor_context)
        group = groups.setdefault(key, {"events": Counter(), "latencies": [], "blockedResponses": 0,
                                        "unclassifiedResponses": 0, "invalidDurations": 0, "hitchFrames": set(),
                                        "cancelledBlocked": 0, "cancelledFree": 0, "cancelledUnknown": 0, "responseBlocked": 0, "hitchLatencies": [], "cleanLatencies": [], "rttSamples": [],
                                        "revengeInputs": set(), "revengeSeen": set(), "revengeMetrics": {}, "presentationCost": {}, "invalidCostReports": 0})
        group["events"][kind] += 1
        frame_ms = row.get("frameMs", 0)
        if isinstance(frame_ms, (int, float)) and math.isfinite(frame_ms) and frame_ms > 50:
            group["hitchFrames"].add((row.get("_capture", "memory"), row.get("frame")))
        if kind == "coop_family_cost":
            cost = re.search(r"family=([^;]+);sampled=(\d+);changed=(\d+);frame_payload_bytes=(\d+);capture_cpu_ms=([0-9eE+.-]+)", detail)
            try:
                family, sampled, changed, payload, cpu = (cost[1], int(cost[2]), int(cost[3]), int(cost[4]), float(cost[5])) if cost else (None, 0, 0, 0, float("nan"))
            except ValueError:
                family, sampled, changed, payload, cpu = None, 0, 0, 0, float("nan")
            if family and sampled > 0 and changed <= sampled and math.isfinite(cpu) and cpu >= 0:
                metric = group["presentationCost"].setdefault(family, {"reports": 0, "sampledFrames": 0, "changedFrames": 0, "serializedFramePayloadBytes": 0, "captureCpuMs": 0})
                metric["reports"] += 1; metric["sampledFrames"] += sampled; metric["changedFrames"] += changed
                metric["serializedFramePayloadBytes"] += payload; metric["captureCpuMs"] += cpu
            else:
                group["invalidCostReports"] += 1
        if kind.startswith("revenge_launch_"):
            cart = re.search(r"\bcart=(-?\d+);generation=(\d+)", detail)
            tick = row.get("inputTick")
            identity = (cart[1], cart[2], tick) if cart and type(tick) is int and 0 <= tick <= 0xffffffff else None
            if kind == "revenge_launch_input" and identity is not None:
                group["revengeInputs"].add(identity)
            elif kind in ("revenge_launch_first_visual", "revenge_launch_confirm", "revenge_launch_reject", "revenge_launch_timeout"):
                duration = row.get("durationMs")
                metric_key = (kind, identity)
                if identity in group["revengeInputs"] and metric_key not in group["revengeSeen"] and type(duration) in (int, float) and math.isfinite(duration) and duration >= 0:
                    group["revengeSeen"].add(metric_key)
                    group["revengeMetrics"].setdefault(kind.removeprefix("revenge_launch_"), []).append(duration)
        if kind == "input_without_motion_cancelled":
            cancellation = detail.split(";at_cancel:", 1)
            cancel_block = re.search(r"\bprediction_block=([^\s;]+)", cancellation[1]) if len(cancellation) == 2 else None
            category = "cancelledUnknown" if not block else "cancelledBlocked" if block[1] != "none" or (cancel_block and cancel_block[1] != "none") else "cancelledFree"
            group[category] += 1
        if kind != "input_to_motion":
            continue
        duration = row.get("durationMs")
        if not isinstance(duration, (int, float)) or not math.isfinite(duration) or duration < 0:
            group["invalidDurations"] += 1
        elif not block:
            group["unclassifiedResponses"] += 1
        elif block[1] != "none":
            group["blockedResponses"] += 1
        else:
            response_context = detail.split(";at_response:", 1)
            response_block = re.search(r"\bprediction_block=([^\s;]+)", response_context[1]) if len(response_context) == 2 else None
            if response_block and response_block[1] != "none":
                group["responseBlocked"] += 1
                continue
            group["latencies"].append(duration)
            rtt = row.get("rttMs")
            if row.get("role") == "client" and type(rtt) in (int, float) and math.isfinite(rtt) and rtt >= 0:
                group["rttSamples"].append(rtt)
            if isinstance(frame_ms, (int, float)) and math.isfinite(frame_ms) and frame_ms > 50:
                group["hitchLatencies"].append(duration)
            else:
                group["cleanLatencies"].append(duration)

    result = []
    for (identity, version, role, scene, context, platform, player, capture, conveyor), group in groups.items():
        samples = sorted(group["latencies"])
        count = len(samples)
        result.append({"build": identity, "protocol": version, "role": role, "scene": scene, "context": context,
                       "device": platform, "player": player, "capture": capture, "conveyor": conveyor,
                       "freeInputToMotionSamples": count,
                       "medianMs": statistics.median(samples) if samples else None,
                       "maxMs": max(samples) if samples else None,
                       "p95Ms": samples[math.ceil(count * 0.95) - 1] if count >= minimum_percentile_samples else None,
                       "percentileMinimumSamples": minimum_percentile_samples,
                       "blockedResponsesExcluded": group["blockedResponses"],
                       "unclassifiedResponsesExcluded": group["unclassifiedResponses"],
                       "invalidDurationsExcluded": group["invalidDurations"],
                       "blockedAtResponseExcluded": group["responseBlocked"],
                       "cancelledBeforeMotionFree": group["cancelledFree"],
                       "cancelledBeforeMotionBlocked": group["cancelledBlocked"],
                       "cancelledBeforeMotionUnknown": group["cancelledUnknown"],
                       "rttMedianAtResponseMs": statistics.median(group["rttSamples"]) if group["rttSamples"] else None,
                       "hitchResponseSamples": len(group["hitchLatencies"]),
                       "nonHitchResponseSamples": len(group["cleanLatencies"]),
                       "nonHitchMedianMs": statistics.median(group["cleanLatencies"]) if group["cleanLatencies"] else None,
                       "observedHitchFramesOver50Ms": len(group["hitchFrames"]),
                       "events": dict(sorted(group["events"].items())),
                       "presentationCostInvalidReports": group["invalidCostReports"],
                       "presentationCost": {name: dict(metric, captureCpuUsPerSample=metric["captureCpuMs"] * 1000 / metric["sampledFrames"])
                           for name, metric in sorted(group["presentationCost"].items())},
                       "revengeLaunch": {name: {"samples": len(values), "medianMs": statistics.median(values),
                           "p95Ms": sorted(values)[math.ceil(len(values) * .95) - 1] if len(values) >= minimum_percentile_samples else None}
                           for name, values in sorted(group["revengeMetrics"].items())}})
    return result


def read_captures(paths):
    for filename in paths:
        path = Path(filename)
        with path.open(encoding="utf-8-sig") as capture:
            for line_number, line in enumerate(capture, 1):
                if not line.strip():
                    continue
                try:
                    row = json.loads(line)
                except json.JSONDecodeError as error:
                    raise ValueError(f"{path}:{line_number}: invalid JSON capture record") from error
                if not isinstance(row, dict):
                    raise ValueError(f"{path}:{line_number}: capture record must be an object")
                row["_capture"] = str(path.resolve())
                yield row


def capture_quality(paths):
    """Report evidence completeness independently of gameplay metrics."""
    result = []
    for filename in paths:
        headers = rows = dropped = limits = overflows = 0
        for row in read_captures([filename]):
            rows += 1
            headers += row.get("kind") == "capture_start"
            limits += row.get("kind") == "capture_limit"
            overflows += row.get("kind") == "input_response_overflow"
            value = row.get("dropped", 0)
            if type(value) is int and value >= 0:
                dropped = max(dropped, value)
        issues = []
        if headers != 1: issues.append("expected exactly one capture_start")
        if rows <= headers: issues.append("no observations")
        if dropped: issues.append("diagnostic rows dropped")
        if limits: issues.append("capture limit reached")
        if overflows: issues.append("deferred input observations overflowed")
        result.append({"capture": str(Path(filename).resolve()), "rows": rows,
                       "headers": headers, "dropped": dropped, "limits": limits, "observationOverflows": overflows,
                       "complete": not issues, "issues": issues})
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--online", nargs="+", required=True, help="Online JSONL captures")
    parser.add_argument("--offline", nargs="+", required=True, help="Matching offline JSONL captures")
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    report = {"quality": {"online": capture_quality(args.online), "offline": capture_quality(args.offline)}, "online": summarize(read_captures(args.online)), "offline": summarize(read_captures(args.offline)),
              "notes": ["Compare the same scene, loadout and input sequence; captures, builds, players and contexts are reported separately.",
                        "Input-to-motion is an observed rendered-frame interval, not raw network latency.",
                        "Old records without input context are counted but excluded from latency statistics.",
                        "Revenge first_visual measures CPU presenter observation after sampled A release, not GPU display or raw hardware latency.",
                        "World presentation payload counts exclude message envelopes, asset definitions, peer fanout and transport overhead; capture CPU excludes diagnostic overhead.",
                        "Hitch counts describe observed logged frames, not every frame in the session.",
                        "Capture flags, hardware and network conditions must be recorded with the reproduction."]}
    text = json.dumps(report, ensure_ascii=False, indent=2, allow_nan=False) + "\n"
    if args.output:
        args.output.write_text(text, encoding="utf-8")
    else:
        print(text, end="")


if __name__ == "__main__":
    main()
