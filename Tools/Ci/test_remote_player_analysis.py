import importlib.util
import tempfile
from pathlib import Path
import unittest

spec = importlib.util.spec_from_file_location('remote_player', Path(__file__).parents[1] / 'Diagnostics/analyze_remote_player.py')
rp = importlib.util.module_from_spec(spec)
spec.loader.exec_module(rp)

N = 120  # 12 s at 10 Hz


def utc(t):
    return '2026-11-06T12:%02d:%06.3fZ' % (int(t // 60), t % 60)


def row(role, t, x, renderers='Right', local=False, kind='player_visual_sample', net=7, ptime=None, detail=None, delta=0.0):
    d = detail if detail is not None else 'renderers=' + renderers + ('' if ptime is None else ';ptime=%.3f' % ptime)
    return dict(kind=kind, role=role, utc=utc(t), netId=net, localPlayer=local, position=dict(x=x, y=0), delta=dict(x=0, y=delta),
                detail=d, timeline=t, bufferMs=0, _t=rp.parse_utc(utc(t)))


def capture(delay=lambda t: .1, speed=2.0, freeze=None, jump_at=None, renderer=lambda i: 'Right', expected=.1):
    server = [row('server', i / 10, speed * i / 10, renderer(i), ptime=i / 10) for i in range(N)]
    client, held = [], None
    for i in range(N):
        t = i / 10
        x = speed * (t - delay(t))
        if freeze and freeze[0] <= t < freeze[1]:
            held = x if held is None else held
            x = held
        else:
            held = None
        if jump_at is not None and i >= jump_at:
            x += 5
        client.append(row('client', t, x, renderer(i), ptime=t - expected))
    return server, client


class RemotePlayerAnalysisTests(unittest.TestCase):
    def _with_root_rows(self, slew, steps=(), frames=300):
        server, client = capture()
        client.append(row('client', 5.0, 0, kind='root_motion_summary',
                          detail='moving_frames=%d;backsteps_px=0;backsteps_visible=0;max_back=0;slew=%s' % (frames, slew)))
        for i, units in enumerate(steps):
            client.append(row('client', 6.0 + i * .5, 0, kind='root_backstep',
                              detail='units=%.3f;holding=False;slew=%s;ptime=%.3f;mirror=%.3f' % (units, slew, 6 + i * .5, 6 + i * .5)))
        client.append(row('client', 7.0, 0, kind='timeline_slew_end', detail='caught_up;max_gap_ms=120.0;duration_ms=240'))
        client.sort(key=lambda r: r['_t'])
        return rp.analyze(server, dict(c1=client))

    def test_visible_root_back_step_is_a_failure_with_or_without_the_clock(self):
        for slew in ('True', 'False'):
            report = self._with_root_rows(slew, steps=(0.04, 0.32))
            bs = report['observers'][0]['rootBackSteps']
            self.assertEqual(1, bs['visibleSteps']); self.assertEqual(0, bs['pixelSteps'] - 1)
            self.assertEqual('on' if slew == 'True' else 'off', bs['clock'])
            self.assertEqual('failures', report['status'])
            self.assertTrue(any('stepped back' in f for f in report['failures']))

    def test_sub_threshold_root_steps_are_reported_not_failed(self):
        report = self._with_root_rows('True', steps=(0.03,))
        bs = report['observers'][0]['rootBackSteps']
        self.assertEqual((300, 0, 0, 1), (bs['movingFrames'], bs['visibleSteps'], bs['pixelSteps'], bs['holds']['count']))
        self.assertEqual({'caught_up': 1}, bs['holds']['reasons'])
        self.assertEqual(report['status'], 'within-provisional-limits', report['failures'])

    def test_builds_without_per_frame_root_rows_are_not_measured(self):
        server, client = capture()
        report = rp.analyze(server, dict(c1=client))
        self.assertFalse(report['observers'][0]['rootBackSteps']['available'])
        self.assertFalse(any('per-frame root' in n for n in report['coverageNotes']))

    def test_constant_interpolation_delay_is_expected_not_a_fault(self):
        server, client = capture()
        report = rp.analyze(server, dict(c1=client))
        o = report['observers'][0]
        self.assertEqual(100, o['root']['lagMs'])
        self.assertEqual('timeline-ptime', o['expectedLagSource'])
        self.assertEqual(report['status'], 'within-provisional-limits', report['failures'])
        self.assertIn('NOT accepted', report['verdict'])

    def test_stationary_player_is_inconclusive_never_zero_lag(self):
        server, client = capture(speed=0.0)
        report = rp.analyze(server, dict(c1=client))
        self.assertEqual('inconclusive', report['status'])
        self.assertFalse(report['observers'][0]['root']['conclusive'])
        self.assertFalse(report['measuredOk'])

    def test_freeze_while_server_moves_is_a_stall(self):
        server, client = capture(freeze=(5.0, 6.5))
        report = rp.analyze(server, dict(c1=client))
        self.assertEqual('failures', report['status'])
        self.assertTrue(any('stall' in f for f in report['failures']), report['failures'])

    def test_lag_growing_beyond_the_designed_delay_is_flagged(self):
        server, client = capture(delay=lambda t: .1 if t < 6 else .45)
        report = rp.analyze(server, dict(c1=client))
        self.assertEqual('failures', report['status'])
        self.assertTrue(any('drifts' in f or 'exceeds expected' in f for f in report['failures']), report['failures'])

    def test_lag_larger_than_designed_delay_is_flagged(self):
        server, client = capture(delay=lambda t: .5, expected=.1)
        report = rp.analyze(server, dict(c1=client))
        self.assertTrue(any('exceeds expected' in f for f in report['failures']), report['failures'])

    def test_observer_teleport_is_reported(self):
        server, client = capture(jump_at=60)
        report = rp.analyze(server, dict(c1=client))
        self.assertTrue(any('discontinuit' in f or 'dense server reference' in f for f in report['failures']), report['failures'])

    def test_owner_rows_are_not_observer_views(self):
        server, client = capture()
        for r in client: r['localPlayer'] = True
        report = rp.analyze(server, dict(c1=client))
        self.assertEqual('inconclusive', report['status'])
        self.assertFalse(report['observers'])

    def test_pose_state_the_server_never_had_is_rejected(self):
        server, client = capture()
        for r in client[40:80]: r['detail'] = 'renderers=AfkAlt'
        report = rp.analyze(server, dict(c1=client))
        self.assertTrue(any('never had' in f for f in report['failures']), report['failures'])

    def test_pose_transitions_follow_the_server_with_the_same_delay(self):
        pose = lambda i: 'Right' if (i // 20) % 2 == 0 else 'Left'
        server, client = capture(renderer=lambda i: pose(i))
        # the observer shows each pose 1 sample (0.1 s) after the server
        for i, r in enumerate(client): r['detail'] = 'renderers=' + pose(max(0, i - 1)) + ';ptime=0'
        report = rp.analyze(server, dict(c1=client))
        pose_report = report['observers'][0]['pose']
        self.assertTrue(pose_report['conclusive'])
        self.assertLess(abs(pose_report['delayMsMedian'] - 100), 1)

    def test_mount_generation_must_exist_on_the_server(self):
        server, client = capture()
        for i in range(0, N, 5):
            server.append(row('server', i / 10, 0, kind='mount_visual_sample', detail='type=Blue;rev=1'))
            client.append(row('client', i / 10 + .01, 0, kind='mount_visual_sample', detail='type=Blue;rev=1'))
        for i in range(60, N, 5):  # observer shows a generation the server never published
            client.append(row('client', i / 10 + .02, 0, kind='mount_visual_sample', detail='type=Blue;rev=9'))
        report = rp.analyze(sorted(server, key=lambda r: r['_t']), dict(c1=sorted(client, key=lambda r: r['_t'])))
        self.assertTrue(any('generations' in f for f in report['failures']), report['failures'])

    def test_intermediate_mount_revisions_are_tolerated(self):
        server, client = capture()
        for i in range(0, N, 5):
            server.append(row('server', i / 10, 0, kind='mount_visual_sample', detail='type=Blue;rev=2' if i < 60 else 'type=Blue;rev=4'))
            client.append(row('client', i / 10 + .01, 0, kind='mount_visual_sample', detail='type=Blue;rev=3' if i == 40 else 'type=Blue;rev=2' if i < 60 else 'type=Blue;rev=4'))
        report = rp.analyze(sorted(server, key=lambda r: r['_t']), dict(c1=sorted(client, key=lambda r: r['_t'])))
        self.assertFalse(any('generations' in f for f in report['failures']), report['failures'])

    def test_observer_rows_after_server_stopped_are_ignored(self):
        server, client = capture()
        last = client[-1]['timeline']
        for k in range(1, 40):  # observer keeps logging a frozen player after the server is gone
            client.append(row('client', last + k / 10, client[-1]['position']['x'], ptime=last + k / 10 - .1))
        report = rp.analyze(server, dict(c1=client))
        self.assertEqual('within-provisional-limits', report['status'], report['failures'])

    def test_single_sample_state_is_sampling_aliasing(self):
        server, client = capture()
        client[50] = row('client', 5.0, client[50]['position']['x'], 'CarryBombDown', ptime=4.9)
        report = rp.analyze(server, dict(c1=client))
        self.assertFalse(any('never had' in f for f in report['failures']), report['failures'])

    def test_sustained_disagreement_at_same_ptime_is_a_failure(self):
        server, client = capture(renderer=lambda i: 'Right')
        for c in client[40:80]:
            c['detail'] = c['detail'].replace('renderers=Right', 'renderers=Down')
        report = rp.analyze(server, dict(c1=client))
        self.assertTrue(any('same presentation time' in f for f in report['failures']), report['failures'])

    def test_unverifiable_samples_are_counted_not_dropped(self):
        server, client = capture(renderer=lambda i: 'Right' if (i // 7) % 2 == 0 else 'Left')
        client[50]['detail'] = 'renderers=Up;ptime=%.3f' % (5.0 - .1)  # one-sample state
        report = rp.analyze(server, dict(c1=client))
        pres = report['observers'][0]['presentation']
        self.assertGreaterEqual(pres['pose']['singleSample'], 1)
        self.assertTrue(any('single-sample' in n for n in report['coverageNotes']), report['coverageNotes'])

    def test_missing_ptime_makes_the_run_inconclusive(self):
        server, client = capture()
        for r in server:
            r['detail'] = r['detail'].split(';')[0]
        report = rp.analyze(server, dict(c1=client))
        self.assertEqual('inconclusive', report['status'])
        self.assertTrue(any('ptime' in i for i in report['inconclusive']), report['inconclusive'])

    def test_fast_server_excursion_with_dense_log_explains_the_observer_step(self):
        server, client = capture(speed=0.0)
        for lst in (server, client):  # a real fast excursion on both sides, densely logged
            for r in lst:
                if 6.0 <= r['timeline'] < 6.4:
                    r['position']['x'] = 0.0 + 4.0 * (r['timeline'] - 6.0)
                elif r['timeline'] >= 6.4:
                    r['position']['x'] = 1.6
        report = rp.analyze(server, dict(c1=client))
        self.assertFalse(any('dense server reference' in f for f in report['failures']), report['failures'])

    def test_observer_jump_the_dense_server_does_not_show_is_flagged(self):
        server, client = capture(jump_at=60)
        report = rp.analyze(server, dict(c1=client))
        self.assertTrue(any('dense server reference' in f or 'discontinuit' in f for f in report['failures']), report['failures'])

    def test_large_step_inside_a_server_log_gap_is_inconclusive_not_a_pass(self):
        server, client = capture(jump_at=60)
        server = [r for r in server if not (5.8 <= r['timeline'] <= 6.5)]
        report = rp.analyze(server, dict(c1=client))
        self.assertTrue(any('sparse' in i for i in report['inconclusive']) or report['failures'], (report['inconclusive'], report['failures']))
        self.assertNotEqual('within-provisional-limits', report['status'])

    def test_ptime_step_back_while_moving_is_measured_at_the_presented_position(self):
        server = [row('server', i / 20, 2.0 * i / 20, ptime=i / 20) for i in range(240)]
        client = []
        for i in range(120):
            p = i / 10 - .1 - (.2 if i >= 60 else 0)
            client.append(row('client', i / 10, 2.0 * p, ptime=p))
        report = rp.analyze(server, dict(c1=client))
        reg = report['observers'][0]['presentation']['ptimeRegressions']
        self.assertEqual(1, reg['count'])
        self.assertEqual(1, reg['visibleBackSteps'])
        self.assertAlmostEqual(0.2, reg['maxBackStepUnits'], delta=0.05)  # clock went 0.1 s back at 2 u/s (the sample interval is 0.1 s, the shift 0.2 s)
        self.assertTrue(any('stepped back' in c for c in report['candidates']), report['candidates'])

    def test_step_short_of_server_peak_within_sampling_bound_is_borderline(self):
        # Server swings 17 u/s with rows 0.065 s apart; the observer's peak is beyond what linear interpolation of those rows shows.
        server = []
        for i in range(300):
            t = i * .065
            x = 0.0 if t < 4 else (-17 * (t - 4) if t < 4.3 else -17 * .3 + 17 * (t - 4.3))
            server.append(row('server', t, x, ptime=t))
        client = [row('client', 4.0 + i / 10, -v, ptime=4.0 + i / 10) for i, v in enumerate([0.0, 1.7, 4.1, 5.0, 3.4])]
        steps = rp.verify_steps(server, client, dict(rp.DEFAULTS))
        self.assertFalse(steps['unexplained'], steps)
        self.assertTrue(steps['borderline'] or steps['explained'], steps)

    def test_sparse_server_reference_is_counted_not_measured_as_observer_error(self):
        import math
        path = lambda t: 3.0 * math.sin(3 * t)
        server, dropped = [], 0
        for i in range(0, 240):
            t = i / 20
            if 4.0 < t < 4.5:
                dropped += 1
                continue
            r = row('server', t, path(t), ptime=t)
            r['dropped'] = dropped
            server.append(r)
        client = [row('client', i / 10 + .05, path(i / 10 - .05 + .05), ptime=i / 10) for i in range(120)]
        report = rp.analyze(server, dict(c1=client))
        pres = report['observers'][0]['presentation']['positionAtSamePtime']
        self.assertGreater(pres['sparseReference']['n'], 0)
        self.assertEqual(pres['sparseReference']['serverGap'], 0)
        self.assertGreater(pres['sparseReference']['logDropped'], 0)
        self.assertLess(pres['p95'], 0.5)
        self.assertTrue(any('sparser' in n for n in report['coverageNotes']), report['coverageNotes'])

    def test_sparse_observed_entities_are_skipped_not_failed(self):
        server, client = capture()
        extra = [row('client', i / 10, 1, net=11) for i in range(3)]
        report = rp.analyze(server, dict(c1=client + extra))
        self.assertTrue(any('/11' in s for s in report['skipped']))
        self.assertEqual(report['status'], 'within-provisional-limits', report['failures'])

    def test_motion_starting_at_the_edge_of_a_still_span_is_not_a_stall(self):
        # Observer waits 8 s then follows a fast server dash that began slightly earlier.
        server = [row('server', i / 10, 0 if i < 80 else 3 * (i - 80) / 10 * 3) for i in range(N)]
        client = [row('client', i / 10, 0 if i < 82 else 3 * (i - 82) / 10 * 3, ptime=i / 10 - .1) for i in range(N)]
        report = rp.analyze(server, dict(c1=client))
        self.assertFalse(any('stall' in f for f in report['failures']), report['failures'])

    def test_riding_arc_samples_are_excluded_from_root_statistics(self):
        server, client = capture()
        for r in client[50:70]:
            r['position']['x'] += 6  # approved arc moves the observer root; not an R01 locomotion fault
        for i in range(50, 70):
            marker = dict(kind='mount_visual_sample', role='client', utc=utc(i / 10), netId=7, localPlayer=False,
                          detail='type=Blue;riding=True;rev=1', delta=dict(x=0, y=0), _t=rp.parse_utc(utc(i / 10)))
            client.append(marker)
        client.sort(key=lambda r: r['_t'])
        report = rp.analyze(server, dict(c1=client))
        self.assertGreater(report['observers'][0]['arcSamplesExcluded'], 0)
        self.assertFalse(any('discontinuit' in f for f in report['failures']), report['failures'])

    def test_empty_renderer_set_is_not_a_new_state(self):
        server, client = capture()
        for r in client[30:36]: r['detail'] = 'renderers=;ptime=0'
        report = rp.analyze(server, dict(c1=client))
        self.assertFalse(any('never had' in f for f in report['failures']), report['failures'])

    def test_truncated_last_line_is_ignored(self):
        with tempfile.TemporaryDirectory() as d:
            p = Path(d) / 'x.jsonl'
            p.write_text('{"kind":"a","utc":"2026-11-06T12:00:00.1234567Z"}\n{"kind":"b","ut', encoding='utf-8')
            self.assertEqual(1, len(rp.load(p)))


if __name__ == '__main__':
    unittest.main()
