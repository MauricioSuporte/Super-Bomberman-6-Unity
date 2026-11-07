# sb6-r01 MCP (restricted, stdio)

Fixed operations for the R01 assisted validation. No generic shell, no network listener, no publishing,
no Packages changes, no `--accept-packages-diff`. Tools: r01_status, r01_start, r01_job, r01_cancel, r01_results, r01_read.

Install (Windows PowerShell, repo root):
1. `powershell -ExecutionPolicy Bypass -File Tools\R01Mcp\install.ps1`          (dry run: Python, hash, config merge preview)
2. `powershell -ExecutionPolicy Bypass -File Tools\R01Mcp\install.ps1 -Apply`   (copies to %USERPROFILE%\.sb6-r01-mcp, backs up the config, adds ONLY mcpServers.sb6-r01)
3. Quit Claude Desktop from the tray, start it again, open a NEW session.

The hash printed identifies the version; it is not a security proof - read sb6_r01_mcp.py (about 350 lines) before -Apply.
Remove: delete the `sb6-r01` entry (or restore the .bak) and the %USERPROFILE%\.sb6-r01-mcp folder.
Cancel ends only the job's own process tree (Windows Job Object; POSIX process group) and reports, never kills, other Unity/SB6 processes.
Not verified on Windows: install.ps1, the Job Object path, the app actually exposing the server to cloud sessions.
