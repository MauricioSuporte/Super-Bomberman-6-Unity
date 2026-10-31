"""Require each actual client to present both approved native launcher actors."""


def validate_launcher_capture(rows, launcher_kind):
    expected = {'arena': 'BattleSpring', 'spring': 'Spring', 'cannon': 'Cannon', 'rope': 'Rope'}[launcher_kind]
    if launcher_kind == 'rope':
        begins = [row for row in rows if row.get('kind') == 'launch_recipe_begin' and
                  'kind=Rope' in row.get('detail', '').split(';')]
        keys = {(row['player'], next((part for part in row['detail'].split(';') if part.startswith('sequence=')), '')) for row in begins}
        if len(keys) < 3:
            raise ValueError('Rope: missing full flight, short blocked prefix or cancelled flight in this client view')
        endpoints = [row for row in rows if row.get('kind') == 'launch_recipe_endpoint' and 'kind=Rope' in row.get('detail', '').split(';')]
        endpoint_keys = {(row['player'], next((part for part in row['detail'].split(';') if part.startswith('sequence=')), '')) for row in endpoints}
        if not keys.issubset(endpoint_keys) or not any(0 < row.get('durationMs', 0) <= 50 for row in endpoints):
            raise ValueError('Rope: confirmed endpoints, including the short prefix, were not actually presented')
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
