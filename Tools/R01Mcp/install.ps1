# Installs the restricted R01 MCP server for Claude Desktop. DRY RUN unless -Apply is given.
#   powershell -ExecutionPolicy Bypass -File Tools\R01Mcp\install.ps1            (shows what it would do)
#   powershell -ExecutionPolicy Bypass -File Tools\R01Mcp\install.ps1 -Apply     (copies server + edits config, with backup)
# It never touches the repository, Packages, Unity or the runner. After -Apply: fully QUIT and restart Claude Desktop.
param([switch]$Apply)
$ErrorActionPreference = 'Stop'
$src = Join-Path $PSScriptRoot 'sb6_r01_mcp.py'
$dstDir = Join-Path $env:USERPROFILE '.sb6-r01-mcp'
$dst = Join-Path $dstDir 'sb6_r01_mcp.py'
$py = (Get-Command python -ErrorAction SilentlyContinue | Where-Object { $_.Source -notlike '*\WindowsApps\*' } | Select-Object -First 1).Source
if (-not $py) { throw 'No real python.exe found on PATH (the Microsoft Store stub does not count). Install Python or edit this script.' }
Write-Host "python : $py"
Write-Host "server : $src"
Write-Host ("sha256 : " + (Get-FileHash -Algorithm SHA256 -LiteralPath $src).Hash)
Write-Host "install: $dst"

# config location: classic install or Store/MSIX install
$candidates = @((Join-Path $env:APPDATA 'Claude\claude_desktop_config.json'))
$candidates += Get-ChildItem (Join-Path $env:LOCALAPPDATA 'Packages') -Directory -Filter 'Claude*' -ErrorAction SilentlyContinue |
  ForEach-Object { Join-Path $_.FullName 'LocalCache\Roaming\Claude\claude_desktop_config.json' }
$cfg = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $cfg) { $cfg = $candidates[0]; Write-Host "config : $cfg (does not exist yet; would be created)" } else { Write-Host "config : $cfg" }

# protocol self-test (read-only: lists tools, writes only a line to Logs\r01-mcp\server.log)
$probe = '{"jsonrpc":"2.0","id":1,"method":"tools/list"}'
$reply = $probe | & $py $src
$n = ([regex]::Matches($reply, '"name"')).Count
Write-Host "selftest: server answered tools/list with $n tool entries"
if ($n -lt 6) { throw 'self-test failed; not installing' }

$merge = Join-Path $PSScriptRoot 'merge_config.py'
if (-not $Apply) {
  Write-Host "`nDRY RUN (nothing written). Config merge preview:"
  & $py $merge $cfg $py $dst
  Write-Host "`nRe-run with -Apply to install."
  return
}
New-Item -ItemType Directory -Force -Path $dstDir | Out-Null
Copy-Item -LiteralPath $src -Destination $dst -Force
if (Test-Path -LiteralPath $cfg) {
  $bak = "$cfg.bak-" + (Get-Date -Format 'yyyyMMdd-HHmmss')
  Copy-Item -LiteralPath $cfg -Destination $bak
  Write-Host "backup : $bak"
}
& $py $merge $cfg $py $dst --apply
if ($LASTEXITCODE -ne 0) { throw 'config merge aborted; config left unchanged' }
Write-Host "Installed. Now fully quit Claude Desktop (tray icon -> Quit) and start it again, then open a NEW session."
Write-Host "To remove: delete the sb6-r01 entry from the config (or restore the .bak) and delete $dstDir"
