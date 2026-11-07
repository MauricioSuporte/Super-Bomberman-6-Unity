#!/usr/bin/env python3
"""R01: summarise [HitchProbe] lines of a smoke folder (server.log, client-N.log).

Each hitch (frame >= threshold) is attributed, in this order, to the first rule that holds:
  gc        a GC collection happened in the frame
  capture   motion-capture format+disk time >= 50% of the frame
  scene     a scene load/unload was logged within 1.5 s before the frame (ptime/realtime)
  outside   time outside the PlayerLoop (sleep, vsync, native waits) >= 50% of the frame
  <phase>   the PlayerLoop phase with the largest share (time, early(network receive), fixed(physics), pre, update, preLate, postLate)
The attribution is a measurement hint, not proof; every hitch is listed, none is dropped.
"""
import argparse, json, re, sys
from pathlib import Path

def lines(path):
    for l in Path(path).read_text(encoding='utf-8-sig', errors='replace').splitlines():
        i = l.find('[HitchProbe] ')
        if i >= 0:
            try: yield json.loads(l[i + 13:])
            except ValueError: pass

def classify(h, scenes):
    ph = h['phasesMs']
    if h['gc0'] or h['gc1'] or h['gc2']: return 'gc'
    if h['captureFormatMs'] + h['captureIoMs'] >= 0.5 * h['dtMs']: return 'capture'
    if any(0 <= h['realtime'] - s <= 1.5 for s in scenes): return 'scene'
    if h['outsideMs'] >= 0.5 * h['dtMs']: return 'outside'
    return max(ph, key=ph.get)

def report(folder):
    out = {}
    for log in sorted(Path(folder).glob('*.log')):
        if log.name.endswith('launcher.log'): continue
        rows = list(lines(log))
        if not rows: continue
        scenes = [r['realtime'] for r in rows if r['kind'] == 'scene']
        hitches = [r for r in rows if r['kind'] == 'hitch']
        sec = [r for r in rows if r['kind'] == 'second']
        clock = [r for r in rows if r['kind'] == 'clock']
        pauses = [r for r in rows if r['kind'] == 'pause']
        by = {}
        for h in hitches: by.setdefault(classify(h, scenes), []).append(h)
        out[log.stem] = dict(
            hitches=len(hitches), attribution={k: len(v) for k, v in by.items()},
            list=[dict(realtime=h['realtime'], ptime=h['ptime'], dtMs=h['dtMs'], cause=classify(h, scenes), phasesMs=h['phasesMs'], outsideMs=h['outsideMs'],
                       gc=[h['gc0'], h['gc1'], h['gc2']], captureMs=round(h['captureFormatMs'] + h['captureIoMs'], 2), scene=h['scene'], paused=h['paused']) for h in hitches],
            seconds=len(sec), maxDtMs=max([s['maxDtMs'] for s in sec], default=None),
            captureCostMsPerSecond=dict(format=round(sum(s['captureFormatMs'] for s in sec) / max(1, len(sec)), 2), io=round(sum(s['captureIoMs'] for s in sec) / max(1, len(sec)), 2)),
            captureRowsPerSecond=round(sum(s['captureRows'] for s in sec) / max(1, len(sec)), 1),
            captureDroppedFinal=sec[-1]['captureDropped'] if sec else None,
            gcCollectionsTotal=[sum(s['gc0'] for s in sec), sum(s['gc1'] for s in sec), sum(s['gc2'] for s in sec)],
            sceneEvents=[(r['realtime'], r['event'], r['name']) for r in rows if r['kind'] == 'scene'],
            pauseEvents=[(r['realtime'], r['paused']) for r in pauses],
            clockJumps=[dict(realtime=c['realtime'], ptime=c['ptime'], deltaMs=c['deltaMs'], rttMs=c['rttMs'], rttVarMs=c['rttVarMs'], dtMs=c['dtMs']) for c in clock])
    return out

if __name__ == '__main__':
    ap = argparse.ArgumentParser(); ap.add_argument('folder', type=Path); ap.add_argument('--json', type=Path)
    a = ap.parse_args(); r = report(a.folder)
    if a.json: a.json.write_text(json.dumps(r, indent=1), encoding='utf-8')
    for name, v in r.items():
        print(f"{name}: hitches={v['hitches']} {v['attribution']} maxDt={v['maxDtMs']} capture ms/s fmt={v['captureCostMsPerSecond']['format']} io={v['captureCostMsPerSecond']['io']} rows/s={v['captureRowsPerSecond']} dropped={v['captureDroppedFinal']} gc={v['gcCollectionsTotal']} clockJumps={len(v['clockJumps'])}")
        for h in v['list']: print(f"   rt={h['realtime']:.2f} dt={h['dtMs']} cause={h['cause']} phases={h['phasesMs']} out={h['outsideMs']} gc={h['gc']} cap={h['captureMs']} scene={h['scene']}")
