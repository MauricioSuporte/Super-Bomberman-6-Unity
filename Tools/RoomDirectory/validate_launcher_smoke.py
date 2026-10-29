"""Require each actual client to present both approved native launcher actors."""


def validate_launcher_capture(rows, launcher_kind):
    expected = {'arena': 'BattleSpring', 'spring': 'Spring', 'cannon': 'Cannon'}[launcher_kind]
    result = {}
    for owner in (True, False):
        events = [row for row in rows if row.get('localPlayer') == owner and
                  ('kind=' + expected) in row.get('detail', '').split(';')]
        begins = [row for row in events if row.get('kind') == 'launch_recipe_begin']
        ends = [row for row in events if row.get('kind') == 'launch_recipe_end']
        samples = [row for row in events if row.get('kind') == 'launch_recipe_sample']
        if not begins or not ends or len(samples) < 2:
            raise ValueError(f'{expected} owner={owner}: missing begin/end/continuous samples')
        if max(row.get('durationMs', -1) for row in samples) - min(row.get('durationMs', -1) for row in samples) < 10:
            raise ValueError(f'{expected} owner={owner}: approved phase never advanced')
        phases = {}
        for row in samples:
            sequence = next((part for part in row.get('detail', '').split(';') if part.startswith('sequence=')), '')
            key = (row['player'], sequence)
            phase = row.get('durationMs', -1)
            if phase + 1 < phases.get(key, -1):
                raise ValueError(f'{expected} owner={owner}: approved phase rewound')
            phases[key] = phase
        actors = {row['player'] for row in begins}
        if not any(row.get('kind') == 'motion_sample' and row.get('player') in actors and
                   row.get('localPlayer') == owner and row['realtime'] > ends[-1]['realtime'] and
                   'prediction_block=none ' in row.get('detail', '') for row in rows):
            raise ValueError(f'{expected} owner={owner}: did not return to unlocked movement')
        result['owner' if owner else 'observer'] = dict(players=sorted(actors),
            begins=len(begins), ends=len(ends), continuousSamples=len(samples))
    return result
