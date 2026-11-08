"""R02 bounded Windows smoke: real disconnect and reconnect of two Development clients through the supported room flow.

Room A runs the native co-op fixture. Client 2 leaves through BombermanNetworkManager.LeaveRoomToBrowser; the dedicated
room closes for everyone (supported behaviour), client 1 is disconnected and both return to the room browser. Room B is
then started and both clients join it through the browser's StartClient path (-load-test-rejoin). Room B runs the full
co-op fixture (pause, stage reload, second pause). Every client stream begin must report no prior samples, removals,
views or statistics, so no history of another scene or session survives.
"""
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

ERROR_MARKERS = ('Exception', '[NetSpawn]', 'Could not spawn', 'Failed to spawn', 'Spawn prefab not found',
                 'has another NetworkIdentity', 'Spawn scene object not found', 'no receiver', 'invalid Command',
                 'Scene change is already in progress', 'called when server was not active',
                 'called when client was not active', 'Catalog asset unavailable')
BEGIN = re.compile(r'\[Coop\] stream begin scene=(\S+) priorSamples=(\d+) priorRemovals=(\d+) priorViews=(\d+) priorStats=(\d+) priorBombSamples=(\d+)')


def read(path):
    return path.read_text(encoding='utf-8-sig', errors='replace')


def check_reconnect(output, scene):
    """Validates the logs of a finished run; returns the summary or raises RuntimeError."""
    server_a, server_b = read(output/'server-a.log'), read(output/'server-b.log')
    if '[Room] scene='+scene not in server_a: raise RuntimeError('room A never reached the stage')
    if len(set(re.findall(r'\[Room\] join connection=(-?\d+)', server_a))) != 2: raise RuntimeError('room A did not join two clients')
    if not re.search(r'\[Room\] disconnect connection=-?\d+ player=[1-9]', server_a): raise RuntimeError('room A saw no player disconnect')
    if '[LoadTest] coop-world started scene='+scene not in server_a: raise RuntimeError('room A fixture did not start before the leave')
    if len(set(re.findall(r'\[Room\] join connection=(-?\d+)', server_b))) != 2: raise RuntimeError('room B did not join two clients')
    for marker in ('coop-world started scene='+scene, 'coop-world paused index=1', 'coop-world resumed index=1',
                   'coop-world reloaded scene='+scene, 'coop-world paused index=2', 'coop-world resumed index=2',
                   'coop-world completed scene='+scene, 'completed'):
        if '[LoadTest] '+marker not in server_b: raise RuntimeError('room B missing checkpoint: '+marker)
    clients = {}
    for index in (1, 2):
        text = read(output/('client-'+str(index)+'.log'))
        left = '[LoadTest] reconnect leaving room scene='+scene in text
        if left != (index == 2): raise RuntimeError('unexpected leaver: client '+str(index))
        waiting = re.findall(r'\[LoadTest\] client disconnected; awaiting rejoin left=(True|False)', text)
        if waiting != [('True' if index == 2 else 'False')]: raise RuntimeError('client '+str(index)+' did not await exactly one rejoin')
        if '[LoadTest] client disconnected; closing capture' not in text: raise RuntimeError('client '+str(index)+' did not close after room B')
        begins = [dict(scene=m[0], samples=int(m[1]), removals=int(m[2]), views=int(m[3]), stats=int(m[4]), bombs=int(m[5])) for m in BEGIN.findall(text)]
        # Room A stage, room B stage, room B reload.
        if len(begins) < 3: raise RuntimeError('client '+str(index)+' logged fewer than three stream begins')
        stale = [b for b in begins if b['samples'] or b['removals'] or b['views'] or b['stats'] or b['bombs']]
        if stale: raise RuntimeError('client '+str(index)+' kept history across a scene/session: '+json.dumps(stale))
        errors = list(dict.fromkeys(line for line in text.splitlines() if any(m.lower() in line.lower() for m in ERROR_MARKERS)))
        clients['client-'+str(index)] = dict(left=left, streamBegins=len(begins), errors=errors)
    errors = {name: list(dict.fromkeys(line for line in read(output/(name+'.log')).splitlines() if any(m.lower() in line.lower() for m in ERROR_MARKERS)))
              for name in ('server-a', 'server-b')}
    errors.update({name: value['errors'] for name, value in clients.items()})
    errors = {name: value for name, value in errors.items() if value}
    return dict(clients=clients, errors=errors)


def run(server, client, output, port, scene, seconds, leave_after, rendered, extra_args):
    output = output.resolve()
    output.mkdir(parents=True, exist_ok=False)
    owned, handles = [], []
    started = time.monotonic()
    try:
        if host_memory()['availableMiB'] < 3072: raise RuntimeError('need at least 3 GiB of available RAM for the three-process smoke')
        hashes = {}
        for role, binary in (('server', server), ('client', client)):
            assembly = (binary.parent/'SB6_Data/Managed/Assembly-CSharp.dll').read_bytes()
            if b'LocalNetworkInputSmoke' not in assembly: raise RuntimeError('the reconnect smoke requires Development players')
            hashes[role] = hashlib.sha256(assembly).hexdigest()
        (output/'config.json').write_text(json.dumps(dict(scene=scene, seconds=seconds, leaveAfter=leave_after, rendered=rendered,
            extraArgs=list(extra_args), ports=[port, port+1], assemblySha256=hashes), indent=2), encoding='utf-8')

        def launch(binary, name, arguments, environment=None):
            handle = (output/(name+'-launcher.log')).open('wb')
            handles.append(handle)
            process = subprocess.Popen([str(binary), '-batchmode'] + ([] if rendered and name.startswith('client-') else ['-nographics']) +
                                       ['-logFile', str(output/(name+'.log'))] + arguments, cwd=binary.parent, env=environment,
                                       stdout=handle, stderr=subprocess.STDOUT, creationflags=subprocess.CREATE_NO_WINDOW)
            owned.append(process)
            return process

        def room(name, room_port):
            status = output/('status-'+name[-1]+'.json')
            env = dict(os.environ, SB6_ROOM_ID='reconnect-'+name, SB6_ROOM_TOKEN=secrets.token_hex(32), SB6_ROOM_STATUS=str(status))
            process = launch(server, name, ['-server', '-room-mode', 'NormalGame', '-port', str(room_port), '-server-idle-seconds', '0',
                                            '-load-test-server', '-load-test-players', '2', '-load-test-duration', str(seconds),
                                            '-load-test-scene', scene, '-online-motion-logs', '-load-test-coop-world'] + list(extra_args), env)
            for _ in range(60):
                if process.poll() is not None: raise RuntimeError(name+' exited before accepting clients')
                try:
                    if json.loads(status.read_text(encoding='utf-8-sig'))['available']: return process
                except (OSError, ValueError, KeyError): pass
                time.sleep(1)
            raise RuntimeError(name+' readiness timeout')

        room_a = room('server-a', port)
        clients = []
        for index in range(2):
            if host_memory()['availableMiB'] < 1536: raise RuntimeError('RAM reserve reached before client launch')
            clients.append(launch(client, 'client-'+str(index+1), ['-online-join', 'kcp://127.0.0.1:'+str(port),
                '-load-test-rejoin', 'kcp://127.0.0.1:'+str(port+1), '-load-test-client', '-load-test-coop-world',
                '-load-test-duration', str(min(1800, 2*seconds + 120)), '-online-motion-logs'] +
                (['-load-test-leave-after', str(leave_after)] if index == 1 else []) +
                (['-load-test-mount-captures', str(output/('client-'+str(index+1)+'-frames')), '-screen-width', '1024', '-screen-height', '896'] if rendered else []) +
                list(extra_args) + ['-local-diagnostic-name', 'SB6Reconnect'+str(index+1)]))
            time.sleep(1)
        # Room A closes for everyone once client 2 leaves (dedicated room contract).
        try: room_a.wait(timeout=leave_after + 150)
        except subprocess.TimeoutExpired: raise RuntimeError('room A did not close after the leave')
        if room_a.returncode != 0: raise RuntimeError('room A failed')
        if any(process.poll() is not None for process in clients): raise RuntimeError('a client quit instead of returning to the browser')
        if host_memory()['availableMiB'] < 1536: raise RuntimeError('RAM reserve reached before room B')
        room_b = room('server-b', port+1)
        try: room_b.wait(timeout=seconds + 90)
        except subprocess.TimeoutExpired: raise RuntimeError('room B did not finish within the bound')
        if room_b.returncode != 0: raise RuntimeError('room B failed')
        for process in clients:
            try: process.wait(timeout=20)
            except subprocess.TimeoutExpired: raise RuntimeError('client did not close after room B')
            if process.returncode != 0: raise RuntimeError('client failed during shutdown')
        summary = check_reconnect(output, scene)
        captures = {}
        for name in ('server-a', 'server-b', 'client-1', 'client-2'):
            lines = read(output/(name+'.log')).splitlines()
            capture = [line.split('[GameplayLatency] Capture: ', 1)[1].strip() for line in lines if '[GameplayLatency] Capture: ' in line]
            if not capture: raise RuntimeError('missing diagnostic capture: '+name)
            source = Path(capture[-1])
            if not source.name.startswith('motion-') or source.suffix != '.jsonl': raise RuntimeError('invalid diagnostic capture name')
            shutil.copy2(source, output/(name+'-motion.jsonl'))
            captures[name] = name+'-motion.jsonl'
        if rendered:
            for index in (1, 2):
                if len({hashlib.sha256(frame.read_bytes()).digest() for frame in (output/('client-'+str(index)+'-frames')).glob('*.png')}) < 3:
                    raise RuntimeError('missing distinct rendered captures for client '+str(index))
        result = dict(actualMirrorClients=2, scene=scene, reconnect=summary, captures=captures, rendered=rendered,
                      assemblySha256=hashes, seconds=time.monotonic()-started)
        (output/'result.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
        if summary['errors']: raise RuntimeError('replication errors; inspect result.json')
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
    parser.add_argument('--port', type=int, default=18787)
    parser.add_argument('--scene', default='Stage_3-4')
    parser.add_argument('--seconds', type=int, default=150)
    parser.add_argument('--leave-after', type=int, default=20, help='seconds of live co-op stage on client 2 before it leaves room A')
    parser.add_argument('--rendered', action='store_true')
    parser.add_argument('--extra-args', default='')
    args = parser.parse_args()
    if os.name != 'nt': parser.error('Windows binaries required')
    if not 1024 <= args.port <= 64999: parser.error('invalid port')
    if not 120 <= args.seconds <= 300: parser.error('duration must be 120-300 seconds')
    if not 5 <= args.leave_after <= 60: parser.error('leave-after must be 5-60 seconds')
    if not args.scene.startswith('Stage_') or any(character in args.scene for character in '/\\'): parser.error('scene must be a Stage_* name')
    run(args.server.resolve(), args.client.resolve(), args.output, args.port, args.scene, args.seconds, args.leave_after, args.rendered,
        __import__('shlex').split(args.extra_args))
