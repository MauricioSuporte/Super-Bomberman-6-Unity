import json
import hashlib
from pathlib import Path
import tempfile
import threading
import unittest
from urllib.request import Request, urlopen
from urllib.error import HTTPError
from supervisor import RoomPool, Server, PROTOCOL


class Process:
    pid = 123
    def __init__(self): self.code = None
    def poll(self): return self.code
    def terminate(self): self.code = -15
    def kill(self): self.code = -9
    def wait(self, timeout=None): return self.code


class RoomsTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.calls = []
        def spawn(args, **kwargs):
            self.calls.append((args, kwargs))
            return Process()
        self.pool = RoomPool("server", "100.64.0.46", capacity=2, logs=self.temp.name, spawn=spawn)

    def tearDown(self):
        self.pool.close()
        self.temp.cleanup()

    def ready(self, room, players=1, available=True, token=None):
        data = dict(token=token or room["token"], players=players, available=available)
        room["status"].write_text(json.dumps(data), encoding="utf-8")
        self.pool.refresh()

    def test_private_room_stores_only_a_salted_verifier_and_requires_six_digits(self):
        public = self.pool.create("private-owner", "NormalGame", "123456")
        self.assertTrue(public["requiresPassword"])
        self.assertNotIn("password", public)
        self.assertNotIn("passwordHash", public)
        env = self.calls[0][1]["env"]
        salt = env["SB6_ROOM_PASSWORD_SALT"]
        self.assertTrue(salt)
        self.assertEqual(hashlib.sha256((salt + "123456").encode("utf-8")).hexdigest(), env["SB6_ROOM_PASSWORD_HASH"])
        self.assertNotIn("123456", self.calls[0][0])
        for password in ("12345", "1234567", "abcdef", 123456):
            with self.assertRaises(ValueError):
                self.pool.create("invalid-owner", "NormalGame", password)

    def test_room_metadata_and_player_limit_are_forwarded_without_password(self):
        room = self.pool.create("custom-owner", "BattleMode", "123456", "ARENA 1", "BOMBER", 3)
        self.assertEqual((room["name"], room["creator"], room["maxPlayers"]), ("ARENA 1", "BOMBER", 3))
        args = self.calls[-1][0]
        self.assertEqual(args[args.index("-room-max-players") + 1], "3")
        self.assertNotIn("password", room)
        for limit in (0, 1, 7, True, "3"):
            with self.assertRaises(ValueError):
                self.pool.create("bad-limit", "BattleMode", max_players=limit)
        with self.assertRaises(ValueError):
            self.pool.create("bad-coop", "NormalGame", max_players=5)
        for name in ("<b>ROOM</b>", "A" * 17, 12):
            with self.assertRaises(ValueError):
                self.pool.create("bad-name", name=name)

    def test_deployed_manifest_controls_directory_build_identity(self):
        binary = Path(self.temp.name) / "SB6.x86_64"
        manifest = binary.parent / "deployment.json"
        revision = "a" * 40
        manifest.write_text(json.dumps({"buildIdentity": revision}), encoding="utf-8")
        pool = RoomPool(binary, "127.0.0.1", logs=Path(self.temp.name) / "manifest-logs")
        self.assertEqual(revision, pool.build_identity)
        manifest.write_text(json.dumps({"buildIdentity": ""}), encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "invalid server build identity"):
            RoomPool(binary, "127.0.0.1", logs=Path(self.temp.name) / "invalid-logs")

    def test_modes_are_fixed_and_use_correct_capacity(self):
        for mode, capacity in (("NormalGame", 4), ("BossRush", 4), ("BattleMode", 6)):
            room = self.pool.create(mode, mode)
            self.assertEqual(mode, room["mode"])
            self.assertEqual(capacity, room["maxPlayers"])
            self.assertIn(mode, self.calls[-1][0])
            self.ready(self.pool.rooms[room["id"]], players=capacity)
            self.assertEqual(mode, self.pool.public(self.pool.rooms[room["id"]])["mode"])
            self.pool.rooms[room["id"]]["process"].code = 0
            self.pool.refresh()
        for invalid in ("unknown", "normalgame", [], None, 1):
            with self.assertRaises(ValueError): self.pool.create("bad", invalid)

    def test_capacity_ports_tokens_and_exit_cleanup(self):
        first = self.pool.create("a")
        second = self.pool.create("b")
        self.assertNotEqual(first["uri"], second["uri"])
        self.assertNotIn("token", first)
        self.assertNotIn("process", first)
        self.assertIn("SB6_ROOM_TOKEN", self.calls[0][1]["env"])
        with self.assertRaises(OverflowError): self.pool.create("c")
        self.pool.rooms[first["id"]]["process"].code = 0
        self.pool.refresh()
        third = self.pool.create("c")
        self.assertEqual(first["uri"], third["uri"])

    def test_authenticated_status_and_timeout(self):
        public = self.pool.create("a")
        room = self.pool.rooms[public["id"]]
        self.ready(room, token="wrong")
        self.assertFalse(room["available"])
        self.ready(room, players=2)
        self.assertTrue(room["available"])
        self.assertEqual(2, room["players"])
        room["heartbeat"] -= 40
        self.pool.refresh()
        self.assertIsNotNone(room["process"].poll())
        self.pool.refresh()
        self.assertEqual({}, self.pool.rooms)

    def test_log_budget_prunes_only_completed_room_history(self):
        first = self.pool.create("a")
        old = Path(self.temp.name) / first["id"]
        (old / "unity.log").write_text("history", encoding="utf-8")
        self.pool.rooms[first["id"]]["process"].code = 0
        self.pool.refresh()
        active = self.pool.create("b")
        current = Path(self.temp.name) / active["id"]
        (current / "unity.log").write_text("active", encoding="utf-8")
        self.pool.prune_history(0)
        self.assertFalse(old.exists())
        self.assertTrue(current.exists())

    def test_http_create_list_and_configuration_hide_room(self):
        server = Server(("127.0.0.1", 0), self.pool)
        thread = threading.Thread(target=server.serve_forever, daemon=True)
        thread.start()
        url = f"http://127.0.0.1:{server.server_port}"
        try:
            request = Request(url + "/rooms", data=json.dumps({"protocol": PROTOCOL, "buildIdentity": self.pool.build_identity}).encode(),
                              headers={"Content-Type": "application/json"})
            with urlopen(request) as response:
                self.assertEqual(202, response.status)
                created = json.load(response)
            room = self.pool.rooms[created["id"]]
            self.ready(room)
            with urlopen(url + "/rooms") as response:
                self.assertEqual(1, len(json.load(response)["rooms"]))
            self.ready(room, available=False)
            with urlopen(url + "/rooms") as response:
                self.assertEqual([], json.load(response)["rooms"])
            before = len(self.pool.rooms)
            request = Request(url + "/rooms", data=json.dumps({"protocol": PROTOCOL, "buildIdentity": "stale-commit"}).encode())
            with self.assertRaises(HTTPError) as error: urlopen(request)
            self.assertEqual(409, error.exception.code)
            self.assertEqual(before, len(self.pool.rooms))
            request = Request(url + "/rooms", data=b'{"protocol":2}')
            with self.assertRaises(HTTPError) as error: urlopen(request)
            self.assertEqual(409, error.exception.code)
        finally:
            server.shutdown()
            server.server_close()
            thread.join()


if __name__ == "__main__": unittest.main()
