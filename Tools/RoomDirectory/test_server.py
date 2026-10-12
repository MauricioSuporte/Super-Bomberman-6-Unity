import json
import threading
import unittest
from urllib.error import HTTPError
from urllib.request import Request, urlopen
from server import DirectoryServer, TTL


class DirectoryTests(unittest.TestCase):
    def setUp(self):
        self.now = 100.0
        self.server = DirectoryServer(("127.0.0.1", 0), clock=lambda: self.now)
        self.thread = threading.Thread(target=self.server.serve_forever, daemon=True)
        self.thread.start()
        self.url = f"http://127.0.0.1:{self.server.server_port}"
        self.room = dict(id="a" * 32, token="b" * 32, name="Battle Mode", protocol="sb6-room-v1",
                         scheme="kcp", port=7777, players=2, available=True)

    def tearDown(self):
        self.server.shutdown()
        self.server.server_close()
        self.thread.join()

    def request(self, path, method="GET", data=None, token=None):
        headers = {"Content-Type": "application/json"}
        if token:
            headers["Authorization"] = "Bearer " + token
        request = Request(self.url + path, method=method, headers=headers,
                          data=json.dumps(data).encode("utf-8") if data is not None else None)
        try:
            with urlopen(request, timeout=3) as response:
                return response.status, json.loads(response.read().decode("utf-8"))
        except HTTPError as error:
            return error.code, json.loads(error.read().decode("utf-8"))

    def test_register_list_expire_and_protocol_isolation(self):
        self.assertEqual(200, self.request("/rooms", "POST", self.room)[0])
        rooms = self.request("/rooms?protocol=sb6-room-v1")[1]["rooms"]
        self.assertEqual("kcp://127.0.0.1:7777", rooms[0]["uri"])
        self.assertNotIn("token", rooms[0])
        self.assertEqual([], self.request("/rooms?protocol=other")[1]["rooms"])
        self.now += TTL + 1
        self.assertEqual([], self.request("/rooms?protocol=sb6-room-v1")[1]["rooms"])

    def test_owner_token_protects_updates_and_deletion(self):
        self.request("/rooms", "POST", self.room)
        self.now += 5
        other = dict(self.room, token="c" * 32)
        self.assertEqual(403, self.request("/rooms", "POST", other)[0])
        self.assertEqual(403, self.request("/rooms/" + self.room["id"], "DELETE", token=other["token"])[0])
        self.room["players"] = 3
        self.assertEqual(200, self.request("/rooms", "POST", self.room)[0])
        self.assertEqual(3, self.request("/rooms?protocol=sb6-room-v1")[1]["rooms"][0]["players"])
        self.assertEqual(200, self.request("/rooms/" + self.room["id"], "DELETE", token=self.room["token"])[0])
        self.assertEqual([], self.request("/rooms?protocol=sb6-room-v1")[1]["rooms"])

    def test_invalid_full_and_incompatible_rooms_are_rejected(self):
        for change in ({"players": 6}, {"port": 0}, {"scheme": "http"}, {"protocol": "old"},
                       {"available": False}, {"name": "\n"}, {"players": True}):
            self.assertEqual(400, self.request("/rooms", "POST", dict(self.room, **change))[0])

    def test_capacity_and_heartbeat_limits(self):
        self.request("/rooms", "POST", self.room)
        self.assertEqual(429, self.request("/rooms", "POST", self.room)[0])
        for number in range(7):
            self.assertEqual(200, self.request("/rooms", "POST", dict(self.room, id=f"{number:032x}"))[0])
        self.assertEqual(429, self.request("/rooms", "POST", dict(self.room, id="d" * 32))[0])


if __name__ == "__main__":
    unittest.main()
