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


class MinecartSmokeTests(unittest.TestCase):
    def rows(self):
        rows = []
        for player, owner in ((1, True), (2, False)):
            for index, phase in enumerate(('Enter', 'Rail', 'Rail', 'Jump', 'PortalSink', 'PortalRise', 'Exit')):
                rows.append(dict(kind='minecart_present', realtime=1+index*.1, player=player,
                    localPlayer=owner, position=dict(x=index,y=0), detail='cart=Cart;generation=1;phase='+phase+';segment=1'))
            rows.append(dict(kind='minecart_end', realtime=2, player=player, localPlayer=owner))
            rows.append(dict(kind='motion_sample', realtime=3, player=player, localPlayer=owner, detail='prediction_block=none mounted=False'))
        return rows

    def test_cart_requires_both_passengers_and_unlocked_handoff(self):
        for kind in ('cart9', 'cart12', 'cart32'):
            self.assertEqual(7, smoke.validate_launcher_capture(self.rows(), kind)['observer']['samples'])
        for missing in ('minecart_end', 'motion_sample'):
            with self.assertRaises(ValueError): smoke.validate_launcher_capture([r for r in self.rows() if r['kind'] != missing], 'cart9')
        with self.assertRaises(ValueError): smoke.validate_launcher_capture([r for r in self.rows() if r['localPlayer']], 'cart9')

    def test_specialized_cart_cannot_pass_without_portal_or_jump(self):
        for kind, phase in (('cart12', 'PortalRise'), ('cart32', 'Jump')):
            with self.assertRaises(ValueError):
                smoke.validate_launcher_capture([r for r in self.rows() if 'phase='+phase not in r.get('detail', '')], kind)

    def test_cart_phase_labels_alone_cannot_validate_frozen_passenger(self):
        rows = self.rows()
        for row in rows: row['position'] = dict(x=0, y=0)
        with self.assertRaises(ValueError): smoke.validate_launcher_capture(rows, 'cart9')


class PortalSmokeTests(unittest.TestCase):
    def test_portals_require_both_roles_destination_and_control(self):
        expected = [dict(player=p, bombPosition=dict(x=0,y=0), position=dict(x=8,y=0)) for p in (1,2)]
        rows = [dict(kind='motion_sample', player=p, localPlayer=owner, realtime=t, position=dict(x=x,y=0), detail='prediction_block=none mounted=False')
                for p,owner in ((1,True),(2,False)) for t,x in ((1,0),(2,8),(3,8))]
        rows += [dict(r, kind='portal_pose_sample') for r in rows]
        self.assertEqual([2], smoke.validate_launcher_capture(rows, 'portal7', expected)['observer']['players'])
        for bad in ([r for r in rows if r.get('kind') != 'portal_pose_sample'], [r for r in rows if r['localPlayer']], [r for r in rows if r['position']['x']==0],
                    [dict(r,detail='prediction_block=server_authoritative ') for r in rows]):
            with self.assertRaises(ValueError): smoke.validate_launcher_capture(bad, 'portal7', expected)

    def test_hidden_portal_approved_entry_still_requires_rendered_arrival(self):
        expected = [dict(player=p, bombPosition=dict(x=0,y=0), position=dict(x=8,y=0)) for p in (1,2)]
        rows = [dict(kind=kind, player=p, localPlayer=owner, realtime=t, position=dict(x=x,y=0), detail='prediction_block=none ')
                for p,owner in ((1,True),(2,False)) for kind,t,x in (('authority_motion_begin',1,0),('portal_pose_sample',2,8),('motion_sample',3,8))]
        rows.append(dict(rows[3], kind='portal_pose_sample'))
        self.assertTrue(smoke.validate_launcher_capture(rows, 'portal7', expected)['owner']['approvedEndpoints'])
        with self.assertRaises(ValueError):
            smoke.validate_launcher_capture([r for r in rows if r['kind'] != 'portal_pose_sample' or r['position']['x'] == 0], 'portal7', expected)
