"""Bounded Windows smoke: actual Mirror clients instantiate replicated pickups."""
import argparse
import hashlib
import json
import os
import re
from pathlib import Path
import secrets
import shutil
import subprocess
import time
from benchmark_local import host_memory


def run(server, client, output, port, mount_input, seconds, prediction_items=False,
        mode='BattleMode', scene=None, diagnostics=False, require_unlocked=False, presenter_removal=False, revenge_input=False, mount_cycles=False, rendered=False, launcher_kind=None, mount_visuals=False):
    if mount_visuals and (mode != 'BattleMode' or not diagnostics or mount_input or launcher_kind or revenge_input):
        raise ValueError('mount visual fixture requires BattleMode diagnostics without other input fixtures')
    if launcher_kind and (launcher_kind not in ('arena', 'spring', 'cannon', 'rope') or not diagnostics or mount_input or revenge_input or presenter_removal):
        raise ValueError('launcher fixture requires diagnostics and no other input fixture')
    if rendered and not (mount_cycles or launcher_kind or mount_visuals):
        raise ValueError('rendered captures require mount cycles or launchers')
    if mount_cycles and (not mount_input or not diagnostics):
        raise ValueError('mount cycles require mount input and diagnostics')
    if revenge_input and (mode != 'BattleMode' or not diagnostics or mount_input):
        raise ValueError('revenge input requires BattleMode diagnostics without the mount input fixture')
    output = output.resolve()
    output.mkdir(parents=True, exist_ok=False)
    owned, handles = [], []
    started = time.monotonic()
    try:
        if host_memory()['availableMiB'] < 3072: raise RuntimeError('need at least 3 GiB of available RAM for the three-process smoke')
        assembly_hashes = {}
        for role, binary in (('server', server), ('client', client)):
            assembly = (binary.parent/'SB6_Data/Managed/Assembly-CSharp.dll').read_bytes()
            if b'LocalNetworkInputSmoke' not in assembly:
                raise RuntimeError('the durable actor smoke requires Development players')
            assembly_hashes[role] = hashlib.sha256(assembly).hexdigest()
        (output/'config.json').write_text(json.dumps(dict(mode=mode, scene=scene, seconds=seconds, requireUnlocked=require_unlocked, presenterRemoval=presenter_removal,
            revengeInput=revenge_input, mountCycles=mount_cycles, rendered=rendered, launcherKind=launcher_kind, mountVisuals=mount_visuals, assemblySha256=assembly_hashes), indent=2), encoding='utf-8')
        env = dict(os.environ, SB6_ROOM_ID='spawn-regression', SB6_ROOM_TOKEN=secrets.token_hex(32),
                   SB6_ROOM_STATUS=str(output/'status.json'))
        def launch(binary, name, arguments, environment=None):
            handle = (output/(name+'-launcher.log')).open('wb')
            handles.append(handle)
            process = subprocess.Popen([str(binary), '-batchmode'] + ([] if rendered and name.startswith('client-') else ['-nographics']) + ['-logFile',
                                        str(output/(name+'.log'))] + arguments,
                                       cwd=binary.parent, env=environment, stdout=handle,
                                       stderr=subprocess.STDOUT, creationflags=subprocess.CREATE_NO_WINDOW)
            owned.append(process)
            return process
        room = launch(server, 'server', ['-server', '-room-mode', mode, '-port', str(port),
                        '-server-idle-seconds', '0', '-load-test-server', '-load-test-players', '2',
                        '-load-test-duration', str(seconds)] +
                        (['-load-test-com', '-load-test-spawns'] if mode == 'BattleMode' else []) +
                        (['-load-test-scene', scene] if scene else []) +
                        (['-online-motion-logs'] if diagnostics else []) +
                        (['-load-test-presenter-removal'] if presenter_removal else []) +
                        (['-load-test-revenge-input'] if revenge_input else []) +
                        (['-load-test-full-input'] if mount_input else []) +
                        (['-load-test-mount-cycles'] if mount_cycles else []) +
                        (['-load-test-launchers', launcher_kind] if launcher_kind else []) +
                        (['-load-test-mount-visuals'] if mount_visuals else []) +
                        (['-load-test-item-motion'] if prediction_items else []), env)
        for _ in range(60):
            if room.poll() is not None: raise RuntimeError('server exited before accepting clients')
            try:
                if json.loads((output/'status.json').read_text(encoding='utf-8-sig'))['available']: break
            except (OSError, ValueError, KeyError): pass
            time.sleep(1)
        else: raise RuntimeError('server readiness timeout')
        # First launch can spend tens of seconds loading Unity before the
        # development timer starts. Bound gameplay from readiness, not Popen.
        deadline = time.monotonic() + seconds + 50
        for index in range(2):
            if host_memory()['availableMiB'] < 1536: raise RuntimeError('RAM reserve reached before client launch')
            launch(client, 'client-'+str(index+1), ['-online-join', 'kcp://127.0.0.1:'+str(port)] +
                   ['-load-test-client'] +
                   (['-online-motion-logs'] if diagnostics else []) +
                   (['-load-test-presenter-removal'] if presenter_removal else []) +
                   (['-load-test-revenge-input'] if revenge_input else []) +
                   (['-load-test-real-input'] if mount_input else []) +
                   (['-load-test-mount-cycles'] if mount_cycles else []) +
                   (['-load-test-launchers', launcher_kind] if launcher_kind else []) +
                   (['-load-test-mount-visuals'] if mount_visuals else []) +
                   (['-load-test-mount-captures', str(output/('client-'+str(index+1)+'-frames')), '-screen-width', '1024', '-screen-height', '896'] if rendered else []) +
                   (['-load-test-item-motion'] if prediction_items else []) +
                   ['-local-diagnostic-name', 'SB6Smoke'+str(index+1)])
            time.sleep(1)
        while time.monotonic() < deadline and room.poll() is None:
            if any(process.poll() is not None for process in owned[1:]):
                # The server disconnects peers before its own Unity process exits.
                # A Development client can therefore flush and quit just before it.
                try: room.wait(timeout=10)
                except subprocess.TimeoutExpired: raise RuntimeError('client exited unexpectedly while server remained active')
                break
            time.sleep(1)
        if room.poll() is None: raise RuntimeError('development server did not finish within the bound')
        if room.returncode != 0: raise RuntimeError('development server failed')
        for process in owned[1:]:
            try: process.wait(timeout=15)
            except subprocess.TimeoutExpired: raise RuntimeError('client did not close its capture after server shutdown')
            if process.returncode != 0: raise RuntimeError('development client failed during shutdown')
        server_text = (output/'server.log').read_text(encoding='utf-8-sig', errors='replace')
        markers = ['[LoadTest] completed']
        if mode == 'BattleMode':
            markers += ['revealed=LineBomb', 'revealed=Mole', 'revealed=Tank',
                        'spawn regression removed LineBomb/Mole/Tank']
        else:
            markers += ['[Room] scene='+scene]
            if len(set(re.findall(r'\[Room\] join connection=(-?\d+)', server_text))) != 2:
                raise RuntimeError('cooperative smoke did not join exactly two client connections')
        for marker in markers:
            if marker not in server_text: raise RuntimeError('missing server checkpoint: '+marker)
        if mount_cycles:
            if '[LoadTest] mount-cycles completed cycles=3 players=2' not in server_text:
                raise RuntimeError('server did not complete three remount/dismount cycles for both owners')
        if launcher_kind and '[LoadTest] launchers completed kind='+launcher_kind+' players=2' not in server_text:
            raise RuntimeError('native launcher fixture did not finish with two actors')
        if mount_visuals and '[LoadTest] mount visuals completed players=2' not in server_text:
            raise RuntimeError('native mount visual fixture did not finish with two owners')
        if launcher_kind == 'rope' and '[LoadTest] rope blocked prefix player=' not in server_text:
            raise RuntimeError('native rope fixture did not exercise an early blocker')
        removed_bomb = None
        if revenge_input:
            if '[LoadTest] revenge-input completed players=2' not in server_text:
                raise RuntimeError('server did not confirm three native shots and boost from both owners')
            if len(set(re.findall(r'\[LoadTest\] revenge-input confirmed player=(\d+) shots=3', server_text))) != 2:
                raise RuntimeError('revenge shots were not confirmed for two distinct players')
        if presenter_removal:
            match = re.search(r'presenter-removal started bomb=(\d+)', server_text)
            if not match: raise RuntimeError('missing presenter removal start')
            removed_bomb = int(match[1])
            for marker in ('removed source', 'completed'):
                if 'presenter-removal '+marker+' bomb='+str(removed_bomb) not in server_text:
                    raise RuntimeError('missing presenter removal '+marker)
        round_reloads = server_text.count('[Room] scene='+('BattleMode_4' if launcher_kind == 'arena' else 'BattleMode_11' if launcher_kind == 'rope' else 'BattleMode_15'))
        if mode == 'BattleMode' and seconds >= 180 and round_reloads < 2: raise RuntimeError('round reload was not exercised')
        if mount_input:
            for marker in ('full-input dismount confirmed players=2', 'full-input world pickup replicated type=Mole',
                           'full-input world pickup replicated type=Tank'):
                if marker not in server_text: raise RuntimeError('missing input checkpoint: '+marker)
        failures = {}
        captures = {}
        for path in [output/'server.log', output/'client-1.log', output/'client-2.log']:
            lines = path.read_text(encoding='utf-8-sig', errors='replace').splitlines()
            if mount_input and path.name.startswith('client-') and not any('full-input dismount acknowledged' in line for line in lines):
                raise RuntimeError('client did not acknowledge the authoritative dismount: '+path.name)
            if mount_cycles and path.name.startswith('client-') and sum('full-input dismount acknowledged' in line for line in lines) < 4:
                raise RuntimeError('client did not acknowledge four dismounts: '+path.name)
            if prediction_items and path.name.startswith('client-'):
                for item_type in ('Skull', 'LineBomb'):
                    if not any('item-motion presented type='+item_type in line for line in lines):
                        raise RuntimeError('client did not present approved item motion '+item_type+': '+path.name)
            if presenter_removal and path.name.startswith('client-') and not any('presenter-removal input suspended' in line for line in lines):
                raise RuntimeError('client input was not suspended before synthetic presenter removal: '+path.name)
            if revenge_input and path.name.startswith('client-'):
                if not any('revenge-input shot acknowledged' in line and 'shots=3' in line for line in lines):
                    raise RuntimeError('client did not acknowledge three native revenge shots: '+path.name)
                if sum('revenge-input charged release' in line for line in lines) != 3:
                    raise RuntimeError('client did not exercise exactly three charged releases: '+path.name)
            errors = [line for line in lines if any(marker.lower() in line.lower() for marker in (
                'Exception', '[NetSpawn]', 'Could not spawn', 'Failed to spawn', 'Spawn prefab not found',
                'has another NetworkIdentity', 'Spawn scene object not found', 'no receiver', 'invalid Command',
                'Scene change is already in progress', 'called when server was not active',
                'called when client was not active', 'Catalog asset unavailable'))]
            if errors: failures[path.name] = list(dict.fromkeys(errors))
            if diagnostics:
                capture_lines = [line.split('[GameplayLatency] Capture: ', 1)[1].strip()
                                 for line in lines if '[GameplayLatency] Capture: ' in line]
                if not capture_lines: raise RuntimeError('missing diagnostic capture: '+path.name)
                source = Path(capture_lines[-1])
                if not source.name.startswith('motion-') or source.suffix != '.jsonl':
                    raise RuntimeError('invalid diagnostic capture name')
                destination = output/(path.stem+'-motion.jsonl')
                shutil.copy2(source, destination)
                rows = [json.loads(line) for line in destination.read_text(encoding='utf-8-sig').splitlines()]
                if scene and not any(row.get('scene') == scene for row in rows):
                    raise RuntimeError('diagnostics did not reach cooperative scene: '+path.name)
                unlocked = sorted({row.get('player') for row in rows if row.get('kind') == 'motion_sample' and
                    row.get('scene') == scene and 'prediction_block=none ' in row.get('detail', '') and
                    (path.stem == 'server' or row.get('localPlayer'))})
                if require_unlocked and (len(unlocked) < (2 if path.stem == 'server' else 1)):
                    raise RuntimeError('cooperative intro never released player movement: '+path.name)
                transfer_samples = 0
                if presenter_removal and path.stem != 'server':
                    transfers = [row for row in rows if row.get('kind') == 'bomb_motion_presenter_transfer' and row.get('bombNetId') == removed_bomb]
                    for transfer in transfers:
                        transfer_started = transfer['realtime']
                        next_apply = min((row['realtime'] for row in rows if row.get('kind') in ('bomb_motion_apply', 'kick_segment_apply') and
                            row.get('bombNetId') == removed_bomb and row['realtime'] > transfer_started), default=float('inf'))
                        transfer_samples += sum(1 for row in rows if row.get('kind') in ('bomb_motion_sample', 'kick_presentation_sample') and
                            row.get('bombNetId') == removed_bomb and transfer_started < row['realtime'] < next_apply and
                            any(abs(row.get('delta', {}).get(axis, 0)) > .0001 for axis in ('x', 'y')))
                    if not transfer_samples: raise RuntimeError('approved arc did not keep moving between presenter transfer and the next apply: '+path.name)
                captures[path.stem] = dict(file=destination.name, unlockedPlayers=unlocked,
                    presenterTransferMovingSamples=transfer_samples,
                    familyCostReports=sum(row.get('kind') == 'coop_family_cost' for row in rows))
                if mount_visuals and path.stem != 'server':
                    from validate_mount_visual_smoke import validate_mount_visual_capture
                    captures[path.stem]['mountVisuals'] = validate_mount_visual_capture(rows)
                if launcher_kind and path.stem != 'server':
                    from validate_launcher_smoke import validate_launcher_capture
                    captures[path.stem]['launchers'] = validate_launcher_capture(rows, launcher_kind)
                if mount_cycles and path.stem != 'server':
                    from validate_mount_smoke import validate_mount_capture
                    captures[path.stem]['riding'] = validate_mount_capture(rows)
                if revenge_input:
                    from validate_revenge_smoke import validate_revenge_capture
                    captures[path.stem]['revenge'] = validate_revenge_capture(rows, path.stem == 'server')
        if diagnostics and mode != 'BattleMode' and not captures['server']['familyCostReports']:
            raise RuntimeError('cooperative server produced no family cost reports')
        events = output/'events.jsonl'
        warning_events = []
        if events.exists():
            entries = [json.loads(line) for line in events.read_text(encoding='utf-8-sig').splitlines()]
            errors = [event['message'] for event in entries if event.get('level') in ('Error', 'Exception', 'Assert')]
            warning_events = [event['message'] for event in entries if event.get('level') == 'Warning']
            if errors: failures['server-events'] = list(dict.fromkeys(errors))
        if rendered:
            for index in (1, 2):
                if len({hashlib.sha256(frame.read_bytes()).digest() for frame in (output/('client-'+str(index)+'-frames')).glob('*.png')}) < 3:
                    raise RuntimeError('missing distinct rendered arc captures for client '+str(index))
        result = dict(actualMirrorClients=2, simulatedClients=False, rendered=rendered, mountCycles=mount_cycles, launcherKind=launcher_kind, mountVisuals=mount_visuals,
                      mode=mode, scene=scene, captures=captures, assemblySha256=assembly_hashes, requireUnlocked=require_unlocked, presenterRemoval=presenter_removal,
                      spawnCheckpoints=mode == 'BattleMode', arenaLoadRequests=round_reloads,
                      syntheticHardwareInput=mount_input or revenge_input or bool(launcher_kind) or mount_visuals, revengeInput=revenge_input, approvedItemMotion=prediction_items,
                      serverDevelopmentDurableActors=True, requestedSeconds=seconds,
                      serverWarningEvents=len(warning_events), serverWarnings=list(dict.fromkeys(warning_events)),
                      errors=failures, seconds=time.monotonic()-started)
        (output/'result.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
        if failures: raise RuntimeError('replication errors; inspect result.json')
        print(json.dumps(result), flush=True)
    except BaseException as error:
        (output/'failure.json').write_text(json.dumps(dict(error=str(error))), encoding='utf-8')
        raise
    finally:
        for process in reversed(owned):
            if process.poll() is None: process.terminate()
        for process in owned:
            try: process.wait(timeout=10)
            except subprocess.TimeoutExpired: process.kill(); process.wait()
        for handle in handles: handle.close()


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--server', type=Path, required=True)
    parser.add_argument('--client', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--port', type=int, default=18777)
    parser.add_argument('--prediction-items', action='store_true', help='Require two clients to present approved Skull/LineBomb motion')
    parser.add_argument('--mode', choices=('BattleMode', 'NormalGame', 'BossRush'), default='BattleMode')
    parser.add_argument('--scene', help='Enabled Stage_* scene for a cooperative Development smoke')
    parser.add_argument('--diagnostics', action='store_true', help='Enable and copy the three process motion logs')
    parser.add_argument('--require-unlocked', action='store_true', help='Require the server and each owner to regain free movement after the cooperative intro')
    parser.add_argument('--presenter-removal', action='store_true', help='Development Stage_1-1 fixture removes the player presenting an approved native bomb arc')
    parser.add_argument('--launchers', choices=('arena', 'spring', 'cannon', 'rope'), help='Exercise native launcher trajectories with two actors')
    parser.add_argument('--mount-visuals', action='store_true', help='Exercise native egg mount, death variants, Pink jump, emote and AFK handoff')
    parser.add_argument('--mount-cycles', action='store_true', help='Require three native remount cycles and egg transfers per owner')
    parser.add_argument('--rendered', action='store_true', help='Enable client graphics and capture mount arcs for visual review')
    parser.add_argument('--mount-input', action='store_true', help='Development client supplies Select through the normal input path')
    parser.add_argument('--revenge-input', action='store_true', help='Development Battle fixture exercises C boost and three charged A releases per owner')
    parser.add_argument('--seconds', type=int, default=210, help='At least 180 also requires a round reload checkpoint')
    args = parser.parse_args()
    if os.name != 'nt': parser.error('Windows binaries required')
    if not 1024 <= args.port <= 65000: parser.error('invalid port')
    if not 60 <= args.seconds <= 300: parser.error('duration must be 60-300 seconds')
    if args.mode != 'BattleMode' and (args.mount_input or args.prediction_items or not args.scene):
        parser.error('cooperative smoke requires --scene and cannot use Battle spawn fixtures')
    if args.require_unlocked and (not args.diagnostics or args.mode == 'BattleMode'):
        parser.error('--require-unlocked requires cooperative --diagnostics')
    if args.presenter_removal and (not args.diagnostics or args.mode != 'NormalGame' or args.scene != 'Stage_1-1'):
        parser.error('--presenter-removal requires NormalGame/Stage_1-1 and --diagnostics')
    if args.revenge_input and (args.mode != 'BattleMode' or not args.diagnostics or args.mount_input):
        parser.error('--revenge-input requires BattleMode/--diagnostics without --mount-input')
    if args.scene and (args.mode == 'BattleMode' or not args.scene.startswith('Stage_') or
                       any(character in args.scene for character in '/\\')):
        parser.error('scene must be a cooperative Stage_* name')
    run(args.server.resolve(), args.client.resolve(), args.output, args.port, args.mount_input, args.seconds,
        args.prediction_items, args.mode, args.scene, args.diagnostics, args.require_unlocked, args.presenter_removal, args.revenge_input, args.mount_cycles, args.rendered, args.launchers, args.mount_visuals)
