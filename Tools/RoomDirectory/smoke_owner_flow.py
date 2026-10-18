"""Two full Development clients exercise native owner menus and room shutdown."""
import argparse
import json
import os
from pathlib import Path
import secrets
import subprocess
import time
from benchmark_local import host_memory, read_json_lines


def run(binary, mode, output, port):
    output.mkdir(parents=True, exist_ok=False)
    processes, handles = [], []
    try:
        if b"LocalOnlineFlowSmoke" not in (binary.parent / "SB6_Data/Managed/Assembly-CSharp.dll").read_bytes():
            raise RuntimeError("native owner flow smoke requires the Development performance player")
        if host_memory()["availableMiB"] < 3072:
            raise RuntimeError("need 3 GiB available for server and two full clients")
        def launch(name, args, env=None):
            handle = (output / (name + "-launcher.log")).open("wb")
            handles.append(handle)
            environment = dict(env or os.environ, SB6_SAVE_DIRECTORY=str(output / ("save-" + name)))
            process = subprocess.Popen([str(binary), "-batchmode", "-nographics", "-logFile", str(output / (name + ".log")), *args],
                                       cwd=binary.parent, env=environment, stdout=handle, stderr=subprocess.STDOUT,
                                       creationflags=subprocess.CREATE_NO_WINDOW)
            processes.append(process)
            return process
        env = dict(os.environ, SB6_ROOM_ID="owner-flow", SB6_ROOM_TOKEN=secrets.token_hex(32), SB6_ROOM_STATUS=str(output / "status.json"))
        server = launch("server", ["-server", "-room-mode", mode, "-port", str(port), "-server-idle-seconds", "0"], env)
        deadline = time.monotonic() + 60
        while time.monotonic() < deadline:
            if server.poll() is not None:
                raise RuntimeError("server exited during boot")
            try:
                if json.loads((output / "status.json").read_text(encoding="utf-8-sig"))["available"]:
                    break
            except (OSError, ValueError, KeyError):
                pass
            time.sleep(1)
        else:
            raise RuntimeError("server readiness timeout")
        for i in range(2):
            if host_memory()["availableMiB"] < 1536:
                raise RuntimeError("RAM reserve reached before launching client")
            launch("client-" + str(i + 1), ["-online-join", f"kcp://127.0.0.1:{port}", "-load-test-owner-flow"])
            # Ensure client 1 is the room owner, not a racing second process.
            if i == 0:
                for _ in range(60):
                    status = json.loads((output / "status.json").read_text(encoding="utf-8-sig"))
                    if status["players"] == 1:
                        break
                    time.sleep(1)
                else:
                    raise RuntimeError("first client did not join")
        deadline = time.monotonic() + 240
        while time.monotonic() < deadline:
            logs = []
            for i in (1, 2):
                path = output / f"client-{i}.log"
                logs.append(path.read_text(encoding="utf-8-sig", errors="replace") if path.exists() else "")
            if all("[OwnerFlowSmoke] returned to title disconnected=True" in text for text in logs):
                break
            if any(process.poll() is not None for process in processes[1:]):
                raise RuntimeError("client exited unexpectedly")
            time.sleep(1)
        else:
            raise RuntimeError("native owner flow did not finish")
        server_log = (output / "server.log").read_text(encoding="utf-8-sig", errors="replace")
        required = ["scene=SkinSelect", "scene=SaveFileMenu", "scene=WorldMap"] if mode == "NormalGame" else ["scene=SkinSelect", "scene=BossRush"]
        if not all(marker in server_log for marker in required):
            raise RuntimeError("native menu scene checkpoint missing")
        if mode == "NormalGame" and not all("gameplay slot=1 difficulty=Hard players=2" in text for text in logs):
            raise RuntimeError("owner save/difficulty/player count did not reach both clients")
        if mode == "BossRush" and not all("players=2 boss=True bossDifficulty=HARD" in text for text in logs):
            raise RuntimeError("boss session missing on a client")
        errors = []
        for name in ("server", "client-1", "client-2"):
            text = (output / (name + ".log")).read_text(encoding="utf-8-sig", errors="replace")
            errors.extend(f"{name}: {line}" for line in text.splitlines() if any(marker in line for marker in
                ("Exception", "[NetSpawn]", "Failed to spawn", "Could not spawn", "Scene change is already in progress")))
        events = read_json_lines(output / "events.jsonl")
        errors.extend(event["message"] for event in events if event.get("level") in ("Error", "Exception", "Assert"))
        owner_save = json.loads((output / "save-client-1/save.dat").read_text(encoding="utf-8-sig"))
        guest_path = output / "save-client-2/save.dat"
        guest_save = json.loads(guest_path.read_text(encoding="utf-8-sig")) if guest_path.exists() else {"slots": [{"started": False}] * 3}
        if not owner_save["slots"][1]["started"] or owner_save["slots"][1]["difficulty"] != 1:
            raise RuntimeError("owner progress was not persisted to the isolated owner file")
        if any(slot["started"] for slot in guest_save["slots"]):
            raise RuntimeError("owner progress leaked into the guest save")
        result = dict(mode=mode, actualClients=2, rendered=False, isolatedSaveDirectories=True, ownerFixturePersistedToDisk=True,
                      guestSaveUnchanged=True,
                      nativeMenus=True, nonOwnerClosedRoom=True, bothReturnedToTitle=True,
                      errors=errors, warnings=[e["message"] for e in events if e.get("level") == "Warning"])
        (output / "result.json").write_text(json.dumps(result, indent=2), encoding="utf-8")
        if errors:
            raise RuntimeError("runtime errors; inspect result.json")
        print(json.dumps(result), flush=True)
    except BaseException as error:
        (output / "failure.json").write_text(json.dumps(dict(error=str(error))), encoding="utf-8")
        raise
    finally:
        for process in reversed(processes):
            if process.poll() is None:
                process.terminate()
        for process in processes:
            try:
                process.wait(timeout=10)
            except subprocess.TimeoutExpired:
                process.kill(); process.wait()
        for handle in handles:
            handle.close()


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--binary", type=Path, required=True)
    parser.add_argument("--mode", choices=("NormalGame", "BossRush"), required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--port", type=int, default=18777)
    args = parser.parse_args()
    run(args.binary.resolve(), args.mode, args.output.resolve(), args.port)
