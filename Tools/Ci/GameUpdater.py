#!/usr/bin/env python3
"""Linux in-place updater. Python 3 standard library only; never runs inside the install tree."""
import argparse
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import shutil
import stat
import subprocess
import sys
import tempfile
import time
import urllib.request
import zipfile

API = 'https://api.github.com/repos/MauricioSuporte/Super-Bomberman-6-Unity/releases/tags/online-v0.5.0'
PREFIX = 'https://github.com/MauricioSuporte/Super-Bomberman-6-Unity/releases/download/'
GAME = 'SB6.x86_64'

def download(url, destination):
    if not url.startswith(PREFIX):
        raise ValueError('Invalid update URL')
    request = urllib.request.Request(url, headers={'User-Agent': 'SB6-Linux-Updater'})
    with urllib.request.urlopen(request, timeout=60) as source, destination.open('wb') as target:
        shutil.copyfileobj(source, target)

def safe_entry(name):
    path = PurePosixPath(name)
    if '\\' in name or path.is_absolute() or '..' in path.parts or ':' in name or not path.parts:
        raise ValueError('Unsafe archive path')
    return path

def apply(install, manifest, work):
    archive = work / 'update.zip'
    download(manifest['linuxUrl'], archive)
    with archive.open('rb') as stream:
        digest = hashlib.file_digest(stream, 'sha256').hexdigest() if hasattr(hashlib, 'file_digest') else None
    if digest is None:
        digest_hash = hashlib.sha256()
        with archive.open('rb') as stream:
            for chunk in iter(lambda: stream.read(65536), b''): digest_hash.update(chunk)
        digest = digest_hash.hexdigest()
    if digest != manifest['linuxSha256']:
        raise ValueError('Update checksum mismatch')
    stage = work / 'stage'
    stage.mkdir()
    with zipfile.ZipFile(archive) as package:
        for entry in package.infolist():
            safe_entry(entry.filename)
            if stat.S_ISLNK(entry.external_attr >> 16): raise ValueError('Archive contains symlink')
        package.extractall(stage)
    if not (stage / GAME).is_file() or not (stage / 'SB6_Data').is_dir():
        raise ValueError('Invalid game package')
    files = json.loads((stage / 'client-files.json').read_text(encoding='utf-8'))
    if not isinstance(files, list) or not {GAME, 'GameUpdater.py', 'client-files.json'}.issubset(files):
        raise ValueError('Incomplete client file manifest')
    staged_files = {p.relative_to(stage).as_posix() for p in stage.rglob('*') if p.is_file()}
    if set(files) != staged_files:
        raise ValueError('Client file manifest does not match package')
    old_manifest = install / 'client-files.json'
    old_files = json.loads(old_manifest.read_text(encoding='utf-8')) if old_manifest.is_file() else []
    for name in files + old_files:
        safe_entry(name)
        if (install / name).resolve().is_relative_to(install) is False: raise ValueError('Install symlink escapes game folder')
    backup = work / 'backup'
    backup.mkdir()
    touched = []
    try:
        for name in dict.fromkeys(old_files + files):
            target = install / name
            saved = backup / name
            if target.is_file():
                saved.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(target, saved)
            touched.append(name)
            source = stage / name
            if source.is_file():
                target.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(source, target)
            elif target.is_file(): target.unlink()
        (install / GAME).chmod((install / GAME).stat().st_mode | 0o111)
        (install / 'GameUpdater.py').chmod((install / 'GameUpdater.py').stat().st_mode | 0o111)
    except Exception:
        for name in reversed(touched):
            target, saved = install / name, backup / name
            if saved.is_file(): shutil.copy2(saved, target)
            elif target.is_file(): target.unlink()
        raise

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--language', type=int, default=0)
    parser.add_argument('--wait-for-game', type=int, default=0)
    parser.add_argument('--apply', type=Path)
    args = parser.parse_args()
    if args.apply is None:
        install = Path(__file__).resolve().parent
        if not (install / GAME).is_file(): raise ValueError('Place updater in game folder')
        work = Path(tempfile.mkdtemp(prefix='SB6Updater-'))
        helper = work / 'GameUpdater.py'
        shutil.copy2(__file__, helper)
        subprocess.Popen([sys.executable, str(helper), '--apply', str(install), '--wait-for-game', str(args.wait_for_game), '--language', str(args.language)],
                         cwd=work, start_new_session=True, stdin=subprocess.DEVNULL,
                         stdout=(work / 'update.log').open('w', encoding='utf-8'), stderr=subprocess.STDOUT)
        return
    install = args.apply.resolve()
    work = Path(__file__).resolve().parent
    try:
        deadline = time.monotonic() + 20
        while args.wait_for_game:
            try: os.kill(args.wait_for_game, 0)
            except ProcessLookupError: break
            if time.monotonic() >= deadline: raise TimeoutError('Close game before updating')
            time.sleep(.1)
        request = urllib.request.Request(API, headers={'User-Agent': 'SB6-Linux-Updater'})
        with urllib.request.urlopen(request, timeout=30) as response:
            body = json.load(response)['body']
        manifest = json.loads(body.split('<!-- sb6-update:', 1)[1].split(' -->', 1)[0])
        apply(install, manifest, work)
    except Exception as error:
        print('Update failed:', error, flush=True)
        # Reopen the intact or rolled-back game even after a download failure.
    subprocess.Popen([str(install / GAME)], cwd=install, start_new_session=True,
                     stdin=subprocess.DEVNULL, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)

if __name__ == '__main__': main()
