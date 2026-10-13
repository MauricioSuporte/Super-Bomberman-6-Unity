"""Create/delete a private two-peer Edgegap probe; never put the API key in Unity."""
import argparse
from datetime import datetime, timezone
import ipaddress
import json
import os
from pathlib import Path
import time
from urllib.error import HTTPError, URLError
from urllib.parse import quote
from urllib.request import Request, urlopen


class RelayError(Exception):
    pass


class EdgegapRelay:
    def __init__(self, token):
        if not token or "\n" in token or "\r" in token:
            raise RelayError("Configure EDGEGAP_RELAY_API_TOKEN locally.")
        self.token = token.removeprefix("token ")

    def request(self, method, path, data=None):
        request = Request("https://api.edgegap.com/v1/relays/sessions" + path,
                          method=method, headers={"Authorization": "token " + self.token,
                                                  "Content-Type": "application/json"},
                          data=json.dumps(data).encode("utf-8") if data is not None else None)
        try:
            with urlopen(request, timeout=10) as response:
                body = response.read(65537)
                if len(body) > 65536:
                    raise RelayError("Relay response too large.")
                return json.loads(body.decode("utf-8")) if body else {}
        except HTTPError as error:
            if method == "DELETE" and error.code == 404:
                return {}
            raise RelayError(f"Relay API returned HTTP {error.code}.") from None
        except (URLError, TimeoutError, ValueError):
            raise RelayError("Relay API unavailable or invalid response.") from None

    def delete(self, session):
        self.request("DELETE", "/" + quote(session, safe=""))

    def create(self, host_ip, client_ip):
        ips = [str(ipaddress.ip_address(host_ip)), str(ipaddress.ip_address(client_ip))]
        if ips[0] == ips[1] or any(not ipaddress.ip_address(ip).is_global for ip in ips):
            raise RelayError("Use distinct public IPs from two different networks.")
        result = self.request("POST", "", {"users": [{"ip": ip} for ip in ips]})
        session = result.get("session_id")
        if not isinstance(session, str) or not session:
            raise RelayError("Relay API did not return a session ID.")
        try:
            for _ in range(20):
                result = self.request("GET", "/" + quote(session, safe=""))
                if result.get("error"):
                    raise RelayError("Relay session could not initialize.")
                if result.get("ready") is True:
                    return session, credentials(result, ips[0], "host"), credentials(result, ips[1], "client")
                time.sleep(1)
            raise RelayError("Relay session initialization timed out.")
        except Exception:
            try:
                self.delete(session)
            except RelayError:
                raise RelayError(f"Initialization failed; manually delete session {session} in Edgegap.") from None
            raise


def credentials(result, ip, role):
    """Mirror sessionId is the uint authorization token, not the string API session_id."""
    try:
        relay = result["relay"]
        user = next(user for user in result["session_users"] if user["ip_address"] == ip)
        data = dict(role=role, relayAddress=relay["host"],
                    relayGameServerPort=relay["ports"]["server"]["port"],
                    relayGameClientPort=relay["ports"]["client"]["port"],
                    sessionId=result["authorization_token"], userId=user["authorization_token"])
        if not isinstance(data["relayAddress"], str) or not data["relayAddress"]:
            raise ValueError()
        for name in ("relayGameServerPort", "relayGameClientPort", "sessionId", "userId"):
            limit = 65535 if "Port" in name else 4294967295
            if type(data[name]) is not int or not 0 < data[name] <= limit:
                raise ValueError()
        return data
    except (KeyError, TypeError, StopIteration, ValueError):
        raise RelayError("Relay API returned incomplete credentials.") from None


def session_status(result):
    """Allowlist diagnostic fields: never print API/session/user authorization tokens."""
    relay = result.get("relay") or {}
    users = result.get("session_users") or []
    return {
        "status": result.get("status"),
        "ready": result.get("ready"),
        "linked": result.get("linked"),
        "has_error": bool(result.get("error")),
        "relay_host": relay.get("host"),
        "authorized_ips": [user.get("ip_address") for user in users if isinstance(user, dict)],
    }


def check_local_credentials(result, directory):
    checks = {}
    for role in ("host", "client"):
        try:
            data = json.loads((directory / (role + ".json")).read_text(encoding="utf-8-sig"))
            checks[role + "_session_matches"] = data.get("sessionId") == result.get("authorization_token")
            checks[role + "_user_authorized"] = any(
                user.get("authorization_token") == data.get("userId")
                for user in result.get("session_users", []) if isinstance(user, dict))
        except (OSError, ValueError):
            checks[role + "_file_readable"] = False
    return checks


def support_snapshot(result, session):
    """Only export the fields requested by support; authorization values are never copied."""
    relay = result.get("relay") or {}
    ports = relay.get("ports") or {}
    return {
        "captured_at_utc": datetime.now(timezone.utc).isoformat(),
        "session_id": session,
        "status": result.get("status"), "ready": result.get("ready"),
        "linked": result.get("linked"),
        "error": None if not result.get("error") else "[ERROR PRESENT: DETAILS OMITTED]",
        "authorization_token": "[REDACTED]",
        "session_users": [{"ip_address": user.get("ip_address"),
                           "authorization_token": "[REDACTED]"}
                          for user in result.get("session_users", []) if isinstance(user, dict)],
        "relay": {"ip": relay.get("ip"), "host": relay.get("host"),
                  "ports": {role: {"port": (ports.get(role) or {}).get("port"),
                                   "protocol": (ports.get(role) or {}).get("protocol")}
                            for role in ("server", "client")}},
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    create = commands.add_parser("create")
    create.add_argument("--host-ip", required=True)
    create.add_argument("--client-ip", required=True)
    create.add_argument("--output", required=True, type=Path)
    delete = commands.add_parser("delete")
    delete.add_argument("--session-file", required=True, type=Path)
    status = commands.add_parser("status")
    status.add_argument("--session-file", required=True, type=Path)
    support = commands.add_parser("support-snapshot")
    support.add_argument("--session-file", required=True, type=Path)
    support.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    try:
        api = EdgegapRelay(os.environ.get("EDGEGAP_RELAY_API_TOKEN"))
        if args.command == "support-snapshot":
            session = json.loads(args.session_file.read_text(encoding="utf-8-sig"))["session_id"]
            result = api.request("GET", "/" + quote(session, safe=""))
            args.output.parent.mkdir(parents=True, exist_ok=True)
            with args.output.open("x", encoding="utf-8", newline="\n") as stream:
                json.dump(support_snapshot(result, session), stream, indent=2)
            print("Saved redacted API snapshot. Attach it only to the private support ticket.")
            return
        if args.command == "status":
            session = json.loads(args.session_file.read_text(encoding="utf-8-sig"))["session_id"]
            result = api.request("GET", "/" + quote(session, safe=""))
            report = session_status(result)
            report.update(check_local_credentials(result, args.session_file.parent))
            print(json.dumps(report, indent=2))
            return
        if args.command == "delete":
            session = json.loads(args.session_file.read_text(encoding="utf-8-sig"))["session_id"]
            api.delete(session)
            print("Relay session deleted. Delete the local credential files too.")
            return
        # A new directory avoids accidentally replacing credentials for a still-running session.
        args.output.mkdir(parents=True, exist_ok=False)
        session, host, client = api.create(args.host_ip, args.client_ip)
        try:
            for name, data in (("host.json", host), ("client.json", client),
                               ("session.json", {"session_id": session})):
                path = args.output / name
                with path.open("x", encoding="utf-8", newline="\n") as stream:
                    json.dump(data, stream, indent=2)
                path.chmod(0o600)
        except Exception:
            try:
                api.delete(session)
            except RelayError:
                print(f"Manually delete session {session} in Edgegap; local export failed.")
            raise
        print("Created host.json, client.json and session.json. Keep these files private.")
    except (RelayError, OSError, ValueError, KeyError) as error:
        parser.exit(1, str(error) + "\n")


if __name__ == "__main__":
    main()
