"""Checks each actual client's mechanism evidence, independent of server completion."""
import re


def validate_mechanism_capture(rows, kind, expected):
    captures = [r for r in rows if r.get('kind') == 'mechanism_capture' and 'fixture='+kind+';' in r.get('detail', '')]
    if not any('phase=1;' in r.get('detail','') for r in captures) or not any('phase=2;' in r.get('detail','') for r in captures):
        raise ValueError('Missing rendered mechanism phases: '+kind)
    if kind == 'boiler':
        if len(expected) != 1: raise ValueError('Missing native boiler endpoint')
        checkpoint = expected[0]
        points = [r['bombPosition'] for r in rows if r.get('bombNetId') == checkpoint['bombNetId'] and r.get('kind') in ('bomb_motion_sample','bomb_motion_apply')]
        for target in (checkpoint['bombPosition'], checkpoint['position']):
            if not any(sum((p[a]-target[a])**2 for a in ('x','y')) < .09 for p in points):
                raise ValueError('Boiler endpoint was not presented')
        if not any(r.get('kind') == 'tile_animation_apply' for r in rows): raise ValueError('Door animation frames were not applied')
    elif kind in ('blackout','world-blackout'):
        states = [r.get('detail','') for r in rows if r.get('kind') == 'mechanism_blackout']
        if not any('active=True;' in s for s in states) or not any('active=False;' in s for s in states):
            raise ValueError('Blackout did not present both approved states')
        if kind == 'world-blackout':
            masks = [r.get('detail','') for r in rows if r.get('kind') == 'mechanism_materials']
            if not any(int(re.search(r'masked=(\d+)', s)[1]) > 0 for s in masks): raise ValueError('No actual blackout mask materials')
            if any('unsupported=0' not in s for s in masks): raise ValueError('Unsupported blackout shader')
    elif kind == 'rocket':
        views = [r.get('detail','') for r in rows if r.get('kind') == 'mechanism_view' and 'visible=True;' in r.get('detail','')]
        frames = [int(m[1]) for s in views if (m := re.search(r'sprite=Rocket(?: 2)?_(\d+);', s))]
        if not all(any(lo <= frame <= hi for frame in frames) for lo,hi in ((2,5),(7,10),(11,16))):
            raise ValueError('Missing presented rocket ascent/descent/impact')
        if not any('sprite=Rocket Target_' in s for s in views): raise ValueError('Missing approved target marker')
    elif kind == 'dynamite':
        if not any(int(re.search(r'explosions=(\d+)', r['detail'])[1]) > 0 for r in captures):
            raise ValueError('No client explosion visuals for native dynamite')
    elif kind == 'sudden':
        def cells(records):
            return {tuple(map(int,m.groups())) for r in records if (m := re.search(r'cell=(-?\d+),(-?\d+)', r.get('detail','')))}
        approved = cells(expected)
        actual = cells([r for r in rows if r.get('kind') == 'sudden_death_commit_apply'])
        if not approved or actual != approved: raise ValueError('Client terrain differs from actual server landings')
        views = [r.get('detail','') for r in rows if r.get('kind') == 'sudden_death_present']
        if not any(re.search(r'falls=[1-9]', s) for s in views): raise ValueError('No client falling visual')
        if not any('paused=True' in s for s in views) and not any(r.get('kind')=='mechanism_sudden_pause' and 'paused=True' in r.get('detail','') for r in rows):
            raise ValueError('No pause evidence')
    elif kind == 'falling':
        views = [r.get('detail','') for r in rows if r.get('kind') == 'mechanism_target']
        if not any('active=True' in s for s in views) or not any('active=False' in s for s in views): raise ValueError('Missing target phases')
        if not any(re.search(r'bombs=[1-9]',s) for s in views): raise ValueError('No replicated falling bomb')
    elif kind == 'powder':
        frames = [int(r['detail'].split('=')[1]) for r in rows if r.get('kind')=='mechanism_powder']
        if not any(f>=0 for f in frames) or -1 not in frames: raise ValueError('Missing powder frame/restore')
    elif kind == 'roof':
        tiles = {int(r['detail'].split('=')[1]) for r in rows if r.get('kind')=='mechanism_roof'}
        if not any(t>=0 and t%4==0 for t in tiles) or not any(t==-1 or t%4==3 for t in tiles): raise ValueError('Missing closed/open roof')
    return dict(kind=kind, phases=True, approvedPresentation=True)
