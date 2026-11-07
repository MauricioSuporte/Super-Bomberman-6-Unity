#!/usr/bin/env python3
"""Restricted MCP server (stdio only) for the SB6 R01 assisted validation.

Exposes FIXED operations only: start a validation step as a background job, poll a job,
cancel a job this server started, list and read result files. There is no generic shell,
no network listener, no publishing, no Packages changes and no killing of foreign processes.
Pure standard library; speaks newline-delimited JSON-RPC 2.0 on stdin/stdout.
stdout carries ONLY protocol messages; diagnostics go to Logs/r01-mcp/server.log and stderr.
"""
import argparse, hashlib, json, os, re, subprocess, sys, threading, time, traceback, uuid
from pathlib import Path

VERSION = "1.0.0"
REPO = Path(r"C:\Users\mauri\source\repos\Super-Bomberman-6-Unity")
SCRIPT_REL = "Logs/R01-validate-assisted.py"
STEPS = ("preflight", "tests", "build", "smoke", "analyze")
CASES = ("locomotion", "locomotion-rendered", "mount-visuals", "regression-sudden", "mount-cycles")
FOLDER_RE = re.compile(r"^R01-assisted-\d{8}-\d{6}$")
JOB_RE = re.compile(r"^job-\d{8}-\d{6}-[0-9a-f]{6}$")
READ_EXT = {".json", ".txt", ".log", ".jsonl", ".md", ".ps1", ".csv"}
MAX_READ = 200_000
MAX_TAIL = 400

_lock = threading.Lock()
_children = {}  # job_id -> Popen started by THIS server process
_winjobs = {}   # job_id -> Windows Job Object handle (Windows only)
RUNNER = r"C:\Users\mauri\.sb6-actions-runner\_work\Super-Bomberman-6-Unity\Super-Bomberman-6-Unity"


def win_job_attach(child):
    """Put the child in a Job Object so cancel can end EVERY descendant, even re-parented ones.
    No kill-on-close: restarting the app must not silently kill a running build. Returns handle or None."""
    if os.name != "nt":
        return None
    try:
        import ctypes
        k = ctypes.WinDLL("kernel32", use_last_error=True)
        k.CreateJobObjectW.argtypes = [ctypes.c_void_p, ctypes.c_wchar_p]; k.CreateJobObjectW.restype = ctypes.c_void_p
        k.AssignProcessToJobObject.argtypes = [ctypes.c_void_p, ctypes.c_void_p]; k.AssignProcessToJobObject.restype = ctypes.c_int
        h = k.CreateJobObjectW(None, None)
        if h and k.AssignProcessToJobObject(h, int(child._handle)):
            return h
        log(f"job object attach failed (error {ctypes.get_last_error()}); falling back to taskkill /T")
    except Exception as e:
        log(f"job object unavailable: {e}")
    return None


def win_job_terminate(h):
    import ctypes
    k = ctypes.WinDLL("kernel32", use_last_error=True)
    k.TerminateJobObject.argtypes = [ctypes.c_void_p, ctypes.c_uint]; k.TerminateJobObject.restype = ctypes.c_int
    return bool(k.TerminateJobObject(h, 1))


def windows_tree(root_pid):
    """pids of root_pid's descendants right now (parent-id chain), for verification after cancel."""
    out = subprocess.run(["powershell", "-NoProfile", "-NonInteractive", "-Command",
                          "Get-CimInstance Win32_Process | Select-Object ProcessId,ParentProcessId | ConvertTo-Json -Compress"],
                         capture_output=True, text=True, timeout=60).stdout
    rows = json.loads(out)
    rows = rows if isinstance(rows, list) else [rows]
    kids, seen, todo = {}, set(), [root_pid]
    for r in rows:
        kids.setdefault(r["ParentProcessId"], []).append(r["ProcessId"])
    while todo:
        for c in kids.get(todo.pop(), []):
            if c not in seen:
                seen.add(c); todo.append(c)
    return sorted(seen)


def runner_leftovers():
    """Unity/SB6 processes using the runner project. REPORTED only - this server never kills them."""
    out = subprocess.run(["powershell", "-NoProfile", "-NonInteractive", "-Command",
                          "Get-CimInstance Win32_Process | Where-Object { $_.Name -in 'Unity.exe','SB6.exe' } | "
                          "Select-Object Name,ProcessId,CommandLine,ExecutablePath | ConvertTo-Json -Compress"],
                         capture_output=True, text=True, timeout=60).stdout.strip()
    if not out:
        return []
    rows = json.loads(out)
    rows = rows if isinstance(rows, list) else [rows]
    key = RUNNER.replace("\\", "/").lower()
    return [f"{r['Name']} pid {r['ProcessId']}" for r in rows
            if key in ((r.get("CommandLine") or "") + " " + (r.get("ExecutablePath") or "")).replace("\\", "/").lower()]


def paths():
    base = REPO / "Logs" / "r01-mcp"
    return base, base / "jobs", base / "active.lock", base / "server.log"


def log(msg):
    try:
        base, jobs, _, logf = paths()
        jobs.mkdir(parents=True, exist_ok=True)
        with open(logf, "a", encoding="utf-8") as f:
            f.write(time.strftime("%Y-%m-%dT%H:%M:%S ") + msg + "\n")
    except OSError:
        pass
    print(msg, file=sys.stderr, flush=True)


def pid_alive(pid):
    if not pid:
        return False
    if os.name == "nt":
        out = subprocess.run(["tasklist", "/FI", f"PID eq {pid}", "/FO", "CSV", "/NH"], capture_output=True, text=True).stdout
        return f'"{pid}"' in out
    try:
        os.kill(pid, 0)
        return True
    except OSError:
        return False


def sha256(p):
    return hashlib.sha256(Path(p).read_bytes()).hexdigest()


def job_file(jid):
    return paths()[1] / f"{jid}.json"


def load_job(jid):
    if not JOB_RE.match(jid or ""):
        raise ValueError("invalid job id")
    p = job_file(jid)
    if not p.is_file():
        raise ValueError("unknown job id")
    return json.loads(p.read_text(encoding="utf-8"))


def save_job(job):
    job_file(job["id"]).write_text(json.dumps(job, indent=2), encoding="utf-8")


def refresh(job):
    """Update a job record from reality (child exit code); marks orphans honestly."""
    if job["state"] != "running":
        return job
    child = _children.get(job["id"])
    if child is not None:
        rc = child.poll()
        if rc is None:
            return job
        job.update(state="finished", exitCode=rc, finishedUtc=utc())
    elif not pid_alive(job.get("pid")):
        job.update(state="lost", exitCode=None, finishedUtc=utc(),
                   note="server restarted or child vanished; exit code unknown - inspect the log and result.json")
    else:
        return job
    save_job(job)
    release_lock(job["id"])
    return job


def utc():
    return time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())


def release_lock(jid):
    lock = paths()[2]
    try:
        if json.loads(lock.read_text(encoding="utf-8")).get("job") == jid:
            lock.unlink()
    except (OSError, ValueError):
        pass


def active_job():
    """Return the running job record if the lock is held by a live job, clearing stale locks."""
    lock = paths()[2]
    if not lock.is_file():
        return None
    try:
        held = json.loads(lock.read_text(encoding="utf-8"))
        job = refresh(load_job(held["job"]))
    except (OSError, ValueError, KeyError):
        lock.unlink(missing_ok=True)
        return None
    if job["state"] == "running":
        return job
    lock.unlink(missing_ok=True)
    return None


def resolve_results(name):
    """Map 'latest' or a folder NAME (never a path) to a directory under Logs/."""
    logs = REPO / "Logs"
    if name in (None, "", "latest"):
        ptr = logs / "R01-latest.txt"
        if not ptr.is_file():
            raise ValueError("no R01-latest.txt yet")
        name = Path(ptr.read_text(encoding="utf-8").strip().replace("\\", "/")).name
    if not FOLDER_RE.match(name):
        raise ValueError("folder must be named R01-assisted-YYYYMMDD-HHMMSS (or 'latest')")
    d = logs / name
    if not d.is_dir():
        raise ValueError("results folder not found")
    return d


def safe_inside(base, rel):
    rel = rel.replace("\\", "/")
    if rel.startswith("/") or ":" in rel or any(part in ("..", "") for part in rel.split("/")):
        raise ValueError("path must be relative, without '..'")
    target = (base / rel).resolve()
    if base.resolve() not in target.parents and target != base.resolve():
        raise ValueError("path escapes the results folder")
    return target


# ---------------------------------------------------------------- tools
def t_status(a):
    script = REPO / SCRIPT_REL
    job = active_job()
    ptr = REPO / "Logs" / "R01-latest.txt"
    return dict(server=VERSION, repo=str(REPO), script=SCRIPT_REL, scriptExists=script.is_file(),
                scriptSha256=sha256(script) if script.is_file() else None, activeJob=job and job["id"],
                activeStep=job and job["step"], latestResults=ptr.read_text(encoding="utf-8").strip() if ptr.is_file() else None)


def t_start(a):
    step = a.get("step")
    if step not in STEPS:
        raise ValueError(f"step must be one of {STEPS}")
    cases = a.get("cases") or []
    if not isinstance(cases, list) or any(c not in CASES for c in cases):
        raise ValueError(f"cases must be a subset of {CASES}")
    script = REPO / SCRIPT_REL
    if not script.is_file():
        raise ValueError("orchestrator script not found")
    argv = [sys.executable, "-X", "utf8", "-u", str(script), "--step", step]
    if step == "analyze":
        argv += ["--results", str(resolve_results(a.get("results")))]
    else:
        if a.get("acceptRunnerLocalDiffs") is True:
            argv += ["--accept-runner-local-diffs"]      # explicit, per call; default false; Packages diffs are never acceptable here
        if step in ("tests", "build", "smoke") and a.get("resume", True):
            resolve_results("latest")          # must exist, else tell the caller to run preflight first
            argv += ["--resume", "latest"]
        if step == "smoke" and cases:
            argv += ["--cases", ",".join(cases)]
    with _lock:
        busy = active_job()
        if busy:
            raise ValueError(f"another job is running: {busy['id']} ({busy['step']}); poll it or cancel it")
        base, jobs, lock, _ = paths()
        jobs.mkdir(parents=True, exist_ok=True)
        jid = f"job-{time.strftime('%Y%m%d-%H%M%S')}-{uuid.uuid4().hex[:6]}"
        logp = jobs / f"{jid}.log"
        try:
            fd = os.open(lock, os.O_CREAT | os.O_EXCL | os.O_WRONLY)   # atomic: refuses concurrent starts
        except FileExistsError:
            raise ValueError("lock is held; another job is starting")
        with os.fdopen(fd, "w", encoding="utf-8") as f:
            json.dump(dict(job=jid, server_pid=os.getpid()), f)
        env = dict(os.environ, PYTHONUTF8="1", PYTHONUNBUFFERED="1")
        flags = subprocess.CREATE_NEW_PROCESS_GROUP if os.name == "nt" else 0
        extra = {} if os.name == "nt" else {"start_new_session": True}   # POSIX: own process group for clean cancel
        try:
            out = open(logp, "wb")
            child = subprocess.Popen(argv, cwd=str(REPO), env=env, stdout=out, stderr=subprocess.STDOUT,
                                     stdin=subprocess.DEVNULL, creationflags=flags, **extra)
        except Exception:
            lock.unlink(missing_ok=True)
            raise
        _children[jid] = child
        wj = win_job_attach(child)
        if wj:
            _winjobs[jid] = wj
        job = dict(id=jid, step=step, argv=argv[1:], state="running", pid=child.pid, startedUtc=utc(), log=str(logp),
                   scriptSha256=sha256(script), exitCode=None)
        save_job(job)
    log(f"start {jid} step={step} argv={argv[1:]} pid={child.pid}")
    return dict(jobId=jid, state="running", note="poll with r01_job; exit codes: 0 ok, 1 run failed, 2 measurement failures, 3 blocked, 4 inconclusive")


def t_job(a):
    job = refresh(load_job(a.get("jobId")))
    n = max(1, min(int(a.get("tail", 40)), MAX_TAIL))
    lines = Path(job["log"]).read_text(encoding="utf-8", errors="replace").splitlines() if Path(job["log"]).is_file() else []
    return dict(job=job, logLines=len(lines), tail=lines[-n:])


def t_cancel(a):
    job = refresh(load_job(a.get("jobId")))
    child = _children.get(job["id"])
    if job["state"] != "running":
        return dict(jobId=job["id"], state=job["state"], note="not running")
    if child is None:
        raise ValueError("this job was not started by the current server process; refusing to kill it")
    # ONLY the process tree this server started (the orchestrator and everything it launched)
    if os.name == "nt":
        tree = windows_tree(child.pid)
        method = "job-object"
        wj = _winjobs.get(job["id"])
        if not (wj and win_job_terminate(wj)):
            method = "taskkill /T"
            subprocess.run(["taskkill", "/PID", str(child.pid), "/T", "/F"], capture_output=True)
        pids = [child.pid] + tree
    else:
        import signal
        method, pids = "process-group", [child.pid]
        for sig in (signal.SIGTERM, signal.SIGKILL):
            try:
                os.killpg(child.pid, sig)
            except ProcessLookupError:
                break
            time.sleep(1.0)
    try:
        child.wait(timeout=20)
    except subprocess.TimeoutExpired:
        pass
    time.sleep(1.0)
    if os.name == "nt":
        survivors = [p for p in pids if pid_alive(p)]
        leftovers = runner_leftovers()
    else:
        try:
            os.killpg(child.pid, 0); survivors = ["process group still has members"]
        except ProcessLookupError:
            survivors = []
        leftovers = []
    job["cancelRequestedUtc"] = utc()
    job["cancelSurvivors"] = survivors
    save_job(job)
    log(f"cancel {job['id']} pid={child.pid} via {method} survivors={survivors} runnerLeftovers={leftovers}")
    return dict(jobId=job["id"], method=method, descendantsSeenBeforeCancel=len(pids) - 1, survivorsOfThisJob=survivors,
                runnerProcessesStillRunning=leftovers,
                note="Only this job's own tree was ended. runnerProcessesStillRunning are REPORTED, never killed (they may be foreign).")


def t_results(a):
    base = resolve_results(a.get("folder"))
    items = []
    for p in sorted(base.rglob("*")):
        if p.is_file() and len(items) < 500:
            items.append(dict(path=p.relative_to(base).as_posix(), bytes=p.stat().st_size))
    return dict(folder=base.name, files=items)


def t_read(a):
    base = resolve_results(a.get("folder"))
    target = safe_inside(base, a.get("file") or "result.json")
    if target.suffix.lower() not in READ_EXT:
        raise ValueError(f"only {sorted(READ_EXT)} files can be read; images stay on disk")
    if not target.is_file():
        raise ValueError("file not found")
    data = target.read_bytes()
    off = max(0, int(a.get("offset", 0)))
    chunk = data[off:off + MAX_READ]
    return dict(file=target.relative_to(base).as_posix(), totalBytes=len(data), offset=off,
                truncated=off + len(chunk) < len(data), text=chunk.decode("utf-8", errors="replace"))


def schema(props, req=()):
    return {"type": "object", "properties": props, "required": list(req), "additionalProperties": False}


TOOLS = {
    "r01_status": (t_status, "Server version, orchestrator script hash, active job, latest results folder.", schema({}), True),
    "r01_start": (t_start, "Start ONE R01 step as a background job and return its jobId. Steps: preflight (also compares sources, read-only), "
                  "tests, build, smoke (optional cases), analyze (needs results = smoke folder name). tests/build/smoke continue the latest "
                  "results folder. Refused if another job runs. Never accepts Packages differences.",
                  schema({"step": {"type": "string", "enum": list(STEPS)},
                          "cases": {"type": "array", "items": {"type": "string", "enum": list(CASES)}},
                          "results": {"type": "string", "description": "analyze only: folder name or 'latest'"},
                          "resume": {"type": "boolean", "description": "default true for tests/build/smoke"},
                          "acceptRunnerLocalDiffs": {"type": "boolean", "description": "default false. Only when the user explicitly decided that files the runner modified itself cannot affect the measurement."}}, ["step"]), False),
    "r01_job": (t_job, "State, exit code and last log lines of a job.", schema({"jobId": {"type": "string"}, "tail": {"type": "integer"}}, ["jobId"]), True),
    "r01_cancel": (t_cancel, "Terminate a job THIS server process started (its own process tree only).", schema({"jobId": {"type": "string"}}, ["jobId"]), False),
    "r01_results": (t_results, "List files of a results folder ('latest' or R01-assisted-YYYYMMDD-HHMMSS).", schema({"folder": {"type": "string"}}), True),
    "r01_read": (t_read, "Read a text/json/log file inside a results folder (relative path, <=200 KB per call, use offset).",
                 schema({"folder": {"type": "string"}, "file": {"type": "string"}, "offset": {"type": "integer"}}), True),
}


def handle(msg):
    method, mid = msg.get("method"), msg.get("id")
    if mid is None:        # notification
        return None
    def ok(result): return {"jsonrpc": "2.0", "id": mid, "result": result}
    def err(code, text): return {"jsonrpc": "2.0", "id": mid, "error": {"code": code, "message": text}}
    if method == "initialize":
        want = (msg.get("params") or {}).get("protocolVersion") or "2025-06-18"
        return ok({"protocolVersion": want, "capabilities": {"tools": {}}, "serverInfo": {"name": "sb6-r01", "version": VERSION}})
    if method == "ping":
        return ok({})
    if method == "tools/list":
        return ok({"tools": [{"name": n, "description": d, "inputSchema": s, "annotations": {"readOnlyHint": ro}}
                             for n, (_, d, s, ro) in TOOLS.items()]})
    if method == "tools/call":
        p = msg.get("params") or {}
        name, args = p.get("name"), p.get("arguments") or {}
        if name not in TOOLS:
            return err(-32602, "unknown tool")
        try:
            log(f"call {name} {json.dumps(args)[:300]}")
            res = TOOLS[name][0](args)
            return ok({"content": [{"type": "text", "text": json.dumps(res, indent=1)}], "isError": False})
        except Exception as e:
            if not isinstance(e, ValueError):
                log("ERROR " + traceback.format_exc())
            return ok({"content": [{"type": "text", "text": f"error: {e}"}], "isError": True})
    return err(-32601, "method not found")


def main():
    global REPO
    ap = argparse.ArgumentParser()
    ap.add_argument("--repo", help="testing only")
    a = ap.parse_args()
    if a.repo:
        REPO = Path(a.repo)
    log(f"server {VERSION} start pid={os.getpid()} repo={REPO} python={sys.executable}")
    stdin = sys.stdin.buffer
    for raw in iter(stdin.readline, b""):
        raw = raw.strip()
        if not raw:
            continue
        try:
            resp = handle(json.loads(raw.decode("utf-8")))
        except json.JSONDecodeError:
            resp = {"jsonrpc": "2.0", "id": None, "error": {"code": -32700, "message": "parse error"}}
        if resp is not None:
            sys.stdout.buffer.write((json.dumps(resp) + "\n").encode("utf-8"))
            sys.stdout.buffer.flush()


if __name__ == "__main__":
    main()
