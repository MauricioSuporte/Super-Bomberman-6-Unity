import importlib.util
from pathlib import Path
import socket
import threading
import time
import unittest
import sys

spec = importlib.util.spec_from_file_location("udp_test_relay", Path(__file__).parents[1] / "Diagnostics/udp_test_relay.py")
relay_module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(relay_module)


class LoopbackRelayTests(unittest.TestCase):
    def test_rejects_invalid_delay_jitter_or_loss(self):
        for arguments in ({"delay_ms": 0, "jitter_ms": 1}, {"loss": -1}, {"loss": 1}):
            with self.assertRaises(ValueError):
                relay_module.LoopbackRelay(0, 0, **arguments)

    def test_two_peers_receive_only_their_echo_after_both_direction_delays(self):
        with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as server:
            server.bind(("127.0.0.1", 0)); server.settimeout(2)
            errors = []
            def echo():
                try:
                    for _ in range(2):
                        payload, address = server.recvfrom(2048)
                        server.sendto(payload, address)
                except Exception as error:
                    errors.append(error)
            worker = threading.Thread(target=echo, daemon=True); worker.start()
            with relay_module.LoopbackRelay(0, server.getsockname()[1], delay_ms=10, jitter_ms=0, loss=0) as relay:
                with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as first, socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as second:
                    first.settimeout(2); second.settimeout(2)
                    start = time.monotonic()
                    first.sendto(b"first", relay.listener.getsockname())
                    second.sendto(b"second", relay.listener.getsockname())
                    self.assertEqual(b"first", first.recvfrom(2048)[0])
                    self.assertEqual(b"second", second.recvfrom(2048)[0])
                    self.assertGreaterEqual(time.monotonic() - start, .02)
                    self.assertEqual(2, len(relay.clients))
            worker.join(timeout=2)
            self.assertFalse(worker.is_alive())
            self.assertEqual([], errors)

    @unittest.skipUnless(sys.platform == "win32", "Windows UDP ICMP notification")
    def test_closed_server_notification_does_not_kill_relay_or_other_traffic(self):
        server = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        server.bind(("127.0.0.1", 0)); port = server.getsockname()[1]; server.close()
        with relay_module.LoopbackRelay(0, port, delay_ms=1, jitter_ms=0, loss=0) as relay:
            with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as client:
                client.settimeout(2); client.sendto(b"closed", relay.listener.getsockname())
                deadline = time.monotonic() + 2
                while relay.unreachable == 0 and relay.error is None and time.monotonic() < deadline:
                    time.sleep(.005)
                self.assertIsNone(relay.error)
                self.assertGreater(relay.unreachable, 0)
                with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as restarted:
                    restarted.bind(("127.0.0.1", port)); restarted.settimeout(2)
                    client.sendto(b"alive", relay.listener.getsockname())
                    payload, address = restarted.recvfrom(2048)
                    restarted.sendto(payload, address)
                    self.assertEqual(b"alive", client.recvfrom(2048)[0])
