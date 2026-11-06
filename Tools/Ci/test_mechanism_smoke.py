import importlib.util
from pathlib import Path
import unittest

spec = importlib.util.spec_from_file_location('mechanism_smoke', Path(__file__).parents[1] / 'RoomDirectory/validate_mechanism_smoke.py')
smoke = importlib.util.module_from_spec(spec)
spec.loader.exec_module(smoke)


def captures(kind):
    return [dict(kind='mechanism_capture', detail=f'fixture={kind};phase={p};explosions=0') for p in (1,2)]


class MechanismSmokeTests(unittest.TestCase):
    def test_boiler_requires_actual_motion_and_door_frames(self):
        expected = [dict(bombNetId=3, bombPosition=dict(x=0,y=0), position=dict(x=0,y=1))]
        rows = captures('boiler') + [dict(kind='bomb_motion_sample', bombNetId=3, bombPosition=dict(x=0,y=y)) for y in (0,.5,1)]
        rows.append(dict(kind='tile_animation_apply'))
        self.assertTrue(smoke.validate_mechanism_capture(rows,'boiler',expected)['approvedPresentation'])
        with self.assertRaises(ValueError): smoke.validate_mechanism_capture(rows[:-1],'boiler',expected)
        with self.assertRaises(ValueError): smoke.validate_mechanism_capture([r for r in rows if r.get('bombPosition',{}).get('y')!=1],'boiler',expected)

    def test_blackout_requires_both_states_and_supported_world_masks(self):
        rows = captures('world-blackout') + [dict(kind='mechanism_blackout', detail=f'active={active};alpha={alpha}') for active,alpha in ((True,.92),(False,0))]
        rows.append(dict(kind='mechanism_materials',detail='masked=3;unsupported=0'))
        self.assertTrue(smoke.validate_mechanism_capture(rows,'world-blackout',[])['phases'])
        with self.assertRaises(ValueError): smoke.validate_mechanism_capture(rows[:-1],'world-blackout',[])
        with self.assertRaises(ValueError): smoke.validate_mechanism_capture(rows+[dict(kind='mechanism_materials',detail='masked=3;unsupported=1')],'world-blackout',[])

    def test_rocket_requires_native_ascent_descent_impact_and_target(self):
        rows = captures('rocket') + [dict(kind='mechanism_view',detail=f'visible=True;sprite=Rocket_{frame};shader=Sprites/Default') for frame in (2,8,12)]
        rows.append(dict(kind='mechanism_view',detail='visible=True;sprite=Rocket Target_0;shader=Sprites/Default'))
        self.assertTrue(smoke.validate_mechanism_capture(rows,'rocket',[])['approvedPresentation'])
        with self.assertRaises(ValueError): smoke.validate_mechanism_capture(rows[:-1],'rocket',[])
        with self.assertRaises(ValueError): smoke.validate_mechanism_capture([r for r in rows if 'Rocket_12;' not in r.get('detail','')],'rocket',[])

    def test_dynamite_requires_client_effect_and_completion_phase(self):
        rows = captures('dynamite');rows[0]['detail']='fixture=dynamite;phase=1;explosions=4'
        self.assertTrue(smoke.validate_mechanism_capture(rows,'dynamite',[])['approvedPresentation'])
        with self.assertRaises(ValueError): smoke.validate_mechanism_capture(rows[:1],'dynamite',[])
        with self.assertRaises(ValueError): smoke.validate_mechanism_capture(captures('dynamite'),'dynamite',[])


    def test_sudden_death_requires_actual_client_fall_pause_and_approved_commit(self):
        rows=captures('sudden')+[dict(kind='sudden_death_present',detail='falls=2;committed=0'),dict(kind='mechanism_sudden_pause',detail='paused=True'),dict(kind='sudden_death_commit_apply',detail='cell=1,2')]
        self.assertTrue(smoke.validate_mechanism_capture(rows,'sudden',[dict(detail='cell=1,2,0')])['approvedPresentation'])
        for missing in ('sudden_death_present','mechanism_sudden_pause','sudden_death_commit_apply'):
            with self.assertRaises(ValueError):smoke.validate_mechanism_capture([r for r in rows if r['kind']!=missing],'sudden',[dict(detail='cell=1,2,0')])

    def test_roof_powder_and_falling_require_both_native_phases(self):
        for kind,event,details in [('roof','mechanism_roof',['tile=0','tile=-1']),('powder','mechanism_powder',['frame=0','frame=-1']),('falling','mechanism_target',['active=True;bombs=0','active=False;bombs=1'])]:
            rows=captures(kind)+[dict(kind=event,detail=d) for d in details]
            self.assertTrue(smoke.validate_mechanism_capture(rows,kind,[])['approvedPresentation'])
            with self.assertRaises(ValueError):smoke.validate_mechanism_capture(rows[:-1],kind,[])
