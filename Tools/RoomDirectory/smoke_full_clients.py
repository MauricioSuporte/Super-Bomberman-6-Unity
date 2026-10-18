"""Bounded Windows smoke: actual Mirror clients instantiate replicated pickups."""
import argparse
import json
import os
from pathlib import Path
import secrets
import subprocess
import time
from benchmark_local import host_memory


def run(server, client, output, port, mount_input, seconds):
    output = output.resolve()
    output.mkdir(parents=True, exist_ok=False)
    owned, handles = [], []
    started = time.monotonic()
    try:
        if host_memory()['availableMiB'] < 3072: raise RuntimeError('need at least 3 GiB of available RAM for the three-process smoke')
        if mount_input and b'LocalNetworkInputSmoke' not in (client.parent/'SB6_Data/Managed/Assembly-CSharp.dll').read_bytes():
            raise RuntimeError('mount input smoke requires a Development client')
        env = dict(os.environ, SB6_ROOM_ID='spawn-regression', SB6_ROOM_TOKEN=secrets.token_hex(32),
                   SB6_ROOM_STATUS=str(output/'status.json'))
        def launch(binary, name, arguments, environment=None):
            handle = (output/(name+'-launcher.log')).open('wb')
            handles.append(handle)
            process = subprocess.Popen([str(binary), '-batchmode', '-nographics', '-logFile',
                                        str(output/(name+'.log'))] + arguments,
                                       cwd=binary.parent, env=environment, stdout=handle,
                                       stderr=subprocess.STDOUT, creationflags=subprocess.CREATE_NO_WINDOW)
            owned.append(process)
            return process
        room = launch(server, 'server', ['-server', '-room-mode', 'BattleMode', '-port', str(port),
                        '-server-idle-seconds', '0', '-load-test-server', '-load-test-players', '2',
                        '-load-test-com', '-load-test-spawns', '-load-test-duration', str(seconds)] +
                        (['-load-test-full-input'] if mount_input else []), env)
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
                   (['-load-test-real-input'] if mount_input else []))
            time.sleep(1)
        while time.monotonic() < deadline and room.poll() is None:
            if any(process.poll() is not None for process in owned[1:]): raise RuntimeError('client exited unexpectedly')
            time.sleep(1)
        if room.poll() is None: raise RuntimeError('development server did not finish within the bound')
        if room.returncode != 0: raise RuntimeError('development server failed')
        server_text = (output/'server.log').read_text(encoding='utf-8-sig', errors='replace')
        for marker in ('revealed=LineBomb', 'revealed=Mole', 'revealed=Tank',
                       'spawn regression removed LineBomb/Mole/Tank', '[LoadTest] completed'):
            if marker not in server_text: raise RuntimeError('missing server checkpoint: '+marker)
        round_reloads = server_text.count('[Room] scene=BattleMode_15')
        if seconds >= 180 and round_reloads < 2: raise RuntimeError('round reload was not exercised')
        if mount_input:
            for marker in ('full-input dismount confirmed players=2', 'full-input world pickup replicated type=Mole',
                           'full-input world pickup replicated type=Tank'):
                if marker not in server_text: raise RuntimeError('missing input checkpoint: '+marker)
        failures = {}
        for path in [output/'server.log', output/'client-1.log', output/'client-2.log']:
            lines = path.read_text(encoding='utf-8-sig', errors='replace').splitlines()
            if mount_input and path.name.startswith('client-') and not any('full-input dismount acknowledged' in line for line in lines):
                raise RuntimeError('client did not acknowledge the authoritative dismount: '+path.name)
            errors = [line for line in lines if any(marker.lower() in line.lower() for marker in (
                'Exception', '[NetSpawn]', 'Could not spawn', 'Failed to spawn', 'Spawn prefab not found',
                'has another NetworkIdentity', 'Spawn scene object not found', 'no receiver', 'invalid Command',
                'Scene change is already in progress'))]
            if errors: failures[path.name] = list(dict.fromkeys(errors))
        events = output/'events.jsonl'
        warning_events = []
        if events.exists():
            entries = [json.loads(line) for line in events.read_text(encoding='utf-8-sig').splitlines()]
            errors = [event['message'] for event in entries if event.get('level') in ('Error', 'Exception', 'Assert')]
            warning_events = [event['message'] for event in entries if event.get('level') == 'Warning']
            if errors: failures['server-events'] = list(dict.fromkeys(errors))
        result = dict(actualMirrorClients=2, simulatedClients=False, rendered=False,
                      spawnCheckpoints=True, arenaLoadRequests=round_reloads,
                      syntheticHardwareInput=mount_input,
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
    parser.add_argument('--mount-input', action='store_true', help='Development client supplies Select through the normal input path')
    parser.add_argument('--seconds', type=int, default=210, help='At least 180 also requires a round reload checkpoint')
    args = parser.parse_args()
    if os.name != 'nt': parser.error('Windows binaries required')
    if not 1024 <= args.port <= 65000: parser.error('invalid port')
    if not 60 <= args.seconds <= 300: parser.error('duration must be 60-300 seconds')
    run(args.server.resolve(), args.client.resolve(), args.output, args.port, args.mount_input, args.seconds)
