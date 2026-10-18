import io
import json
from pathlib import Path
import tarfile
import tempfile
import unittest
from unittest.mock import patch

import activate_release


class ActivationTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.home = Path(self.temp.name)
        self.base = self.home / "sb6"
        self.base.mkdir()
        (self.base / "server").mkdir()
        (self.base / "server/current.txt").write_text("current release", encoding="utf-8")
        self.archive = self.base / "ci-test.tar.gz"
        self.make_archive("SB6.x86_64")

    def tearDown(self):
        self.temp.cleanup()

    def make_archive(self, name):
        with tarfile.open(self.archive, "w:gz") as archive:
            item = tarfile.TarInfo(name)
            item.size = 4
            archive.addfile(item, io.BytesIO(b"test"))

    def test_active_rooms_timeout_preserves_running_release(self):
        with patch.object(activate_release.Path, "home", return_value=self.home), \
             patch.object(activate_release, "urlopen", return_value=io.BytesIO(b'{"rooms":[{"players":2}]}')), \
             patch.object(activate_release.time, "monotonic", side_effect=[0, 1801]), \
             patch.object(activate_release.subprocess, "run") as run:
            with self.assertRaisesRegex(RuntimeError, "rooms remained active"):
                activate_release.activate("ci-test", "server-ci-test", "backup-ci-test")
            run.assert_not_called()
        self.assertEqual("current release", (self.base / "server/current.txt").read_text(encoding="utf-8"))
        self.assertTrue(self.archive.exists())

    def test_archive_cannot_escape_stage(self):
        self.make_archive("../escape")
        with patch.object(activate_release.Path, "home", return_value=self.home):
            with self.assertRaisesRegex(ValueError, "unsafe archive"):
                activate_release.activate("ci-test", "server-ci-test", "backup-ci-test")
        self.assertFalse((self.base / "escape").exists())

    def test_success_only_prunes_ci_backups_and_keeps_three(self):
        for i in range(5):
            backup = self.base / f"backup-ci-old-{i}"
            backup.mkdir()
            (backup / "deployment.json").write_text(json.dumps({"protocol": 7}), encoding="utf-8")
        manual = self.base / "server-before-manual"
        manual.mkdir()
        with patch.object(activate_release.Path, "home", return_value=self.home), \
             patch.object(activate_release, "urlopen", return_value=io.BytesIO(b'{"rooms":[]}')), \
             patch.object(activate_release.subprocess, "run") as run:
            activate_release.activate("ci-test", "server-ci-test", "backup-ci-test")
            run.assert_called_once()
        self.assertEqual(3, len(list(self.base.glob("backup-ci-*"))))
        self.assertTrue(manual.is_dir())
        self.assertFalse(self.archive.exists())

    def test_invalid_release_names_are_rejected_before_mutation(self):
        with self.assertRaises(ValueError):
            activate_release.activate("../server", "server-ci-test", "backup-ci-test")


if __name__ == "__main__":
    unittest.main()
