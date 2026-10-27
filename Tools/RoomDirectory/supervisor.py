"""Bounded, local Unity room supervisor. Public HTTP is exposed through a proxy."""
import argparse
import hmac
import hashlib
import json
import logging
from logging.handlers import RotatingFileHandler
import os
import re
from pathlib import Path
import secrets
import signal
import subprocess
import shutil
import threading
import time
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import urlsplit

PROTOCOL = 50
MODES = {"BattleMode": ("Battle Mode", 6), "NormalGame": ("Normal Game", 4), "BossRush": ("Boss Rush", 4)}
log = logging.getLogger("rooms")


class RoomPool:
    def __init__(self, binary, host, *, first_port=7777, capacity=3, logs="logs", spawn=subprocess.Popen):
        self.binary, self.host = str(Path(binary).resolve()), host
        self.first_port, self.capacity, self.spawn = first_port, capacity, spawn
        self.build_identity = "local-development"
        for filename in ("deployment.json", "build-info.json"):
            manifest = Path(self.binary).parent / filename
            if manifest.is_file():
                value = json.loads(manifest.read_text(encoding="utf-8-sig"))
                self.build_identity = value.get("buildIdentity", value.get("sourceCommit", ""))
                if not isinstance(self.build_identity, str) or not re.fullmatch(r"[0-9a-f]{40}", self.build_identity):
                    raise ValueError("invalid server build identity")
                break
        self.logs = Path(logs).resolve()
        self.logs.mkdir(parents=True, exist_ok=True)
        self.rooms, self.last_create = {}, {}
        self.lock = threading.RLock()

    def reap(self):
        for key, room in list(self.rooms.items()):
            code = room["process"].poll()
            expired = time.monotonic() - room["heartbeat"] > 30
            if code is None and expired:
                room["process"].terminate()
                # Keep the port reserved until the OS confirms the process stopped.
                if time.monotonic() - room["heartbeat"] > 35:
                    room["process"].kill()
            if code is not None:
                log.info(json.dumps(dict(event="room_exit", room=key, exit_code=code)))
                del self.rooms[key]

    def create(self, ip, mode="BattleMode", password="", name="", creator="", max_players=None):
        if not isinstance(mode, str) or mode not in MODES:
            raise ValueError("invalid room mode")
        if not isinstance(password, str) or (password and not re.fullmatch(r"[0-9]{6}", password)):
            raise ValueError("private room passwords require six digits")
        for value in (name, creator):
            if not isinstance(value, str) or len(value) > 16 or any(not (c.isalnum() or c in " _-") for c in value):
                raise ValueError("invalid room name or creator")
        if max_players is None:
            max_players = MODES[mode][1]
        if type(max_players) is not int or not 2 <= max_players <= MODES[mode][1]:
            raise ValueError("invalid player limit")
        with self.lock:
            self.reap()
            now = time.monotonic()
            if now - self.last_create.get(ip, -100) < 5:
                raise OverflowError("wait before creating another room")
            self.last_create = {k: t for k, t in self.last_create.items() if now - t < 60}
            if len(self.rooms) >= self.capacity:
                raise OverflowError("server capacity reached")
            key, token = secrets.token_hex(16), secrets.token_hex(32)
            used = {r["port"] for r in self.rooms.values()}
            port = next(p for p in range(self.first_port, self.first_port + self.capacity) if p not in used)
            directory = self.logs / key
            directory.mkdir()
            env = dict(os.environ, SB6_ROOM_ID=key, SB6_ROOM_TOKEN=token,
                       SB6_ROOM_STATUS=str(directory / "status.json"))
            salt = secrets.token_hex(16) if password else ""
            digest = hashlib.sha256((salt + password).encode("utf-8")).hexdigest() if password else ""
            env["SB6_ROOM_PASSWORD_SALT"] = salt
            env["SB6_ROOM_PASSWORD_HASH"] = digest
            # Preserve loader/native failures that happen before Unity opens its log.
            with (directory / "launcher.log").open("ab") as launch_log:
                process = self.spawn([self.binary, "-batchmode", "-nographics", "-server",
                                      "-room-mode", mode, "-room-max-players", str(max_players), "-port", str(port), "-server-idle-seconds", "60",
                                      "-logFile", str(directory / "unity.log")],
                                     cwd=str(Path(self.binary).parent), env=env,
                                     stdout=launch_log, stderr=subprocess.STDOUT)
            room = dict(id=key, mode=mode, maxPlayers=max_players, creator=creator.strip(), name=name.strip() or MODES[mode][0] + " " + key[:6], uri=f"kcp://{self.host}:{port}",
                        players=0, available=False, requiresPassword=bool(password), process=process, port=port, token=token,
                        heartbeat=now, status=directory / "status.json", last_status=0)
            self.rooms[key] = room
            self.last_create[ip] = now
            log.info(json.dumps(dict(event="room_create", room=key, pid=process.pid, port=port)))
            return self.public(room)

    @staticmethod
    def public(room):
        return {k: room[k] for k in ("id", "creator", "name", "uri", "players", "available", "mode", "maxPlayers", "requiresPassword")}

    def refresh(self):
        with self.lock:
            for room in self.rooms.values():
                try:
                    modified = room["status"].stat().st_mtime_ns
                    if modified == room["last_status"]:
                        continue
                    data = json.loads(room["status"].read_text(encoding="utf-8-sig"))
                    if not hmac.compare_digest(data.get("token", ""), room["token"]):
                        continue
                    room.update(players=int(data["players"]), available=bool(data["available"]),
                                heartbeat=time.monotonic(), last_status=modified)
                except (OSError, ValueError, KeyError, TypeError):
                    pass  # Atomic status replacement can race the first read.
            self.reap()
            for key, room in self.rooms.items():
                for name in ("unity.log", "launcher.log", "events.jsonl", "metrics.jsonl"):
                    path = self.logs / key / name
                    if path.exists() and path.stat().st_size > 10_000_000:
                        # Unity holds the file open. Copy/truncate keeps its descriptor valid.
                        for i in (2, 1):
                            old = path.with_name(f"{name}.{i}")
                            if old.exists():
                                old.replace(path.with_name(f"{name}.{i + 1}"))
                        shutil.copyfile(path, path.with_name(f"{name}.1"))
                        with path.open("r+b") as stream:
                            stream.truncate(0)

    def close(self):
        with self.lock:
            processes = [r["process"] for r in self.rooms.values()]
        for process in processes:
            if process.poll() is None:
                process.terminate()
        for process in processes:
            try:
                process.wait(timeout=5)
            except subprocess.TimeoutExpired:
                process.kill()
                process.wait(timeout=5)

    def prune_history(self, budget_bytes=512 * 1024 * 1024):
        # Only completed room folders created by this supervisor are eligible.
        with self.lock:
            folders = [p for p in self.logs.iterdir() if p.is_dir() and not p.is_symlink()
                       and re.fullmatch(r"[a-f0-9]{32}", p.name)
                       and p.resolve().parent == self.logs]
            sizes = {p: sum(f.stat().st_size for f in p.iterdir() if f.is_file()) for p in folders}
            total = sum(sizes.values())
            for folder in sorted(folders, key=lambda p: p.stat().st_mtime):
                if total <= budget_bytes:
                    break
                if folder.name in self.rooms:
                    continue
                shutil.rmtree(folder)
                total -= sizes[folder]
                log.info(json.dumps(dict(event="history_pruned", room=folder.name, bytes=sizes[folder])))


class Server(ThreadingHTTPServer):
    daemon_threads = True

    def __init__(self, address, pool):
        self.pool = pool
        super().__init__(address, Handler)


class Handler(BaseHTTPRequestHandler):
    def setup(self):
        super().setup()
        self.connection.settimeout(5)

    def log_message(self, *_args):
        pass

    def reply(self, code, body):
        data = json.dumps(body).encode("utf-8")
        self.send_response(code)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(data)))
        self.send_header("Cache-Control", "no-store")
        self.end_headers()
        self.wfile.write(data)

    def do_GET(self):
        path = urlsplit(self.path).path
        self.server.pool.refresh()
        with self.server.pool.lock:
            if path == "/health":
                return self.reply(200, {"ok": True, "protocol": PROTOCOL, "buildIdentity": self.server.pool.build_identity})
            if path == "/rooms":
                rooms = [self.server.pool.public(r) for r in self.server.pool.rooms.values()
                         if r["available"] and r["players"] < r["maxPlayers"]]
                return self.reply(200, {"rooms": rooms})
            if path.startswith("/rooms/"):
                room = self.server.pool.rooms.get(path.split("/")[-1])
                if room:
                    return self.reply(200, self.server.pool.public(room))
        self.reply(404, {"error": "room not found"})

    def do_POST(self):
        if self.path != "/rooms":
            return self.reply(404, {"error": "not found"})
        try:
            length = int(self.headers.get("Content-Length", "0"))
            if not 0 < length <= 1024:
                raise ValueError()
            data = json.loads(self.rfile.read(length).decode("utf-8"))
            if data.get("protocol") != PROTOCOL or data.get("buildIdentity") != self.server.pool.build_identity:
                return self.reply(409, {"error": "incompatible build or protocol"})
            private = data.get("isPrivate", False)
            password = data.get("password", "") if private else ""
            if not isinstance(private, bool) or (private and not re.fullmatch(r"[0-9]{6}", password if isinstance(password, str) else "")):
                raise ValueError("private room passwords require six digits")
            room = self.server.pool.create(self.client_address[0], data.get("mode", "BattleMode"), password, data.get("name", ""), data.get("creator", ""), data.get("maxPlayers"))
            self.reply(202, room)
        except OverflowError as error:
            self.reply(429, {"error": str(error)})
        except (ValueError, AttributeError, UnicodeError):
            self.reply(400, {"error": "invalid request"})
        except OSError:
            log.exception("room launch failed")
            self.reply(503, {"error": "room launch failed"})


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--binary", required=True)
    parser.add_argument("--host", required=True, help="Client-reachable KCP address")
    parser.add_argument("--bind", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=8080)
    parser.add_argument("--first-port", type=int, default=7777)
    parser.add_argument("--capacity", type=int, default=3)
    parser.add_argument("--logs", default="logs")
    parser.add_argument("--stop-file", help="Local launcher stop request file")
    args = parser.parse_args()
    if not 1 <= args.capacity <= 32 or not 1 <= args.first_port <= 65536 - args.capacity:
        parser.error("invalid capacity/port range")
    Path(args.logs).mkdir(parents=True, exist_ok=True)
    handler = RotatingFileHandler(Path(args.logs) / "supervisor.log", maxBytes=5_000_000, backupCount=3, encoding="utf-8")
    logging.Formatter.converter = time.gmtime
    logging.basicConfig(level=logging.INFO, handlers=[handler, logging.StreamHandler()],
                        format="%(asctime)sZ %(levelname)s %(message)s")
    pool = RoomPool(args.binary, args.host, first_port=args.first_port, capacity=args.capacity, logs=args.logs)
    stopping = threading.Event()
    def monitor():
        next_report = 0
        next_prune = 0
        while not stopping.wait(1):
            try:
                pool.refresh()
                if time.monotonic() >= next_prune:
                    next_prune = time.monotonic() + 60
                    pool.prune_history()
                if time.monotonic() >= next_report:
                    next_report = time.monotonic() + 10
                    memory = {}
                    if Path("/proc/meminfo").exists():
                        memory = {line.split(":")[0]: int(line.split()[1])
                                  for line in Path("/proc/meminfo").read_text(encoding="utf-8").splitlines()}
                    with pool.lock:
                        count = len(pool.rooms)
                    log.info(json.dumps(dict(event="host_metrics", rooms=count,
                        load1=os.getloadavg()[0] if hasattr(os, "getloadavg") else None,
                        availableMiB=memory["MemAvailable"] / 1024 if "MemAvailable" in memory else None,
                        diskFreeGiB=shutil.disk_usage(pool.logs).free / 1073741824)))
            except OSError:
                log.exception("supervisor monitoring failed")
    threading.Thread(target=monitor, daemon=True).start()
    def stop(*_args):
        raise KeyboardInterrupt()
    signal.signal(signal.SIGTERM, stop)
    try:
        with Server((args.bind, args.port), pool) as server:
            log.info("room service ready capacity=%s", args.capacity)
            server.timeout = 1
            while not args.stop_file or not Path(args.stop_file).exists():
                server.handle_request()
    except KeyboardInterrupt:
        pass
    finally:
        stopping.set()
        pool.close()


if __name__ == "__main__":
    main()
