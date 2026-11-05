"""Require each actual client to present both approved native launcher actors."""


def validate_launcher_capture(rows, launcher_kind, portal_expected=None):
    if launcher_kind.startswith('portal'):
        return validate_portal_capture(rows, portal_expected)
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


def validate_portal_capture(rows, expected):
    if not expected or len({r['player'] for r in expected}) != 2:
        raise ValueError('Portal fixture did not confirm two native destinations')
    result = {}
    def near(row, point):
        p = row.get('position', {})
        return sum((p.get(axis, float('inf'))-point.get(axis, 0))**2 for axis in ('x', 'y')) < .36
    for owner in (True, False):
        actors = []
        for checkpoint in expected:
            samples = [r for r in rows if r.get('kind') == 'portal_pose_sample' and r.get('player') == checkpoint['player'] and r.get('localPlayer') == owner]
            if not samples: continue
            # Hidden travel can start between rendered frames. The owner's approved
            # entry event establishes its source; destination must be a final pose.
            entries = [r for r in rows if owner and r.get('kind') == 'authority_motion_begin' and r.get('player') == checkpoint['player'] and r.get('localPlayer') == owner]
            starts = [r for r in samples + entries if near(r, checkpoint['bombPosition'])]
            if not starts: raise ValueError(f'Portal owner={owner}: no approved source presented')
            ends = [r for r in samples if r['realtime'] > starts[0]['realtime'] and near(r, checkpoint['position'])]
            if not ends: raise ValueError(f'Portal owner={owner}: no approved destination presented')
            if not any(r['realtime'] > ends[0]['realtime'] and 'prediction_block=none ' in r.get('detail', '') for r in rows if r.get('kind') == 'motion_sample' and r.get('player') == checkpoint['player'] and r.get('localPlayer') == owner):
                raise ValueError(f'Portal owner={owner}: no free movement after arrival')
            actors.append(checkpoint['player'])
        if not actors: raise ValueError(f'Portal owner={owner}: missing actor view')
        result['owner' if owner else 'observer'] = dict(players=actors, approvedEndpoints=True, unlocked=True)
    return result


def validate_field_capture(rows, expected):
    if len(expected) != 3 or {r.get('detail') for r in expected} != {'kind=Magnet', 'kind=Search', 'kind=Kick'}:
        raise ValueError('Missing native field motion checkpoints')
    result = []
    for checkpoint in expected:
        points = [r['bombPosition'] for r in rows if r.get('bombNetId') == checkpoint['bombNetId'] and
                  r.get('kind') in ('bomb_motion_sample', 'kick_presentation_sample', 'bomb_motion_apply', 'kick_segment_apply')]
        origin, end = checkpoint['bombPosition'], checkpoint['position']
        corner = {a: origin[a] + checkpoint['delta'][a] for a in ('x', 'y')}
        def near(p, q): return sum((p[a]-q[a])**2 for a in ('x','y')) < .0625
        cursor = 0
        for target in (origin, corner, end):
            matches = [i for i in range(cursor, len(points)) if near(points[i], target)]
            if not matches: raise ValueError('Field path missing presented endpoint: '+checkpoint['detail'])
            cursor = matches[0]
        for p in points:
            if (not origin['x']-.06 <= p['x'] <= end['x']+.06 or not origin['y']-.06 <= p['y'] <= end['y']+.06 or
                abs(p['y']-origin['y']) > .06 and abs(p['x']-corner['x']) > .06):
                raise ValueError('Field path cut its approved corner: '+checkpoint['detail'])
        result.append(checkpoint['detail'])
    return result
