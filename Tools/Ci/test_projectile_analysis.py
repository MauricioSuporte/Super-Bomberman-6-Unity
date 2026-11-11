import unittest
from Tools.Diagnostics.analyze_projectiles import compare, compare_offline


def sample(kind, time, x, sprite='a', visible=1, paused=0, entity=3):
    return dict(kind=kind, position=dict(x=x, y=0), detail=f'id={entity};family=StarProjectile;ptime={time};sprite={sprite};visible={visible};paused={paused}')


class ProjectileAnalysisTests(unittest.TestCase):
    def test_linear_authoritative_path_is_compared_at_presentation_time(self):
        native = [sample('r05_native', i/20, i/20) for i in range(10)]
        result = compare(native, [sample('r05_view', .125, .125), sample('r05_view', .225, .225)])
        self.assertEqual('PASSED', result['status'])
        self.assertAlmostEqual(0, result['families']['StarProjectile']['positionMax'])

    def test_missing_or_shifted_projectile_fails(self):
        native = [sample('r05_native', i/20, i/20) for i in range(10)]
        self.assertEqual('FAILED', compare(native, [])['status'])
        self.assertEqual('FAILED', compare(native, [sample('r05_view', .125, 2)])['status'])

    def test_stationary_is_not_reported_as_motion(self):
        native = [sample('r05_native', i/20, 0) for i in range(10)]
        report = compare(native, [sample('r05_view', .125, 0)])
        self.assertEqual(0, report['families']['StarProjectile']['movingEntities'])

    def test_unknown_sprite_fails(self):
        native = [sample('r05_native', i/20, 0) for i in range(10)]
        self.assertEqual('FAILED', compare(native, [sample('r05_view', .125, 0, sprite='wrong')])['status'])

    def test_reference_gaps_are_explicit_and_not_passed(self):
        native = [sample('r05_native', 0, 0), sample('r05_native', 1, 1)]
        report = compare(native, [sample('r05_view', .5, .5)])
        self.assertNotEqual('PASSED', report['status'])
        self.assertEqual(1, report['families']['StarProjectile']['outsideReference'])

    def test_paused_pose_is_not_compared_to_advancing_clock(self):
        native = [sample('r05_native', i/20, i/20) for i in range(10)]
        report = compare(native, [sample('r05_view', .125, 0, paused=1)])
        self.assertEqual('PASSED', report['status'])
        self.assertEqual(1, report['families']['StarProjectile']['pausedSamples'])

    def test_no_projectile_is_inconclusive(self):
        self.assertEqual('INCONCLUSIVE', compare([], [])['status'])

    def test_last_native_frame_boundary_proves_presence_without_inventing_a_reference(self):
        native = [sample('r05_native', i/20, 0) for i in range(4)]
        report = compare(native, [sample('r05_view', .1504, 0)])
        self.assertEqual('PASSED', report['status'])
        self.assertEqual(1, report['families']['StarProjectile']['visibleSamples'])
        self.assertIsNone(report['families']['StarProjectile']['positionMax'])

    def test_hidden_observer_cannot_count_as_visible_at_the_last_native_frame(self):
        native = [sample('r05_native', i/20, 0) for i in range(4)]
        report = compare(native, [sample('r05_view', .1504, 0, visible=0)])
        self.assertEqual('FAILED', report['status'])

    def test_finished_autonomous_shot_before_scene_presentation_is_not_replayed(self):
        native = [sample('r05_native', i/20, 0) for i in range(4)]
        native += [sample('r05_native', .6+i/20, 0, entity=4) for i in range(4)]
        observer = [dict(kind='r05_epoch_begin', detail='ptime=0.5'), sample('r05_view', .675, 0, entity=4),
                    dict(kind='r05_epoch_end', detail='ptime=1')]
        report = compare(native, observer)
        self.assertEqual('PASSED', report['status'])
        self.assertEqual([3], report['outsideObservationEpoch'])

    def test_controlled_fixture_cannot_be_excused_by_a_late_observer(self):
        native = [sample('r05_native', i/20, 0) for i in range(4)]
        for row in native:
            row['detail'] += ';case=controlled'
        native += [sample('r05_native', .6+i/20, 0, entity=4) for i in range(4)]
        observer = [dict(kind='r05_epoch_begin', detail='ptime=0.5'), sample('r05_view', .675, 0, entity=4),
                    dict(kind='r05_epoch_end', detail='ptime=1')]
        self.assertEqual('FAILED', compare(native, observer)['status'])

    def test_missing_autonomous_shot_inside_presented_scene_still_fails(self):
        native = [sample('r05_native', .6+i/20, 0) for i in range(4)]
        observer = [dict(kind='r05_epoch_begin', detail='ptime=0.5'), dict(kind='r05_epoch_end', detail='ptime=1')]
        self.assertEqual('FAILED', compare(native, observer)['status'])

    def test_held_approved_state_is_not_compared_to_an_unreceived_future(self):
        native = [sample('r05_native', i/20, i/20) for i in range(10)]
        view = sample('r05_view', .3, .1)
        view['detail'] += ';state=0.1;newest=0.1;stateMeasured=1'
        report = compare(native, [view])
        self.assertEqual('PASSED', report['status'])
        self.assertAlmostEqual(0, report['families']['StarProjectile']['positionMax'])
        self.assertAlmostEqual(200, report['families']['StarProjectile']['starvedMaxMs'])

    def test_unknown_automatic_clip_transition_is_retained_as_a_candidate(self):
        native = [sample('r05_native', i/20, i/20, sprite='a' if i < 4 else 'b') for i in range(10)]
        view = sample('r05_view', .3, .1)
        view['detail'] += ';state=0.1;newest=0.1;stateMeasured=1;animating=1'
        report = compare(native, [view])
        self.assertEqual('PASSED', report['status'])
        self.assertEqual(1, len(report['candidates']))

    def test_paused_visual_cannot_follow_a_buffered_root(self):
        native = [sample('r05_native', i/20, 0) for i in range(10)]
        frozen = [sample('r05_view', .125, 0, paused=1), sample('r05_view', .225, 0, paused=1)]
        self.assertEqual('PASSED', compare(native, frozen)['status'])
        frozen[-1]['position']['x'] = .48
        report = compare(native, frozen)
        self.assertEqual('FAILED', report['status'])
        self.assertAlmostEqual(.48, report['families']['StarProjectile']['pauseDriftMax'])

    def test_offline_matching_uses_controlled_cases_and_retains_missing_cases(self):
        def fixture(time, x, label):
            row = sample('r05_native', time, x)
            row['detail'] += ';case=' + label
            return row
        online = [fixture(i/20, i/20, 'scene/0/Star/0') for i in range(10)]
        offline = [fixture(i/20, i/10, 'scene/0/Star/0') for i in range(10)]
        offline += [fixture(0, 0, 'scene/1/Star/0')]
        report = compare_offline(online, offline)
        self.assertEqual('MEASURED', report['status'])
        self.assertEqual(1, report['matchedCases'])
        self.assertEqual(['scene/1/Star/0'], report['offlineOnly'])
        self.assertAlmostEqual(-1, report['cases']['scene/0/Star/0']['speedDifference'])

    def test_autonomous_cases_do_not_invent_offline_parity(self):
        rows = [sample('r05_native', i/20, i/20) for i in range(10)]
        self.assertEqual('INCONCLUSIVE', compare_offline(rows, rows)['status'])

    def test_authority_pause_arrives_later_at_observer_and_is_not_motion_time(self):
        native = [sample('r05_native', i/20, 0, paused=1) for i in range(10)]
        report = compare(native, [sample('r05_view', .125, .16)])
        family = report['families']['StarProjectile']
        self.assertEqual(1, family['pauseTransitionSamples'])
        self.assertEqual(0, family['positionSamples'])
        self.assertIsNone(family['positionP95'])
        self.assertIsNone(family['holdMaxMs'])

    def test_moving_delivery_hold_is_separate_from_legitimate_stationary_hold(self):
        native = [sample('r05_native', i/20, i/20) for i in range(10)]
        observed = sample('r05_view', .35, .125)
        observed['detail'] += ';state=.125;stateMeasured=1;newest=.125'
        result = compare(native, [observed])['families']['StarProjectile']
        self.assertEqual(1, result['movingHoldSamples'])
        self.assertAlmostEqual(225, result['movingHoldMaxMs'])
        for r in native:r['position']['x'] = .125
        result = compare(native, [observed])['families']['StarProjectile']
        self.assertEqual(0, result['movingHoldSamples'])

    def test_moving_hold_is_reported_even_when_old_state_has_a_reference_gap(self):
        native = [sample('r05_native', t, t) for t in [0, .2, .25, .3, .35, .4]]
        observed = sample('r05_view', .35, .1)
        observed['detail'] += ';state=.1;stateMeasured=1;newest=.1'
        family = compare(native, [observed])['families']['StarProjectile']
        self.assertEqual(0, family['positionSamples'])
        self.assertEqual(1, family['movingHoldSamples'])
        self.assertAlmostEqual(250, family['movingHoldMaxMs'])

    def test_waiting_for_resume_sample_does_not_count_entire_pause_as_moving_hold(self):
        native = [sample('r05_native', t, x, paused=p) for t,x,p in [(0,0,0),(.05,0,1),(.1,0,1),(.15,0,0),(.2,1,0),(.25,2,0)]]
        observed = sample('r05_view', .225, 0)
        observed['detail'] += ';state=.075;stateMeasured=1;newest=.075'
        family = compare(native,[observed])['families']['StarProjectile']
        self.assertEqual(0,family['movingHoldSamples'])
        self.assertEqual(1,family['pauseHistoryHoldSamples'])
