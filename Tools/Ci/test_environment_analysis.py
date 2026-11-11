import unittest
from Tools.Diagnostics.analyze_bosses import pose_steps
from Tools.Ci.test_boss_analysis import sample

class EnvironmentAnalysisTests(unittest.TestCase):
    def test_environment_prefix_keeps_strict_approved_pose(self):
        native = [sample("r08_native", 1, 10), sample("r08_native", 1.1, 11)]
        good = pose_steps(native, [sample("r08_view", 1.08, 10)], "r08")["1"]
        bad = pose_steps(native, [sample("r08_view", 1.08, 11)], "r08")["1"]
        self.assertEqual((good["compared"], good["mismatches"]), (1, 0))
        self.assertEqual(bad["mismatches"], 1)

    def test_other_diagnostic_prefix_is_not_environment_evidence(self):
        self.assertEqual(pose_steps([sample("r06_native", 1, 10)], [sample("r06_view", 1.08, 10)], "r08"), {})


class EnvironmentAudioAnalysisTests(unittest.TestCase):
    def capture(self, wrong=False, pause=True):
        import tempfile, json
        from pathlib import Path
        from Tools.Diagnostics.analyze_environment import audio_report
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            native = [dict(kind="r08_loop_native", detail=f"rev={rev};id=5;phase=0.2;loop={loop};paused={paused};clip=1")
                      for rev, loop, paused in [(1, 1, 0), (2, 1, 1), (3, 0, 0)]]
            view = [dict(r, kind="r08_loop_view") for r in native if pause or ";paused=1;" not in r["detail"]]
            if wrong: view[0]["detail"] = view[0]["detail"].replace("loop=1", "loop=0")
            view.append(dict(kind="r08_loop_snapshot_view", detail="rev=4;count=0"))
            for filename, data in [("server-motion.jsonl", native), ("client-1-motion.jsonl", view), ("client-2-motion.jsonl", view)]:
                (root / filename).write_text("\n".join(json.dumps(r) for r in data), encoding="utf-8")
            return audio_report(root)

    def test_actual_loop_pause_tail_and_removal_required(self):
        self.assertEqual(self.capture()["client-1"]["status"], "PASSED")

    def test_wrong_loop_state_fails(self):
        self.assertEqual(self.capture(wrong=True)["client-1"]["status"], "FAILED")

    def test_missing_pause_is_inconclusive(self):
        self.assertEqual(self.capture(pause=False)["client-1"]["status"], "INCONCLUSIVE")


class EnvironmentCoverageTests(unittest.TestCase):
    def test_other_family_cannot_approve_missing_native_mechanism(self):
        from Tools.Diagnostics.analyze_environment import require_scenario_coverage
        result = {"reports": {"client-1": {"status": "PASSED", "problems": [], "families": {"Vulcan": {"visibleSamples": 10, "movingEntities": 1}}}}}
        require_scenario_coverage(result, "R08-Stage_3-2")
        self.assertEqual(result["reports"]["client-1"]["status"], "INCONCLUSIVE")
        self.assertEqual(result["reports"]["client-1"]["missingScenarioFamilies"], ["Volcano"])


class EnvironmentPoseAttributeTests(unittest.TestCase):
    def test_future_flip_cannot_be_hidden_by_unchanged_sprite(self):
        native = [sample("r08_native", 1, 10), sample("r08_native", 1.1, 10)]
        native[0]["detail"] += ";flipY=0"
        native[1]["detail"] += ";flipY=1"
        observed = sample("r08_view", 1.08, 10)
        observed["detail"] += ";flipY=1"
        result = pose_steps(native, [observed], "r08", ("sprite", "flipY"))["1"]
        self.assertEqual(result["mismatches"], 1)
        self.assertEqual(result["examples"][0]["fields"], ["flipY"])
