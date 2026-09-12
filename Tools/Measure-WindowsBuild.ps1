[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$BuildPath,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string]$BaselineJson,
    [switch]$CreateZip
)

$ErrorActionPreference = 'Stop'
$sourcePath = (Resolve-Path -LiteralPath $BuildPath).Path.TrimEnd('\', '/')
if (!(Test-Path -LiteralPath $sourcePath -PathType Container) -or
    !(Get-ChildItem -LiteralPath $sourcePath -Filter 'UnityPlayer.dll' -File)) {
    throw 'BuildPath must be a Windows Unity player folder containing UnityPlayer.dll.'
}
$destinationPath = [IO.Path]::GetFullPath($OutputDirectory)
if ($destinationPath.Equals($sourcePath, [StringComparison]::OrdinalIgnoreCase) -or
    $destinationPath.StartsWith($sourcePath + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'OutputDirectory must be outside the build folder.'
}
$baseline = if ($BaselineJson) { Get-Content -LiteralPath $BaselineJson -Raw | ConvertFrom-Json } else { $null }
$files = @(Get-ChildItem -LiteralPath $sourcePath -Recurse -File)
if ($files | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }) {
    throw 'Resolve linked or cloud-only files before measuring and packaging this build.'
}
$runPath = Join-Path $destinationPath ([DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff') + '-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Path $runPath -ErrorAction Stop | Out-Null
$totalBytes = [long](($files | Measure-Object Length -Sum).Sum)
$files | Sort-Object Length -Descending | Select-Object @{N='Path';E={$_.FullName.Substring($sourcePath.Length + 1)}}, Length |
    Export-Csv -LiteralPath (Join-Path $runPath 'files.csv') -NoTypeInformation -Encoding UTF8
$zipBytes = $null
if ($CreateZip) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zipPath = Join-Path $runPath 'Windows.zip'
    [IO.Compression.ZipFile]::CreateFromDirectory($sourcePath, $zipPath, [IO.Compression.CompressionLevel]::Optimal, $false)
    $zipBytes = (Get-Item -LiteralPath $zipPath).Length
}
$result = [ordered]@{
    Source = $sourcePath
    MeasuredUtc = [DateTime]::UtcNow.ToString('o')
    FileCount = $files.Count
    InstalledBytes = $totalBytes
    InstalledMiB = [math]::Round($totalBytes / 1MB, 2)
    ZipBytes = $zipBytes
    ZipMiB = if ($null -ne $zipBytes) { [math]::Round($zipBytes / 1MB, 2) } else { $null }
    InstalledSavingsBytes = if ($null -ne $baseline) { $baseline.InstalledBytes - $totalBytes } else { $null }
    InstalledSavingsPercent = if ($null -ne $baseline -and $baseline.InstalledBytes -gt 0) { [math]::Round(100 * (1 - $totalBytes / $baseline.InstalledBytes), 2) } else { $null }
    ZipSavingsBytes = if ($null -ne $baseline -and $null -ne $baseline.ZipBytes -and $null -ne $zipBytes) { $baseline.ZipBytes - $zipBytes } else { $null }
    ZipSavingsPercent = if ($null -ne $baseline -and $baseline.ZipBytes -gt 0 -and $null -ne $zipBytes) { [math]::Round(100 * (1 - $zipBytes / $baseline.ZipBytes), 2) } else { $null }
    ByExtension = @($files | Group-Object Extension | ForEach-Object {
        [ordered]@{ Extension = $_.Name; Bytes = [long](($_.Group | Measure-Object Length -Sum).Sum) }
    })
}
$result | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $runPath 'measurement.json') -Encoding UTF8
[pscustomobject]$result | Format-List Source, FileCount, InstalledMiB, ZipMiB, InstalledSavingsBytes, InstalledSavingsPercent, ZipSavingsBytes, ZipSavingsPercent
Write-Output "Reports: $runPath"
