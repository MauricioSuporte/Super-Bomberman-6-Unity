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
    if rooms(): raise SystemExit('Active rooms; deployment left unchanged')
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
    if rooms(): raise SystemExit('A room opened during preflight; deployment left unchanged')
    service_env = dict(os.environ, XDG_RUNTIME_DIR=f'/run/user/{os.getuid()}')
    def service(action, check=True):
        return subprocess.run(['systemctl','--user',action,'sb6-rooms.service'],env=service_env,check=check)
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
        service('start')
        for _ in range(20):
            try:
                rooms()
                with urlopen('http://127.0.0.1:8080/health', timeout=3) as response: health = json.load(response)
                if health.get('protocol') != manifest['protocol']: raise RuntimeError('API protocol differs from release')
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
        service('start', check=False)
        raise
