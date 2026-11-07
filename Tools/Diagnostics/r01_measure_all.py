"""Sequential R01 measurement session: cost (baseline / R01 / R01 switches off, interleaved per repetition) and the
online x offline comparison (R01 build), then both reports. One run at a time; no builds may run meanwhile."""
import argparse
import json
from pathlib import Path
import subprocess
import sys
import time

sys.path.insert(0, str(Path(__file__).resolve().parent))
import r01_compare  # noqa: E402
import r01_compare_run  # noqa: E402

MAX_ATTEMPTS = 6  # per comparison mode (validity amendment in r01_compare.py)

HERE = Path(__file__).resolve().parent


def busy():
    out = subprocess.run(["powershell", "-NoProfile", "-Command",
                          "Get-CimInstance Win32_Process | Where-Object { $_.Name -eq 'Runner.Worker.exe' -or ($_.Name -eq 'Unity.exe' -and "
                          "$_.CommandLine -like '*sb6-actions-runner*') } | ForEach-Object { $_.Name + ' ' + $_.ProcessId }"],
                         capture_output=True, text=True).stdout.strip()
    return out


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--baseline", type=Path, required=True)
    p.add_argument("--r01", type=Path, required=True)
    p.add_argument("--output", type=Path, required=True)
    p.add_argument("--repetitions", type=int, default=3)
    p.add_argument("--only", choices=("all", "cost", "compare"), default="all")
    p.add_argument("--frame-stats", action="store_true", help="cost runs: per-second frame work/allocation rows in every process")
    p.add_argument("--require-capture-off", action="store_true", help="cost report: a run is valid only if every process reports the motion capture off")
    a = p.parse_args()
    a.output.mkdir(parents=True, exist_ok=True)
    port = 29110
    session = {"started": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()), "runs": []}
    plan = []
    if a.only in ("all", "cost"):
        for rep in range(1, a.repetitions + 1):
            plan += [("cost", f"baseline-{rep}", a.baseline, "online", False, ""),
                     ("cost", f"r01-{rep}", a.r01, "online", False, ""),
                     ("cost", f"r01off-{rep}", a.r01, "online", False, "-online-no-pose-enforce -online-no-mount-gate")]
    valid = {"offline": [], "online": []}
    attempts = {"offline": 0, "online": 0}

    def compare_plan():
        # Alternate offline/online attempts until each mode has the required valid runs or hits MAX_ATTEMPTS.
        while True:
            pending = [m for m in ("offline", "online") if len(valid[m]) < a.repetitions and attempts[m] < MAX_ATTEMPTS]
            if not pending:
                return
            for mode in pending:
                attempts[mode] += 1
                yield ("compare", f"{mode}-{attempts[mode]}", a.r01, mode, True, "")

    def input_valid(mode, folder):
        try:
            if mode == "offline":
                rows = r01_compare.read_rows(folder / "offline-compare.jsonl")
                return r01_compare.window(rows) is not None and not r01_compare.check_input(rows, "offline")[0]
            return not r01_compare.owner_input_problems(folder)
        except (OSError, ValueError, KeyError):
            return False

    import itertools
    if a.only in ("all", "compare"):
        plan = itertools.chain(plan, compare_plan())
    for group, name, build, mode, capture, extra in plan:
        other = busy()
        if other:  # an Actions job or a Unity build on the runner would share the CPU with the measured processes
            raise SystemExit(f"stopping before {name}: {other} is running (nothing of ours was left running)")
        port += 1
        folder = a.output / group / name
        print(f"[{time.strftime('%H:%M:%S')}] {name} ({mode}, port {port})", flush=True)
        try:
            config = r01_compare_run.run(build / "SB6.exe", folder, mode, port, 75, capture, extra.split(),
                                         frame_stats=a.frame_stats and group == "cost")
            session["runs"].append({"name": name, "folder": str(folder), "exits": config.get("exits"),
                                    "window": config.get("scriptWindowWallClock")})
        except Exception as error:  # recorded, never dropped silently
            session["runs"].append({"name": name, "folder": str(folder), "error": repr(error)})
            print("  error:", repr(error), flush=True)
        if group == "compare":
            ok = input_valid(mode, folder)
            session["runs"][-1]["inputEquivalent"] = ok
            if ok:
                valid[mode].append(folder)
            print(f"  input equivalent: {ok}", flush=True)
        (a.output / "session.json").write_text(json.dumps(session, indent=2), encoding="utf-8")
        time.sleep(5)
    py = sys.executable
    if a.only in ("all", "cost"):
        cost = a.output / "cost"
        reps = range(1, a.repetitions + 1)
        subprocess.run([py, "-X", "utf8", str(HERE / "r01_cost_report.py"),
                        "--baseline", *[str(cost / f"baseline-{r}") for r in reps],
                        "--r01", *[str(cost / f"r01-{r}") for r in reps],
                        "--r01-switches-off", *[str(cost / f"r01off-{r}") for r in reps],
                        "--output", str(a.output / "R01-custo.json")] + (["--require-capture-off"] if a.require_capture_off else []))
    if a.only in ("all", "compare"):
        cmp_ = a.output / "compare"  # every attempt goes to the analyzer, which lists the excluded ones
        subprocess.run([py, "-X", "utf8", str(HERE / "r01_compare.py"),
                        "--offline", *[str(cmp_ / f"offline-{r}") for r in range(1, attempts["offline"] + 1)],
                        "--online", *[str(cmp_ / f"online-{r}") for r in range(1, attempts["online"] + 1)],
                        "--output", str(a.output / "R01-comparacao.json")])
    session["finished"] = time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())
    (a.output / "session.json").write_text(json.dumps(session, indent=2), encoding="utf-8")


if __name__ == "__main__":
    main()
