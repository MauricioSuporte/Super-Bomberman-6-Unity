"""Validate and atomically activate a staged SB6 release, retaining a rollback."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import secrets
import shutil
import socket
import struct
import subprocess
import time
from urllib.request import urlopen


def rooms():
    with urlopen('http://127.0.0.1:8080/rooms', timeout=3) as response:
        return json.load(response)['rooms']


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('stage', help='Staging directory name inside ~/sb6')
    parser.add_argument('backup', help='New rollback directory name inside ~/sb6')
    args = parser.parse_args()
    base = (Path.home()/'sb6').resolve()
    for value in (args.stage, args.backup):
        if not re.fullmatch(r'[a-z0-9][a-z0-9-]{3,70}', value) or value == 'server': parser.error('invalid directory name')
    stage, backup, live = base/args.stage, base/args.backup, base/'server'
    if stage.resolve().parent != base or live.resolve().parent != base: parser.error('unexpected symlink target')
    if backup.exists() or args.stage == args.backup: parser.error('backup must be a new directory')
    binary = stage/'SB6.x86_64'
    if not binary.is_file(): parser.error('staged executable missing')
    assembly = (stage/'SB6_Data/Managed/Assembly-CSharp.dll').read_bytes()
    manifest = json.loads((stage/'deployment.json').read_text(encoding='utf-8-sig'))
    if hashlib.sha256(assembly).hexdigest() != manifest['assemblySha256']: parser.error('assembly hash mismatch')
    staged_supervisor = stage/'supervisor.py'
    supervisor = base/'supervisor.py'
    supervisor_backup = base/(args.backup+'-supervisor.py')
    if supervisor_backup.exists(): parser.error('supervisor backup must be new')
    if not staged_supervisor.is_file(): parser.error('matching directory supervisor missing from stage')
    supervisor_bytes = staged_supervisor.read_bytes()
    if hashlib.sha256(supervisor_bytes).hexdigest() != manifest['supervisorSha256']: parser.error('supervisor hash mismatch')
    match = re.search(rb'^PROTOCOL = (\d+)\r?$', supervisor_bytes, re.M)
    if not match or int(match[1]) != manifest['protocol']: parser.error('directory/server protocol mismatch')
    if any(name in assembly for name in (b'LocalServerLoadTest', b'TickLocalLoadTest', b'LocalNetworkInputSmoke', b'LocalOnlineFlowSmoke')):
        parser.error('development harness in release')
    print('Starting staged server boot and KCP check', flush=True)
    smoke = base/(args.stage+'-smoke')
    smoke.mkdir(mode=0o700)
    env = dict(os.environ, SB6_ROOM_ID='deployment-smoke', SB6_ROOM_TOKEN=secrets.token_hex(32),
               SB6_ROOM_STATUS=str(smoke/'status.json'))
    with (smoke/'launcher.log').open('wb') as launch:
        process = subprocess.Popen([str(binary), '-batchmode', '-nographics', '-server',
                    '-room-mode', 'BossRush', '-port', '18887', '-server-idle-seconds', '60',
                    '-logFile', str(smoke/'unity.log')], cwd=stage, env=env, stdout=launch, stderr=subprocess.STDOUT)
        try:
            for _ in range(60):
                if process.poll() is not None: raise RuntimeError('staged server exited during boot')
                try:
                    if json.loads((smoke/'status.json').read_text(encoding='utf-8-sig'))['available']: break
                except (OSError, ValueError, KeyError): pass
                time.sleep(1)
            else: raise RuntimeError('staged server boot timed out')
            packet = b'\x01'+struct.pack('<I',0)+struct.pack('<IBBHIIII',0,81,0,128,0,0,0,1)+b'\x01'
            with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as probe:
                probe.settimeout(3)
                probe.sendto(packet,('127.0.0.1',18887))
                response, _ = probe.recvfrom(4096)
                if len(response)<6 or response[0] not in (1,2): raise RuntimeError('invalid KCP reply')
                probe.sendto(b'\x02'+response[1:5]+b'\x05',('127.0.0.1',18887))
            print('Release boot and KCP preflight passed', flush=True)
        finally:
            if process.poll() is None: process.terminate()
            try: process.wait(timeout=15)
            except subprocess.TimeoutExpired: process.kill(); process.wait()
    service_env = dict(os.environ, XDG_RUNTIME_DIR=f'/run/user/{os.getuid()}')
    def service(action, check=True):
        print('Room service:', action, flush=True)
        return subprocess.run(['systemctl','--user',action] + ([] if action == 'daemon-reload' else ['sb6-rooms.service']), env=service_env, check=check, timeout=35, stdin=subprocess.DEVNULL)
    service_file = Path.home()/'.config/systemd/user/sb6-rooms.service'
    service_before = service_file.read_bytes()
    service_text = service_before.decode('utf-8-sig')
    service_after, replacements = re.subn(r'--capacity\s+\d+', '--capacity 3', service_text)
    if replacements != 1: raise RuntimeError('expected one room capacity in service')
    # KillMode=control-group stops the supervisor and every active room process.
    print('Activating release: disconnecting all current rooms', flush=True)
    service('stop')
    swapped = False
    supervisor_swapped = False
    try:
        shutil.copy2(supervisor, supervisor_backup)
        live.rename(backup)
        try: stage.rename(live)
        except BaseException:
            backup.rename(live)
            raise
        swapped = True
        shutil.copy2(live/'supervisor.py', base/'supervisor-next.py')
        (base/'supervisor-next.py').replace(supervisor)
        supervisor_swapped = True
        service_file.write_text(service_after, encoding='utf-8')
        service('daemon-reload')
        service('start')
        for _ in range(20):
            try:
                rooms()
                with urlopen('http://127.0.0.1:8080/health', timeout=3) as response: health = json.load(response)
                if health.get('protocol') != manifest['protocol'] or health.get('buildIdentity') != manifest['buildIdentity']:
                    raise RuntimeError('API protocol/build identity differs from release')
                break
            except OSError: time.sleep(1)
        else: raise RuntimeError('API did not restart')
        print(json.dumps(dict(published=str(live), rollback=str(backup))), flush=True)
    except BaseException:
        service('stop', check=False)
        if swapped:
            live.rename(stage)
            backup.rename(live)
        if supervisor_swapped: shutil.copy2(supervisor_backup, supervisor)
        service_file.write_bytes(service_before)
        service('daemon-reload', check=False)
        service('start', check=False)
        raise
