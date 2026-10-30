import importlib.util
from pathlib import Path
import unittest

spec = importlib.util.spec_from_file_location('mount_visual_smoke', Path(__file__).parents[1] / 'RoomDirectory/validate_mount_visual_smoke.py')
smoke = importlib.util.module_from_spec(spec)
spec.loader.exec_module(smoke)


class MountVisualSmokeTests(unittest.TestCase):
    def rows(self):
        rows = []
        for owner in (True, False):
            rows.append(dict(kind='mount_visual_sample', localPlayer=owner, detail='type=Blue;riding=True;mount=Down;body=MountUp'))
        for mount in ('Mole', 'Tank'):
            rows += [dict(kind='mount_visual_sample', detail='type='+mount+';body=HeadOnlyDown', delta=dict(x=0, y=1.3)) for _ in range(2)]
        for mount, renderer in (('Blue', 'Death'), ('Pink', 'DeathByExplosion')):
            for frame in (0, 1): rows.append(dict(kind='mount_death_visual', detail=f'type={mount};renderer={renderer};frame={frame}'))
        rows += [dict(kind='player_visual_sample', localPlayer=True, accepted=True, detail='renderers=Right') for _ in range(4)]
        return rows

    def test_requires_both_views_and_advancing_death_frames(self):
        self.assertEqual([0, 1], smoke.validate_mount_visual_capture(self.rows())['Pink']['deathFrames'])

    def test_a_mounted_state_without_visible_ground_mount_is_insufficient(self):
        rows = self.rows(); rows[1]['detail'] = 'type=Blue;riding=True;mount=;body=MountUp'
        with self.assertRaises(ValueError): smoke.validate_mount_visual_capture(rows)

    def test_static_or_wrong_death_variant_is_rejected(self):
        for static in (True, False):
            rows = self.rows()
            for row in rows:
                if row['kind'] == 'mount_death_visual' and 'type=Pink' in row['detail']:
                    row['detail'] = row['detail'].replace('frame=1', 'frame=0') if static else row['detail'].replace('DeathByExplosion', 'Death')
            with self.assertRaises(ValueError): smoke.validate_mount_visual_capture(rows)

    def test_retained_emote_or_extra_sprite_during_walking_is_rejected(self):
        for name in ('AfkAlt', 'Down,Up', 'Emote'):
            rows = self.rows(); rows[-1]['detail'] = 'renderers='+name
            with self.assertRaises(ValueError): smoke.validate_mount_visual_capture(rows)
