import json
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "Diagnostics"))
import r01_compare as rc  # noqa: E402

ACTIONS = {"MoveRight": (1, 0, "R"), "MoveLeft": (-1, 0, "L"), "MoveUp": (0, 1, "U"), "MoveDown": (0, -1, "D")}


def log(kind, u, detail):
    return {"k": kind, "f": 0, "t": u / 1000, "u": u, "d": detail}


def setup_row(role, player, u, **overrides):
    values = dict(role=role, player=player, scene="BattleMode_15", players=2, spawnX=-7, spawnY=5, speed=5, bombs=1, radius=2,
                  suddenDeath="Off", destructibles="40:abc", fps=60, vsync=0) | overrides
    return log("setup", u, ";".join(f"{k}={v}" for k, v in values.items()))


def capture(role_p1, role_p2, delay_ms=0.0, scripted=True, speed=1.0, renderer_override=None, p2_moves=False, p2_state=None):
    """60 fps synthetic capture of the 12-segment script; P1 walks 2.4 u/segment in the scripted direction."""
    rows, frame_ms = [], 1000 / 60
    if role_p1 not in ("server", "observer"):
        rows.append(setup_row(role_p1, 1, 0))
    for role in {role_p1, role_p2} - {"server", "observer"} - {role_p1}:
        rows.append(setup_row(role, 2, 0))
    if role_p1 in ("server", "observer"):
        rows.append(setup_row(role_p1, 1, 0, **({"fps": 30} if role_p1 == "server" else {})))
    if scripted:
        rows.append(log("script_start", 1000, "player=1"))
    x, y, facing = -7.0, 5.0, "Body_Down"
    segments = list(rc.EXPECTED_ACTIONS)
    total = 1000 + 3000 * len(segments) + 3000
    u, active, done = 1000.0, -1, False
    while u <= total + 600:
        st = (u - 1000) / 1000
        index = int(st // 3)
        holding = index < len(segments) and st - index * 3 < rc.HOLD_SECONDS
        current = index if holding else -1
        if scripted and current != active:  # same edge rule as the harness: log on the frame the state changes
            if active >= 0:
                rows.append(log("segment_end", u, f"index={active};st={st:.4f}"))
            if current >= 0:
                rows.append(log("segment_start", u, f"index={current};action={segments[current]};st={st:.4f}"))
            active = current
        if scripted and not done and index >= len(segments):
            done = True
            rows.append(log("script_done", u, f"st={st:.4f}"))
        presented = u + delay_ms
        if holding:
            dx, dy, _ = ACTIONS[segments[index]]
            x += dx * speed * frame_ms / 1000
            y += dy * speed * frame_ms / 1000
            facing = "Body_" + segments[index].removeprefix("Move")
        rows.append({"k": "s", "role": role_p1, "u": presented, "p": 1, "x": x, "y": y, "m": False, "dead": False,
                     "r": renderer_override(st, facing) if renderer_override else facing})
        rows.append({"k": "s", "role": role_p2, "u": presented, "p": 2, "x": 7.0 + (0.5 if p2_moves and st > 5 else 0), "y": -5.0,
                     "m": False, "dead": False, "r": p2_state(role_p2, st) if p2_state else "Body_Down"})
        u += frame_ms
    return rows


def write(folder, name, rows):
    folder.mkdir(parents=True, exist_ok=True)
    (folder / name).write_text("\n".join(json.dumps(r) for r in rows) + "\n", encoding="utf-8")


class R01CompareTests(unittest.TestCase):
    def make(self, root, offline_runs=2, server_p2=None, observer_p2=None, **online):
        offline, remote = [], []
        for i in range(offline_runs):
            folder = root / f"offline{i}"
            write(folder, "offline-compare.jsonl", capture("offline", "offline"))
            offline.append(folder)
        for i in range(offline_runs):
            folder = root / f"online{i}"
            write(folder, "server-compare.jsonl", capture("server", "server", delay_ms=40, scripted=False, p2_state=server_p2))
            client1 = capture("owner", "observer", p2_state=observer_p2, **online)
            write(folder, "client-1-compare.jsonl", client1)
            write(folder, "client-2-compare.jsonl", capture("observer", "owner", delay_ms=90, scripted=False, p2_state=server_p2))
            remote.append(folder)
        return rc.report(offline, remote, required=offline_runs if offline_runs >= 2 else 2)

    def test_equivalent_runs_are_demonstrated_and_report_remote_delay(self):
        with tempfile.TemporaryDirectory() as tmp:
            result = self.make(Path(tmp))
        self.assertEqual("demonstrated", result["verdict"], json.dumps(result["online"], indent=1)[:2000])
        observer = result["online"][0]["roles"]["observer"]
        self.assertEqual(12, observer["runs"])
        self.assertAlmostEqual(90, observer["delayBehindOwnerMs"]["median"], delta=1)

    def test_a_double_pose_never_seen_offline_fails(self):
        double = lambda st, facing: facing + ",Body_Up" if 10 < st < 10.1 else facing
        with tempfile.TemporaryDirectory() as tmp:
            result = self.make(Path(tmp), renderer_override=double)
        self.assertEqual("failed", result["verdict"])
        problems = result["online"][0]["roles"]["owner"]["problems"]
        self.assertTrue(any("double pose" in p for p in problems), problems)

    def test_slower_owner_motion_fails_displacement(self):
        with tempfile.TemporaryDirectory() as tmp:
            result = self.make(Path(tmp), speed=0.85)
        self.assertEqual("failed", result["verdict"])
        self.assertTrue(any(p.startswith("D displacement") for p in result["online"][0]["roles"]["owner"]["problems"]))

    def test_idle_player_two_moving_fails(self):
        with tempfile.TemporaryDirectory() as tmp:
            result = self.make(Path(tmp), p2_moves=True)
        self.assertTrue(any("idle P2 moved" in p for p in result["online"][0]["roles"]["observer"]["problems"]))

    def test_single_offline_run_cannot_demonstrate(self):
        with tempfile.TemporaryDirectory() as tmp:
            result = self.make(Path(tmp), offline_runs=1)
        self.assertTrue(result["verdict"].startswith("inconclusive"))

    def test_a_run_with_a_stretched_hold_is_excluded_not_paired(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            result = self.make(root)
            folder = root / "offline-bad"
            rows = capture("offline", "offline")
            end = next(r for r in rows if r["k"] == "segment_end" and r["d"].startswith("index=11;"))
            end["d"] = "index=11;st=35.5275"
            write(folder, "offline-compare.jsonl", rows)
            again = rc.report([folder, root / "offline0", root / "offline1"], [root / "online0", root / "online1"], required=2)
        self.assertEqual("demonstrated", result["verdict"])
        self.assertEqual("demonstrated", again["verdict"])
        self.assertEqual([str(folder)], [e["folder"] for e in again["excludedInputNotEquivalent"]])

    def test_too_few_valid_runs_is_inconclusive(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            self.make(root)
            result = rc.report([root / "offline0", root / "offline1"], [root / "online0"], required=2)
        self.assertTrue(result["verdict"].startswith("inconclusive: fewer"))

    def test_random_afk_variant_is_accepted_only_when_all_online_roles_agree(self):
        afk2 = lambda role, st: "Afk2" if st > 5 else "Body_Down"
        with tempfile.TemporaryDirectory() as tmp:
            agreed = self.make(Path(tmp), server_p2=afk2, observer_p2=afk2)
        self.assertEqual("demonstrated", agreed["verdict"])
        with tempfile.TemporaryDirectory() as tmp:
            split = self.make(Path(tmp), server_p2=afk2)
        problems = split["online"][0]["roles"]["observer"]["problems"]
        self.assertTrue(any("differs from the server" in p for p in problems), problems)

    def test_a_player_missing_at_the_window_start_fails(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            self.make(root)
            rows = rc.read_rows(root / "online0" / "server-compare.jsonl")
            rows = [r for r in rows if not (r.get("k") == "s" and r["p"] == 2 and r["u"] < 3000)]
            write(root / "online0", "server-compare.jsonl", rows)
            result = rc.report([root / "offline0", root / "offline1"], [root / "online0", root / "online1"], required=2)
        self.assertTrue(any("P2 not sampled" in p for p in result["online"][0]["roles"]["server"]["problems"]))

    def test_setup_mismatch_fails(self):
        a = {"scene": "BattleMode_15", "players": "2", "speed": "5", "bombs": "1", "radius": "2", "suddenDeath": "Off",
             "destructibles": "40:abc", "spawnX": "-7", "spawnY": "5", "fps": "60", "vsync": "0"}
        self.assertEqual([], rc.check_setup(a, dict(a), True))
        self.assertTrue(rc.check_setup(a, dict(a, bombs="3"), True))
        self.assertTrue(rc.check_setup(a, dict(a, spawnX="-6.98"), True))
        self.assertEqual([], rc.check_setup(a, dict(a, fps="30"), False))

    def test_gaps_up_to_the_merge_limit_keep_one_run(self):
        track = [{"u": 0, "x": 0, "y": 0}, {"u": 16, "x": 0.1, "y": 0}, {"u": 100, "x": 0.1, "y": 0}, {"u": 116, "x": 0.2, "y": 0},
                 {"u": 400, "x": 0.2, "y": 0}, {"u": 416, "x": 0.3, "y": 0}]
        self.assertEqual(2, len(rc.motion_runs(track)))


if __name__ == "__main__":
    unittest.main()
