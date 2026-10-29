import importlib.util
from pathlib import Path
import unittest


spec = importlib.util.spec_from_file_location('revenge_smoke', Path(__file__).parents[1] / 'RoomDirectory/validate_revenge_smoke.py')
smoke = importlib.util.module_from_spec(spec)
spec.loader.exec_module(smoke)


class RevengeSmokeTests(unittest.TestCase):
    def rows(self):
        rows = [dict(kind='revenge_cart_present', player=player) for player in (1, 2)]
        for tick in range(3):
            for kind in ('input', 'first_visual', 'confirm'):
                rows.append(dict(kind='revenge_launch_' + kind, player=1, inputTick=tick,
                                 durationMs=2 if kind == 'first_visual' else 80,
                                 detail='cart=-7;generation=2;after_presenter_observation'))
        return rows

    def test_correlates_three_releases_without_claiming_percentile_or_rendering(self):
        result = smoke.validate_revenge_capture(self.rows())
        self.assertEqual(3, result['confirmedReleases'])
        self.assertEqual(2, result['medianFirstVisualMs'])
        self.assertNotIn('p95Ms', result)

    def test_missing_or_duplicate_response_fails(self):
        rows = self.rows()
        for changed in (rows[:-1], rows + [rows[-1]]):
            with self.assertRaises(RuntimeError):
                smoke.validate_revenge_capture(changed)

    def test_invalid_response_or_wrong_generation_fails(self):
        for replacement in ({'durationMs': float('nan')}, {'detail': 'cart=-7;generation=3'}):
            rows = self.rows()
            rows[-1].update(replacement)
            with self.assertRaises(RuntimeError):
                smoke.validate_revenge_capture(rows)

    def test_rejection_and_timeout_fail(self):
        for kind in ('reject', 'timeout'):
            with self.assertRaises(RuntimeError):
                smoke.validate_revenge_capture(self.rows() + [dict(kind='revenge_launch_' + kind)])

    def test_observer_is_required(self):
        with self.assertRaises(RuntimeError):
            smoke.validate_revenge_capture(self.rows()[1:])

    def test_server_requires_two_distinct_boost_owners(self):
        rows = [dict(kind='revenge_cart_authority', player=player, held=65) for player in (1, 2)]
        self.assertEqual([1, 2], smoke.validate_revenge_capture(rows, True)['boostOwners'])
        with self.assertRaises(RuntimeError):
            smoke.validate_revenge_capture(rows[:1] * 2, True)
