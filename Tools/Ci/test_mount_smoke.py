import importlib.util
from pathlib import Path
import unittest

spec = importlib.util.spec_from_file_location('mount_smoke', Path(__file__).parents[1] / 'RoomDirectory/validate_mount_smoke.py')
smoke = importlib.util.module_from_spec(spec)
spec.loader.exec_module(smoke)


class MountSmokeTests(unittest.TestCase):
    def rows(self):
        rows = []
        for arc, count in (('Mount', 3), ('Dismount', 4)):
            for _ in range(count):
                for kind, reason in (('begin', 'gesture_only'), ('confirm', 'phase_preserved'), ('end', 'authority_released')):
                    rows.append(dict(kind='riding_prediction_' + kind, detail=reason + ';arc=' + arc + ';pending=False', localPlayer=True))
        return rows

    def test_requires_prediction_and_confirmation_on_owner(self):
        rows = self.rows() + [dict(kind='riding_prediction_end', detail='confirmation_timeout;arc=Mount;', localPlayer=False)]
        self.assertEqual(3, smoke.validate_mount_capture(rows)['Mount']['confirmedPreservingPhase'])

    def test_server_only_approved_arcs_are_insufficient(self):
        rows = self.rows()
        for row in rows:
            if row['kind'] == 'riding_prediction_begin': row['detail'] = row['detail'].replace('gesture_only', 'authority')
        with self.assertRaises(ValueError): smoke.validate_mount_capture(rows)

    def test_missing_mount_is_rejected(self):
        with self.assertRaises(ValueError):
            smoke.validate_mount_capture([row for row in self.rows() if ';arc=Mount;' not in row['detail']])

    def test_missing_dismount_is_rejected(self):
        with self.assertRaises(ValueError):
            smoke.validate_mount_capture([row for row in self.rows() if ';arc=Dismount;' not in row['detail']])

    def test_unconfirmed_prediction_is_rejected(self):
        rows = self.rows(); rows.pop(1)
        with self.assertRaises(ValueError): smoke.validate_mount_capture(rows)

    def test_rejection_or_timeout_fails_the_success_fixture(self):
        rows = self.rows()
        rows.append(dict(kind='riding_prediction_end', detail='confirmation_timeout;arc=Mount;', localPlayer=True))
        with self.assertRaises(ValueError): smoke.validate_mount_capture(rows)
