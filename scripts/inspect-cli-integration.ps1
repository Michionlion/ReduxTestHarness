[CmdletBinding()]
param(
    [string] $GameRoot = $env:KSP2_ROOT,
    [string] $UnityRoot = $env:UNITY_6000_4_1F1_ROOT,
    [switch] $AsJson
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

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
$managed = Join-Path $GameRoot 'KSP2_x64_Data\Managed'
$assembly = Join-Path $managed 'Assembly-CSharp.dll'
$spaceWarpUi = Join-Path $managed 'SpaceWarp2.UI.dll'
if (-not $UnityRoot) {
    $UnityRoot = 'C:\Program Files\Unity\Hub\Editor\6000.4.1f1'
}
$UnityRoot = [IO.Path]::GetFullPath($UnityRoot)
$unityMono = Join-Path $UnityRoot 'Editor\Data\MonoBleedingEdge\bin\mono.exe'
$ikdasm = Join-Path $UnityRoot 'Editor\Data\MonoBleedingEdge\lib\mono\4.5\ikdasm.exe'

foreach ($required in @($assembly, $spaceWarpUi, $unityMono, $ikdasm)) {
    if (-not (Test-Path -LiteralPath $required)) {
        throw "Required file was not found: $required"
    }
}

$gameEvidence = (& $unityMono $ikdasm $assembly 2>$null |
    Select-String -Pattern 'Redux\.CliIntegration|VERSION_TEXT|DEBUG_INFO') -join "`n"
$uiEvidence = (& $unityMono $ikdasm $spaceWarpUi 2>$null |
    Select-String -Pattern 'SpaceWarpConsoleCliIntegrationBridge') -join "`n"

$result = [ordered]@{
    assembly = $assembly
    assemblySha256 = (Get-FileHash -LiteralPath $assembly -Algorithm SHA256).Hash
    reduxVersion = if ($gameEvidence -match 'VERSION_TEXT = &quot;([^&]+)&quot;') {
        $Matches[1]
    } elseif ($gameEvidence -match 'VERSION_TEXT = "([^"]+)"') {
        $Matches[1]
    } else { $null }
    reduxCommit = if ($gameEvidence -match 'DEBUG_INFO = &quot;([0-9a-f]+)&quot;') {
        $Matches[1]
    } elseif ($gameEvidence -match 'DEBUG_INFO = "([0-9a-f]+)"') {
        $Matches[1]
    } else { $null }
    cliIntegrationServer = $gameEvidence.Contains('Redux.CliIntegration.CliIntegrationServer')
    cliIntegrationRepl = $gameEvidence.Contains('Redux.CliIntegration.CliIntegrationCSharpRepl')
    cliIntegrationReport = $gameEvidence.Contains('Redux.CliIntegration.CliIntegrationRunReport')
    spaceWarpReflectionBridge = $uiEvidence.Contains('SpaceWarpConsoleCliIntegrationBridge')
    startupSaveKeyResidue = $gameEvidence.Contains('KSP2Redux.CliIntegration.StartupSavePath')
}

if ($AsJson) {
    $result | ConvertTo-Json
}
else {
    $result.GetEnumerator() | Format-Table -AutoSize
}
