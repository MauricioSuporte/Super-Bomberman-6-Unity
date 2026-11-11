"""R06 renderer comparison. Discrete poses hold until the next authority edge."""
import argparse
import bisect
import json
from collections import defaultdict
from pathlib import Path

from Tools.Diagnostics.analyze_projectiles import compare, detail, rows


def pose_steps(native, observed):
    tracks = defaultdict(list)
    for row in native:
        if row.get('kind') == 'r06_native':
            state = detail(row)
            tracks[state['id']].append((float(state['ptime']), state))
    for track in tracks.values():
        track.sort(key=lambda entry: entry[0])
    times = {key: [entry[0] for entry in track] for key, track in tracks.items()}
    result = defaultdict(lambda: dict(compared=0, mismatches=0, outsideReference=0,
                                      referenceGaps=0, pauseOrHidden=0, examples=[],
                                      deliveryPoseHolds=0, deliveryHoldMaxMs=0))
    for row in observed:
        if row.get('kind') != 'r06_view':
            continue
        state = detail(row)
        key = state['id']
        bucket = result[key]
        clock = float(state['ptime'])
        # A manual pose must hold the last RECEIVED state if delivery stalls.
        # That is not a different pose from the confirmed state. Report delivery
        # delay separately instead of pretending the future packet was received.
        reference = clock if state.get('animating') == '1' else float(state.get('state', clock))
        index = bisect.bisect_right(times.get(key, []), reference) - 1
        track = tracks[key]
        if index < 0 or index + 1 >= len(track):
            bucket['outsideReference'] += 1
            continue
        before, after = track[index:index + 2]
        if after[0] - before[0] > .15:
            bucket['referenceGaps'] += 1
            continue
        if (state.get('visible') != '1' or before[1].get('visible') != '1' or
                state.get('paused') == '1' or before[1].get('paused') == '1' or after[1].get('paused') == '1'):
            bucket['pauseOrHidden'] += 1
            continue
        bucket['compared'] += 1
        live_index = bisect.bisect_right(times[key], clock) - 1
        newest = float(state.get('newest', clock))
        if (newest < clock and 0 <= live_index < len(track) - 1 and
                track[live_index][1].get('visible') == '1' and
                track[live_index][1].get('sprite') != state['sprite']):
            bucket['deliveryPoseHolds'] += 1
            bucket['deliveryHoldMaxMs'] = max(bucket['deliveryHoldMaxMs'], (clock - newest) * 1000)
        if state['sprite'] != before[1]['sprite']:
            bucket['mismatches'] += 1
            if len(bucket['examples']) < 10:
                bucket['examples'].append(dict(clock=clock, state=reference, sourceTime=before[0],
                                                expected=before[1]['sprite'], actual=state['sprite']))
    return dict(result)


def analyze(folder):
    native = rows(folder / 'server-motion.jsonl')
    mapping = {}
    for row in native:
        if row.get('kind') == 'coop_family_ids':
            for entry in detail(row).get('ids', '').split('|'):
                if ':' in entry:
                    key, family = entry.split(':', 1)
                    mapping[key] = family

    def normalize(source):
        result = []
        for row in source:
            if row.get('kind') not in ('r06_native', 'r06_view'):
                continue
            state = detail(row)
            state['family'] = mapping.get(state['id'], 'UnclassifiedBoss')
            result.append(dict(row, kind='r05_native' if row['kind'] == 'r06_native' else 'r05_view',
                               detail=';'.join(key + '=' + value for key, value in state.items())))
        return result

    reports = {}
    for client in (1, 2):
        observed = rows(folder / f'client-{client}-motion.jsonl')
        report = compare(normalize(native), normalize(observed))
        # The projectile comparator's +/-50ms neighbourhood can exclude a valid
        # held pose 60ms old and include only the FUTURE edge. Keep its diagnostic
        # count, but score bosses against the preceding authority state instead.
        report['problems'] = [p for p in report['problems'] if not p.endswith(': unexpected sprite sequence')]
        steps = pose_steps(native, observed)
        for family, bucket in report['families'].items():
            bucket['neighborhoodSpriteMismatch'] = bucket.pop('spriteMismatch')
            family_steps = [value for key, value in steps.items() if mapping.get(key, 'UnclassifiedBoss') == family]
            bucket['poseCompared'] = sum(value['compared'] for value in family_steps)
            bucket['spriteMismatch'] = sum(value['mismatches'] for value in family_steps)
            if bucket['spriteMismatch']:
                report['problems'].append(f'{family}: unexpected held pose')
        report['poseSteps'] = steps
        report['deliveryPoseHolds'] = sum(v['deliveryPoseHolds'] for v in steps.values())
        report['deliveryHoldMaxMs'] = max((v['deliveryHoldMaxMs'] for v in steps.values()), default=0)
        report['status'] = ('FAILED' if report['problems'] else
                            'PASSED' if sum(v['compared'] for v in steps.values()) and report['observedEntities'] else 'INCONCLUSIVE')
        report['limitations'].append('Discrete sprites use preceding authority state; gaps >150ms and pause excluded explicitly.')
        report['limitations'].append('Manual pose checked at received state time; deliveryPoseHolds separately records a newer source pose not received. This does not establish stall-free delivery.')
        reports[f'client-{client}'] = report
    return dict(reports=reports)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('folder', type=Path)
    args = parser.parse_args()
    result = analyze(args.folder)
    (args.folder / 'r06-analysis.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
    for name, report in result['reports'].items():
        print(name, report['status'], report['problems'])
    raise SystemExit(1 if any(r['status'] == 'FAILED' for r in result['reports'].values()) else
                     2 if any(r['status'] == 'INCONCLUSIVE' for r in result['reports'].values()) else 0)
