import unittest
from Tools.Diagnostics.analyze_bomb_abilities import inspect_action, inspect_capture


class BombAbilityAnalysisTests(unittest.TestCase):
    def action(self, kind, detail='', bomb=5):
        return dict(kind=kind, detail=detail, bombNetId=bomb)

    def test_native_and_observer_samples_do_not_prove_owner_prediction(self):
        rows=[self.action('bomb_motion_apply'), self.action('bomb_motion_sample')]
        report=inspect_action(rows,5,('Lift','Throw'),True)
        self.assertEqual(4,len(report['problems']))
        self.assertFalse(inspect_action(rows,5,('Lift','Throw'),False)['problems'])

    def test_lift_is_not_proof_of_throw_prediction(self):
        rows=[self.action('bomb_motion_prediction_begin','Lift'),self.action('bomb_motion_prediction_confirm','kind=Lift;lead=0'),
              self.action('bomb_motion_apply'),self.action('bomb_motion_sample')]
        report=inspect_action(rows,5,('Lift','Throw'),True)
        self.assertEqual(['missing owner prediction: Throw','missing owner confirmation: Throw'],report['problems'])

    def test_kick_uses_its_existing_diagnostic_names(self):
        rows=[self.action(k) for k in ('kick_prediction_begin','kick_prediction_confirm','kick_segment_apply','kick_presentation_sample')]
        self.assertFalse(inspect_action(rows,5,('Kick',),True)['problems'])
        self.assertIn('observer predicted another player action',inspect_action(rows,5,('Kick',),False)['problems'])

    def test_other_bomb_and_rejection_cannot_count_as_confirmation(self):
        rows=[self.action('bomb_motion_prediction_begin','Punch'),self.action('bomb_motion_prediction_confirm','kind=Punch',6),
              self.action('bomb_motion_prediction_reject','ack;kind=Punch'),self.action('bomb_motion_apply'),self.action('bomb_motion_sample')]
        report=inspect_action(rows,5,('Punch',),True)
        self.assertIn('missing owner confirmation: Punch',report['problems'])
        self.assertIn('legitimate fixture action was rejected',report['problems'])

    def test_truncated_dropped_or_empty_measurement_never_passes(self):
        start=dict(kind='capture_start')
        self.assertTrue(inspect_capture([start]))
        for rows in ([],[dict(kind='motion_sample')],[start,dict(kind='capture_limit')],[start,dict(kind='motion_sample',dropped=1)]):
            self.assertFalse(inspect_capture(rows))


if __name__=='__main__':
    unittest.main()
