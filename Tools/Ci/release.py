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
SSH = os.environ.get("SB6_DEPLOY_SSH", "mauricio@100.64.0.46")
REPORTS = ROOT / "Build/CI"


def command(args, **kwargs):
    print("Running:", " ".join(str(a) for a in args), flush=True)
    started = time.monotonic()
    # Do not let native tools attach to the PowerShell/runner console.
    if os.name == "nt":
        kwargs.setdefault("creationflags", subprocess.CREATE_NO_WINDOW)
    if kwargs.get("text") or kwargs.get("universal_newlines"):
        kwargs.setdefault("encoding", "utf-8")
    result = subprocess.run([str(a) for a in args], cwd=ROOT, check=True, **kwargs)
    print(f"Completed in {time.monotonic() - started:.1f}s", flush=True)
    return result


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
    launch = {}
    if os.name == "nt":
        # Unity manages its console during shutdown. Give it a separate hidden
        # console so it cannot invalidate the runner's console handles.
        startup = subprocess.STARTUPINFO()
        startup.dwFlags |= subprocess.STARTF_USESHOWWINDOW
        startup.wShowWindow = subprocess.SW_HIDE
        launch = dict(creationflags=subprocess.CREATE_NEW_CONSOLE, startupinfo=startup)
    command([executable, "-batchmode", "-nographics", "-projectPath", ROOT, "-logFile", log, *args],
            timeout=3600, env=environment, **launch)


def build():
    REPORTS.mkdir(parents=True, exist_ok=True)
    logs = ROOT / "Logs/CI"
    logs.mkdir(parents=True, exist_ok=True)
    profile = os.environ.get("SB6_RELEASE_PROFILE", "full")
    if profile not in ("quick", "full"):
        raise RuntimeError("invalid release profile")
    commit = command(["git", "rev-parse", "HEAD"], capture_output=True, text=True).stdout.strip()
    expected = os.environ.get("GITHUB_SHA", commit)
    if not re.fullmatch(r"[0-9a-f]{40}", commit) or commit != expected:
        raise RuntimeError("checkout does not match triggering commit")
    dirty_before_build = bool(command(["git", "status", "--porcelain", "--untracked-files=no"],
                                      capture_output=True, text=True).stdout.strip())
    stamp = ROOT / "Assets/Resources/OnlineBuildIdentity.txt"
    stamp.write_text(commit, encoding="utf-8")
    print("Release profile:", profile, "build identity:", commit, flush=True)
    tests = REPORTS / "online-tests.xml"
    tests.unlink(missing_ok=True)
    if profile == "full":
        unity(["-runTests", "-testPlatform", "EditMode", "-testFilter", "OnlineRoomEditModeTests", "-testResults", tests], logs / "tests.log")
        result = ET.parse(tests).getroot()
        if result.get("result") != "Passed" or int(result.get("failed", "1")):
            raise RuntimeError("online tests failed; release left unchanged")
    else:
        print("Quick profile: Unity tests skipped; both player builds still compile.", flush=True)
    command([os.sys.executable, "-m", "unittest", "discover", "-s", "Tools/RoomDirectory", "-p", "test_supervisor.py"])
    command([os.sys.executable, "-m", "unittest", "discover", "-s", "Tools/Ci", "-p", "test_*.py"])
    unity(["-quit", "-executeMethod", "DedicatedServerBuild.BuildReleaseArtifacts"], logs / "release.log")
    stage = ROOT / "Build/LinuxServer"
    assembly = stage / "SB6_Data/Managed/Assembly-CSharp.dll"
    if any(marker in assembly.read_bytes() for marker in (b"LocalServerLoadTest", b"TickLocalLoadTest", b"LocalNetworkInputSmoke", b"LocalOnlineFlowSmoke")):
        raise RuntimeError("development harness present in release")
    shutil.copy2(ROOT / "Tools/RoomDirectory/supervisor.py", stage / "supervisor.py")
    # The isolated checkout revision was validated before launching Unity.
    # Reuse it for every artifact rather than launching Git again after the build.
    editor_version = re.search(r"^m_EditorVersion: (.+)$", (ROOT / "ProjectSettings/ProjectVersion.txt").read_text(encoding="utf-8-sig"), re.M)[1].strip()
    manifest = dict(builtUtc=datetime.now(timezone.utc).isoformat(), sourceCommit=commit, unity=editor_version,
                    workingTreeDirtyBeforeBuild=dirty_before_build, kind="Linux Dedicated Server release", profile=profile, buildIdentity=commit, protocol=protocol(),
                    metricsVersion=2, benchmarkHarness=False, assemblySha256=sha(assembly),
                    executableSha256=sha(stage / "SB6.x86_64"), supervisorSha256=sha(stage / "supervisor.py"))
    for destination in (stage / "deployment.json", REPORTS / "deployment.json"):
        destination.write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    (ROOT / "Build/OnlineClient/Windows/build-info.json").write_text(
        json.dumps(dict(sourceCommit=commit, protocol=protocol(), builtUtc=manifest["builtUtc"]), indent=2),
        encoding="utf-8")
    archive = REPORTS / "linux-release.tar.gz"
    with tarfile.open(archive, "w:gz", compresslevel=1) as package:
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
    # The remote helper validates the release and disconnects rooms on activation.
    completed = subprocess.run([*ssh, f"python3 ~/sb6/ci-tools/activate_release.py {name} {stage} {backup}"],
                               cwd=ROOT, capture_output=True, text=True, encoding="utf-8", timeout=2100, check=False,
                               **({"creationflags": subprocess.CREATE_NO_WINDOW} if os.name == "nt" else {}))
    (REPORTS / "deploy-result.txt").write_text(completed.stdout + completed.stderr, encoding="utf-8")
    print(completed.stdout + completed.stderr, flush=True)
    completed.check_returncode()


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("action", choices=("build", "deploy"))
    options = parser.parse_args()
    (build if options.action == "build" else deploy)()
