import importlib.util
from pathlib import Path
import unittest

spec = importlib.util.spec_from_file_location('launcher_smoke', Path(__file__).parents[1] / 'RoomDirectory/validate_launcher_smoke.py')
smoke = importlib.util.module_from_spec(spec)
spec.loader.exec_module(smoke)


class LauncherSmokeTests(unittest.TestCase):
    def rows(self):
        rows = []
        for player, owner in ((1, True), (2, False)):
            for kind, time, phase in (('begin', 1, 0), ('sample', 1.1, 100), ('sample', 1.2, 200), ('end', 2, 800)):
                rows.append(dict(kind='launch_recipe_' + kind, realtime=time, durationMs=phase,
                    localPlayer=owner, player=player, detail='sequence=1;approved_curve;kind=Cannon'))
            rows.append(dict(kind='motion_sample', realtime=3, localPlayer=owner, player=player, detail='prediction_block=none mounted=False'))
        return rows

    def test_both_roles_must_present_and_regain_movement(self):
        result = smoke.validate_launcher_capture(self.rows(), 'cannon')
        self.assertEqual([1], result['owner']['players'])
        self.assertEqual([2], result['observer']['players'])

    def test_rope_requires_both_roles_and_unlocked_handoff(self):
        rows = self.rows()
        for row in rows: row['detail'] = row['detail'].replace('kind=Cannon', 'kind=Rope')
        with self.assertRaises(ValueError): smoke.validate_launcher_capture(rows, 'rope')
        rows += [dict(row, realtime=row['realtime'] + 4, detail=row['detail'].replace('sequence=1', 'sequence=2'))
                 for row in rows if row['player'] == 1]
        with self.assertRaises(ValueError): smoke.validate_launcher_capture(rows, 'rope')
        endpoints = [dict(row, kind='launch_recipe_endpoint', durationMs=20 if 'sequence=2' in row['detail'] else 800)
                     for row in rows if row['kind'] == 'launch_recipe_end']
        rows += endpoints
        result = smoke.validate_launcher_capture(rows, 'rope')
        self.assertEqual(2, result['observer']['continuousSamples'])

    def test_owner_only_does_not_validate_the_other_client_view(self):
        with self.assertRaises(ValueError): smoke.validate_launcher_capture([r for r in self.rows() if r['localPlayer']], 'cannon')

    def test_rewinding_phase_fails_even_with_successful_server_checkpoints(self):
        rows = self.rows(); rows[2]['durationMs'] = 80
        with self.assertRaises(ValueError): smoke.validate_launcher_capture(rows, 'cannon')

    def test_frozen_phase_or_locked_finish_cannot_pass(self):
        for frozen in (True, False):
            rows = self.rows()
            if frozen: rows[2]['durationMs'] = 100
            else: rows = [r for r in rows if r['kind'] != 'motion_sample']
            with self.assertRaises(ValueError): smoke.validate_launcher_capture(rows, 'cannon')
