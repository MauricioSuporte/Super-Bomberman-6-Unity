import unittest
from Tools.Diagnostics.analyze_bosses import pose_steps


def sample(kind, time, sprite, visible='1', paused='0'):
    return dict(kind=kind, detail=f'id=1;ptime={time};sprite={sprite};visible={visible};paused={paused}')


class BossPoseTests(unittest.TestCase):
    def test_held_pose_remains_valid_after_neighborhood_radius(self):
        native = [sample('r06_native', 1, 10), sample('r06_native', 1.1, 11)]
        result = pose_steps(native, [sample('r06_view', 1.08, 10)])['1']
        self.assertEqual((result['compared'], result['mismatches']), (1, 0))

    def test_future_pose_is_a_real_mismatch(self):
        native = [sample('r06_native', 1, 10), sample('r06_native', 1.1, 11)]
        self.assertEqual(pose_steps(native, [sample('r06_view', 1.08, 11)])['1']['mismatches'], 1)

    def test_edge_selects_new_pose_exactly(self):
        native = [sample('r06_native', 1, 10), sample('r06_native', 1.1, 11), sample('r06_native', 1.2, 12)]
        self.assertEqual(pose_steps(native, [sample('r06_view', 1.1, 11)])['1']['mismatches'], 0)

    def test_missing_reference_is_not_a_pass(self):
        result = pose_steps([], [sample('r06_view', 1, 10)])['1']
        self.assertEqual((result['compared'], result['outsideReference']), (0, 1))

    def test_sparse_reference_is_explicitly_unmeasured(self):
        native = [sample('r06_native', 1, 10), sample('r06_native', 2, 11)]
        result = pose_steps(native, [sample('r06_view', 1.5, 10)])['1']
        self.assertEqual((result['compared'], result['referenceGaps']), (0, 1))

    def test_pause_is_not_scored_as_motion(self):
        native = [sample('r06_native', 1, 10, paused='1'), sample('r06_native', 1.1, 11)]
        result = pose_steps(native, [sample('r06_view', 1.05, 11)])['1']
        self.assertEqual((result['compared'], result['pauseOrHidden']), (0, 1))

    def test_manual_delivery_hold_is_explicit_not_wrong_confirmed_pose(self):
        native = [sample('r06_native', 1, 10), sample('r06_native', 1.1, 11), sample('r06_native', 1.2, 11)]
        observed = sample('r06_view', 1.15, 10)
        observed['detail'] += ';state=1;newest=1;animating=0'
        result = pose_steps(native, [observed])['1']
        self.assertEqual((result['compared'], result['mismatches'], result['deliveryPoseHolds']), (1, 0, 1))
        self.assertAlmostEqual(result['deliveryHoldMaxMs'], 150)

    def test_guessing_future_pose_during_delivery_hold_is_wrong(self):
        native = [sample('r06_native', 1, 10), sample('r06_native', 1.1, 11), sample('r06_native', 1.2, 11)]
        observed = sample('r06_view', 1.15, 11)
        observed['detail'] += ';state=1;newest=1;animating=0'
        self.assertEqual(pose_steps(native, [observed])['1']['mismatches'], 1)
