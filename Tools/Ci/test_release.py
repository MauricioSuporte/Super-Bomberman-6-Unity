import json
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch

import release


class ReleaseProfileTests(unittest.TestCase):
    def run_profile(self, profile):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            reports = root / "Build/CI"
            (root / "Assets/Resources").mkdir(parents=True)
            managed = root / "Build/LinuxServer/SB6_Data/Managed"
            managed.mkdir(parents=True)
            (managed / "Assembly-CSharp.dll").write_bytes(b"release assembly")
            (root / "Build/LinuxServer/SB6.x86_64").write_bytes(b"server")
            (root / "Build/OnlineClient/Windows").mkdir(parents=True)
            (root / "ProjectSettings").mkdir()
            (root / "ProjectSettings/ProjectVersion.txt").write_text("m_EditorVersion: 6000.6.0f1", encoding="utf-8")
            (root / "Tools/RoomDirectory").mkdir(parents=True)
            (root / "Tools/RoomDirectory/supervisor.py").write_text("PROTOCOL = 9", encoding="utf-8")
            revision = "a" * 40

            def command(args, **kwargs):
                output = revision if list(args[:2]) == ["git", "rev-parse"] else ""
                return subprocess.CompletedProcess(args, 0, stdout=output)

            def unity(args, log):
                if "-runTests" in args:
                    reports.joinpath("online-tests.xml").write_text('<test-run result="Passed" failed="0"/>', encoding="utf-8")

            with patch.object(release, "ROOT", root), patch.object(release, "REPORTS", reports), patch.object(release, "protocol", return_value=9), patch.object(release, "command", side_effect=command), patch.object(release, "unity", side_effect=unity) as build, patch.dict(release.os.environ, {"SB6_RELEASE_PROFILE": profile, "GITHUB_SHA": revision}):
                release.build()
                manifest = json.loads((reports / "deployment.json").read_text(encoding="utf-8"))
                self.assertEqual(revision, manifest["buildIdentity"])
                self.assertEqual(profile, manifest["profile"])
                self.assertEqual(revision, (root / "Assets/Resources/OnlineBuildIdentity.txt").read_text(encoding="utf-8"))
                self.assertTrue((reports / "linux-release.tar.gz").is_file())
                return [call.args[0] for call in build.call_args_list]

    def test_quick_builds_players_without_running_unity_tests(self):
        calls = self.run_profile("quick")
        self.assertEqual(1, len(calls))
        self.assertIn("DedicatedServerBuild.BuildReleaseArtifacts", calls[0])

    def test_full_runs_unity_tests_before_building(self):
        calls = self.run_profile("full")
        self.assertEqual(2, len(calls))
        self.assertIn("-runTests", calls[0])
        self.assertIn("DedicatedServerBuild.BuildReleaseArtifacts", calls[1])


if __name__ == "__main__":
    unittest.main()
