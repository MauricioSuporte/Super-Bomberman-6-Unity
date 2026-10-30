"""Check both client views of native mount transitions and visual cleanup."""


def fields(row):
    return dict(part.split('=', 1) for part in row.get('detail', '').split(';') if '=' in part)


def validate_mount_visual_capture(rows):
    result = {}
    mount_rows = [r for r in rows if r.get('kind') == 'mount_visual_sample']
    for owner in (True, False):
        visible_riding = [r for r in mount_rows if r.get('localPlayer') == owner and
                          fields(r).get('riding') == 'True' and fields(r).get('mount')]
        if not visible_riding:
            raise ValueError(f'owner={owner}: no visible mount during approved riding arc')
        result['owner' if owner else 'observer'] = dict(visibleRidingSamples=len(visible_riding))
    for mount_type in ('Mole', 'Tank'):
        head = [r for r in mount_rows if fields(r).get('type') == mount_type and 'head' in fields(r).get('body', '').lower()]
        if len(head) < 2 or not any(abs(r.get('delta', {}).get('y', 0)) > 1 for r in head):
            raise ValueError(mount_type + ': no approved elevated head presentation')
        result[mount_type] = dict(headSamples=len(head))
    for mount_type, explosion in (('Blue', False), ('Pink', True)):
        death = [r for r in rows if r.get('kind') == 'mount_death_visual' and fields(r).get('type') == mount_type and
                 ('explosion' in fields(r).get('renderer', '').lower()) == explosion]
        frames = {int(fields(r).get('frame', -1)) for r in death}
        if len(frames) < 2: raise ValueError(mount_type + ': death animation did not advance with the correct cause')
        result[mount_type] = dict(deathFrames=sorted(frames), explosion=explosion)
    walking = [r for r in rows if r.get('kind') == 'player_visual_sample' and r.get('localPlayer') and r.get('accepted')]
    if len(walking) < 4: raise ValueError('Owner never resumed predicted walking')
    for row in walking:
        names = fields(row).get('renderers', '')
        if ',' in names or any(word in names.lower() for word in ('afk', 'emote', 'inactivity')):
            raise ValueError('Owner retained an emote/AFK/extra renderer during predicted walking: ' + names)
    result['walkingSamples'] = len(walking)
    return result
