"""Connect one actual Windows client to an allocated public room, then leave."""
import argparse
import json
from pathlib import Path
import subprocess
import time
from urllib.request import urlopen


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--client', type=Path, required=True)
    parser.add_argument('--room', required=True)
    parser.add_argument('--port', type=int, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    if args.port not in (7777, 7778): parser.error('only the allocated public gameplay ports are supported')
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=False)
    binary = args.client.resolve()
    process = None
    try:
        with (output/'launcher.log').open('wb') as launch:
            process = subprocess.Popen([str(binary), '-batchmode', '-nographics', '-online-join',
                        'kcp://192.48.205.46:'+str(args.port), '-logFile', str(output/'client.log')],
                        cwd=binary.parent, stdout=launch, stderr=subprocess.STDOUT,
                        creationflags=subprocess.CREATE_NO_WINDOW)
            observed = None
            for _ in range(60):
                if process.poll() is not None: raise RuntimeError('client exited before connection verification')
                with urlopen('http://192.48.205.46:8080/rooms', timeout=4) as response:
                    rooms = json.load(response)['rooms']
                observed = next((room for room in rooms if room['id'] == args.room and room['players'] == 1), None)
                if observed: break
                time.sleep(1)
            if observed is None: raise RuntimeError('public directory never reported the client connected')
            time.sleep(10)
            text = (output/'client.log').read_text(encoding='utf-8-sig', errors='replace')
            errors = [line for line in text.splitlines() if any(marker in line for marker in
                ('Exception', 'Could not spawn', 'Failed to spawn', 'incompatible connection'))]
            result = dict(publicRoom=observed, actualMirrorClient=True, rendered=False,
                          scope='Public lobby/authentication; no match started', errors=errors)
            (output/'result.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
            if errors: raise RuntimeError('client errors; inspect result.json')
            print(json.dumps(result), flush=True)
    except BaseException as error:
        (output/'failure.json').write_text(json.dumps(dict(error=str(error))), encoding='utf-8')
        raise
    finally:
        if process is not None and process.poll() is None:
            process.terminate()
            try: process.wait(timeout=10)
            except subprocess.TimeoutExpired: process.kill(); process.wait()
