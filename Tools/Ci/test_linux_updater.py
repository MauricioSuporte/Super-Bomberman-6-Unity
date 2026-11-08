import hashlib
import json
from pathlib import Path
import shutil
import tempfile
import unittest
from unittest.mock import patch
import zipfile

import GameUpdater as updater


class LinuxUpdaterTests(unittest.TestCase):
    def test_update_preserves_unmanaged_saves_and_removes_obsolete_managed_files(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            install, work = root / 'game', root / 'work'
            install.mkdir(); work.mkdir()
            (install / updater.GAME).write_bytes(b'old game')
            (install / 'obsolete.dat').write_bytes(b'obsolete')
            (install / 'save.dat').write_bytes(b'player save')
            (install / 'client-files.json').write_text(json.dumps([updater.GAME, 'obsolete.dat', 'client-files.json']), encoding='utf-8')
            package = root / 'package.zip'
            files = [updater.GAME, 'GameUpdater.py', 'SB6_Data/content', 'client-files.json']
            with zipfile.ZipFile(package, 'w') as archive:
                archive.writestr(updater.GAME, b'new game')
                archive.writestr('GameUpdater.py', b'updater')
                archive.writestr('SB6_Data/content', b'assets')
                archive.writestr('client-files.json', json.dumps(files))
            manifest = {'linuxUrl': 'unused', 'linuxSha256': hashlib.sha256(package.read_bytes()).hexdigest()}
            with patch.object(updater, 'download', side_effect=lambda url, destination: shutil.copy2(package, destination)):
                updater.apply(install, manifest, work)
            self.assertEqual(b'new game', (install / updater.GAME).read_bytes())
            self.assertEqual(b'player save', (install / 'save.dat').read_bytes())
            self.assertFalse((install / 'obsolete.dat').exists())

    def test_corrupted_download_does_not_touch_install(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            (root / updater.GAME).write_bytes(b'old game')
            work = root / 'work'; work.mkdir()
            with patch.object(updater, 'download', side_effect=lambda url, destination: destination.write_bytes(b'corrupt')):
                with self.assertRaisesRegex(ValueError, 'checksum'):
                    updater.apply(root, {'linuxUrl': 'unused', 'linuxSha256': '0' * 64}, work)
            self.assertEqual(b'old game', (root / updater.GAME).read_bytes())

    def test_archive_paths_cannot_escape_install(self):
        for name in ('../save.dat', '/tmp/save.dat', 'C:/save.dat', 'folder/../../save.dat', 'folder\\save.dat'):
            with self.subTest(name=name), self.assertRaises(ValueError): updater.safe_entry(name)

    def test_failed_copy_rolls_back_game(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp); install = root / 'game'; work = root / 'work'
            install.mkdir(); work.mkdir()
            (install / updater.GAME).write_bytes(b'old')
            package = root / 'package.zip'
            files = [updater.GAME, 'GameUpdater.py', 'SB6_Data/content', 'client-files.json']
            with zipfile.ZipFile(package, 'w') as archive:
                for name in files: archive.writestr(name, json.dumps(files) if name == 'client-files.json' else 'new')
            original_copy = shutil.copy2
            def copy(source, destination, *args, **kwargs):
                if 'stage' in Path(source).parts and Path(source).name == 'content': raise OSError('disk full')
                return original_copy(source, destination, *args, **kwargs)
            with patch.object(updater, 'download', side_effect=lambda url, destination: original_copy(package, destination)), patch.object(updater.shutil, 'copy2', side_effect=copy):
                with self.assertRaisesRegex(OSError, 'disk full'):
                    updater.apply(install, {'linuxUrl': 'unused', 'linuxSha256': hashlib.sha256(package.read_bytes()).hexdigest()}, work)
            self.assertEqual(b'old', (install / updater.GAME).read_bytes())

if __name__ == '__main__': unittest.main()
