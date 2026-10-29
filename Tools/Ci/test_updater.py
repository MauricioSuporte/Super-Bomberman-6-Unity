"""Validate the actual standalone updater without network access or touching a game installation."""
import hashlib
import http.server
import os
from pathlib import Path
import subprocess
import tempfile
import threading
import unittest
import zipfile


@unittest.skipUnless(os.name == "nt", "Windows updater")
class UpdaterTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.work = tempfile.TemporaryDirectory(prefix="sb6-updater-tests-")
        cls.root = Path(cls.work.name)
        cls.executable = cls.root / "Updater.exe"
        compiler = Path(os.environ.get("WINDIR", "C:/Windows")) / "Microsoft.NET/Framework64/v4.0.30319/csc.exe"
        source = Path(__file__).with_name("SB6Updater.cs").resolve()
        cls.run_native([compiler, "/nologo", "/codepage:65001", "/target:exe", "/warnaserror+", f"/out:{cls.executable}",
            "/reference:System.Web.Extensions.dll", "/reference:System.IO.Compression.dll",
            "/reference:System.IO.Compression.FileSystem.dll", source], check=True)
        harness = cls.root / "InstallProbe.cs"
        harness.write_text('''using System; using System.Reflection; using System.Globalization; using System.Text;
class InstallProbe { static int Main(string[] args) { try {
Console.OutputEncoding = Encoding.UTF8;
var type = Assembly.LoadFrom(args[0]).GetType("SB6Updater");
var flags = BindingFlags.Static | BindingFlags.NonPublic;
if (args[1] == "progress") {
 type.GetField("language", flags).SetValue(null, int.Parse(args[2]));
 Console.WriteLine(type.GetMethod("ProgressText", flags).Invoke(null, new object[] {
 double.Parse(args[3], CultureInfo.InvariantCulture), double.Parse(args[4], CultureInfo.InvariantCulture) }));
} else if (args[1] == "speed") {
 type.GetField("language", flags).SetValue(null, int.Parse(args[2]));
 Console.WriteLine(type.GetMethod("DownloadSpeedText", flags).Invoke(null, new object[] {
 long.Parse(args[3]), double.Parse(args[4], CultureInfo.InvariantCulture) }));
} else if (args[1] == "sequence") {
 type.GetField("progressEnabled", flags).SetValue(null, true);
 var report = type.GetMethod("Report", flags);
 var complete = type.GetMethod("CompleteProgress", flags);
 foreach (var stage in new [] { "Checking", "Downloading", "Verifying", "Extracting", "Installing" }) {
  report.Invoke(null, new object[] { stage, 0L, 1L, stage == "Downloading" });
  if (stage == "Downloading") System.Threading.Thread.Sleep(600);
  report.Invoke(null, new object[] { stage, 50L, 100L, stage == "Downloading" });
  report.Invoke(null, new object[] { stage, 100L, 100L, stage == "Downloading" });
  complete.Invoke(null, null);
 }
} else if (args[1] == "download" || args[1] == "validate" || args[1] == "extract" || args[1] == "install-progress") {
 type.GetField("progressEnabled", flags).SetValue(null, true);
 if (args[1] == "download") type.GetMethod("Download", flags).Invoke(null, new object[] { new Uri(args[2]), args[3] });
 else if (args[1] == "validate") type.GetMethod("ValidatePackage", flags).Invoke(null, new object[] { args[2], args[3], args[4] });
 else if (args[1] == "extract") type.GetMethod("Extract", flags).Invoke(null, new object[] { args[2], args[3], args[4], args[5] });
 else type.GetMethod("Install", flags).Invoke(null, new object[] { args[2], args[3], args[4] });
} else if (args[1] == "language") {
 Console.WriteLine(type.GetMethod("ReadLanguage", flags).Invoke(null, new object[] { args[2] }));
} else {
 type.GetMethod("Install", flags).Invoke(null, new object[] { args[1], args[2], args[3] });
} return 0;
} catch { return 1; } } }''', encoding="utf-8")
        cls.probe = cls.root / "InstallProbe.exe"
        cls.run_native([compiler, "/nologo", "/codepage:65001", "/target:exe", f"/out:{cls.probe}", harness], check=True)

    @classmethod
    def tearDownClass(cls):
        cls.work.cleanup()

    @staticmethod
    def run_native(args, **kwargs):
        return subprocess.run([str(arg) for arg in args], capture_output=True,
            creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0), **kwargs)

    def test_progress_and_remaining_time_follow_each_game_language(self):
        for language, label in enumerate(("ETA: ", "残り時間: ", "Restante: ", "Tempo restante: ")):
            with self.subTest(language=language):
                result = self.run_native([self.probe, self.executable, "progress", language, .5, 10])
                self.assertEqual(0, result.returncode)
                output = result.stdout.decode("utf-8-sig")
                self.assertIn("[############------------] 50%", output)
                self.assertIn(label + "00:10", output)
        result = self.run_native([self.probe, self.executable, "progress", 3, 1, 10])
        self.assertIn("100% | Tempo restante: 00:00", result.stdout.decode("utf-8-sig"))
        result = self.run_native([self.probe, self.executable, "progress", 3, -1, 10])
        self.assertIn("?% | Tempo restante: calculando...", result.stdout.decode("utf-8-sig"))

    def test_standalone_language_uses_save_and_handles_invalid_saves(self):
        path = self.root / "language-save.dat"
        for data, expected in (('{"language":3}', 3), ('{"language":1}', 1),
                               ('{"language":99}', 0), ("null", 0), ("corrupt", 0)):
            with self.subTest(data=data):
                path.write_text(data, encoding="utf-8")
                result = self.run_native([self.probe, self.executable, "language", path])
                self.assertEqual(0, result.returncode)
                self.assertEqual(str(expected), result.stdout.decode("utf-8-sig").strip())

    def test_each_stage_starts_at_zero_and_completes_independently(self):
        result = self.run_native([self.probe, self.executable, "sequence"])
        self.assertEqual(0, result.returncode)
        output = result.stdout.decode("utf-8-sig")
        self.assertEqual(5, output.count("100% | ETA: 00:00"))
        self.assertEqual(5, output.count(" 0% | ETA: estimating..."))
        # The new stage must not inherit the preceding download's elapsed time.
        verify = output.split("Verifying", 1)[1].split("Extracting", 1)[0]
        self.assertIn("50% | ETA: estimating...", verify)
        self.assertNotIn("B/s", verify)
        self.assertIn("B/s", output.split("Downloading", 1)[1].split("Verifying", 1)[0])

    def test_download_speed_uses_bytes_per_stage_second_in_all_languages(self):
        for language in range(4):
            result = self.run_native([self.probe, self.executable, "speed", language, 2097152, 2])
            self.assertEqual(0, result.returncode)
            self.assertIn(" | 1.0 MiB/s", result.stdout.decode("utf-8-sig"))
        result = self.run_native([self.probe, self.executable, "speed", 0, 1024, 0])
        self.assertIn(" | estimating...", result.stdout.decode("utf-8-sig"))

    def test_download_completes_with_known_or_unknown_size_but_not_when_truncated(self):
        payload = b"download fixture" * 10000
        class Handler(http.server.BaseHTTPRequestHandler):
            def do_GET(self):
                self.send_response(200)
                if self.path != "/unknown":
                    self.send_header("Content-Length", str(len(payload) + (1 if self.path == "/truncated" else 0)))
                self.end_headers()
                self.wfile.write(payload)
            def log_message(self, *args):
                pass
        with http.server.ThreadingHTTPServer(("127.0.0.1", 0), Handler) as server:
            worker = threading.Thread(target=server.serve_forever, daemon=True)
            worker.start()
            try:
                for scenario in ("known", "unknown", "truncated"):
                    target = self.root / (scenario + ".download")
                    result = self.run_native([self.probe, self.executable, "download",
                        f"http://127.0.0.1:{server.server_port}/{scenario}", target], timeout=15)
                    output = result.stdout.decode("utf-8-sig")
                    if scenario == "truncated":
                        self.assertNotEqual(0, result.returncode)
                        self.assertNotIn("100%", output)
                    else:
                        self.assertEqual(0, result.returncode)
                        self.assertEqual(payload, target.read_bytes())
                        self.assertIn("100% | ETA: 00:00", output)
                        if scenario == "unknown": self.assertIn("?%", output)
            finally:
                server.shutdown()
                worker.join(timeout=5)

    def test_verification_only_completes_after_digest_and_structure_are_valid(self):
        folder = "Game"
        archive, digest = self.package({folder + "/Super Bomberman 6.exe": b"game", folder + "/build-info.json": b"{}"})
        for expected, success in ((digest, True), ("0" * 64, False)):
            result = self.run_native([self.probe, self.executable, "validate", archive, expected, folder])
            self.assertEqual(success, result.returncode == 0)
            self.assertEqual(success, "100% | ETA: 00:00" in result.stdout.decode("utf-8-sig"))
        archive, digest = self.package({folder + "/Super Bomberman 6.exe": b"game"})
        result = self.run_native([self.probe, self.executable, "validate", archive, digest, folder])
        self.assertNotEqual(0, result.returncode)
        self.assertNotIn("100%", result.stdout.decode("utf-8-sig"))

    def test_extraction_only_completes_after_manifest_matches_the_release(self):
        archive, _ = self.package({"Game/Super Bomberman 6.exe": b"game", "Game/build-info.json": b'{"sourceCommit":"approved"}'})
        for expected in ("approved", "wrong"):
            result = self.run_native([self.probe, self.executable, "extract", archive, self.root / ("stage-" + expected), "Game", expected])
            self.assertEqual(expected == "approved", result.returncode == 0)
            self.assertEqual(expected == "approved", "100% | ETA: 00:00" in result.stdout.decode("utf-8-sig"))

    def package(self, entries):
        archive = self.root / (self.id().split(".")[-1] + ".zip")
        with zipfile.ZipFile(archive, "w") as package:
            for name, data in entries.items():
                package.writestr(name, data)
        return archive, hashlib.sha256(archive.read_bytes()).hexdigest()

    def test_accepts_complete_versioned_package(self):
        folder = "Super Bomberman 6 v0.5.0"
        archive, digest = self.package({folder + "/Super Bomberman 6.exe": b"game", folder + "/build-info.json": b"{}"})
        self.assertEqual(0, self.run_native([self.executable, "--validate-package", archive, digest, folder]).returncode)

    def test_rejects_corrupt_download(self):
        archive, _ = self.package({"Game/game.exe": b"game"})
        self.assertNotEqual(0, self.run_native([self.executable, "--validate-package", archive, "0" * 64, "Game"]).returncode)

    def test_rejects_archive_path_escape(self):
        archive, digest = self.package({"Game/../../escaped.exe": b"bad"})
        self.assertNotEqual(0, self.run_native([self.executable, "--validate-package", archive, digest, "Game"]).returncode)

    def test_rejects_incomplete_package(self):
        archive, digest = self.package({"Game/Super Bomberman 6.exe": b"game"})
        self.assertNotEqual(0, self.run_native([self.executable, "--validate-package", archive, digest, "Game"]).returncode)

    def test_rolls_back_failed_install_and_preserves_untracked_files(self):
        with tempfile.TemporaryDirectory(prefix="sb6-install-test-") as temporary:
            root = Path(temporary)
            source, target, backup = root / "source", root / "target", root / "backup"
            source.mkdir(); target.mkdir()
            (source / "a.txt").write_text("new", encoding="utf-8")
            (source / "b.txt").write_text("blocked", encoding="utf-8")
            (target / "a.txt").write_text("old", encoding="utf-8")
            (target / "b.txt").mkdir()
            (target / "OnlineMotion.log").write_text("keep", encoding="utf-8")
            result = self.run_native([self.probe, self.executable, "install-progress", source, target, backup])
            self.assertNotEqual(0, result.returncode)
            self.assertNotIn("100%", result.stdout.decode("utf-8-sig"))
            self.assertEqual("old", (target / "a.txt").read_text(encoding="utf-8"))
            self.assertEqual("keep", (target / "OnlineMotion.log").read_text(encoding="utf-8"))

    def test_removes_only_obsolete_files_tracked_by_the_previous_package(self):
        with tempfile.TemporaryDirectory(prefix="sb6-install-test-") as temporary:
            root = Path(temporary)
            source, target, backup = root / "source", root / "target", root / "backup"
            source.mkdir(); target.mkdir()
            (source / "new.dll").write_bytes(b"new")
            (target / "old.dll").write_bytes(b"old")
            (target / "personal.txt").write_bytes(b"keep")
            (target / "client-files.json").write_text('["old.dll"]', encoding="utf-8")
            result = self.run_native([self.probe, self.executable, "install-progress", source, target, backup])
            self.assertEqual(0, result.returncode)
            self.assertIn("100% | ETA: 00:00", result.stdout.decode("utf-8-sig"))
            self.assertFalse((target / "old.dll").exists())
            self.assertEqual(b"old", (backup / "old.dll").read_bytes())
            self.assertEqual(b"keep", (target / "personal.txt").read_bytes())


if __name__ == "__main__":
    unittest.main()
