import importlib.util
from pathlib import Path
import unittest


spec = importlib.util.spec_from_file_location("prediction_analysis", Path(__file__).parents[1] / "Diagnostics/analyze_prediction.py")
analysis = importlib.util.module_from_spec(spec)
spec.loader.exec_module(analysis)


class PredictionAnalysisTests(unittest.TestCase):
    def test_pusher_context_is_separate_and_uses_the_input_side(self):
        result = analysis.summarize([
            {"kind": "input_to_motion", "durationMs": 10, "detail": "at_input:prediction_block=none mounted=False pusher=False;at_response:prediction_block=none pusher=True"},
            {"kind": "input_to_motion", "durationMs": 40, "detail": "prediction_block=none mounted=False pusher=True"},
            {"kind": "input_to_motion", "durationMs": 70, "detail": "prediction_block=none mounted=True pusher=True"},
        ])
        self.assertEqual([(r["context"], r["medianMs"]) for r in result],
                         [("walking", 10), ("pusher_walking", 40), ("pusher_mounted", 70)])

    def test_boat_motion_is_not_mixed_with_walking_or_mounted_boats(self):
        result = analysis.summarize([
            {"kind": "input_to_motion", "durationMs": 10, "detail": "prediction_block=none mounted=False boat=False"},
            {"kind": "input_to_motion", "durationMs": 30, "detail": "prediction_block=none mounted=False boat=True"},
            {"kind": "input_to_motion", "durationMs": 50, "detail": "prediction_block=none mounted=True boat=True"},
        ])
        self.assertEqual([(row["context"], row["medianMs"]) for row in result],
                         [("walking", 10), ("boat", 30), ("boat_mounted", 50)])

    def test_boat_transition_uses_input_context_and_excludes_pending_permission(self):
        result = analysis.summarize([
            {"kind": "input_to_motion", "durationMs": 10, "detail": "at_input:prediction_block=none mounted=False boat=False;at_response:prediction_block=none mounted=False boat=True"},
            {"kind": "input_to_motion", "durationMs": 300, "detail": "at_input:prediction_block=boat_context_pending mounted=False boat=True;at_response:prediction_block=none mounted=False boat=True"},
        ])
        self.assertEqual(result[0]["context"], "walking")
        self.assertEqual(result[0]["medianMs"], 10)
        self.assertEqual(result[1]["context"], "boat")
        self.assertIsNone(result[1]["medianMs"])
        self.assertEqual(result[1]["blockedResponsesExcluded"], 1)

    def test_blocked_and_unclassified_input_never_become_latency_samples(self):
        result = analysis.summarize([
            {"kind": "input_to_motion", "durationMs": 500, "detail": "at_input:prediction_block=input_locked mounted=True;at_response:prediction_block=none"},
            {"kind": "input_to_motion", "durationMs": 400},
            {"kind": "input_to_motion", "durationMs": 16, "detail": "at_input:prediction_block=none mounted=True;at_response:prediction_block=none"},
            {"kind": "input_no_motion_500ms", "durationMs": 500},
        ])
        mounted = next(row for row in result if row["context"] == "mounted")
        self.assertEqual(mounted["medianMs"], 16)
        self.assertEqual(mounted["blockedResponsesExcluded"], 1)
        self.assertEqual(sum(row["unclassifiedResponsesExcluded"] for row in result), 1)
        self.assertEqual(sum(row["freeInputToMotionSamples"] for row in result), 1)

    def test_builds_and_protocols_do_not_mix(self):
        result = analysis.summarize([
            {"kind": "capture_start", "build": "one", "protocol": 24},
            {"kind": "input_to_motion", "durationMs": 12, "detail": "prediction_block=none mounted=False"},
            {"kind": "capture_start", "build": "two", "protocol": 25},
            {"kind": "input_to_motion", "durationMs": 40, "detail": "prediction_block=none mounted=False"},
        ])
        self.assertEqual([(row["build"], row["protocol"], row["medianMs"]) for row in result], [("one", 24, 12), ("two", 25, 40)])

    def test_percentile_requires_an_adequate_sample(self):
        rows = [{"kind": "input_to_motion", "durationMs": i, "detail": "prediction_block=none mounted=False"} for i in range(1, 21)]
        self.assertIsNone(analysis.summarize(rows[:19])[0]["p95Ms"])
        self.assertEqual(analysis.summarize(rows)[0]["p95Ms"], 19)

    def test_invalid_durations_are_excluded(self):
        result = analysis.summarize([{"kind": "input_to_motion", "durationMs": value} for value in [-1, float("nan"), float("inf"), "slow"]])
        self.assertEqual(result[0]["invalidDurationsExcluded"], 4)
        self.assertIsNone(result[0]["medianMs"])

    def test_hitch_frames_are_deduplicated_and_preserved_across_captures(self):
        result = analysis.summarize([
            {"kind": "sample", "frame": 1, "frameMs": 100, "_capture": "one"},
            {"kind": "other", "frame": 1, "frameMs": 100, "_capture": "one"},
            {"kind": "sample", "frame": 1, "frameMs": 100, "_capture": "two"},
        ])
        self.assertEqual(sum(row["observedHitchFramesOver50Ms"] for row in result), 2)
        self.assertEqual(len(result), 2)

    def test_players_and_capture_platforms_do_not_mix(self):
        result = analysis.summarize([
            {"kind": "capture_start", "device": "WindowsEditor"},
            {"kind": "input_to_motion", "player": 1, "durationMs": 10, "detail": "prediction_block=none mounted=False"},
            {"kind": "input_to_motion", "player": 2, "durationMs": 100, "detail": "prediction_block=none mounted=False"},
            {"kind": "capture_start", "device": "WindowsPlayer"},
            {"kind": "input_to_motion", "player": 1, "durationMs": 20, "detail": "prediction_block=none mounted=False"},
        ])
        self.assertEqual([(r["device"], r["player"], r["medianMs"]) for r in result],
                         [("WindowsEditor", 1, 10), ("WindowsEditor", 2, 100), ("WindowsPlayer", 1, 20)])

    def test_missing_header_does_not_inherit_previous_capture_build(self):
        result = analysis.summarize([
            {"kind": "capture_start", "build": "one", "protocol": 27, "_capture": "complete"},
            {"kind": "sample", "_capture": "complete"},
            {"kind": "sample", "_capture": "truncated"},
        ])
        self.assertEqual([(r["capture"], r["build"], r["protocol"]) for r in result],
                         [("complete", "one", 27), ("truncated", "unknown", None)])

    def test_conveyor_phase_groups_exclude_per_cell_traversal_target(self):
        result = analysis.summarize([
            {"kind": "input_to_motion", "durationMs": 10, "detail": "at_input:prediction_block=none mounted=False conveyor=Level/Ground:cw:slow:(1, 0, 0);at_response:prediction_block=none"},
            {"kind": "input_to_motion", "durationMs": 20, "detail": "prediction_block=none mounted=False conveyor=Level/Ground:cw:slow:(2, 0, 0)"},
            {"kind": "input_to_motion", "durationMs": 100, "detail": "prediction_block=none mounted=False conveyor=Level/Ground:ccw:fast:free"},
        ])
        self.assertEqual([(r["conveyor"], r["medianMs"]) for r in result],
                         [("Level/Ground:cw:slow", 15), ("Level/Ground:ccw:fast", 100)])


if __name__ == "__main__":
    unittest.main()
