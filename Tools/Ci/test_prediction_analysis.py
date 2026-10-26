import importlib.util
from pathlib import Path
import unittest


spec = importlib.util.spec_from_file_location("prediction_analysis", Path(__file__).parents[1] / "Diagnostics/analyze_prediction.py")
analysis = importlib.util.module_from_spec(spec)
spec.loader.exec_module(analysis)


class PredictionAnalysisTests(unittest.TestCase):
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
        self.assertEqual(result[0]["observedHitchFramesOver50Ms"], 2)


if __name__ == "__main__":
    unittest.main()
