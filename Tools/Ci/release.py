"""Release pipeline for the dedicated Windows self-hosted Unity runner.

Never opens or closes the developer's Editor: Actions checks out a separate copy.
SSH uses the Windows account's existing OpenSSH configuration, not a repo key.
"""
import argparse
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import tarfile
import time
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
SSH = "mauricio@100.64.0.46"
REPORTS = ROOT / "Build/CI"


def command(args, **kwargs):
    return subprocess.run([str(a) for a in args], cwd=ROOT, check=True, **kwargs)


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def protocol():
    source = (ROOT / "Assets/Scripts/Netcode/OnlineVersionAuthenticator.cs").read_text(encoding="utf-8-sig")
    version = int(re.search(r"ProtocolVersion\s*=\s*(\d+)", source)[1])
    directory = (ROOT / "Tools/RoomDirectory/supervisor.py").read_text(encoding="utf-8-sig")
    if version != int(re.search(r"^PROTOCOL = (\d+)", directory, re.M)[1]):
        raise RuntimeError("directory and game protocols differ")
    return version


def unity(args, log):
    version = re.search(r"^m_EditorVersion: (.+)$", (ROOT / "ProjectSettings/ProjectVersion.txt").read_text(encoding="utf-8-sig"), re.M)[1].strip()
    executable = Path(os.environ.get("SB6_UNITY", f"C:/Program Files/Unity/Hub/Editor/{version}/Editor/Unity.exe"))
    if not executable.is_file():
        raise RuntimeError(f"Unity {version} not installed at {executable}")
    environment = dict(os.environ, SB6_SAVE_DIRECTORY=str(REPORTS / "test-save"))
    command([executable, "-batchmode", "-nographics", "-projectPath", ROOT, "-logFile", log, *args], timeout=3600, env=environment)


def build():
    REPORTS.mkdir(parents=True, exist_ok=True)
    logs = ROOT / "Logs/CI"
    logs.mkdir(parents=True, exist_ok=True)
    tests = REPORTS / "online-tests.xml"
    tests.unlink(missing_ok=True)
    unity(["-runTests", "-testPlatform", "EditMode", "-testFilter", "OnlineRoomEditModeTests", "-testResults", tests], logs / "tests.log")
    result = ET.parse(tests).getroot()
    if result.get("result") != "Passed" or int(result.get("failed", "1")):
        raise RuntimeError("online tests failed; release left unchanged")
    command([os.sys.executable, "-m", "unittest", "discover", "-s", "Tools/RoomDirectory", "-p", "test_supervisor.py"])
    command([os.sys.executable, "-m", "unittest", "discover", "-s", "Tools/Ci", "-p", "test_*.py"])
    unity(["-quit", "-executeMethod", "DedicatedServerBuild.BuildReleaseArtifacts"], logs / "release.log")
    stage = ROOT / "Build/LinuxServer"
    assembly = stage / "SB6_Data/Managed/Assembly-CSharp.dll"
    if any(marker in assembly.read_bytes() for marker in (b"LocalServerLoadTest", b"TickLocalLoadTest", b"LocalNetworkInputSmoke", b"LocalOnlineFlowSmoke")):
        raise RuntimeError("development harness present in release")
    shutil.copy2(ROOT / "Tools/RoomDirectory/supervisor.py", stage / "supervisor.py")
    commit = command(["git", "rev-parse", "HEAD"], capture_output=True, text=True).stdout.strip()
    expected = os.environ.get("GITHUB_SHA", commit)
    if commit != expected:
        raise RuntimeError("checkout does not match triggering commit")
    editor_version = re.search(r"^m_EditorVersion: (.+)$", (ROOT / "ProjectSettings/ProjectVersion.txt").read_text(encoding="utf-8-sig"), re.M)[1].strip()
    dirty = bool(command(["git", "status", "--porcelain", "--untracked-files=no"], capture_output=True, text=True).stdout.strip())
    manifest = dict(builtUtc=datetime.now(timezone.utc).isoformat(), sourceCommit=commit, unity=editor_version,
                    workingTreeDirty=dirty, kind="Linux Dedicated Server release", protocol=protocol(),
                    metricsVersion=2, benchmarkHarness=False, assemblySha256=sha(assembly),
                    executableSha256=sha(stage / "SB6.x86_64"), supervisorSha256=sha(stage / "supervisor.py"))
    for destination in (stage / "deployment.json", REPORTS / "deployment.json"):
        destination.write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    for folder in (ROOT / "Build/OnlineClient/Windows", ROOT / "Build/WindowsServer"):
        (folder / "build-info.json").write_text(json.dumps(dict(sourceCommit=commit, protocol=protocol(), builtUtc=manifest["builtUtc"]), indent=2), encoding="utf-8")
    server = ROOT / "Build/WindowsServer"
    shutil.copy2(ROOT / "Tools/RoomDirectory/supervisor.py", server / "supervisor.py")
    (server / "Start-Local-Server.cmd").write_text(
        '@echo off\ncd /d "%~dp0"\npython supervisor.py --binary "%~dp0SB6Server.exe" '
        '--host 127.0.0.1 --bind 127.0.0.1 --port 18081 --first-port 17777 --capacity 3 --logs logs\npause\n',
        encoding="utf-8", newline="\r\n")
    (server / "README.txt").write_text(
        "Install Python 3, then open Start-Local-Server.cmd.\n"
        "On this PC start each Windows client with:\n"
        'SB6.exe -online-directory http://127.0.0.1:18081\n'
        "Enter Game Modes > Online. Keep this entire server folder together.\n"
        "Closing the server terminal disconnects its rooms. Public server is separate.\n",
        encoding="utf-8")
    archive = REPORTS / "linux-release.tar.gz"
    with tarfile.open(archive, "w:gz") as package:
        for child in stage.iterdir():
            package.add(child, arcname=child.name)
    print("Release artifacts ready for commit", commit, flush=True)


def deploy():
    manifest = json.loads((REPORTS / "deployment.json").read_text(encoding="utf-8"))
    commit = manifest["sourceCommit"]
    if not re.fullmatch(r"[0-9a-f]{40}", commit):
        raise RuntimeError("invalid source commit")
    suffix = re.sub(r"[^a-z0-9-]", "", os.environ.get("GITHUB_RUN_ID", str(int(time.time()))))
    attempt = os.environ.get("GITHUB_RUN_ATTEMPT", "1")
    if not attempt.isdigit():
        raise RuntimeError("invalid run attempt")
    name = f"ci-{commit[:12]}-{suffix}-{attempt}"
    stage, backup = "server-" + name, "backup-" + name
    # BatchMode prevents a workflow from hanging on a passphrase/host-key prompt.
    ssh = ["ssh", "-o", "BatchMode=yes", "-o", "ConnectTimeout=10", SSH]
    command([*ssh, "mkdir -p ~/sb6/ci-tools"])
    for local, remote in ((REPORTS / "linux-release.tar.gz", f"sb6/{name}.tar.gz"),
                          (ROOT / "Tools/RoomDirectory/deploy_staged.py", "sb6/ci-tools/deploy_staged.py"),
                          (ROOT / "Tools/Ci/activate_release.py", "sb6/ci-tools/activate_release.py")):
        command(["scp", "-o", "BatchMode=yes", "-o", "ConnectTimeout=10", local, f"{SSH}:{remote}"])
    # The remote helper validates names, waits for empty rooms, and keeps rollback.
    completed = subprocess.run([*ssh, f"python3 ~/sb6/ci-tools/activate_release.py {name} {stage} {backup}"],
                               cwd=ROOT, capture_output=True, text=True, timeout=2100, check=False)
    (REPORTS / "deploy-result.txt").write_text(completed.stdout + completed.stderr, encoding="utf-8")
    print(completed.stdout + completed.stderr, flush=True)
    completed.check_returncode()


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("action", choices=("build", "deploy"))
    options = parser.parse_args()
    (build if options.action == "build" else deploy)()
