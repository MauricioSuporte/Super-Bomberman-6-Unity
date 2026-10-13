# Relay authentication remains pending

Investigation on 2026-10-12, Unity 6000.6.0f1 and Mirror 96.6.4.
This report contains no API keys, session authorization tokens or user tokens.

## Evidence

- Both clients now resolve the same IPv4 relay endpoint.
- Host diagnostics recorded 57 ping attempts, 57 successful socket sends and
  27 ping responses. The returned state remains 1 (Checking).
- The remote client sends authentication pings, receives state 4
  (SessionTimeout), and never establishes the KCP connection.
- API reports Linked, ready=true, linked=true, no error.
- Local session and user authorization tokens match the API response for both
  participants. Runtime JSON parsing preserves both uint values exactly.
- Host IPv4 observed by STUN remains 179.252.130.209, matching the registered
  host IP. Notebook was registered as 177.174.235.192.
- A separate Python UDP socket on the host, outside Unity/Mirror, sent ten
  documented authentication pings and received five responses, all state 1.
  Packet format: uint32 user token, uint32 session token, byte 1; little endian.
- The local host's successful version handshake is its in-process connection;
  it does not prove relay authentication or remote-client connectivity.

## Conclusion and remaining boundary

The fresh-session follow-up (teste4) reproduced the same behavior: host
57/57/27 ping attempts/sends/replies, state Checking throughout; client receives
SessionTimeout on IPv4. Recreating the session did not resolve the observed
authentication failure. This still does not identify the provider's internal
cause or prove that its protocol contract is fully compatible with this client.

On 2026-10-13, after reboot, a new session (teste5) registered host
179.252.130.209 and notebook 177.174.239.237. Host-only testing again recorded
57/57/27 ping attempts/sends/replies, with state 1/Checking throughout the
first 30 seconds. No remote client was needed to reproduce the blocked host
authentication. Both runtime authorization values matched the credential file.

The current evidence isolates the failure to relay authentication/session
handling or its protocol contract, before remote KCP/game-version negotiation.
It does not establish the provider's internal cause. Adding more Unity update
logs or increasing the KCP timeout would not explain this boundary.

## Next checks

Create one fresh session and immediately test it with the same current builds,
after confirming the two UDP IPv4 addresses. If the fresh session behaves the
same, ask Edgegap to verify relay-side session/user registration and the exact
meaning of persistent Checking on the host alongside SessionTimeout on clients.
Provide the API session ID privately through their support channel if needed;
do not send administrative or authorization tokens.

Protocol reference:
https://github.com/edgegap/distributed-relay-examples#game-server-communication-with-relay
