import unittest
from unittest.mock import patch
from relay_probe import EdgegapRelay, RelayError, credentials, session_status


class RelayProbeTests(unittest.TestCase):
    def test_status_never_returns_authorization_tokens_or_raw_error(self):
        result = self.ready()
        result['error'] = 'private details'
        result['secret'] = 'private details'
        report = session_status(result)
        self.assertEqual(['8.8.8.8', '1.1.1.1'], report['authorized_ips'])
        self.assertTrue(report['has_error'])
        text = str(report)
        self.assertNotIn('authorization_token', text)
        self.assertNotIn('private details', text)
        self.assertNotIn('4000000000', text)

    def ready(self):
        return {"ready": True, "authorization_token": 4000000000,
                "session_users": [{"ip_address": "8.8.8.8", "authorization_token": 3000000000},
                                  {"ip_address": "1.1.1.1", "authorization_token": 2000000000}],
                "relay": {"host": "relay.example.com", "ports": {
                    "server": {"port": 12345}, "client": {"port": 23456}}}}

    def test_maps_uint_tokens_and_separates_users(self):
        result = self.ready()
        host = credentials(result, "8.8.8.8", "host")
        client = credentials(result, "1.1.1.1", "client")
        self.assertEqual(4000000000, host["sessionId"])
        self.assertNotEqual(host["userId"], client["userId"])
        self.assertNotIn("session_users", client)

    def test_rejects_invalid_ports_tokens_and_missing_user(self):
        for value in (0, -1, True, 4294967296):
            result = self.ready()
            result["authorization_token"] = value
            with self.assertRaises(RelayError):
                credentials(result, "8.8.8.8", "host")
        result = self.ready()
        result["relay"]["ports"]["client"]["port"] = 65536
        with self.assertRaises(RelayError):
            credentials(result, "8.8.8.8", "host")
        with self.assertRaises(RelayError):
            credentials(self.ready(), "9.9.9.9", "host")

    def test_failed_initialization_deletes_allocated_session(self):
        api = EdgegapRelay("test-only")
        with patch.object(api, "request", side_effect=[{"session_id": "probe-S"}, {"error": "failed"}]), \
                patch.object(api, "delete") as delete:
            with self.assertRaises(RelayError):
                api.create("8.8.8.8", "1.1.1.1")
            delete.assert_called_once_with("probe-S")

    def test_rejects_private_or_identical_ips_before_allocation(self):
        api = EdgegapRelay("test-only")
        with patch.object(api, "request") as request:
            for host, client in (("192.168.1.2", "1.1.1.1"), ("8.8.8.8", "8.8.8.8")):
                with self.assertRaises(RelayError):
                    api.create(host, client)
            request.assert_not_called()
