"""Run the installed ARM64 PortMaster player on a PC with Docker Linux containers."""
import argparse
from pathlib import Path
import subprocess
import tempfile


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--game", type=Path, required=True, help="prepared game directory")
    parser.add_argument("--host", type=Path, required=True, help="handheld ua_host64 binary")
    parser.add_argument("--output", type=Path, required=True, help="directory for independent runs")
    parser.add_argument("--seconds", type=int, default=90)
    parser.add_argument("--input-script", help="runtime UA_INPUT_SCRIPT events")
    args = parser.parse_args()
    game = args.game.resolve()
    host = args.host.resolve()
    if not host.is_file() or not all((game / f).exists() for f in ("base.apk", "android_package.json", "lib/libunity.so")):
        parser.error("missing player files")
    if args.seconds < 1:
        parser.error("--seconds must be positive")
    args.output.mkdir(parents=True, exist_ok=True)
    run = Path(tempfile.mkdtemp(prefix="run-", dir=args.output)).resolve()
    image = "sb6-portmaster-arm64"
    subprocess.run(["docker", "build", "--platform", "linux/arm64", "-t", image,
                    str(Path(__file__).resolve().parent)], check=True)
    command = ["docker", "run", "--rm", "--platform", "linux/arm64", "--network", "none",
               "--cpus", "4", "--memory", "1g", "--memory-swap", "1g",
               "-e", "QEMU_CPU=cortex-a35", "-e", "UA_EGL_PBUFFER=1",
               "-e", "ANDROID_WINDOW_SIZE=1024x768", "-e", "EGL_PLATFORM=surfaceless",
               "-e", "LIBGL_ALWAYS_SOFTWARE=1", "-e", "SDL_VIDEODRIVER=dummy",
               "-e", "SDL_AUDIODRIVER=dummy", "-e", "UA_FRAME_RATE=60",
               "-e", "UA_FPS=2", "-e", "UA_SDK_INT=26", "-e", "UA_VIRTUAL_PAD=1",
               "-e", "UA_PAD_LAYOUT=nintendo"]
    if args.input_script:
        command += ["-e", "UA_INPUT_SCRIPT=" + args.input_script]
    for src, dest, readonly in [(game, "/package", True), (host, "/host", True), (run, "/run-sb6", False)]:
        command += ["--mount", f"type=bind,source={src},target={dest}" + (",readonly" if readonly else "")]
    command += [image, "sh", "-ec",
                'cd /run-sb6; ln -s /package/base.apk /package/android_package.json /package/lib .; '
                'cp /host ./ua_host; chmod +x ./ua_host; exec timeout -k 5 "$1" ./ua_host /run-sb6',
                "sb6", str(args.seconds)]
    log = run / "sandbox.log"
    print(log, flush=True)
    with log.open("wb") as stream:
        result = subprocess.run(command, stdout=stream, stderr=subprocess.STDOUT)
    output = log.read_bytes()
    if result.returncode not in (0, 124, 137) or b"[fps]" not in output:
        raise SystemExit(f"ARM64 frame delivery unconfirmed (exit {result.returncode}). See {log}")


if __name__ == "__main__":
    main()
