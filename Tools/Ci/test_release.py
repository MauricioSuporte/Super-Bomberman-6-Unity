import json
from contextlib import redirect_stdout
import io
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch

import release


class ReleaseProfileTests(unittest.TestCase):
    def test_updater_pointer_changes_only_after_a_successful_asset_upload(self):
        for fail_upload in (False, True):
            with self.subTest(fail_upload=fail_upload), tempfile.TemporaryDirectory() as temporary:
                reports = Path(temporary)
                package = reports / "SB6-Windows-2026-10-19_10-00-00.zip"
                package.write_bytes(b"client archive")
                manifest = dict(sourceCommit="a" * 40, version="0.5.0", folder="Super Bomberman 6 v0.5.0",
                    package=package.name, sha256=release.sha(package))
                (reports / "client-update.json").write_text(json.dumps(manifest), encoding="utf-8")
                calls = []

                def api(request, **kwargs):
                    calls.append(request)
                    if request.get_method() == "GET":
                        response = dict(id=42, assets=[], upload_url="https://uploads.github.com/repos/test/assets{?name,label}")
                    elif request.full_url.startswith("https://uploads.github.com/"):
                        self.assertEqual(b"client archive", request.data.read())
                        if fail_upload:
                            raise release.urllib.error.HTTPError(request.full_url, 503, "unavailable", {}, None)
                        response = dict(browser_download_url="https://github.com/MauricioSuporte/Super-Bomberman-6-Unity/releases/download/online-v0.5.0/" + package.name)
                    else:
                        self.assertEqual("PATCH", request.get_method())
                        body = json.loads(request.data)["body"]
                        self.assertIn(manifest["sha256"], body)
                        self.assertIn(package.name, body)
                        response = {}
                    return io.BytesIO(json.dumps(response).encode("utf-8"))

                with patch.object(release, "REPORTS", reports), patch.dict(release.os.environ, {"GITHUB_TOKEN": "test-token"}), patch.object(release.urllib.request, "urlopen", side_effect=api):
                    if fail_upload:
                        with self.assertRaises(release.urllib.error.HTTPError):
                            release.publish_client()
                    else:
                        release.publish_client()
                self.assertEqual(not fail_upload, any(call.get_method() == "PATCH" for call in calls))

    def run_profile(self, profile, target="both"):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            reports = root / "Build/CI"
            (root / "Assets/Resources").mkdir(parents=True)
            managed = root / "Build/LinuxServer/SB6_Data/Managed"
            managed.mkdir(parents=True)
            (managed / "Assembly-CSharp.dll").write_bytes(b"release assembly")
            (root / "Build/LinuxServer/SB6.x86_64").write_bytes(b"server")
            (root / "Build/OnlineClient/Windows").mkdir(parents=True)
            (root / "Build/OnlineClient/Windows/SB6.exe").write_bytes(b"stale executable")
            (root / "Build/OnlineClient/Windows/Super Bomberman 6.exe").write_bytes(b"current executable")
            updater = root / "Updater.exe"
            updater.write_bytes(b"standalone updater")
            (root / "ProjectSettings").mkdir()
            (root / "ProjectSettings/ProjectSettings.asset").write_text("  bundleVersion: 0.5.0\n", encoding="utf-8")
            (root / "ProjectSettings/ProjectVersion.txt").write_text("m_EditorVersion: 6000.6.0f1", encoding="utf-8")
            (root / "Tools/RoomDirectory").mkdir(parents=True)
            (root / "Tools/RoomDirectory/supervisor.py").write_text("PROTOCOL = 9", encoding="utf-8")
            revision = "a" * 40

            unity_finished = False

            def command(args, **kwargs):
                self.assertFalse(unity_finished, "No subprocess should be needed after the player builds")
                output = revision if list(args[:2]) == ["git", "rev-parse"] else ""
                return subprocess.CompletedProcess(args, 0, stdout=output)

            def unity(args, log, **kwargs):
                nonlocal unity_finished
                if "-executeMethod" in args:
                    unity_finished = True
                if "-runTests" in args:
                    reports.joinpath("online-tests.xml").write_text('<test-run result="Passed" failed="0"/>', encoding="utf-8")

            with patch.object(release, "ROOT", root), patch.object(release, "REPORTS", reports), patch.object(release, "build_updater", return_value=updater), patch.object(release, "protocol", return_value=9), patch.object(release, "command", side_effect=command), patch.object(release, "unity", side_effect=unity) as build, patch.dict(release.os.environ, {"SB6_RELEASE_PROFILE": profile, "SB6_RELEASE_TARGET": target, "GITHUB_SHA": revision}):
                release.build()
                manifest = json.loads((reports / "deployment.json").read_text(encoding="utf-8"))
                self.assertEqual(revision, manifest["buildIdentity"])
                self.assertEqual(profile, manifest["profile"])
                self.assertEqual(revision, (root / "Assets/Resources/OnlineBuildIdentity.txt").read_text(encoding="utf-8"))
                self.assertTrue((reports / "linux-release.tar.gz").is_file())
                if target == "server":
                    self.assertEqual("server", manifest["target"])
                    self.assertFalse((reports / "client-update.json").exists())
                    self.assertFalse((root / "Build/Distribution").exists())
                else:
                    folder = root / "Build/Distribution/Super Bomberman 6 v0.5.0"
                    self.assertTrue((folder / "build-info.json").is_file())
                    self.assertTrue((folder / "Super Bomberman 6.exe").is_file())
                    self.assertTrue((folder / "Start-Online-Diagnostics.cmd").is_file())
                    self.assertTrue((folder / "GameUpdater.exe").is_file())
                    update = json.loads((reports / "client-update.json").read_text(encoding="utf-8"))
                    self.assertEqual(release.sha(reports / update["package"]), update["sha256"])
                    self.assertEqual(folder.name, update["folder"])
                    self.assertIn("GameUpdater.exe", json.loads((folder / "client-files.json").read_text(encoding="utf-8")))
                    self.assertFalse((folder / "SB6.exe").exists())
                return [call.args[0] for call in build.call_args_list]

    @unittest.skipUnless(release.os.name == "nt", "Windows console flags")
    def test_native_tools_do_not_inherit_the_runner_console(self):
        result = subprocess.CompletedProcess(["git", "--version"], 0, stdout="git")
        with patch.object(release.subprocess, "run", return_value=result) as run:
            release.command(["git", "--version"], capture_output=True, text=True)
        self.assertEqual(subprocess.CREATE_NO_WINDOW, run.call_args.kwargs["creationflags"])
        self.assertEqual("utf-8", run.call_args.kwargs["encoding"])
        self.assertEqual(subprocess.DEVNULL, run.call_args.kwargs["stdin"])
        self.assertEqual(300, run.call_args.kwargs["timeout"])

    @unittest.skipUnless(release.os.name == "nt", "Windows console flags")
    def test_unity_uses_its_own_hidden_console(self):
        with patch.object(release.Path, "is_file", return_value=True), patch.object(release, "command") as run:
            release.unity(["-quit"], Path("release.log"))
        options = run.call_args.kwargs
        self.assertEqual(subprocess.CREATE_NEW_CONSOLE, options["creationflags"])
        self.assertEqual(subprocess.SW_HIDE, options["startupinfo"].wShowWindow)
        self.assertTrue(options["startupinfo"].dwFlags & subprocess.STARTF_USESHOWWINDOW)

    def test_server_only_does_not_build_or_package_a_windows_client(self):
        calls = self.run_profile("quick", "server")
        self.assertEqual(1, len(calls))
        self.assertIn("DedicatedServerBuild.BuildLinuxServer", calls[0])
        self.assertIn("Linux64", calls[0])
        self.assertIn("Server", calls[0])

    def test_protocol_mismatch_fails_before_updater_or_unity(self):
        with patch.object(release, "protocol", side_effect=RuntimeError("protocol mismatch")), \
             patch.object(release, "build_updater") as updater, patch.object(release, "unity") as unity:
            with self.assertRaisesRegex(RuntimeError, "protocol mismatch"):
                release.build()
            updater.assert_not_called()
            unity.assert_not_called()

    def test_preflight_cannot_wait_on_stdin_and_has_a_short_deadline(self):
        with patch.object(release, "protocol", return_value=18), patch.object(release, "command") as run:
            release.preflight()
        args = run.call_args.args[0]
        self.assertIn("-n", args)
        self.assertIn("BatchMode=yes", args)
        self.assertIn("ServerAliveInterval=10", args)
        self.assertEqual(40, run.call_args.kwargs["timeout"])

    def test_timeout_is_propagated_instead_of_silently_retrying_activation(self):
        with redirect_stdout(io.StringIO()), patch.object(release.subprocess, "run", side_effect=subprocess.TimeoutExpired("ssh", 1)) as run:
            with self.assertRaises(subprocess.TimeoutExpired):
                release.command(["ssh", "test"], timeout=1)
        self.assertEqual(1, run.call_count)

    def test_real_command_ignores_parent_stdin_and_logs_progress(self):
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary) / "ssh.log"
            with path.open("w", encoding="utf-8") as output:
                release.command([release.os.sys.executable, "-c",
                    "import sys; assert sys.stdin.read() == ''; print('progress visible')"],
                    stdout=output, stderr=subprocess.STDOUT, heartbeat_log=path, timeout=10)
            self.assertIn("progress visible", path.read_text(encoding="utf-8"))

    def test_deploy_has_bounded_noninteractive_transfers_and_persistent_activation_log(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            reports = root / "Build/CI"
            reports.mkdir(parents=True)
            (reports / "deployment.json").write_text(json.dumps({"sourceCommit": "a" * 40}), encoding="utf-8")
            for path in (reports / "linux-release.tar.gz", root / "Tools/RoomDirectory/deploy_staged.py",
                         root / "Tools/Ci/activate_release.py"):
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_bytes(b"test payload")
            with redirect_stdout(io.StringIO()), patch.object(release, "ROOT", root), patch.object(release, "REPORTS", reports), \
                 patch.object(release, "command") as run:
                release.deploy()
            calls = run.call_args_list
            self.assertEqual(5, len(calls))
            self.assertEqual(40, calls[0].kwargs["timeout"])
            self.assertTrue(all(call.kwargs["timeout"] <= 480 for call in calls))
            self.assertTrue(all("BatchMode=yes" in call.args[0] for call in calls))
            self.assertIn("-n", calls[-1].args[0])
            self.assertIn("python3 -u", calls[-1].args[0][-1])
            self.assertTrue((reports / "deploy-result.txt").exists())
            self.assertTrue(json.loads((reports / "deploy-status.json").read_text(encoding="utf-8"))["success"])

    def test_summary_distinguishes_server_deploy_from_client_publication(self):
        with tempfile.TemporaryDirectory() as temporary:
            reports = Path(temporary)
            summary = reports / "summary.md"
            with patch.object(release, "REPORTS", reports), patch.dict(release.os.environ,
                    {"GITHUB_STEP_SUMMARY": str(summary), "SB6_RELEASE_TARGET": "server"}):
                release.summary()
            text = summary.read_text(encoding="utf-8")
            self.assertIn("**não**", text)
            self.assertIn("Editor no mesmo commit", text)

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
