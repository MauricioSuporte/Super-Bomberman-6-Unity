"""R05: correlate actual native/projected renderers by identity and approved time.

CPU state measurements only; screenshots/manual perception are separate evidence.
Missing data is inconclusive, never a successful comparison.
"""
import argparse
import bisect
import json
import math
from collections import defaultdict
from pathlib import Path


def detail(row):
    return dict(part.split('=', 1) for part in row.get('detail', '').split(';') if '=' in part)


def percentile(values, fraction):
    return sorted(values)[max(0, math.ceil(len(values) * fraction) - 1)] if values else None


def rows(path):
    result = [json.loads(line) for line in path.read_text(encoding='utf-8-sig').splitlines() if line.strip()]
    if not result or any(r.get('kind') == 'capture_limit' or r.get('dropped', 0) > 0 for r in result):
        raise ValueError('empty/truncated/dropped capture: ' + str(path))
    return result


def compare(native_rows, observer_rows):
    epochs = []; epoch = None
    for row in observer_rows:
        if row.get('kind') == 'r05_epoch_begin':
            epoch = float(detail(row)['ptime'])
        elif row.get('kind') == 'r05_epoch_end' and epoch is not None:
            epochs.append((epoch, float(detail(row)['ptime']))); epoch = None
    if epoch is not None:
        epochs.append((epoch, max((r.get('timeline', epoch) for r in observer_rows), default=epoch)))
    tracks = defaultdict(list)
    for row in native_rows:
        if row.get('kind') != 'r05_native':
            continue
        d = detail(row)
        tracks[int(d['id'])].append((float(d['ptime']), row, d))
    times = {}
    for key, track in tracks.items():
        track.sort(key=lambda entry: entry[0])
        times[key] = [entry[0] for entry in track]
    families = defaultdict(lambda: dict(samples=0, visibleSamples=0, movingEntities=set(), entities=set(), positionErrors=[], holdMs=[], starvedMs=[], pauseDrift=[], movingHolds=[],
                                        spriteMismatch=0, visibilityMismatch=0, unverifiedAutoSpriteSamples=0, pausedSamples=0, pauseTransitionSamples=0, pauseHistoryHoldSamples=0, removed=0, outsideReference=0))
    problems = []
    candidates = []
    observed = set()
    pause_positions = {}
    for row in observer_rows:
        if row.get('kind') not in ('r05_view', 'r05_removed'):
            continue
        d = detail(row); key = int(d['id'])
        if key not in tracks:
            continue
        family = tracks[key][0][2]['family']; bucket = families[family]
        if row['kind'] == 'r05_removed':
            bucket['removed'] += 1
            continue
        if d.get('paused') == '1' and d.get('visible') == '1':
            origin = pause_positions.setdefault(key, row['position'])
            bucket['pauseDrift'].append(math.hypot(*(row['position'][axis]-origin[axis] for axis in ('x', 'y'))))
        else:
            pause_positions.pop(key, None)
        clock = float(d['ptime']); at = float(d.get('state', clock)); track = tracks[key]
        index = bisect.bisect_right(times[key], at) - 1
        state_paused = any(entry[2].get('paused') == '1' for entry in track[max(0,index):index+2])
        if clock-at > .1 and d.get('paused') != '1' and d.get('visible') == '1' and state_paused:
            bucket['pauseHistoryHoldSamples'] += 1
        if clock-at > .1 and d.get('paused') != '1' and d.get('visible') == '1' and not state_paused:
            live_index = bisect.bisect_right(times[key], clock)-1
            if 0 <= live_index < len(track)-1:
                a, b = track[live_index:live_index+2]
                if (b[0]-a[0] <= .15 and a[2].get('visible') == b[2].get('visible') == '1' and
                        a[2].get('paused') != '1' and b[2].get('paused') != '1'):
                    fraction = (clock-a[0])/max(1e-9, b[0]-a[0])
                    live_error = math.hypot(*(row['position'][axis] -
                        (a[1]['position'][axis]*(1-fraction)+b[1]['position'][axis]*fraction) for axis in ('x','y')))
                    if live_error > .125: bucket['movingHolds'].append((clock-at)*1000)
        # The last native observation precedes destruction by a frame. A real
        # visible observer sample at that boundary proves presence, but cannot
        # establish interpolated position error without a following reference.
        nearest = min(track[max(0, index):min(len(track), index+2)] or track[-1:], key=lambda item: abs(item[0]-at))
        if d.get('visible') == '1' and nearest[2].get('visible') == '1' and abs(nearest[0]-at) <= .025:
            observed.add(key); bucket['entities'].add(key); bucket['visibleSamples'] += 1
        if index < 0 or index + 1 >= len(track):
            bucket['outsideReference'] += 1
            continue
        before, after = track[index], track[index + 1]
        if after[0] - before[0] > .15:
            bucket['outsideReference'] += 1
            continue
        if (d.get('paused') != '1' and before[2].get('visible') == after[2].get('visible') and
                d.get('visible') != before[2].get('visible') and at - before[0] > .005):
            bucket['visibilityMismatch'] += 1
        if d.get('visible') != '1' or before[2].get('visible') != '1' or after[2].get('visible') != '1':
            continue
        observed.add(key); bucket['entities'].add(key); bucket['samples'] += 1
        if d.get('paused') == '1':
            bucket['pausedSamples'] += 1
            continue  # Presented pose intentionally freezes; an advancing network clock is not gameplay time.
        if before[2].get('paused') == '1' or after[2].get('paused') == '1':
            # Pause reaches peers after the authority stops. A held pre-pause pose
            # cannot be compared against the still-advancing transport clock.
            # Keep these boundary/resume samples explicit, separate from motion.
            bucket['pauseTransitionSamples'] += 1
            continue
        if d.get('visible') == '1' and d.get('paused') != '1' and d.get('stateMeasured') == '1':
            bucket['holdMs'].append(max(0, (clock-at)*1000))
            bucket['starvedMs'].append(max(0, (clock-float(d.get('newest', clock)))*1000))
        alpha = (at - before[0]) / max(1e-9, after[0] - before[0])
        reference = {axis: before[1]['position'][axis] * (1-alpha) + after[1]['position'][axis] * alpha for axis in ('x', 'y')}
        error = math.hypot(*(row['position'][axis] - reference[axis] for axis in ('x', 'y')))
        bucket['positionErrors'].append(error)
        sprite_at = clock if d.get('animating') == '1' else at
        sprite_index = bisect.bisect_right(times[key], sprite_at)-1
        nearby = {entry[2].get('sprite') for entry in track[max(0, sprite_index-2):sprite_index+3] if abs(entry[0] - sprite_at) <= .05}
        # An automatic clip may continue its known recipe while a later native
        # transition has not arrived. Keep that delivery hold explicit; it is
        # not proof that an already-received authoritative sprite was wrong.
        if d.get('animating') == '1' and float(d.get('newest', clock)) < clock:
            bucket['unverifiedAutoSpriteSamples'] += 1
            if nearby and d.get('sprite') not in nearby and len(candidates) < 100:
                candidates.append(dict(id=key, family=family, clock=clock, state=at,
                                       newest=float(d['newest']), sprite=d.get('sprite'),
                                       currentNativeSprites=sorted(nearby), frameMs=row.get('frameMs'),
                                       reason='known automatic clip while later authority state is not received'))
            nearby = set()
        if nearby and d.get('sprite') not in nearby:
            bucket['spriteMismatch'] += 1
    required = set()
    outside_epochs = []
    for key, track in tracks.items():
        visible = [entry for entry in track if entry[2].get('visible') == '1']
        if not visible:
            continue
        duration = visible[-1][0] - visible[0][0]
        family = visible[0][2]['family']
        if duration >= .08:
            controlled = visible[0][2].get('case', 'autonomous') != 'autonomous'
            if controlled or not epochs or any(start <= visible[0][0] and visible[-1][0] <= end for start, end in epochs):
                required.add(key)
            else:
                outside_epochs.append(key)
        if max(math.hypot(entry[1]['position']['x'] - visible[0][1]['position']['x'],
                          entry[1]['position']['y'] - visible[0][1]['position']['y']) for entry in visible) > .2:
            families[family]['movingEntities'].add(key)
    missing = sorted(required - observed)
    if missing:
        problems.append('visible native entities absent in observer: ' + str(missing))
    result = {}
    for family, bucket in families.items():
        moving_holds = bucket.pop('movingHolds')
        bucket['movingHoldSamples'] = len(moving_holds)
        bucket['movingHoldMaxMs'] = max(moving_holds, default=None)
        drift = bucket.pop('pauseDrift')
        bucket['pauseDriftMax'] = max(drift, default=None)
        if drift and max(drift) > .125:
            problems.append(f'{family}: visual moved over 2 px while paused')
        errors = bucket.pop('positionErrors')
        holds = bucket.pop('holdMs')
        starved = bucket.pop('starvedMs')
        bucket['entities'] = len(bucket['entities']); bucket['movingEntities'] = len(bucket['movingEntities'])
        bucket['positionSamples'] = len(errors)
        bucket['positionP95'] = percentile(errors, .95); bucket['positionMax'] = max(errors, default=None)
        bucket['holdP95Ms'] = percentile(holds, .95); bucket['holdMaxMs'] = max(holds, default=None)
        bucket['starvedP95Ms'] = percentile(starved, .95); bucket['starvedMaxMs'] = max(starved, default=None)
        if errors and bucket['positionP95'] > .125:
            problems.append(f'{family}: position p95 above 2 px')
        if bucket['spriteMismatch']:
            problems.append(f'{family}: unexpected sprite sequence')
        if bucket['visibilityMismatch']:
            problems.append(f'{family}: unexpected visibility')
        result[family] = bucket
    return dict(status='FAILED' if problems else 'PASSED' if required and observed else 'INCONCLUSIVE',
                problems=problems, requiredEntities=len(required), observedEntities=len(observed), families=result,
                presentationEpochs=epochs, outsideObservationEpoch=outside_epochs,
                candidates=candidates,
                limitations=['CPU renderer state, not GPU/hardware input latency', 'pause on either peer/reference excluded; pause transitions counted separately',
                             'effects visible under 80 ms do not establish per-entity coverage', 'reference gaps over 150 ms excluded'])


def analyze(folder):
    server = rows(folder/'server-motion.jsonl')
    reports = {f'client-{i}': compare(server, rows(folder/f'client-{i}-motion.jsonl')) for i in (1, 2)}
    scene = json.loads((folder/'config.json').read_text(encoding='utf-8-sig'))['scene']
    required = {
        'Stage_3-6': ['HeliProjectile', 'BirdRobotRocket', 'BirdRobotRocketImpact'],
        'Stage_3-4': ['MatangoProjectile', 'FarohFireProjectile', 'DespiderWebProjectile'],
        'Stage_3-5': ['IcicleIceProjectile', 'BubbleFuramaFlame'],
        'Stage_3-3': ['FishManSpear', 'JellyFishShot'],
        'Stage_3-2': ['FuramaFlameVisual'],
        'Stage_2-3': ['TankShot'],
        'Stage_1-7': ['StarProjectile'], 'Stage_2-7': ['StarProjectile', 'SunMaskHeartProjectile'],
        'Stage_World3Hall': ['FreezerVenusProjectile', 'QueenOyobeeProjectile', 'QueenOyobeePixelTrail'],
    }.get(scene, [])
    for report in reports.values():
        for family in required:
            if report['families'].get(family, {}).get('visibleSamples', 0) == 0:
                report['problems'].append('required family not measured: '+family)
                report['status'] = 'FAILED'
        for family in set(required) - {'BirdRobotRocketImpact', 'DespiderWebProjectile', 'QueenOyobeePixelTrail'}:
            if report['families'].get(family, {}).get('movingEntities', 0) == 0:
                report['problems'].append('required moving trajectory not exercised: '+family)
                report['status'] = 'FAILED'
            if report['families'].get(family, {}).get('positionSamples', 0) < 3:
                report['problems'].append('too few aligned moving-family position samples: '+family)
                if report['status'] != 'FAILED': report['status'] = 'INCONCLUSIVE'
    status = 'FAILED' if any(r['status'] == 'FAILED' for r in reports.values()) else 'PASSED' if all(r['status'] == 'PASSED' for r in reports.values()) else 'INCONCLUSIVE'
    return dict(status=status, clients=reports)


def native_summary(capture):
    """Offline/authority kinematics, not an assertion that autonomous targets match."""
    tracks = defaultdict(list)
    for row in capture:
        if row.get('kind') == 'r05_native':
            d = detail(row)
            if d.get('case', 'autonomous') != 'autonomous':
                tracks[d['case']].append((float(d['ptime']), row, d))
    result = {}
    for label, track in tracks.items():
        track.sort(key=lambda item: item[0]); distances = []; duration = 0; visible = 0
        for _, _, d in track:
            visible += d.get('visible') == '1'
        for previous, current in zip(track, track[1:]):
            dt = current[0] - previous[0]
            if not 0 < dt <= .15 or previous[2].get('paused') == '1' or current[2].get('paused') == '1':
                continue
            if previous[2].get('visible') != '1' or current[2].get('visible') != '1':
                continue
            delta = math.hypot(*(current[1]['position'][a]-previous[1]['position'][a] for a in ('x', 'y')))
            if delta > 1e-6:
                distances.append(delta); duration += dt
        result[label] = dict(family=track[0][2]['family'], samples=len(track), visibleSamples=visible,
                             motionSamples=len(distances), movingSeconds=duration,
                             speedWhenMoving=sum(distances)/duration if duration else None,
                             sprites=list(dict.fromkeys(d['sprite'] for _, _, d in track)))
    return result


def compare_offline(online_rows, offline_rows):
    online = native_summary(online_rows); offline = native_summary(offline_rows)
    matches = sorted(online.keys() & offline.keys())
    cases = {}
    for label in matches:
        a, b = online[label], offline[label]
        cases[label] = dict(online=a, offline=b,
                            speedDifference=(a['speedWhenMoving']-b['speedWhenMoving'])
                            if a['speedWhenMoving'] is not None and b['speedWhenMoving'] is not None else None)
    return dict(status='MEASURED' if matches else 'INCONCLUSIVE', matchedCases=len(matches), cases=cases,
                onlineOnly=sorted(online.keys()-offline.keys()), offlineOnly=sorted(offline.keys()-online.keys()),
                limitations=['native factory launches in the same authored scene/build; not a GPU or hardware test',
                             'targets, collisions and autonomous scene AI can differ between runs',
                             'speed excludes rest, pause, invisible samples and reference gaps over 150 ms',
                             'MEASURED is evidence to review, not an automatic offline parity approval'])


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__); p.add_argument('folder', type=Path); p.add_argument('--output', type=Path, required=True)
    args = p.parse_args(); result = analyze(args.folder)
    args.output.write_text(json.dumps(result, indent=2), encoding='utf-8')
    print(result['status']); raise SystemExit(0 if result['status'] == 'PASSED' else 2 if result['status'] == 'FAILED' else 3)
