"""Validate the actual standalone updater without network access or touching a game installation."""
import hashlib
import os
from pathlib import Path
import subprocess
import tempfile
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
        harness.write_text('''using System; using System.Reflection;
class InstallProbe { static int Main(string[] args) { try {
Assembly.LoadFrom(args[0]).GetType("SB6Updater").GetMethod("Install", BindingFlags.Static | BindingFlags.NonPublic)
.Invoke(null, new object[] { args[1], args[2], args[3] }); return 0;
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
            result = self.run_native([self.probe, self.executable, source, target, backup])
            self.assertNotEqual(0, result.returncode)
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
            self.assertEqual(0, self.run_native([self.probe, self.executable, source, target, backup]).returncode)
            self.assertFalse((target / "old.dll").exists())
            self.assertEqual(b"old", (backup / "old.dll").read_bytes())
            self.assertEqual(b"keep", (target / "personal.txt").read_bytes())


if __name__ == "__main__":
    unittest.main()
