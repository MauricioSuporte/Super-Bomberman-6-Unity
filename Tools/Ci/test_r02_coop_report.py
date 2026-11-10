import json
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "Diagnostics"))
import r02_coop_report as report  # noqa: E402


def shape(family, frames, error_sum, error_max, bins, hold_starts=0, extra=0, native_moving=0, native_sum=0.0, stalls=0):
    return {"kind": "coop_family_shape", "dropped": 0, "detail":
            f"family={family};frames={frames};error_sum_px={error_sum};error_max_px={error_max};over1=0;over2=0;hold_starts={hold_starts};"
            f"hold_frames=0;hold_sum_px=0;hold_max_px=0;extra_frames={extra};native_moving={native_moving};native_step_sum_px={native_sum};"
            f"native_step_max_px=1;native_stalls={stalls};bins={','.join(str(b) for b in bins)}"}


def cost(family, sampled, changed, payload, cpu, entries=0, bytes_=0):
    return {"kind": "coop_family_cost", "dropped": 0, "detail":
            f"family={family};sampled={sampled};changed={changed};frame_payload_bytes={payload};capture_cpu_ms={cpu};"
            f"excludes_envelopes_asset_definitions_peer_fanout_and_diagnostic_overhead;motion_entries={entries};motion_anchors=0;"
            f"motion_bytes={bytes_};motion_cpu_ms=0.5"}


def window(realtime, items):
    ids = "|".join(":".join(str(v) for v in item) for item in items)
    return {"kind": "coop_present_window", "realtime": realtime, "dropped": 0, "detail": f"v=2;paused=0;offset_ms=16.667;ids={ids}"}


# id, presented, interpolated, moving, stalls, stepSum, stepMax, delaySum, delayMax, cpuUs, arrivals, continuous, starved,
# starvedMs, late, marginSum, marginMin, pausedHeld, pausedMoving
def client_item(identity, moving, stalls, starved, continuous, delay_sum=0.0):
    return (identity, 60, 50, moving, stalls, moving * 1.0, 2.0, delay_sum, 70.0, 120.0, 20, continuous, starved, 5.0, 0, 200.0, -3.0, 0, 0)


class R02CoopReportTests(unittest.TestCase):
    def test_steady_cost_tail_excludes_setup_without_mutating_markers(self):
        markers = {"start": 1000, "pause": 46000}
        selected = report.steady_cost_window(markers, 30)
        self.assertEqual(selected["start"], 16000)
        self.assertEqual(selected["fixtureStart"], 1000)
        self.assertEqual(selected["steadySeconds"], 30)
        self.assertEqual(markers["start"], 1000)

    def test_steady_cost_tail_rejects_insufficient_data(self):
        with self.assertRaisesRegex(ValueError, "shorter"):
            report.steady_cost_window({"start": 1000, "pause": 20000}, 30)

    def test_steady_cost_tail_rejects_invalid_duration(self):
        for seconds in (0, -1, float("nan"), float("inf")):
            with self.subTest(seconds=seconds), self.assertRaises(ValueError):
                report.steady_cost_window({"start": 1000, "pause": 46000}, seconds)

    def test_percentile_uses_upper_bin_edge_and_handles_empty(self):
        self.assertIsNone(report.percentile_from_bins([], .95))
        self.assertAlmostEqual(0.1, report.percentile_from_bins([95, 5], .95))
        self.assertAlmostEqual(0.2, report.percentile_from_bins([90, 10], .95))

    def test_server_families_aggregate_shape_and_cost_and_flag_shape_loss(self):
        rows = [shape("Frog", 100, 50.0, 1.5, [80, 20]), shape("Frog", 100, 50.0, 2.5, [80, 10, 0, 0, 0, 0, 0, 0, 0, 0, 0, 10], hold_starts=2),
                cost("Frog", 10, 5, 600, 0.25, entries=12, bytes_=200), cost("Frog", 10, 5, 600, 0.25)]
        families = report.server_families(rows)
        frog = families["Frog"]
        self.assertEqual(200, frog["shapeFrames"]); self.assertAlmostEqual(0.5, frog["errorMeanPx"])
        self.assertEqual(2.5, frog["errorMaxPx"]); self.assertEqual(2, frog["holdStarts"])
        self.assertEqual([160, 30, 0, 0, 0, 0, 0, 0, 0, 0, 0, 10], frog["bins"])
        self.assertTrue(frog["shapeLoss"], "max > 2 px loses the shape")
        self.assertEqual(1200, frog["framePayloadBytes"]); self.assertEqual(12, frog["motionEntries"]); self.assertEqual(2, frog["costReports"])

    def test_client_families_join_ids_and_compute_starvation_and_stalls(self):
        mapping = report.family_ids([{"kind": "coop_family_ids", "detail": "ids=11:Frog|-12:Pillar"}])
        self.assertEqual({11: "Frog", -12: "Pillar"}, mapping)
        rows = [window(1.0, [client_item(11, 40, 4, 5, 10, delay_sum=2000.0), client_item(-12, 0, 0, 0, 0), client_item(99, 10, 0, 0, 2)]),
                window(2.0, [client_item(11, 40, 0, 0, 10, delay_sum=2000.0)])]
        families, seconds = report.client_families(rows, mapping)
        self.assertEqual(2.0, seconds)
        frog = families["Frog"]
        self.assertAlmostEqual(.05, frog["stallFraction"]); self.assertAlmostEqual(.25, frog["starvedFraction"])
        self.assertAlmostEqual(50.0, frog["delayMeanMs"]); self.assertEqual(70.0, frog["delayMaxMs"]); self.assertEqual(-3.0, frog["marginMinMs"])
        self.assertIsNone(families["Pillar"]["starvedFraction"])
        self.assertIn("UnmappedId", families)

    def test_positional_starvation_is_optional_v3_subset(self):
        v3 = client_item(11, 40, 0, 6, 10) + (4, 2, 8.0)
        families, _ = report.client_families([window(1.0, [v3]), window(2.0, [client_item(11, 40, 0, 0, 10)])], {11: "Frog"})
        frog = families["Frog"]
        self.assertAlmostEqual(.3, frog["starvedFraction"])
        self.assertIsNone(frog["moveStarvedFraction"], "a v2 window without the subset makes the subset unknown, not zero")
        families, _ = report.client_families([window(1.0, [v3])], {11: "Frog"})
        self.assertAlmostEqual(.5, families["Frog"]["moveStarvedFraction"]); self.assertAlmostEqual(4.0, families["Frog"]["moveStarvedMeanMs"])

    def test_dropped_rows_and_capture_limit_invalidate_a_capture(self):
        self.assertEqual([], report.capture_problems([{"kind": "x", "dropped": 0}], "server"))
        problems = report.capture_problems([{"kind": "x", "dropped": 3}, {"kind": "capture_limit"}], "client-1")
        self.assertEqual(2, len(problems))

    def test_family_table_skips_invalid_runs_and_compare_uses_range_overlap(self):
        def run(p95, starved, problems=()):
            return {"problems": list(problems), "server": {"Frog": {"errorP95Px": p95, "errorMaxPx": p95 + .5}},
                    "clients": {"client-1": {"families": {"Frog": {"starvedFraction": starved}}}}}
        legacy = report.family_table([run(1.5, .4), run(1.4, .35), run(0.1, .0, problems=["dropped"])])
        fixed = report.family_table([run(0.3, .0), run(0.4, .01)])
        self.assertEqual([1.5, 1.4], legacy["Frog"]["server.errorP95Px"]["values"])
        diff = report.compare_tables(legacy, fixed)["Frog"]
        self.assertEqual("measured difference", diff["clients.starvedFraction"]["classification"])
        verdict = report.shape_verdict(legacy)["Frog"]
        self.assertTrue(verdict["shapeLoss"]); self.assertFalse(report.shape_verdict(fixed)["Frog"]["shapeLoss"])
        self.assertTrue(report.shape_verdict(fixed)["Frog"]["fixed"])
        overlap = report.compare_tables(legacy, report.family_table([run(1.45, .36)]))["Frog"]
        self.assertEqual("not resolved (ranges overlap)", overlap["server.errorP95Px"]["classification"])

    def test_iso_to_u_matches_the_harness_clock(self):
        # 2026-11-07T18:14:12.440Z in harness units: unix ms + 62135596800000 - 63900000000000
        self.assertAlmostEqual(1794075252440 + 62135596800000 - 63900000000000, report.iso_to_u("2026-11-07T18:14:12.4408925Z"), delta=1)

    def test_cost_run_requires_capture_off_and_cuts_the_fixture_window(self):
        with tempfile.TemporaryDirectory() as tmp:
            folder = Path(tmp)
            (folder / "config.json").write_text(json.dumps({"scene": "Stage_1-1", "assemblySha256": {}, "extraArgs": []}), encoding="utf-8")
            start, pause = 1000000.0, 1031000.0
            metrics_utc = "2026-11-07T18:14:12.4408925Z"
            stop_u = report.iso_to_u(metrics_utc)
            start, pause = stop_u - 20000, stop_u + 5000
            server = [f"[LoadTest] coop-world started scene=Stage_1-1 u={start}", f"[LoadTest] coop-world paused index=1 u={pause}",
                      "[R01Frame] installed gcRecorderValid=True motionCapture=False",
                      "[ServerMetrics] " + json.dumps({"utc": metrics_utc, "scene": "Stage_1-1", "includesTransition": False, "players": 2,
                                                       "incomingKiBPerSecond": 2.0, "outgoingKiBPerSecond": 30.0,
                                                       "markerNames": ["SB6.Coop.Presentation", "SB6.Coop.Motion"], "averageMarkerMs": [0.2, 0.05]})]
            for name in report.PROCESSES:
                lines = server if name == "server" else ["[R01Frame] installed gcRecorderValid=True motionCapture=False"]
                lines = lines + ["[R01Frame] " + json.dumps({"u": start + 2000 + 1000 * i, "seconds": 1, "frames": 60, "workMeanMs": 1.0,
                                                             "workP95Ms": 1.2, "workMaxMs": 2.0, "gcAllocKiB": 100.0, "gcCollections": 0}) for i in range(5)]
                (folder / (name + ".log")).write_text("\n".join(lines), encoding="utf-8")
            samples = [{"u": start + 1000 * i, **{f"{n}#cycles": 3e8 * i for n in report.PROCESSES}} for i in range(26)]
            (folder / "cpu-samples.json").write_text(json.dumps(samples), encoding="utf-8")
            result = report.cost_run(folder)
            self.assertEqual([], result["problems"])
            self.assertAlmostEqual(300.0, result["metrics"]["server.mcyclesPerSecond"])
            self.assertAlmostEqual(30.0, result["metrics"]["server.outgoingKiBPerSecond"])
            self.assertAlmostEqual(0.05, result["metrics"]["serverMarkerMs.SB6.Coop.Motion"])
            self.assertAlmostEqual(100.0, result["metrics"]["client-1.frame.gcAllocKiBPerSecond"])
            (folder / "client-2.log").write_text("[GameplayLatency] Capture: x\n[R01Frame] installed gcRecorderValid=True motionCapture=True", encoding="utf-8")
            self.assertTrue(any("motion capture" in p for p in report.cost_run(folder)["problems"]))


    def test_v4_window_splits_delay_holds_and_starvation(self):
        # v3 subset + approvedHolds, approvedHoldMs, lagSum, lagMax, holdSum, holdMax, ageSum, ageMax, sparse delay bins
        item = client_item(11, 40, 0, 6, 10, delay_sum=4000.0) + (4, 2, 8.0) + (3, 60.0, 3200.0, 90.0, 800.0, 1300.0, 1000.0, 400.0, "8=30/9=8/60=2")
        other = client_item(12, 10, 0, 0, 2, delay_sum=900.0) + (0, 0, 0.0) + (0, 0.0, 900.0, 95.0, 0.0, 0.0, 100.0, 60.0, "9=10")
        families, _ = report.client_families([window(1.0, [item, other])], {11: "Frog", 12: "Pillar"})
        frog = families["Frog"]
        self.assertEqual(100, frog["delayP95Ms"], "38 of 40 moving frames are at most 100 ms")
        self.assertEqual(float("inf"), report.delay_percentile(frog["delayBins"], .99), "p99 falls in the >= 600 ms overflow bin")
        self.assertEqual(90, frog["delayP50Ms"])
        self.assertAlmostEqual(80.0, frog["lagMeanMs"]); self.assertAlmostEqual(20.0, frog["holdMeanMs"]); self.assertEqual(1300.0, frog["holdMaxMs"])
        self.assertAlmostEqual(50.0, frog["ageMeanMs"]); self.assertEqual(3, frog["approvedHolds"]); self.assertAlmostEqual(20.0, frog["approvedHoldMeanMs"])
        self.assertEqual(2, frog["moveStarved"])
        everything = families["*all*"]
        self.assertEqual(50, sum(everything["delayBins"])); self.assertEqual(100, everything["delayP95Ms"])
        self.assertEqual(1300.0, everything["holdMaxMs"]); self.assertEqual(95.0, everything["lagMaxMs"])
        v3, _ = report.client_families([window(1.0, [client_item(11, 40, 0, 6, 10) + (4, 2, 8.0)])], {11: "Frog"})
        self.assertIsNone(v3["Frog"]["delayP95Ms"], "a window without bins has no p95")
        self.assertIsNone(v3["Frog"]["approvedHolds"])

    def test_delay_percentile_edges(self):
        bins = [0] * report.DELAY_BINS
        self.assertIsNone(report.delay_percentile(bins, .95))
        bins[3] = 95; bins[12] = 5
        self.assertEqual(40, report.delay_percentile(bins, .95)); self.assertEqual(130, report.delay_percentile(bins, .99))
        self.assertEqual(report.parse_delay_bins("3=95/12=5"), bins)

    def test_reconnect_check_requires_clean_stream_begins(self):
        sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "RoomDirectory"))
        import smoke_coop_reconnect as reconnect
        scene = "Stage_3-4"
        begin = "[Coop] stream begin scene=Stage_3-4 priorSamples=0 priorRemovals=0 priorViews=0 priorStats=0 priorBombSamples=0"
        with tempfile.TemporaryDirectory() as tmp:
            folder = Path(tmp)
            (folder / "server-a.log").write_text("\n".join(["[Room] join connection=1", "[Room] join connection=2", "[Room] scene=Stage_3-4",
                "[LoadTest] coop-world started scene=Stage_3-4 u=1", "[Room] disconnect connection=2 player=2 owner=False"]), encoding="utf-8")
            (folder / "server-b.log").write_text("\n".join(["[Room] join connection=5", "[Room] join connection=6"] +
                ["[LoadTest] " + m for m in ("coop-world started scene=Stage_3-4", "coop-world paused index=1", "coop-world resumed index=1",
                 "coop-world reloaded scene=Stage_3-4", "coop-world paused index=2", "coop-world resumed index=2",
                 "coop-world completed scene=Stage_3-4", "completed")]), encoding="utf-8")
            def client(index, extra=()):
                lines = [begin, "[LoadTest] client disconnected; awaiting rejoin left=" + ("True" if index == 2 else "False"), begin, begin,
                         "[LoadTest] client disconnected; closing capture"] + list(extra)
                if index == 2: lines.insert(1, "[LoadTest] reconnect leaving room scene=Stage_3-4")
                (folder / f"client-{index}.log").write_text("\n".join(lines), encoding="utf-8")
            client(1); client(2)
            summary = reconnect.check_reconnect(folder, scene)
            self.assertEqual({}, summary["errors"]); self.assertEqual(3, summary["clients"]["client-2"]["streamBegins"])
            client(1, [begin.replace("priorSamples=0", "priorSamples=4")])
            with self.assertRaises(RuntimeError): reconnect.check_reconnect(folder, scene)
            client(1); client(2, ["[LoadTest] client disconnected; awaiting rejoin left=True"])
            with self.assertRaises(RuntimeError): reconnect.check_reconnect(folder, scene)

    def test_delay_spikes_classify_hold_versus_network_lag(self):
        def v4(identity, delay_max, hold_max, lag_max):
            return client_item(identity, 40, 0, 0, 10) + (4, 2, 8.0) + (0, 0.0, 100.0, lag_max, 10.0, hold_max, 50.0, 80.0, "6=40")
        def row(utc, rtt, items):
            ids = "|".join(":".join(str(v) for v in item) for item in items)
            return {"kind": "coop_present_window", "utc": utc, "realtime": 1.0, "rttMs": rtt, "bufferMs": 40.0, "dropped": 0,
                    "detail": "v=4;paused=0;offset_ms=16.667;ids=" + ids}
        with tempfile.TemporaryDirectory() as tmp:
            folder = Path(tmp)
            held = list(v4(11, 0, 0, 0)); held[8] = 1300.0
            lagged = list(v4(12, 0, 0, 0)); lagged[8] = 700.0
            held[-4], held[-6] = 1250.0, 50.0   # holdMax, lagMax
            lagged[-4], lagged[-6] = 20.0, 680.0
            client1 = [row("2026-11-07T18:00:00.0000000Z", 30.0, [held, v4(13, 0, 0, 0)]), row("2026-11-07T18:00:01.0000000Z", 300.0, [lagged])]
            (folder / "client-1-motion.jsonl").write_text("\n".join(json.dumps(r) for r in client1), encoding="utf-8")
            (folder / "client-2-motion.jsonl").write_text(json.dumps(row("2026-11-07T18:00:01.5000000Z", 280.0, [v4(13, 0, 0, 0)])), encoding="utf-8")
            (folder / "server-motion.jsonl").write_text(json.dumps({"kind": "coop_family_ids", "detail": "ids=11:Frog|12:Speeder"}), encoding="utf-8")
            spikes = report.delay_spikes(folder)
            self.assertEqual(["Frog", "Speeder"], [s["family"] for s in spikes])
            self.assertEqual("presentation hold", spikes[0]["dominant"]); self.assertEqual("network/timeline lag", spikes[1]["dominant"])
            self.assertEqual([30.0, 300.0], spikes[0]["client-1.rttRangeMs"]); self.assertEqual([280.0, 280.0], spikes[1]["client-2.rttRangeMs"])

    def test_delay_spike_origin_separates_server_stall_client_stall_and_unisolated_delay(self):
        def item(identity, delay, hold, lag):
            values = list(client_item(identity, 40, 0, 0, 10) + (4, 2, 8.0) + (0, 0.0, 100.0, lag, 10.0, hold, 50.0, 80.0, "6=40"))
            values[8] = delay
            return values
        def window(utc, items, frame_ms):
            return {"kind": "coop_present_window", "utc": utc, "realtime": 1.0, "rttMs": 20.0, "bufferMs": 40.0, "dropped": 0, "frameMs": frame_ms,
                    "detail": "v=4;paused=0;offset_ms=16.667;ids=" + "|".join(":".join(str(v) for v in i) for i in items)}
        def server(utc, frame_ms):
            return {"kind": "simulation_tick", "utc": utc, "frameMs": frame_ms}
        with tempfile.TemporaryDirectory() as tmp:
            folder = Path(tmp)
            client1 = [window("2026-11-07T18:00:00.0000000Z", [item(11, 900.0, 800.0, 100.0)], 16.7),
                       window("2026-11-07T18:00:10.0000000Z", [item(11, 800.0, 100.0, 700.0)], 610.0),
                       window("2026-11-07T18:00:20.0000000Z", [item(11, 700.0, 600.0, 100.0)], 16.7)]
            (folder / "client-1-motion.jsonl").write_text("\n".join(json.dumps(r) for r in client1), encoding="utf-8")
            rows = [{"kind": "coop_family_ids", "detail": "ids=11:Frog"}, server("2026-11-07T18:00:00.5000000Z", 520.0),
                    server("2026-11-07T18:00:10.5000000Z", 30.0), server("2026-11-07T18:00:20.5000000Z", 40.0)]
            (folder / "server-motion.jsonl").write_text("\n".join(json.dumps(r) for r in rows), encoding="utf-8")
            spikes = report.delay_spikes(folder)
            self.assertEqual(["server stall", "client stall", "Mirror-observed delay (origin not isolated)"], [s["origin"] for s in spikes])
            self.assertEqual(520.0, spikes[0]["server.frameMaxMs"]); self.assertEqual(610.0, spikes[1]["client-1.frameMaxMs"])

if __name__ == "__main__":
    unittest.main()
