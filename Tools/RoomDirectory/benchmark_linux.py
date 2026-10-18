"""Linux /proc adapter for the same bounded real KCP load-test runner."""
import os
from pathlib import Path
import sys
import benchmark_local as bench


def memory():
    values = {line.split(':')[0]: int(line.split()[1]) for line in
              Path('/proc/meminfo').read_text(encoding='utf-8').splitlines()}
    ticks = list(map(int, Path('/proc/stat').read_text(encoding='utf-8').splitlines()[0].split()[1:]))
    network = {}
    for line in Path('/proc/net/dev').read_text(encoding='utf-8').splitlines()[2:]:
        name, counters = line.split(':', 1)
        counters = list(map(int, counters.split()))
        network[name.strip()] = dict(rxBytes=counters[0], txBytes=counters[8],
                                     rxDrops=counters[3], txDrops=counters[11])
    return dict(totalMiB=values['MemTotal']/1024, availableMiB=values['MemAvailable']/1024,
                availableCommitMiB=(values['MemAvailable']+values['SwapFree'])/1024,
                cpuStealTicks=ticks[7], cpuTotalTicks=sum(ticks[:8]),
                network=network,
                cpuPressure=Path('/proc/pressure/cpu').read_text(encoding='utf-8').strip(),
                memoryPressure=Path('/proc/pressure/memory').read_text(encoding='utf-8').strip())


def process(pid):
    try:
        stat = Path(f'/proc/{pid}/stat').read_text(encoding='utf-8').rsplit(')', 1)[1].split()
        status = Path(f'/proc/{pid}/status').read_text(encoding='utf-8').splitlines()
        fields = {line.split(':')[0]: line.split(':')[1].strip() for line in status}
        # VmData is private virtual data, not Windows PrivateUsage/commit. Keep its
        # meaning explicit in Linux reports; RSS is independently available below.
        return dict(pid=pid, cpuSeconds=(int(stat[11])+int(stat[12]))/os.sysconf('SC_CLK_TCK'),
                    rssMiB=int(fields['VmRSS'].split()[0])/1024,
                    privateMiB=int(fields.get('VmData', '0 kB').split()[0])/1024)
    except (OSError, KeyError, ValueError):
        return None


def cpu():
    values = list(map(int, Path('/proc/stat').read_text(encoding='utf-8').splitlines()[0].split()[1:]))
    # guest counters are already included in user/nice. Steal remains in total.
    return values[3]+values[4], sum(values[:8])


if __name__ == '__main__':
    if not sys.platform.startswith('linux'): raise SystemExit('Linux only')
    bench.host_memory = memory
    bench.process_sample = process
    bench.host_cpu = cpu
    # Measured headless Linux boot-to-stage growth is lower than the Windows
    # graphical player's 96 MiB/room; keep 300 MiB for the shared load client.
    bench.ENTRY_GROWTH_MIB = 32
    bench.subprocess.CREATE_NO_WINDOW = 0
    bench.main(require_windows=False)
