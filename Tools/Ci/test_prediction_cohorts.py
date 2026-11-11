import importlib.util
from pathlib import Path
import json
import unittest

spec = importlib.util.spec_from_file_location("prediction_cohorts", Path(__file__).parents[1] / "Diagnostics/prediction_cohorts.py")
cohorts = importlib.util.module_from_spec(spec)
spec.loader.exec_module(cohorts)


class PredictionCohortTests(unittest.TestCase):
    def fixture(self):
        lines = []
        for second in range(130):
            lines.append('[R01Frame] ' + json.dumps({'u': second * 1000 - cohorts.FRAME_TO_UNIX_MS}))
            if second == 2: lines.append('[P0] cohort walking')
            if second == 65: lines.append('[P0] cohort mounted')
            if second == 125: lines.append('[P0] reproduction completed')
        return '\n'.join(lines)

    def test_conservative_brackets_exclude_setup_transition_and_tail(self):
        windows = cohorts.windows(self.fixture())
        self.assertEqual({'beginUnix': 5, 'endUnix': 63}, windows['windows']['walking'])
        self.assertEqual({'beginUnix': 68, 'endUnix': 123}, windows['windows']['mounted'])
        rows = [{'kind': 'capture_start'},
                {'kind': 'input_to_motion', 'utc': '1970-01-01T00:00:04Z'},
                {'kind': 'input_to_motion', 'utc': '1970-01-01T00:00:06Z'},
                {'kind': 'input_to_motion', 'utc': '1970-01-01T00:01:05Z'},
                {'kind': 'input_to_motion', 'utc': '1970-01-01T00:02:04Z'}]
        self.assertEqual([rows[0], rows[2]], list(cohorts.select(rows, windows)))

    def test_missing_or_duplicate_markers_block_selection(self):
        for text in (self.fixture().replace('[P0] cohort mounted', ''), self.fixture() + '\n[P0] cohort mounted'):
            with self.assertRaises(ValueError): cohorts.windows(text)

    def test_invalid_row_clock_is_not_silently_dropped(self):
        with self.assertRaises(ValueError):
            list(cohorts.select([{'kind': 'input_to_motion'}], cohorts.windows(self.fixture())))

    def test_offline_completion_uses_previous_bound_without_inventing_next_frame(self):
        text = self.fixture().split('[P0] reproduction completed')[0] + '[P0] reproduction completed'
        result = cohorts.windows(text)
        self.assertEqual([125, None], result['markerBracketsUnix']['end'])
        self.assertEqual(123, result['windows']['mounted']['endUnix'])

    def test_clock_rewind_blocks_mixed_cohort_selection(self):
        line = '[R01Frame] ' + json.dumps({'u': 70 * 1000 - cohorts.FRAME_TO_UNIX_MS})
        rewind = '[R01Frame] ' + json.dumps({'u': 50 * 1000 - cohorts.FRAME_TO_UNIX_MS})
        with self.assertRaisesRegex(ValueError, 'backwards'):
            cohorts.windows(self.fixture().replace(line, rewind))
