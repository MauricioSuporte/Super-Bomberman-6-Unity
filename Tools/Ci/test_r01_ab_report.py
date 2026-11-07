import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "Diagnostics"))
import r01_ab_report as ab  # noqa: E402


def step_row(ptime, pprev, before, after, units=0.3):
    return {"kind": "root_backstep", "detail": f"units={units};holding=False;slew=True;ptime={ptime};pprev={pprev};mirror={ptime}",
            "position": {"x": after[0], "y": after[1]}, "delta": {"x": after[0] - before[0], "y": after[1] - before[1]}}


class R01AbReportTests(unittest.TestCase):
    # Dense server track (one row per frame): walks down to -4.125 across a 0.184 s stall (frames 3 -> 4), then back up.
    track = [(31.950, -7.0, -3.312, 1), (31.967, -7.0, -3.188, 2), (31.983, -7.0, -3.250, 3), (32.167, -7.0, -4.125, 4),
             (32.184, -7.0, -4.062, 5), (32.201, -7.0, -3.938, 6), (32.217, -7.0, -3.938, 7), (32.234, -7.0, -3.875, 8)]
    keys = [t[0] for t in track]

    def test_stale_root_catching_up_through_the_server_reversal_is_path_forward(self):
        # Observed in locomotion_on1: root stuck before the stall, then -3.765 -> -4.097 while the server already turned up.
        self.assertEqual("path_forward", ab.classify(step_row(32.188, 32.171, (-7.0, -3.765), (-7.0, -4.097)), self.track, self.keys))

    def test_returning_to_an_earlier_authoritative_state_is_presentation(self):
        # On the way back up the root goes from -3.9 down to -4.0 again: an earlier state, a real back-step.
        self.assertEqual("presentation", ab.classify(step_row(32.226, 32.209, (-7.0, -3.938), (-7.0, -4.06)), self.track, self.keys))

    def test_a_state_the_server_never_had_is_off_path(self):
        self.assertEqual("off_path", ab.classify(step_row(32.2, 32.18, (-7.0, -4.06), (-6.5, -4.0)), self.track, self.keys))

    def test_capture_gap_is_unverifiable_and_old_rows_are_unclassified(self):
        gappy = [(10.00, 0.0, 0.0, 1), (10.30, 0.5, 0.0, 9)]
        self.assertEqual("unverifiable", ab.classify(step_row(10.2, 10.1, (0.3, 0.0), (0.1, 0.0)), gappy, [t[0] for t in gappy]))
        row = step_row(32.188, 32.171, (-7.0, -3.765), (-7.0, -4.097)); row["detail"] = row["detail"].replace(";pprev=32.171", "")
        self.assertEqual("unclassified", ab.classify(row, self.track, self.keys))

    def test_server_reversal_within_two_frames_is_ambiguous(self):
        # locomotion_on2 90.885: the server went 1.125 -> 0.812 -> 1.062 in two frames; either reading of 1.052 -> 0.841 fits.
        track = [(90.831, 1.438, 2.0, 4978), (90.848, 1.125, 2.0, 4979), (90.864, 0.812, 2.0, 4980), (90.881, 1.062, 2.0, 4981), (90.898, 1.062, 2.0, 4982)]
        self.assertEqual("ambiguous_reversal", ab.classify(step_row(90.885, 90.868, (1.052, 2.0), (0.841, 2.0)), track, [x[0] for x in track]))

    def test_capture_gaps_are_told_from_server_simulation_gaps(self):
        self.assertEqual((0, 1), ab.capture_gaps(self.track))
        self.assertEqual((1, 0), ab.capture_gaps([(1.0, 0, 0, 10), (1.5, 0, 0, 30)]))


if __name__ == "__main__":
    unittest.main()
