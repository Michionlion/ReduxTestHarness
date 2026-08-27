[CmdletBinding()]
param(
    [string] $GameRoot = $env:KSP2_ROOT,
    [switch] $SkipBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $GameRoot) {
    $GameRoot = 'G:\SteamLibrary\steamapps\common\Kerbal Space Program 2'
}
$GameRoot = [IO.Path]::GetFullPath($GameRoot)
if (-not (Test-Path -LiteralPath (Join-Path $GameRoot 'KSP2_x64.exe'))) {
    throw "KSP2 was not found under $GameRoot"
}

if (-not $SkipBuild) {
    & (Join-Path $PSScriptRoot 'build-mod.ps1') -GameRoot $GameRoot
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

$source = Join-Path $repoRoot 'build\ReduxTestHarness'
$destination = Join-Path $GameRoot 'mods\ReduxTestHarness'
New-Item -ItemType Directory -Force -Path $destination | Out-Null
Copy-Item -LiteralPath (Join-Path $source 'ReduxTestHarness.dll') -Destination $destination -Force
Copy-Item -LiteralPath (Join-Path $source 'ReduxTestHarness.pdb') -Destination $destination -Force -ErrorAction SilentlyContinue
Copy-Item -LiteralPath (Join-Path $source 'swinfo.json') -Destination $destination -Force
Set-Content -LiteralPath (Join-Path $destination 'test-mode.enabled') `
    -Value 'Developer test endpoint explicitly enabled.' -Encoding utf8NoBOM
Write-Host "Installed and enabled: $destination"

