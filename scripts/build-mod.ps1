[CmdletBinding()]
param(
    [string] $GameRoot = $env:KSP2_ROOT,
    [string] $Configuration = 'Debug'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $GameRoot) {
    $GameRoot = 'G:\SteamLibrary\steamapps\common\Kerbal Space Program 2'
}
$GameRoot = [IO.Path]::GetFullPath($GameRoot)
$managed = Join-Path $GameRoot 'KSP2_x64_Data\Managed'
if (-not (Test-Path -LiteralPath (Join-Path $managed 'Assembly-CSharp.dll'))) {
    throw "KSP2 managed assemblies were not found under $managed"
}

$unityRoot = 'C:\Program Files\Unity\Hub\Editor\6000.4.1f1'
$compiler = Join-Path $unityRoot 'Editor\Data\MonoBleedingEdge\lib\mono\4.5\csc.exe'
$mono = Join-Path $unityRoot 'Editor\Data\MonoBleedingEdge\bin\mono.exe'
if (-not (Test-Path -LiteralPath $compiler)) {
    throw "Unity C# compiler was not found: $compiler"
}
if (-not (Test-Path -LiteralPath $mono)) {
    throw "Unity Mono runtime was not found: $mono"
}

$sourceRoot = Join-Path $repoRoot 'src\ReduxTestHarness'
$outputRoot = Join-Path $repoRoot 'build\ReduxTestHarness'
New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null

$references = @(
    'Assembly-CSharp.dll',
    'MoonSharp.Interpreter.dll',
    'netstandard.dll',
    'Newtonsoft.Json.dll',
    'ReduxLib.dll',
    'SpaceWarp2.dll',
    'UnityEngine.dll',
    'UnityEngine.CoreModule.dll',
    'UnityEngine.IMGUIModule.dll',
    'UnityEngine.ScreenCaptureModule.dll',
    'UnityEngine.UIModule.dll'
) | ForEach-Object { Join-Path $managed $_ }

foreach ($reference in $references) {
    if (-not (Test-Path -LiteralPath $reference)) {
        throw "Required reference is missing: $reference"
    }
}

$sources = @(Get-ChildItem -LiteralPath $sourceRoot -Filter '*.cs' -File |
    Sort-Object Name | ForEach-Object FullName)
if ($sources.Count -eq 0) {
    throw "No C# sources found under $sourceRoot"
}

$arguments = [Collections.Generic.List[string]]::new()
$arguments.Add('/nologo')
$arguments.Add('/target:library')
$arguments.Add('/langversion:7.3')
$arguments.Add('/deterministic+')
$arguments.Add('/optimize' + $(if ($Configuration -eq 'Release') { '+' } else { '-' }))
$arguments.Add('/debug:portable')
$arguments.Add('/define:KSP2_X64')
$arguments.Add('/out:' + (Join-Path $outputRoot 'ReduxTestHarness.dll'))
foreach ($reference in $references) { $arguments.Add('/reference:' + $reference) }
foreach ($source in $sources) { $arguments.Add($source) }

& $mono $compiler @arguments
if ($LASTEXITCODE -ne 0) {
    throw "C# compilation failed with exit code $LASTEXITCODE"
}

Copy-Item -LiteralPath (Join-Path $sourceRoot 'swinfo.json') `
    -Destination (Join-Path $outputRoot 'swinfo.json') -Force
Write-Host "Built: $outputRoot"
