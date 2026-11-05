import json
import tempfile
import threading
import unittest
from unittest.mock import patch
from urllib.error import HTTPError
from urllib.request import Request, urlopen

from supervisor import ChatStore, PROTOCOL, RoomPool, Server


class ChatTests(unittest.TestCase):
    def test_identity_is_bound_to_session_and_ip_and_messages_are_plain_text(self):
        chat = ChatStore()
        token = chat.join("alice-ip", "ALICE")["token"]
        chat.send("alice-ip", token, "  olá\n日本語\u202e <b>oi</b>  ")
        line = chat.poll("alice-ip", token, 0)["messages"][0]
        self.assertEqual(("ALICE", "olá 日本語 <b>oi</b>"), (line["sender"], line["text"]))
        self.assertEqual([], chat.poll("alice-ip", token, line["id"])["messages"])
        with self.assertRaises(PermissionError): chat.poll("other-ip", token, 0)
        with self.assertRaises(PermissionError): chat.send("alice-ip", "fake", "oi")

    def test_history_is_bounded_and_expires_and_sessions_expire(self):
        chat = ChatStore()
        with patch("supervisor.time.monotonic", return_value=100):
            token = chat.join("ip", "BOMBER")["token"]
        for i in range(80):
            with patch("supervisor.time.monotonic", return_value=101 + i):
                chat.send("ip", token, str(i))
        with patch("supervisor.time.monotonic", return_value=181):
            history = chat.poll("ip", token, 0)["messages"]
            self.assertEqual(64, len(history))
            self.assertEqual("16", history[0]["text"])
            self.assertEqual("79", chat.join("new-ip", "NEW")["messages"][-1]["text"])
        with patch("supervisor.time.monotonic", return_value=500):
            self.assertEqual([], chat.join("newer-ip", "NEWER")["messages"])
            with self.assertRaises(PermissionError): chat.poll("ip", token, 0)

    def test_rate_limit_is_shared_by_sessions_from_the_same_ip(self):
        chat = ChatStore()
        with patch("supervisor.time.monotonic", return_value=100):
            first = chat.join("ip", "ONE")["token"]
            with self.assertRaises(OverflowError): chat.join("ip", "TWO")
        with patch("supervisor.time.monotonic", return_value=101):
            second = chat.join("ip", "TWO")["token"]
            chat.send("ip", first, "one")
            with self.assertRaises(OverflowError): chat.send("ip", second, "two")
        with patch("supervisor.time.monotonic", return_value=102):
            chat.send("ip", second, "two")

    def test_invalid_names_empty_messages_and_unicode_limits(self):
        chat = ChatStore()
        for name in (None, [], "", "X" * 17, "<BOMBER>"):
            with self.assertRaises(ValueError): chat.join("ip", name)
        token = chat.join("ip", "BOMBER")["token"]
        for text in (None, [], " \n\t ", "x" * 513):
            with self.assertRaises(ValueError): chat.send("ip", token, text)
        chat.send("ip", token, "a" * 127 + "😀")
        self.assertEqual("a" * 127, chat.poll("ip", token, 0)["messages"][0]["text"])

    def test_http_two_browsers_exchange_and_reject_incompatible_or_expired_clients(self):
        with tempfile.TemporaryDirectory() as logs:
            pool = RoomPool("unused-binary", "127.0.0.1", logs=logs)
            server = Server(("127.0.0.1", 0), pool)
            thread = threading.Thread(target=server.serve_forever, daemon=True)
            thread.start()
            url = f"http://127.0.0.1:{server.server_port}"

            def post(path, **body):
                body.setdefault("protocol", PROTOCOL)
                body.setdefault("buildIdentity", pool.build_identity)
                request = Request(url + path, data=json.dumps(body).encode("utf-8"),
                                  headers={"Content-Type": "application/json"})
                with urlopen(request, timeout=3) as response: return json.load(response)

            try:
                alice = post("/chat/session", name="ALICE")["token"]
                # Distinct peers may share a NAT; registration still has bounded rate.
                server.chat.last_join.clear()
                bob = post("/chat/session", name="BOB")["token"]
                post("/chat", token=alice, name="SPOOFED", text="bora jogar")
                request = Request(url + "/chat?after=0", headers={"X-Chat-Token": bob})
                with urlopen(request, timeout=3) as response:
                    line = json.load(response)["messages"][0]
                self.assertEqual(("ALICE", "bora jogar"), (line["sender"], line["text"]))
                for path, body, status in (
                    ("/chat", dict(token=bob, text="flood"), 429),
                    ("/chat/session", dict(name="OLD", protocol=PROTOCOL - 1), 409),
                    ("/chat", dict(token="expired", text="oi"), 401),
                ):
                    with self.assertRaises(HTTPError) as error: post(path, **body)
                    self.assertEqual(status, error.exception.code)
                with self.assertRaises(HTTPError) as error: urlopen(url + "/chat?after=0", timeout=3)
                self.assertEqual(401, error.exception.code)
                with self.assertRaises(HTTPError) as error:
                    urlopen(Request(url + "/chat?after=abc", headers={"X-Chat-Token": bob}), timeout=3)
                self.assertEqual(400, error.exception.code)
            finally:
                server.shutdown()
                server.server_close()
                thread.join()
                pool.close()


if __name__ == "__main__": unittest.main()
