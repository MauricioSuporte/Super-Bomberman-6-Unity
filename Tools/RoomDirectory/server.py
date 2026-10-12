"""Small ephemeral room directory. Gameplay traffic still goes directly to KCP hosts."""
import argparse
import hmac
import ipaddress
import json
import re
import threading
import time
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import parse_qs, urlsplit

PROTOCOL = "sb6-room-v1"
TTL = 20
MAX_ROOMS = 500
IDENTIFIER = re.compile(r"^[a-f0-9]{32}$")


class DirectoryServer(ThreadingHTTPServer):
    daemon_threads = True

    def __init__(self, address, *, trust_proxy=False, clock=time.monotonic):
        super().__init__(address, Handler)
        self.rooms = {}
        self.lock = threading.Lock()
        self.clock = clock
        self.trust_proxy = trust_proxy

    def expire(self):
        now = self.clock()
        for key in list(self.rooms):
            if self.rooms[key]["expires"] <= now:
                del self.rooms[key]


class Handler(BaseHTTPRequestHandler):
    # Prevent abandoned body reads from occupying worker threads indefinitely.
    def setup(self):
        super().setup()
        self.connection.settimeout(5)

    def log_message(self, *_args):
        pass  # Avoid logging bearer tokens or request bodies.

    def reply(self, status, data):
        body = json.dumps(data, ensure_ascii=False).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Cache-Control", "no-store")
        self.end_headers()
        self.wfile.write(body)

    def client_ip(self):
        value = self.client_address[0]
        if self.server.trust_proxy:
            value = self.headers.get("X-Real-IP", "")
        return str(ipaddress.ip_address(value))

    def do_GET(self):
        path = urlsplit(self.path)
        if path.path == "/health":
            self.reply(200, {"ok": True})
            return
        if path.path != "/rooms":
            self.reply(404, {"error": "not found"})
            return
        protocol = parse_qs(path.query).get("protocol", [""])[0]
        with self.server.lock:
            self.server.expire()
            rooms = [record["room"] for record in self.server.rooms.values() if protocol == PROTOCOL]
        self.reply(200, {"rooms": sorted(rooms, key=lambda room: room["id"])})

    def do_POST(self):
        if self.path != "/rooms":
            self.reply(404, {"error": "not found"})
            return
        try:
            length = int(self.headers.get("Content-Length", "0"))
            if not 0 < length <= 2048:
                raise ValueError("invalid body size")
            data = json.loads(self.rfile.read(length).decode("utf-8"))
            if not isinstance(data, dict):
                raise ValueError("expected object")
            key, token = data.get("id", ""), data.get("token", "")
            if not isinstance(key, str) or not IDENTIFIER.fullmatch(key):
                raise ValueError("invalid id")
            if not isinstance(token, str) or not IDENTIFIER.fullmatch(token):
                raise ValueError("invalid token")
            if data.get("protocol") != PROTOCOL or data.get("scheme") != "kcp":
                raise ValueError("incompatible protocol")
            port, players = data.get("port"), data.get("players")
            if type(port) is not int or not 1 <= port <= 65535:
                raise ValueError("invalid port")
            if type(players) is not int or not 0 <= players < 6 or data.get("available") is not True:
                raise ValueError("room unavailable")
            name = data.get("name")
            if not isinstance(name, str) or not 1 <= len(name) <= 64 or any(ord(c) < 32 for c in name):
                raise ValueError("invalid name")
            ip = self.client_ip()
        except (ValueError, TypeError, UnicodeError):
            self.reply(400, {"error": "invalid registration"})
            return
        with self.server.lock:
            self.server.expire()
            old = self.server.rooms.get(key)
            if old and (old["ip"] != ip or not hmac.compare_digest(old["token"], token)):
                self.reply(403, {"error": "room owner mismatch"})
                return
            if old and self.server.clock() - old["updated"] < 1:
                self.reply(429, {"error": "heartbeat too frequent"})
                return
            if not old and (len(self.server.rooms) >= MAX_ROOMS or
                            sum(r["ip"] == ip for r in self.server.rooms.values()) >= 8):
                self.reply(429, {"error": "directory capacity"})
                return
            host = f"[{ip}]" if ":" in ip else ip
            self.server.rooms[key] = {
                "ip": ip, "token": token, "expires": self.server.clock() + TTL,
                "updated": self.server.clock(),
                "room": {"id": key, "name": name, "uri": f"kcp://{host}:{port}", "players": players},
            }
        self.reply(200, {"ok": True})

    def do_DELETE(self):
        key = self.path.removeprefix("/rooms/")
        if not self.path.startswith("/rooms/") or not IDENTIFIER.fullmatch(key):
            self.reply(404, {"error": "not found"})
            return
        try:
            ip = self.client_ip()
        except ValueError:
            self.reply(400, {"error": "invalid client address"})
            return
        token = self.headers.get("Authorization", "").removeprefix("Bearer ")
        if not IDENTIFIER.fullmatch(token):
            self.reply(403, {"error": "invalid owner token"})
            return
        with self.server.lock:
            self.server.expire()
            old = self.server.rooms.get(key)
            if old and (old["ip"] != ip or not hmac.compare_digest(old["token"], token)):
                self.reply(403, {"error": "room owner mismatch"})
                return
            self.server.rooms.pop(key, None)
        self.reply(200, {"ok": True})


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--bind", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=8080)
    parser.add_argument("--trust-proxy", action="store_true")
    args = parser.parse_args()
    with DirectoryServer((args.bind, args.port), trust_proxy=args.trust_proxy) as server:
        print(f"Room directory listening on {args.bind}:{args.port}", flush=True)
        server.serve_forever()
