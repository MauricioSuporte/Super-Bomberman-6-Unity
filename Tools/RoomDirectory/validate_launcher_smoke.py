"""Require each actual client to present both approved native launcher actors."""


def validate_launcher_capture(rows, launcher_kind):
    if launcher_kind.startswith('cart'):
        return validate_minecart_capture(rows, launcher_kind)
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


def validate_minecart_capture(rows, kind):
    result = {}
    for owner in (True, False):
        samples = [r for r in rows if r.get('kind') == 'minecart_present' and r.get('localPlayer') == owner]
        phases = {part[6:] for r in samples for part in r.get('detail', '').split(';') if part.startswith('phase=')}
        required = {'Enter', 'Rail', 'Exit'}
        if kind == 'cart12': required |= {'PortalSink', 'PortalRise'}
        if kind == 'cart32': required |= {'Jump'}
        if not required.issubset(phases) or len(samples) < 5:
            raise ValueError(f'{kind} owner={owner}: missing continuous native phases {required-phases}')
        rail_positions = {(r.get('position', {}).get('x'), r.get('position', {}).get('y')) for r in samples
                          if 'phase=Rail' in r.get('detail', '').split(';') and 'position' in r}
        if len(rail_positions) < 2: raise ValueError(f'{kind} owner={owner}: rail presentation never moved')
        ends = [r for r in rows if r.get('kind') == 'minecart_end' and r.get('localPlayer') == owner]
        if not ends: raise ValueError(f'{kind} owner={owner}: no approved release')
        actors = {r['player'] for r in samples}
        if not any(r.get('kind') == 'motion_sample' and r.get('player') in actors and r.get('localPlayer') == owner and
                   r['realtime'] > ends[-1]['realtime'] and 'prediction_block=none ' in r.get('detail', '') for r in rows):
            raise ValueError(f'{kind} owner={owner}: no free movement after release')
        result['owner' if owner else 'observer'] = dict(samples=len(samples), phases=sorted(phases), releases=len(ends))
    return result
