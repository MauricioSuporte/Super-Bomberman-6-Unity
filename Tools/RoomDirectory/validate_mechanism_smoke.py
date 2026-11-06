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
    return dict(kind=kind, phases=True, approvedPresentation=True)
