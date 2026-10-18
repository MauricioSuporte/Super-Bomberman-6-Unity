"""Windows-only synthetic KCP load test. Owns and cleans up only its child processes."""
import argparse
import ctypes
import hashlib
from ctypes import wintypes as w
import json
import os
import re
from pathlib import Path
import secrets
import statistics
import subprocess
import time

ENTRY_GROWTH_MIB = 96


class MemoryStatus(ctypes.Structure):
    _fields_ = [("length", w.DWORD), ("load", w.DWORD)] + [(name, ctypes.c_ulonglong) for name in
        ("total", "available", "totalPage", "availablePage", "totalVirtual", "availableVirtual", "extended")]


class ProcessMemory(ctypes.Structure):
    _fields_ = [("cb", w.DWORD), ("faults", w.DWORD)] + [(name, ctypes.c_size_t) for name in
        ("peakRss", "rss", "peakPaged", "paged", "peakNonPaged", "nonPaged", "pagefile", "peakPagefile", "private")]


def host_memory():
    data = MemoryStatus(); data.length = ctypes.sizeof(data)
    if not ctypes.windll.kernel32.GlobalMemoryStatusEx(ctypes.byref(data)):
        raise ctypes.WinError()
    return dict(totalMiB=data.total / 1048576, availableMiB=data.available / 1048576,
                availableCommitMiB=data.availablePage / 1048576)


def process_sample(pid):
    kernel = ctypes.windll.kernel32
    kernel.OpenProcess.argtypes = [w.DWORD, w.BOOL, w.DWORD]; kernel.OpenProcess.restype = w.HANDLE
    kernel.CloseHandle.argtypes = [w.HANDLE]
    kernel.GetProcessTimes.argtypes = [w.HANDLE] + [ctypes.POINTER(w.FILETIME)] * 4
    ctypes.windll.psapi.GetProcessMemoryInfo.argtypes = [w.HANDLE, ctypes.POINTER(ProcessMemory), w.DWORD]
    handle = kernel.OpenProcess(0x410, False, pid)
    if not handle: return None
    try:
        memory = ProcessMemory(); memory.cb = ctypes.sizeof(memory)
        created, exited, system, user = (w.FILETIME() for _ in range(4))
        if not ctypes.windll.psapi.GetProcessMemoryInfo(handle, ctypes.byref(memory), memory.cb): return None
        if not kernel.GetProcessTimes(handle, ctypes.byref(created), ctypes.byref(exited), ctypes.byref(system), ctypes.byref(user)): return None
        seconds = sum((value.dwHighDateTime << 32) + value.dwLowDateTime for value in (system, user)) / 10000000
        return dict(pid=pid, cpuSeconds=seconds, rssMiB=memory.rss / 1048576, privateMiB=memory.private / 1048576)
    finally: kernel.CloseHandle(handle)


def host_cpu():
    idle, system, user = (w.FILETIME() for _ in range(3))
    if not ctypes.windll.kernel32.GetSystemTimes(ctypes.byref(idle), ctypes.byref(system), ctypes.byref(user)):
        raise ctypes.WinError()
    values = [(value.dwHighDateTime << 32) + value.dwLowDateTime for value in (idle, system, user)]
    return values[0], values[1] + values[2]


def read_json_lines(path):
    result = []
    if path.exists():
        for line in path.read_text(encoding="utf-8-sig", errors="replace").splitlines():
            try: result.append(json.loads(line))
            except ValueError: pass
    return result


def run(args):
    root = args.output.resolve(); root.mkdir(parents=True, exist_ok=False)
    binary = args.binary.resolve()
    owned = []; handles = []; rooms = []
    baseline = dict(memory=host_memory(), logicalProcessors=os.cpu_count(), binary=str(binary),
                    mode=args.mode, rooms=args.rooms, playersPerRoom=args.players, seconds=args.seconds,
                    jobWorkers=args.job_workers, fillCom=args.com, scene=args.scene, syntheticDurableActors=not args.release,
                    spawnRegressionCheck=args.spawn_check,
                    frameBudgetScope="stable gameplay; transition windows retained in raw metrics",
                    entryGrowthMiBPerRoom=ENTRY_GROWTH_MIB)
    assembly = binary.parent / "SB6_Data" / "Managed" / "Assembly-CSharp.dll"
    baseline["assemblySha256"] = hashlib.sha256(assembly.read_bytes()).hexdigest()
    client_binary = args.client_binary.resolve() if args.client_binary else binary
    (root / "config.json").write_text(json.dumps(baseline, indent=2), encoding="utf-8")
    outcome = "complete"
    try:
        for index in range(args.rooms):
            before_boot = host_memory()
            if before_boot["availableMiB"] < args.reserve_mib + 256 or before_boot["availableCommitMiB"] < 1536:
                raise RuntimeError("insufficient RAM headroom to start another test server")
            room = root / f"room-{index + 1}"; room.mkdir(); rooms.append(room)
            env = dict(os.environ, SB6_ROOM_ID=room.name, SB6_ROOM_TOKEN=secrets.token_hex(32), SB6_ROOM_STATUS=str(room / "status.json"))
            command = [str(binary), "-batchmode", "-nographics", "-server", "-room-mode", args.mode,
                       "-port", str(args.port + index), "-server-idle-seconds", "0",
                       "-logFile", str(room / "unity.log")]
            if not args.release:
                command += ["-load-test-server", "-load-test-players", str(args.players),
                            "-load-test-duration", str(args.seconds + 240)]
            if args.job_workers: command += ["-job-worker-count", str(args.job_workers)]
            if args.com: command += ["-load-test-com"]
            if args.spawn_check: command += ["-load-test-spawns"]
            if args.scene: command += ["-load-test-scene", args.scene]
            handle = (room / "launcher.log").open("wb"); handles.append(handle)
            owned.append(subprocess.Popen(command, cwd=binary.parent, env=env, stdout=handle, stderr=subprocess.STDOUT,
                                          creationflags=subprocess.CREATE_NO_WINDOW))
            deadline = time.monotonic() + 90
            while time.monotonic() < deadline:
                if owned[-1].poll() is not None: raise RuntimeError("server exited during boot")
                try:
                    if json.loads((room / "status.json").read_text(encoding="utf-8-sig"))["available"]: break
                except (OSError, ValueError, KeyError): pass
                time.sleep(1)
            else: raise RuntimeError("server boot timed out")
            print(f"room {index+1}/{args.rooms} ready; available RAM {host_memory()['availableMiB']:.0f} MiB", flush=True)
            if host_memory()["availableMiB"] < args.reserve_mib: raise RuntimeError("memory reserve reached during boot")
        servers = list(owned)
        # Measured boot-to-gameplay growth was about 70-90 MiB per room, in
        # addition to the shared client process. Reserve that burst before entry.
        expected = args.rooms * args.players
        entry_headroom = args.reserve_mib + 300 + expected + ENTRY_GROWTH_MIB * args.rooms
        if host_memory()["availableMiB"] < entry_headroom:
            raise RuntimeError("insufficient RAM headroom for simultaneous gameplay entry")
        handle = (root / "client-launcher.log").open("wb"); handles.append(handle)
        client_command = [str(client_binary), "-batchmode", "-nographics", "-load-test-client", "-load-test-port", str(args.port),
                                   "-load-test-rooms", str(args.rooms), "-load-test-players", str(args.players),
                                   "-load-test-duration", str(args.seconds + 150), "-job-worker-count", "1",
                                   "-logFile", str(root / "clients.log")]
        if args.release: client_command += ["-load-test-configure"]
        client = subprocess.Popen(client_command, cwd=client_binary.parent,
                                  stdout=handle, stderr=subprocess.STDOUT, creationflags=subprocess.CREATE_NO_WINDOW)
        owned.append(client)
        deadline = time.monotonic() + 90
        startup_low = 0
        while time.monotonic() < deadline:
            if client.poll() is not None: raise RuntimeError("load generator exited")
            startup_memory = host_memory()
            startup_low = startup_low + 1 if startup_memory["availableMiB"] < args.reserve_mib or startup_memory["availableCommitMiB"] < 1024 else 0
            if startup_low >= 3: raise RuntimeError("memory reserve reached while spawning players")
            text = (root / "clients.log").read_text(encoding="utf-8-sig", errors="replace") if (root / "clients.log").exists() else ""
            if f"spawned={expected} " in text: break
            time.sleep(1)
        else: raise RuntimeError("not all synthetic peers spawned; inspect logs")
        print(f"all {expected} peers spawned; warming up 15 seconds", flush=True)
        warm_low = 0
        for _ in range(15):
            memory = host_memory()
            with (root / "warmup-memory.jsonl").open("a", encoding="utf-8") as warmup:
                warmup.write(json.dumps(dict(utc=time.time(), memory=memory))+"\n")
            warm_low = warm_low + 1 if memory["availableMiB"] < args.reserve_mib or memory["availableCommitMiB"] < 1024 else 0
            if warm_low >= 3: raise RuntimeError("memory reserve reached during gameplay warm-up")
            time.sleep(1)
        # Only complete ten-second Unity windows generated after warm-up enter the summary.
        warmup_metrics = [read_json_lines(room / "metrics.jsonl") for room in rooms]
        baseline["metricsVersion"] = min(rows[-1].get("metricsVersion", 1) if rows else 0 for rows in warmup_metrics)
        if baseline["metricsVersion"] < 2:
            baseline["frameBudgetScope"] = "legacy windows include scene/round transitions"
        (root / "config.json").write_text(json.dumps(baseline, indent=2), encoding="utf-8")
        initial = [len(rows) + 1 for rows in warmup_metrics]
        previous = {}; samples = []; start = time.monotonic(); last = start; low = 0; busy = 0
        previous_host = host_cpu()
        with (root / "host.jsonl").open("w", encoding="utf-8") as output:
            while time.monotonic() - start < args.seconds:
                now = time.monotonic(); elapsed = now - last; last = now
                sample = dict(seconds=round(now-start, 2), memory=host_memory(), servers=[], clients=None)
                current_host = host_cpu()
                idle_delta = current_host[0] - previous_host[0]; total_delta = current_host[1] - previous_host[1]
                sample["hostCpuPercent"] = (1 - idle_delta / total_delta) * 100 if total_delta else 0
                previous_host = current_host
                for proc in owned:
                    current = process_sample(proc.pid)
                    if proc.poll() is not None: raise RuntimeError(f"owned process exited: {proc.pid}")
                    if current:
                        current["cpuPercentOneCore"] = max(0, (current["cpuSeconds"] - previous.get(proc.pid, current["cpuSeconds"])) / max(.001, elapsed) * 100)
                        previous[proc.pid] = current["cpuSeconds"]
                        if proc in servers: sample["servers"].append(current)
                        else: sample["clients"] = current
                samples.append(sample); output.write(json.dumps(sample)+"\n"); output.flush()
                memory = sample["memory"]
                low = low + 1 if memory["availableMiB"] < args.reserve_mib or memory["availableCommitMiB"] < 1024 else 0
                if low >= 3: outcome = "memory reserve reached"; break
                busy = busy + 1 if sample["hostCpuPercent"] > 85 else 0
                if busy >= 3: outcome = "host CPU reserve reached"; break
                if int(now-start) % 20 < 2:
                    print(f"t={now-start:.0f}s server RSS={sum(s['rssMiB'] for s in sample['servers']):.0f} MiB free={memory['availableMiB']:.0f} MiB", flush=True)
                time.sleep(2)
        details = []
        peer_states = re.findall(r"\[LoadClients\] connected=(\d+)/(\d+) spawned=(\d+)",
                                (root / "clients.log").read_text(encoding="utf-8-sig", errors="replace"))
        final_peers = tuple(map(int, peer_states[-1])) if peer_states else (0, expected, 0)
        if final_peers[:2] != (expected, expected) or (not args.release and final_peers[2] != expected):
            raise RuntimeError("not all load peers remained connected (and spawned for durable-actor tests)")
        if args.flow_only:
            flow_rooms = []
            for room in rooms:
                events = read_json_lines(room / "events.jsonl")
                errors = [e["message"] for e in events if e.get("level") in ("Error", "Exception", "Assert")]
                warnings = [e["message"] for e in events if e.get("level") == "Warning"]
                flow_rooms.append(dict(room=room.name, scenes=sorted({r["scene"] for r in read_json_lines(room / "metrics.jsonl")}), errors=errors, warnings=warnings))
            result = dict(scope="functional release flow only; no capacity estimate", initialSpawnedPeers=expected,
                          finalConnectedPeers=final_peers[0], finalSpawnedPeers=final_peers[2], roomResults=flow_rooms,
                          passed=outcome == "complete" and not any(r["errors"] for r in flow_rooms))
            (root / "flow.json").write_text(json.dumps(result, indent=2), encoding="utf-8")
            print(json.dumps(result, indent=2), flush=True)
            if not result["passed"]: raise RuntimeError("release flow failed")
            return
        for room, offset in zip(rooms, initial):
            windows = read_json_lines(room / "metrics.jsonl")[offset:]
            windows = [row for row in windows if row["scene"].startswith(("Stage_", "BattleMode_")) and row["players"] == args.players]
            excluded_transitions = sum(row.get("includesTransition", False) for row in windows)
            windows = [row for row in windows if not row.get("includesTransition", False)]
            if not windows: raise RuntimeError("no gameplay measurement windows")
            errors = []
            for line in (room / "unity.log").read_text(encoding="utf-8-sig", errors="replace").splitlines():
                if any(key in line for key in ("Exception", "invalid Command", "Unknown Command", "no receiver", "Catalog asset unavailable")): errors.append(line)
            errors.extend(event["message"] for event in read_json_lines(room / "events.jsonl")
                          if event.get("level") in ("Error", "Exception", "Assert"))
            errors = list(dict.fromkeys(errors))
            details.append(dict(room=room.name, windows=len(windows), scenes=sorted({row["scene"] for row in windows}),
                                excludedTransitionWindows=excluded_transitions,
                                minFps=min(row["fps"] for row in windows), meanFps=statistics.mean(row["fps"] for row in windows),
                                maxP95Ms=max(row["p95FrameMs"] for row in windows), maxP99Ms=max(row["p99FrameMs"] for row in windows),
                                worstFrameMs=max(row["maxFrameMs"] for row in windows),
                                slowFramePercent=sum(row["framesOver33Ms"] for row in windows)/sum(row["frames"] for row in windows)*100,
                                meanCpuOneCore=statistics.mean(row["cpuPercentOneCore"] for row in windows),
                                meanGcKiBps=statistics.mean(row["gcAllocatedKiBPerSecond"] for row in windows),
                                meanOutgoingKiBps=statistics.mean(row["outgoingKiBPerSecond"] for row in windows),
                                markers={name: statistics.mean(row["averageMarkerMs"][i] for row in windows) for i,name in enumerate(windows[0]["markerNames"])}, errors=errors))
        result = dict(outcome=outcome, roomResults=details,
                      finalConnectedPeers=final_peers[0], finalSpawnedPeers=final_peers[2],
                      serverPeakRssMiB=max(sum(s["rssMiB"] for s in sample["servers"]) for sample in samples),
                      serverPeakPrivateMiB=max(sum(s["privateMiB"] for s in sample["servers"]) for sample in samples),
                      clientPeakRssMiB=max(sample["clients"]["rssMiB"] for sample in samples if sample["clients"]),
                      meanHostCpuPercent=statistics.mean(sample["hostCpuPercent"] for sample in samples),
                      minimumAvailableMiB=min(sample["memory"]["availableMiB"] for sample in samples))
        (root / "summary.json").write_text(json.dumps(result,indent=2),encoding="utf-8")
        print(json.dumps(result, indent=2),flush=True)
    except BaseException as error:
        (root / "failure.json").write_text(json.dumps(dict(error=str(error), memory=host_memory()),indent=2),encoding="utf-8")
        raise
    finally:
        for proc in reversed(owned):
            if proc.poll() is None: proc.terminate()
        for proc in owned:
            try: proc.wait(timeout=10)
            except subprocess.TimeoutExpired: proc.kill(); proc.wait()
        for handle in handles: handle.close()


def main(require_windows=True):
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--binary",type=Path,default=Path("Build/ServerBenchmark/Windows/SB6.exe"))
    parser.add_argument("--client-binary",type=Path,help="Separate development load generator for a release server")
    parser.add_argument("--release",action="store_true",help="Coop release server with normal rules; owner sends normal configure request")
    parser.add_argument("--flow-only",action="store_true",help="Release connectivity and scene flow; no capacity claim")
    parser.add_argument("--output",type=Path,required=True)
    parser.add_argument("--mode",choices=("BattleMode","NormalGame","BossRush"),default="BossRush")
    parser.add_argument("--rooms",type=int,default=1); parser.add_argument("--players",type=int,default=4)
    parser.add_argument("--seconds",type=int,default=60); parser.add_argument("--port",type=int,default=18777)
    parser.add_argument("--reserve-mib",type=int,default=384); parser.add_argument("--job-workers",type=int,default=0)
    parser.add_argument("--com",action="store_true",help="Fill unconnected Battle Mode slots with native COM")
    parser.add_argument("--spawn-check",action="store_true",help="Development-only reveal/remove regression for LineBomb/Mole/Tank")
    parser.add_argument("--scene",help="Development-only enabled stage override for cooperative load tests")
    args=parser.parse_args()
    if require_windows and os.name != "nt": parser.error("Windows host sampler required")
    if args.com and args.mode != "BattleMode": parser.error("COM only exists in Battle Mode")
    if args.spawn_check and (args.release or args.mode != "BattleMode"): parser.error("spawn check requires development Battle Mode")
    if args.scene and args.mode == "BattleMode": parser.error("stage override is cooperative only")
    if args.release and (args.mode == "BattleMode" or args.scene or args.com or not args.client_binary):
        parser.error("release tests require cooperative mode, no overrides, and --client-binary")
    if not (1 <= args.rooms <= 32 and 2 <= args.players <= (6 if args.mode=="BattleMode" else 4) and 20 <= args.seconds <= 1200): parser.error("invalid load bounds")
    if args.flow_only and not args.release: parser.error("flow-only requires --release")
    run(args)


if __name__ == "__main__":
    main()

