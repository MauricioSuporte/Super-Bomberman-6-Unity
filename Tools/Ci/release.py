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
import threading
import xml.etree.ElementTree as ET
import urllib.error
import urllib.parse
import urllib.request
import zipfile

ROOT = Path(__file__).resolve().parents[2]
SSH = os.environ.get("SB6_DEPLOY_SSH", "mauricio@100.64.0.46")
REPORTS = ROOT / "Build/CI"


def command(args, **kwargs):
    """Bound every external tool and never give SSH the runner's stdin pipe."""
    heartbeat_log = kwargs.pop("heartbeat_log", None)
    print("Running:", " ".join(str(a) for a in args), flush=True)
    started = time.monotonic()
    kwargs.setdefault("stdin", subprocess.DEVNULL)
    kwargs.setdefault("timeout", 300)
    if os.name == "nt":
        kwargs.setdefault("creationflags", subprocess.CREATE_NO_WINDOW)
    if kwargs.get("text") or kwargs.get("universal_newlines"):
        kwargs.setdefault("encoding", "utf-8")
    stop = threading.Event()
    reporter = None
    if heartbeat_log is not None:
        reporter = threading.Thread(target=report_progress,
            args=(Path(heartbeat_log), stop, started), daemon=True)
        reporter.start()
    try:
        result = subprocess.run([str(a) for a in args], cwd=ROOT, check=True, **kwargs)
    except subprocess.TimeoutExpired:
        print(f"::error::Command exceeded {kwargs['timeout']}s: {args[0]}", flush=True)
        raise
    finally:
        stop.set()
        if reporter is not None:
            reporter.join(timeout=2)
        print(f"Command finished after {time.monotonic() - started:.1f}s", flush=True)
    return result


def report_progress(path, stop, started):
    offset = 0
    while True:
        finished = stop.wait(10)
        if path.is_file():
            with path.open("rb") as log:
                size = log.seek(0, 2)
                log.seek(max(0 if size < offset else offset, size - 4096))
                text = log.read().decode("utf-8", errors="replace")
                offset = size
            lines = [line.strip() for line in text.splitlines() if line.strip()]
            if lines:
                print(f"[{path.name}] " + " | ".join(lines[-3:])[-900:], flush=True)
        if finished:
            return
        print(f"Still running ({time.monotonic() - started:.0f}s); log: {path}", flush=True)


def ssh_options():
    return ["-o", "BatchMode=yes", "-o", "ConnectTimeout=10",
            "-o", "ConnectionAttempts=1", "-o", "ServerAliveInterval=10",
            "-o", "ServerAliveCountMax=2", "-o", "StrictHostKeyChecking=yes"]


def preflight():
    # Run before Unity: an unreachable VM or protocol mismatch should not cost a build.
    version = protocol()
    command(["ssh", "-n", "-T", *ssh_options(), SSH,
             "test -d ~/sb6/server && command -v python3 && df -h ~/sb6"], timeout=40)
    print("Deployment preflight passed; protocol:", version, flush=True)


def sha(path):
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def build_updater():
    compiler = Path(os.environ.get("WINDIR", "C:/Windows")) / "Microsoft.NET/Framework64/v4.0.30319/csc.exe"
    output = REPORTS / "GameUpdater.exe"
    command([compiler, "/nologo", "/codepage:65001", "/target:exe", "/optimize+", "/warnaserror+", f"/out:{output}",
             "/reference:System.Web.Extensions.dll", "/reference:System.IO.Compression.dll",
             "/reference:System.IO.Compression.FileSystem.dll", ROOT / "Tools/Ci/SB6Updater.cs"])
    return output


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
            timeout=1800, env=environment, heartbeat_log=log, **launch)


def build():
    REPORTS.mkdir(parents=True, exist_ok=True)
    logs = ROOT / "Logs/CI"
    logs.mkdir(parents=True, exist_ok=True)
    profile = os.environ.get("SB6_RELEASE_PROFILE", "full")
    if profile not in ("quick", "full"):
        raise RuntimeError("invalid release profile")
    target = os.environ.get("SB6_RELEASE_TARGET", "both")
    if target not in ("both", "server"):
        raise RuntimeError("invalid release target")
    wire_protocol = protocol()
    updater = build_updater() if target == "both" else None
    commit = command(["git", "rev-parse", "HEAD"], capture_output=True, text=True).stdout.strip()
    expected = os.environ.get("GITHUB_SHA", commit)
    if not re.fullmatch(r"[0-9a-f]{40}", commit) or commit != expected:
        raise RuntimeError("checkout does not match triggering commit")
    dirty_before_build = bool(command(["git", "status", "--porcelain", "--untracked-files=no"],
                                      capture_output=True, text=True).stdout.strip())
    stamp = ROOT / "Assets/Resources/OnlineBuildIdentity.txt"
    stamp.write_text(commit, encoding="utf-8")
    print("Release profile:", profile, "target:", target, "build identity:", commit, flush=True)
    tests = REPORTS / "online-tests.xml"
    tests.unlink(missing_ok=True)
    if profile == "full":
        unity(["-runTests", "-testPlatform", "EditMode", "-testFilter", "OnlineRoomEditModeTests;BattleModeComEditorTests", "-testResults", tests], logs / "tests.log")
        result = ET.parse(tests).getroot()
        if result.get("result") != "Passed" or int(result.get("failed", "1")):
            raise RuntimeError("online tests failed; release left unchanged")
    else:
        print("Quick profile: Unity tests skipped; requested players still compile.", flush=True)
    command([os.sys.executable, "-m", "unittest", "discover", "-s", "Tools/RoomDirectory", "-p", "test_supervisor.py"])
    command([os.sys.executable, "-m", "unittest", "discover", "-s", "Tools/Ci", "-p", "test_*.py"])
    method = "DedicatedServerBuild.BuildLinuxServer" if target == "server" else "DedicatedServerBuild.BuildReleaseArtifacts"
    build_args = ["-quit", "-executeMethod", method]
    if target == "server":
        # Select Linux before import, avoiding a Windows import then a target switch.
        build_args += ["-buildTarget", "Linux64", "-standaloneBuildSubtarget", "Server"]
    unity(build_args, logs / "release.log")
    stage = ROOT / "Build/LinuxServer"
    assembly = stage / "SB6_Data/Managed/Assembly-CSharp.dll"
    if any(marker in assembly.read_bytes() for marker in (b"LocalServerLoadTest", b"TickLocalLoadTest", b"LocalNetworkInputSmoke", b"LocalOnlineFlowSmoke")):
        raise RuntimeError("development harness present in release")
    shutil.copy2(ROOT / "Tools/RoomDirectory/supervisor.py", stage / "supervisor.py")
    # The isolated checkout revision was validated before launching Unity.
    # Reuse it for every artifact rather than launching Git again after the build.
    editor_version = re.search(r"^m_EditorVersion: (.+)$", (ROOT / "ProjectSettings/ProjectVersion.txt").read_text(encoding="utf-8-sig"), re.M)[1].strip()
    manifest = dict(builtUtc=datetime.now(timezone.utc).isoformat(), sourceCommit=commit, unity=editor_version,
                    workingTreeDirtyBeforeBuild=dirty_before_build, kind="Linux Dedicated Server release", profile=profile, target=target, buildIdentity=commit, protocol=wire_protocol,
                    metricsVersion=2, benchmarkHarness=False, assemblySha256=sha(assembly),
                    executableSha256=sha(stage / "SB6.x86_64"), supervisorSha256=sha(stage / "supervisor.py"))
    for destination in (stage / "deployment.json", REPORTS / "deployment.json"):
        destination.write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    if target == "both":
        package_client(commit, manifest["builtUtc"], updater, wire_protocol)
    else:
        # Never expose a previous run's updater manifest in server-only mode.
        (REPORTS / "client-update.json").unlink(missing_ok=True)
    archive = REPORTS / "linux-release.tar.gz"
    with tarfile.open(archive, "w:gz", compresslevel=1) as package:
        for child in stage.iterdir():
            package.add(child, arcname=child.name)
    print("Release artifacts ready for commit", commit, flush=True)


def package_client(commit, built_utc, updater, wire_protocol):
    (ROOT / "Build/OnlineClient/Windows/build-info.json").write_text(
        json.dumps(dict(sourceCommit=commit, protocol=wire_protocol, builtUtc=built_utc), indent=2),
        encoding="utf-8")
    settings = (ROOT / "ProjectSettings/ProjectSettings.asset").read_text(encoding="utf-8-sig")
    version = re.search(r"^  bundleVersion: ([0-9A-Za-z._-]+)\s*$", settings, re.M)
    if version is None:
        raise RuntimeError("missing or invalid game version for the client package")
    distribution = ROOT / "Build/Distribution" / f"Super Bomberman 6 v{version[1]}"
    shutil.copytree(ROOT / "Build/OnlineClient/Windows", distribution,
                    ignore=shutil.ignore_patterns("SB6.exe", "SB6_Data", "SB6_BurstDebugInformation_DoNotShip"))
    shutil.copy2(updater, distribution / "GameUpdater.exe")
    (distribution / "Start-Online-Diagnostics.cmd").write_text(
        '@echo off\ncd /d "%~dp0"\nstart "" "Super Bomberman 6.exe" -online-motion-logs -logFile "%~dp0OnlineMotion.log"\n',
        encoding="utf-8", newline="\r\n")
    files = sorted(path.relative_to(distribution).as_posix() for path in distribution.rglob("*") if path.is_file())
    (distribution / "client-files.json").write_text(json.dumps(files + ["client-files.json"]), encoding="utf-8")
    stamp = os.environ.get("SB6_ARTIFACT_TIMESTAMP", datetime.now(timezone.utc).strftime("%Y-%m-%d_%H-%M-%S"))
    if not re.fullmatch(r"[0-9_-]+", stamp):
        raise RuntimeError("invalid package timestamp")
    package = REPORTS / f"SB6-Windows-{stamp}.zip"
    with zipfile.ZipFile(package, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=1) as archive:
        for path in sorted(distribution.rglob("*")):
            if path.is_file():
                archive.write(path, path.relative_to(distribution.parent).as_posix())
    (REPORTS / "client-update.json").write_text(json.dumps(dict(sourceCommit=commit, version=version[1],
        folder=distribution.name, package=package.name, sha256=sha(package)), indent=2), encoding="utf-8")

def deploy():
    manifest = json.loads((REPORTS / "deployment.json").read_text(encoding="utf-8"))
    commit = manifest["sourceCommit"]
    (REPORTS / "deploy-status.json").unlink(missing_ok=True)
    if not re.fullmatch(r"[0-9a-f]{40}", commit):
        raise RuntimeError("invalid source commit")
    suffix = re.sub(r"[^a-z0-9-]", "", os.environ.get("GITHUB_RUN_ID", str(int(time.time()))))
    attempt = os.environ.get("GITHUB_RUN_ATTEMPT", "1")
    if not attempt.isdigit():
        raise RuntimeError("invalid run attempt")
    name = f"ci-{commit[:12]}-{suffix}-{attempt}"
    stage, backup = "server-" + name, "backup-" + name
    ssh = ["ssh", "-n", "-T", *ssh_options(), SSH]
    command([*ssh, "mkdir -p ~/sb6/ci-tools"], timeout=40)
    for local, remote in ((REPORTS / "linux-release.tar.gz", f"sb6/{name}.tar.gz"),
                          (ROOT / "Tools/RoomDirectory/deploy_staged.py", "sb6/ci-tools/deploy_staged.py"),
                          (ROOT / "Tools/Ci/activate_release.py", "sb6/ci-tools/activate_release.py")):
        size_mb = Path(local).stat().st_size / 1024**2
        print(f"Uploading {Path(local).name} ({size_mb:.1f} MiB)", flush=True)
        log = REPORTS / (Path(local).name + ".upload.txt")
        with log.open("w", encoding="utf-8") as output:
            command(["scp", "-v", *ssh_options(), local, f"{SSH}:{remote}"],
                    timeout=300, stdout=output, stderr=subprocess.STDOUT, heartbeat_log=log)
    # No activation retry: an interrupted connection may leave the transaction running remotely.
    log = REPORTS / "deploy-result.txt"
    with log.open("w", encoding="utf-8") as output:
        command([*ssh, f"python3 -u ~/sb6/ci-tools/activate_release.py {name} {stage} {backup}"],
                timeout=480, stdout=output, stderr=subprocess.STDOUT, heartbeat_log=log)
    (REPORTS / "deploy-status.json").write_text(json.dumps(dict(sourceCommit=commit,
        activatedUtc=datetime.now(timezone.utc).isoformat(), success=True)), encoding="utf-8")
    print("::notice title=Servidor atualizado::" + commit, flush=True)
    print("Server activated successfully:", commit, flush=True)


def publish_client():
    """Expose the successfully deployed client without distributing GitHub credentials."""
    token = os.environ["GITHUB_TOKEN"]
    repository = "MauricioSuporte/Super-Bomberman-6-Unity"
    endpoint = f"https://api.github.com/repos/{repository}/releases"
    tag = "online-v0.5.0"
    update = json.loads((REPORTS / "client-update.json").read_text(encoding="utf-8"))
    if not re.fullmatch(r"SB6-Windows-[0-9_-]+\.zip", update["package"]):
        raise RuntimeError("invalid client archive")
    package = REPORTS / update["package"]
    if sha(package) != update["sha256"]:
        raise RuntimeError("client package changed after build")

    def request(url, method="GET", payload=None, content_type="application/json"):
        body = json.dumps(payload).encode("utf-8") if payload is not None else None
        headers = {"Authorization": f"Bearer {token}", "Accept": "application/vnd.github+json",
                   "User-Agent": "SB6-Release", "Content-Type": content_type}
        with urllib.request.urlopen(urllib.request.Request(url, data=body, headers=headers, method=method), timeout=120) as response:
            return json.load(response)

    try:
        release = request(f"{endpoint}/tags/{tag}")
    except urllib.error.HTTPError as error:
        if error.code != 404:
            raise
        release = request(endpoint, "POST", dict(tag_name=tag, target_commitish=update["sourceCommit"],
            name="Online v0.5.0 — atualização automática", prerelease=True, make_latest="false"))
    # Upload before atomically changing the public update pointer. Failed uploads leave the old client available.
    upload = release["upload_url"].split("{")[0] + "?name=" + urllib.parse.quote(package.name)
    existing = next((asset for asset in release["assets"] if asset["name"] == package.name), None)
    if existing is None:
        # Stream large Unity packages rather than loading the complete ZIP into runner RAM.
        with package.open("rb") as stream:
            headers = {"Authorization": f"Bearer {token}", "User-Agent": "SB6-Release",
                       "Content-Type": "application/zip", "Content-Length": str(package.stat().st_size)}
            with urllib.request.urlopen(urllib.request.Request(upload, data=stream, headers=headers, method="POST"), timeout=1800) as response:
                existing = json.load(response)
    update["url"] = existing["browser_download_url"]
    update.pop("package")
    body = "Cliente correspondente ao servidor online publicado. Execute GameUpdater.exe com o jogo fechado.\n\n"
    body += f"Build: {update['sourceCommit']}\n\n<!-- sb6-update:{json.dumps(update, separators=(',', ':'))} -->"
    request(f"{endpoint}/{release['id']}", "PATCH", dict(body=body, prerelease=True, make_latest="false"))
    print("Client updater channel published:", update["sourceCommit"], flush=True)


def summary():
    destination = os.environ.get("GITHUB_STEP_SUMMARY")
    if not destination:
        return
    target = os.environ.get("SB6_RELEASE_TARGET", "both")
    status_file = REPORTS / "deploy-status.json"
    deployed = status_file.is_file() and json.loads(status_file.read_text(encoding="utf-8")).get("success")
    lines = ["### Homologação online", "", f"- Alvo: `{target}`",
             f"- Perfil: `{os.environ.get('SB6_RELEASE_PROFILE', 'quick')}`",
             f"- Servidor atualizado nesta execução: **{'sim' if deployed else 'não'}**",
             f"- Commit: `{os.environ.get('GITHUB_SHA', 'local')}`"]
    if target == "server":
        lines += ["", "Somente servidor: use o Editor no mesmo commit. O cliente Windows e seu atualizador não foram publicados."]
    with Path(destination).open("a", encoding="utf-8") as output:
        output.write("\n".join(lines) + "\n")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("action", choices=("preflight", "build", "deploy", "publish-client", "summary"))
    options = parser.parse_args()
    {"preflight": preflight, "build": build, "deploy": deploy, "publish-client": publish_client, "summary": summary}[options.action]()
