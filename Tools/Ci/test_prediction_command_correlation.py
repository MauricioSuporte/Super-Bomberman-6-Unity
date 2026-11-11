import importlib.util
from pathlib import Path
import unittest

spec = importlib.util.spec_from_file_location("prediction_commands", Path(__file__).parents[1] / "Diagnostics/correlate_prediction_commands.py")
commands = importlib.util.module_from_spec(spec)
spec.loader.exec_module(commands)


class PredictionCommandTests(unittest.TestCase):
    def row(self, kind, tick=5, net_id=10, detail=";command_tick=5"):
        return dict(kind=kind, role="server" if kind=="input_receive" else "client",
                    player=2, netId=net_id, inputTick=tick, held=8, detail=detail)

    def test_matches_response_origin_instead_of_latest_processed_tick(self):
        server=[self.row("input_receive")]
        client=[self.row("input_send"),self.row("input_to_motion")]
        self.assertEqual(1, commands.correlate(server,client)["matchedSendReceiveResponse"])
        self.assertEqual(0, commands.correlate([self.row("input_receive",tick=4)],client)["matchedSendReceiveResponse"])

    def test_wrong_actor_or_unknown_tick_cannot_be_guessed(self):
        client=[self.row("input_send"),self.row("input_to_motion"),self.row("input_to_motion",detail=";command_tick=unknown")]
        report=commands.correlate([self.row("input_receive",net_id=11)],client)
        self.assertEqual(0,report["matchedSendReceiveResponse"])
        self.assertEqual(1,report["unknownOriginTick"])
        self.assertEqual(1,len(report["unmatched"]))

    def test_window_selection_does_not_drop_a_send_just_before_window(self):
        report=commands.correlate([self.row("input_receive")],[self.row("input_send")], [self.row("input_to_motion")])
        self.assertEqual(1,report["matchedSendReceiveResponse"])
