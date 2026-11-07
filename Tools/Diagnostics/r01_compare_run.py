"""R01 cost / online-vs-offline runs on this Windows machine (Development players with R01ComparisonHarness).

online:  dedicated local server + 2 rendered clients, BattleMode_15, 2 players (no COM, no spawn fixture), Sudden Death Off,
         client 1 (player 1) runs the fixed input script, client 2 only observes.
offline: one rendered player process, same arena, 2 players (P1/P2 Man), same script and frame pacing.

Each process's CPU time is sampled once per second through GetProcessTimes on the handle this script owns; the server's own
[ServerMetrics] lines (cpu, KiB/s in/out, BattlePerf markers) come from server.log. Nothing else is started or stopped.
"""
import argparse
import ctypes
import ctypes.wintypes as wt
import hashlib
import json
import os
from pathlib import Path
import secrets
import subprocess
import sys
import time

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "RoomDirectory"))
from benchmark_local import host_memory  # noqa: E402

kernel32 = ctypes.windll.kernel32


def cpu_seconds(process):
    creation, exit_, kernel, user = wt.FILETIME(), wt.FILETIME(), wt.FILETIME(), wt.FILETIME()
    if not kernel32.GetProcessTimes(wt.HANDLE(int(process._handle)), ctypes.byref(creation), ctypes.byref(exit_), ctypes.byref(kernel), ctypes.byref(user)):
        return None
    ticks = lambda ft: (ft.dwHighDateTime << 32) | ft.dwLowDateTime
    return (ticks(kernel) + ticks(user)) / 1e7


def cpu_cycles(process):
    # Exact cycle count of all threads (not tick-sampled like GetProcessTimes, whose ~15.6 ms charging cannot resolve a
    # process that uses well under 1% of a core).
    cycles = ctypes.c_ulonglong()
    if not kernel32.QueryProcessCycleTime(wt.HANDLE(int(process._handle)), ctypes.byref(cycles)):
        return None
    return cycles.value


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def run(binary, output, mode, port, seconds, capture, client_extra, frame_stats=False):
    output = output.resolve()
    output.mkdir(parents=True, exist_ok=False)
    if host_memory()["availableMiB"] < 3072:
        raise RuntimeError("need at least 3 GiB of available RAM")
    assembly = binary.parent / "SB6_Data/Managed/Assembly-CSharp.dll"
    config = dict(mode=mode, binary=str(binary), assemblySha256=sha(assembly), seconds=seconds, port=port, capture=capture,
                  clientExtra=client_extra, frameStats=frame_stats, utcStart=time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()))
    (output / "config.json").write_text(json.dumps(config, indent=2), encoding="utf-8")
    owned, handles, samples = {}, [], []

    def launch(name, arguments, environment=None, graphics=True):
        handle = (output / (name + "-launcher.log")).open("wb")
        handles.append(handle)
        process = subprocess.Popen([str(binary), "-batchmode"] + ([] if graphics else ["-nographics"]) +
                                   ["-logFile", str(output / (name + ".log"))] + arguments + (["-r01-frame-stats"] if frame_stats else []),
                                   cwd=binary.parent, env=environment, stdout=handle, stderr=subprocess.STDOUT,
                                   creationflags=subprocess.CREATE_NO_WINDOW)
        owned[name] = process
        return process

    markers = {}

    def sample():
        row = dict(t=time.time())
        for name, process in owned.items():
            row[name] = cpu_seconds(process)
            row[name + "#cycles"] = cpu_cycles(process)
        samples.append(row)
        # Wall-clock (1 s resolution) of the scripted window, read from the driving process's own log.
        driver = output / (("offline" if mode == "offline" else "client-1") + ".log")
        if len(markers) < 2 and driver.is_file():
            text = driver.read_text(encoding="utf-8", errors="replace")
            for kind in ("script_start", "script_done"):
                if kind not in markers and "[R01Compare] " + kind in text:
                    markers[kind] = row["t"]

    window = ["-screen-width", "1024", "-screen-height", "896"]
    try:
        if mode == "offline":
            launch("offline", ["-r01-offline-compare", "-r01-compare-walk", "-r01-compare-sd-off"] +
                   (["-r01-compare-capture", str(output / "offline-compare.jsonl"), "-online-motion-logs"] if capture else []) + window)
            deadline = time.monotonic() + seconds + 120
            while time.monotonic() < deadline and owned["offline"].poll() is None:
                sample(); time.sleep(1)
        else:
            env = dict(os.environ, SB6_ROOM_ID="r01-compare", SB6_ROOM_TOKEN=secrets.token_hex(32), SB6_ROOM_STATUS=str(output / "status.json"))
            server = launch("server", ["-server", "-room-mode", "BattleMode", "-port", str(port), "-server-idle-seconds", "0",
                                       "-load-test-server", "-load-test-players", "2", "-load-test-duration", str(seconds), "-r01-compare-sd-off"] +
                            (["-r01-compare-capture", str(output / "server-compare.jsonl"), "-online-motion-logs"] if capture else []), env, graphics=False)
            for _ in range(90):
                if server.poll() is not None:
                    raise RuntimeError("server exited before accepting clients")
                try:
                    if json.loads((output / "status.json").read_text(encoding="utf-8-sig"))["available"]:
                        break
                except (OSError, ValueError, KeyError):
                    pass
                time.sleep(1)
            else:
                raise RuntimeError("server readiness timeout")
            for index in (1, 2):
                launch(f"client-{index}", ["-online-join", f"kcp://127.0.0.1:{port}", "-load-test-client", "-load-test-duration", str(seconds + 40),
                                           "-r01-compare-walk", "-local-diagnostic-name", f"R01Cmp{index}"] +
                       (["-r01-compare-capture", str(output / f"client-{index}-compare.jsonl"), "-online-motion-logs"] if capture else []) + window + client_extra)
                time.sleep(2)
            deadline = time.monotonic() + seconds + 90
            while time.monotonic() < deadline and server.poll() is None:
                sample(); time.sleep(1)
            end = time.monotonic() + 30
            while time.monotonic() < end and any(p.poll() is None for p in owned.values()):
                sample(); time.sleep(1)
    finally:
        sample()
        for process in owned.values():
            if process.poll() is None:
                process.kill()
        for process in owned.values():
            try:
                process.wait(timeout=20)
            except subprocess.TimeoutExpired:
                pass
        for handle in handles:
            handle.close()
        exits = {name: process.returncode for name, process in owned.items()}
        motion = {}
        for name in owned:  # the motion capture file each process announced in its own log
            log = output / (name + ".log")
            text = log.read_text(encoding="utf-8", errors="replace") if log.is_file() else ""
            marker = "[GameplayLatency] Capture: "
            if marker in text:
                source = Path(text.split(marker, 1)[1].splitlines()[0].strip())
                if source.is_file():
                    target = output / (name + "-motion.jsonl")
                    target.write_bytes(source.read_bytes())
                    motion[name] = str(target)
        config["motionCaptures"] = motion
        (output / "cpu-samples.json").write_text(json.dumps(samples), encoding="utf-8")
        config.update(scriptWindowWallClock=markers, exits=exits, utcEnd=time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()))
        (output / "config.json").write_text(json.dumps(config, indent=2), encoding="utf-8")
    return config


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--binary", type=Path, required=True)
    p.add_argument("--output", type=Path, required=True)
    p.add_argument("--mode", choices=("online", "offline"), required=True)
    p.add_argument("--port", type=int, default=29110)
    p.add_argument("--seconds", type=int, default=75)
    p.add_argument("--capture", action="store_true", help="write per-frame comparison samples (off for cost runs)")
    p.add_argument("--frame-stats", action="store_true", help="per-second frame work/allocation rows in every process")
    p.add_argument("--client-extra", default="", help="extra arguments for both clients, e.g. R01 A/B switches")
    a = p.parse_args()
    print(json.dumps(run(a.binary, a.output, a.mode, a.port, a.seconds, a.capture, a.client_extra.split()), indent=2))


if __name__ == "__main__":
    main()
