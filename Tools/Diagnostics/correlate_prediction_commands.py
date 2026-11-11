"""Correlate directional response origins; never infer transit latency from UTC."""
import re


def identity(row):
    return (row.get("player"), row.get("netId"), row.get("inputTick"), row.get("held", 0) & 15)


def correlate(server_rows, client_rows, response_rows=None):
    received = {identity(row) for row in server_rows if row.get("kind") == "input_receive"}
    sent = set()
    responses = []
    for row in client_rows:
        if row.get("role") != "client": continue
        if row.get("kind") == "input_send": sent.add(identity(row))
        if row.get("kind") == "input_to_motion": responses.append(row)
    if response_rows is not None:
        responses = [row for row in response_rows if row.get("role") == "client" and row.get("kind") == "input_to_motion"]
    matched = unknown = 0
    unmatched = []
    for row in responses:
        marker = re.search(r";command_tick=(\d+)\b", row.get("detail", ""))
        if not marker:
            unknown += 1; continue
        key = identity(row)
        valid_tick = int(marker[1]) == row.get("inputTick")
        if valid_tick and key in sent and key in received:
            matched += 1
        else:
            unmatched.append({"player": key[0], "netId": key[1], "tick": key[2], "directionMask": key[3],
                              "markerMatchesTick": valid_tick, "sendSeen": key in sent, "receiveSeen": key in received})
    return {"responses": len(responses), "matchedSendReceiveResponse": matched,
            "unknownOriginTick": unknown, "unmatched": unmatched,
            "scope": "same netId/player/original command tick/directional mask; receipt is not proof that this exact tick was simulated; no UTC latency inference"}
