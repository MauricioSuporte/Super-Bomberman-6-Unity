"""Loopback-only UDP relay for isolated Development smoke captures."""
import heapq
import random
import selectors
import socket
import threading
import time


class LoopbackRelay:
    def __init__(self, listen_port, server_port, delay_ms=50, jitter_ms=5, loss=0.01, seed=70):
        if not 0 <= loss < 1 or delay_ms < jitter_ms or jitter_ms < 0:
            raise ValueError("Invalid delay/jitter/loss")
        self.listen_port, self.server_port = listen_port, server_port
        self.delay, self.jitter, self.loss = delay_ms / 1000, jitter_ms / 1000, loss
        self.random = random.Random(seed)
        self.selector = selectors.DefaultSelector()
        self.clients, self.pending = {}, []
        self.serial = self.forwarded = self.dropped = self.unreachable = 0
        self.stop = threading.Event()
        self.error = None

    def __enter__(self):
        self.listener = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        self.listener.bind(("127.0.0.1", self.listen_port)); self.listener.setblocking(False)
        self.selector.register(self.listener, selectors.EVENT_READ, None)
        self.worker = threading.Thread(target=self._run, daemon=True); self.worker.start()
        return self

    def _schedule(self, sock, payload, destination):
        if self.random.random() < self.loss:
            self.dropped += 1; return
        if len(self.pending) >= 20000:
            raise RuntimeError("Relay backlog exceeded")
        self.serial += 1
        heapq.heappush(self.pending, (time.monotonic() + self.delay + self.random.uniform(-self.jitter, self.jitter),
                                      self.serial, sock, payload, destination))

    def _run(self):
        try:
            while not self.stop.is_set():
                for key, _ in self.selector.select(.002):
                    try:
                        payload, address = key.fileobj.recvfrom(65535)
                    except ConnectionResetError as error:
                        # Windows reports asynchronous UDP ICMP port-unreachable
                        # on recvfrom, notably after the test server has quit.
                        # Preserve silence/disconnect as a gameplay failure; this
                        # notification must not kill the relay's other peers.
                        if getattr(error, "winerror", None) != 10054: raise
                        self.unreachable += 1
                        continue
                    if key.data is None:
                        if address not in self.clients:
                            if len(self.clients) >= 8: raise RuntimeError("Too many test peers")
                            peer = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
                            peer.bind(("127.0.0.1", 0)); peer.setblocking(False)
                            self.clients[address] = peer
                            self.selector.register(peer, selectors.EVENT_READ, address)
                        self._schedule(self.clients[address], payload, ("127.0.0.1", self.server_port))
                    else:
                        if address != ("127.0.0.1", self.server_port): continue
                        self._schedule(self.listener, payload, key.data)
                while self.pending and self.pending[0][0] <= time.monotonic():
                    _, _, sock, payload, destination = heapq.heappop(self.pending)
                    sock.sendto(payload, destination); self.forwarded += 1
        except Exception as error:
            self.error = repr(error); self.stop.set()

    def __exit__(self, *args):
        self.stop.set(); self.worker.join(timeout=2)
        for peer in self.clients.values(): peer.close()
        self.listener.close(); self.selector.close()
        if self.error and not args[0]: raise RuntimeError(self.error)
