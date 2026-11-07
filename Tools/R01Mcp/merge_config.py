"""Adds/updates ONLY mcpServers['sb6-r01'] in a Claude Desktop config; every other key and server is preserved.
usage: merge_config.py CONFIG_PATH PYTHON_EXE SERVER_PATH [--apply]   (without --apply: prints the result, writes nothing)
Aborts without writing if the existing file is not valid JSON."""
import json, sys
from pathlib import Path

def main():
    cfg, py, server = Path(sys.argv[1]), sys.argv[2], sys.argv[3]
    apply = "--apply" in sys.argv
    data = {}
    if cfg.is_file() and cfg.read_text(encoding="utf-8-sig").strip():
        try:
            data = json.loads(cfg.read_text(encoding="utf-8-sig"))
        except json.JSONDecodeError as e:
            sys.exit(f"ABORT: {cfg} is not valid JSON ({e}); nothing written")
    if not isinstance(data, dict) or not isinstance(data.get("mcpServers", {}), dict):
        sys.exit("ABORT: unexpected config structure; nothing written")
    before = {k: v for k, v in data.get("mcpServers", {}).items()}
    data.setdefault("mcpServers", {})["sb6-r01"] = {"command": py, "args": [server]}
    others = [k for k in before if k != "sb6-r01"]
    print(f"preserved servers: {others or 'none'}; other top-level keys: {[k for k in data if k != 'mcpServers'] or 'none'}")
    print("sb6-r01 was " + ("already present (will be replaced)" if "sb6-r01" in before else "absent (will be added)"))
    if apply:
        cfg.parent.mkdir(parents=True, exist_ok=True)
        cfg.write_text(json.dumps(data, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
        print("written:", cfg)

main()
