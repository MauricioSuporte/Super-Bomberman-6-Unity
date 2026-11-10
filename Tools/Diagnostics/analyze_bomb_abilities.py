"""A14 evidence: actual owner predictions AND observer playback, not only server checkpoints."""
import argparse
from collections import Counter
import json
from pathlib import Path
import re


def inspect_action(rows, bomb_id, expected_kinds, owner):
    relevant = [r for r in rows if r.get('bombNetId') == bomb_id]
    begins = Counter(r.get('detail', '').split(';')[0] if r['kind'] == 'bomb_motion_prediction_begin' else 'Kick'
                     for r in relevant if r.get('kind') in ('bomb_motion_prediction_begin', 'kick_prediction_begin'))
    confirms = Counter((re.search(r'kind=(\w+)', r.get('detail', '')) or [None, 'Kick'])[1]
                       for r in relevant if r.get('kind') in ('bomb_motion_prediction_confirm', 'kick_prediction_confirm'))
    rejects = [r.get('detail') for r in relevant if r.get('kind') in ('bomb_motion_prediction_reject', 'kick_prediction_reject')]
    applied = sum(r.get('kind') in ('bomb_motion_apply', 'kick_segment_apply') for r in relevant)
    samples = sum(r.get('kind') in ('bomb_motion_sample', 'kick_presentation_sample') for r in relevant)
    problems = []
    if owner:
        for kind in expected_kinds:
            if not begins[kind]: problems.append('missing owner prediction: ' + kind)
            if not confirms[kind]: problems.append('missing owner confirmation: ' + kind)
        if rejects: problems.append('legitimate fixture action was rejected')
    elif begins:
        problems.append('observer predicted another player action')
    if not applied: problems.append('no approved segment was presented')
    if not samples: problems.append('no actual presenter samples')
    return dict(predictions=dict(begins), confirmations=dict(confirms), rejections=rejects,
                appliedSegments=applied, presentedSamples=samples, problems=problems)


def inspect_capture(rows):
    return bool(rows) and any(r.get('kind') == 'capture_start' for r in rows) and not any(
        r.get('kind') == 'capture_limit' or r.get('dropped', 0) > 0 for r in rows)


def analyze(folder):
    config = json.loads((folder/'config.json').read_text(encoding='utf-8-sig'))
    result = json.loads((folder/'result.json').read_text(encoding='utf-8-sig'))
    server = (folder/'server.log').read_text(encoding='utf-8-sig')
    starts = re.findall(r'\[LoadTest\] a14 native started family=(\w+) player=(\d+) bomb=(\d+)', server)
    problems = []
    server_rows = [json.loads(line) for line in (folder/'server-motion.jsonl').read_text(encoding='utf-8-sig').splitlines() if line.strip()]
    if not inspect_capture(server_rows): problems.append('server: invalid/incomplete measurement')
    if '[LoadTest] a14 completed actors=2' not in server: problems.append('both native owners did not complete')
    pairs = [(family, int(player)) for family, player, _ in starts]
    for player in (1, 2):
        for family in ('Kick', 'Punch', 'Glove', 'Blue', 'Yellow', 'Rubber'):
            if pairs.count((family, player)) != 1: problems.append(f'native case coverage: {family}/{player}')
            if f'[LoadTest] a14 completed family={family} player={player}' not in server:
                problems.append(f'native case incomplete: {family}/{player}')
        if f'[LoadTest] a14 completed family=Bounce player={player}' not in server:
            problems.append(f'native bounce missing: {player}')
    hashes = config['assemblySha256']
    if hashes.get('server') != hashes.get('client'): problems.append('server/client assembly mismatch')
    if hashes != result.get('assemblySha256'): problems.append('config/result assembly mismatch')
    if result.get('errors'): problems.append('smoke execution errors')
    clients = {}
    for name in ('client-1', 'client-2'):
        rows = [json.loads(line) for line in (folder/(name+'-motion.jsonl')).read_text(encoding='utf-8-sig').splitlines() if line.strip()]
        owners = {r['player'] for r in rows if r.get('localPlayer') and r.get('player') in (1, 2)}
        if len(owners) != 1: problems.append(name + ': ambiguous local owner')
        owner = next(iter(owners)) if len(owners) == 1 else None
        if not inspect_capture(rows): problems.append(name + ': invalid/incomplete measurement')
        actions = []
        for family, player, bomb in starts:
            expected = ('Lift', 'Throw') if family == 'Glove' else ('Punch',) if family in ('Punch', 'Blue', 'Rubber') else (family,)
            action = inspect_action(rows, int(bomb), expected, int(player) == owner)
            action.update(family=family, player=int(player), bomb=int(bomb), owner=int(player) == owner)
            for issue in action['problems']: problems.append(f'{name}/{family}/{player}: {issue}')
            actions.append(action)
        bounces = {p: sum(r.get('kind') == 'player_bounce_apply' and r.get('player') == p and
                          'moving=True' in (r.get('detail') or '') for r in rows) for p in (1, 2)}
        if not all(bounces.values()): problems.append(name + ': missing bounce presentation')
        clients[name] = dict(owner=owner, actions=actions, bounceApplies=bounces)
    if {client['owner'] for client in clients.values()} != {1, 2}:
        problems.append('two distinct local owners were not measured')
    return dict(status='FAILED' if problems else 'PASSED', assemblySha256=hashes, clients=clients, problems=problems,
                note='Development local fixture. Counts prove CPU presentation and confirmation, not hardware/GPU latency or human visual acceptance.')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('folder', type=Path)
    parser.add_argument('--output', required=True, type=Path)
    args = parser.parse_args()
    report = analyze(args.folder)
    args.output.write_text(json.dumps(report, indent=2), encoding='utf-8')
    print(json.dumps({'status': report['status'], 'problems': report['problems']}, indent=2))
    return 0 if report['status'] == 'PASSED' else 2


if __name__ == '__main__':
    raise SystemExit(main())
