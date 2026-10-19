"""VM side of CI deployment: safe archive extraction and immediate activation."""
import argparse
import json
from pathlib import Path
import re
import shutil
import subprocess
import tarfile


def activate(name, stage_name, backup_name):
    if any(not re.fullmatch(r"[a-z0-9][a-z0-9-]{3,70}", x) for x in (name, stage_name, backup_name)):
        raise ValueError("invalid release names")
    base = Path.home() / "sb6"
    if shutil.disk_usage(base).free < 2 * 1024**3:
        raise RuntimeError("less than 2 GiB free; release left unchanged")
    stage = base / stage_name
    stage.mkdir(mode=0o700)
    with tarfile.open(base / (name + ".tar.gz"), "r:gz") as package:
        for item in package.getmembers():
            if not (item.isfile() or item.isdir()) or not (stage / item.name).resolve().is_relative_to(stage.resolve()):
                raise ValueError("unsafe archive member")
        if hasattr(tarfile, "data_filter"):
            package.extractall(stage, filter="data")
        else:
            # Older Debian 12 Python: the explicit regular-file/path checks above
            # provide the same required boundary without the newer filter API.
            package.extractall(stage)
    (stage / "SB6.x86_64").chmod(0o700)
    subprocess.run(["python3", str(base / "ci-tools/deploy_staged.py"), stage_name, backup_name], check=True, timeout=120)
    (base / (name + ".tar.gz")).unlink()
    # Retain three CI rollbacks only. Manually named backups are never touched.
    backups = sorted((p for p in base.glob("backup-ci-*")
                      if p.is_dir() and not p.is_symlink() and (p / "deployment.json").is_file()),
                     key=lambda p: p.stat().st_mtime, reverse=True)
    for obsolete in backups[3:]:
        if obsolete.resolve().parent != base.resolve():
            raise RuntimeError("rollback path escaped deployment directory")
        shutil.rmtree(obsolete)
        supervisor = base / (obsolete.name + "-supervisor.py")
        if supervisor.is_file() and not supervisor.is_symlink():
            supervisor.unlink()
    print("Published", stage_name, "rollback", backup_name, flush=True)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("name")
    parser.add_argument("stage")
    parser.add_argument("backup")
    args = parser.parse_args()
    activate(args.name, args.stage, args.backup)
