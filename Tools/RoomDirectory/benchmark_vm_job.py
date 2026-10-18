"""Start one bounded benchmark as a user systemd service on the Linux VM."""
import argparse
import os
from pathlib import Path
import re
import subprocess


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('name')
    parser.add_argument('arguments', nargs=argparse.REMAINDER)
    args = parser.parse_args()
    if not re.fullmatch(r'[a-z0-9][a-z0-9-]{0,48}', args.name): parser.error('invalid case name')
    base = Path.home() / 'sb6'
    output = base / 'benchmarks' / args.name
    unit = Path.home() / '.config/systemd/user' / f'sb6-bench-{args.name}.service'
    if unit.exists() or output.exists(): parser.error('use a new case name')
    options = args.arguments
    if options and options[0] == '--': options = options[1:]
    # systemd ExecStart parsing is not a shell. Reject control characters and
    # escape quotes/backslashes and specifiers in each individual argument.
    command = ['/usr/bin/python3', str(base/'bench-tools/benchmark_linux.py'),
               '--binary', str(base/'benchmark-server-replication-v6-final/SB6.x86_64'),
               '--output', str(output), '--reserve-mib', '768'] + options
    def quote(value):
        if any(ord(char)<32 for char in value): raise ValueError('control character in argument')
        return '"'+value.replace('\\','\\\\').replace('"','\\"').replace('%','%%')+'"'
    unit.write_text('[Unit]\nDescription=SB6 bounded capacity test '+args.name+'\n'
                    '[Service]\nType=exec\nWorkingDirectory='+str(base)+'\n'
                    'ExecStart='+' '.join(map(quote,command))+'\n'
                    'MemoryMax=2500M\nTasksMax=512\nRuntimeMaxSec=1200\n'
                    'KillMode=control-group\nTimeoutStopSec=15\nUMask=0077\n'
                    'StandardOutput=append:'+str(base/'benchmarks'/f'{args.name}-launcher.log')+'\n'
                    'StandardError=inherit\n', encoding='utf-8')
    env = dict(os.environ, XDG_RUNTIME_DIR=f'/run/user/{os.getuid()}')
    subprocess.run(['systemctl','--user','daemon-reload'],env=env,check=True)
    subprocess.run(['systemctl','--user','start',unit.name],env=env,check=True)
    print(unit.name, flush=True)
