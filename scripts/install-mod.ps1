[CmdletBinding()]
param(
    [string] $GameRoot = $env:KSP2_ROOT,
    [string] $UnityRoot = $env:UNITY_6000_4_1F1_ROOT,
    [switch] $SkipBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $GameRoot) {
    $GameRoot = @(
        'G:\SteamLibrary\steamapps\common\Kerbal Space Program 2',
        'C:\Program Files (x86)\Steam\steamapps\common\Kerbal Space Program 2'
    ) | Where-Object { Test-Path -LiteralPath (Join-Path $_ 'KSP2_x64.exe') } |
        Select-Object -First 1
}
if (-not $GameRoot) {
    throw 'KSP2 was not found. Pass -GameRoot or set KSP2_ROOT.'
}
$GameRoot = [IO.Path]::GetFullPath($GameRoot)
if (-not (Test-Path -LiteralPath (Join-Path $GameRoot 'KSP2_x64.exe'))) {
    throw "KSP2 was not found under $GameRoot"
}

$runningKsp = @(Get-Process -Name 'KSP2_x64' -ErrorAction SilentlyContinue)
if ($runningKsp.Count -gt 0) {
    $ids = ($runningKsp | ForEach-Object Id) -join ', '
    throw "KSP2 is running (process $ids). Close it before installing ReduxTestHarness so Windows can replace the loaded mod DLL."
}

if (-not $SkipBuild) {
    & (Join-Path $PSScriptRoot 'build-mod.ps1') `
        -GameRoot $GameRoot `
        -UnityRoot $UnityRoot
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

$source = Join-Path $repoRoot 'build\ReduxTestHarness'
$builtDll = Join-Path $source 'ReduxTestHarness.dll'
if (-not (Test-Path -LiteralPath $builtDll)) {
    throw "The built mod was not found: $builtDll. Run build-mod.ps1 or omit -SkipBuild."
}
$destination = Join-Path $GameRoot 'mods\ReduxTestHarness'
New-Item -ItemType Directory -Force -Path $destination | Out-Null
Copy-Item -LiteralPath $builtDll -Destination $destination -Force
Copy-Item -LiteralPath (Join-Path $source 'ReduxTestHarness.pdb') -Destination $destination -Force -ErrorAction SilentlyContinue
Copy-Item -LiteralPath (Join-Path $source 'swinfo.json') -Destination $destination -Force
Set-Content -LiteralPath (Join-Path $destination 'test-mode.enabled') `
    -Value 'Developer test endpoint explicitly enabled.' -Encoding utf8NoBOM
Write-Host "Installed and enabled: $destination"
