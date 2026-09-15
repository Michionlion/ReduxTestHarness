#Requires -Version 7.4
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$GameRoot,
    [Parameter(Mandatory)][string]$BetterAaRoot,
    [Parameter(Mandatory)][string]$ReleaseZip,
    [Parameter(Mandatory)][string]$Fixture,
    [Parameter(Mandatory)][string]$Output,
    [Parameter(Mandatory)][string]$UnityRoot,
    [double]$StartYaw = 350,
    [ValidateRange(5, 10000)][double]$Distance = 80,
    [switch]$Preview,
    [string]$FFmpeg = 'ffmpeg',
    [string]$FFprobe = 'ffprobe'
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$harness = Split-Path $PSScriptRoot
$GameRoot = (Resolve-Path -LiteralPath $GameRoot).Path
$Output = [IO.Path]::GetFullPath($Output)
if (Test-Path -LiteralPath $Output) { throw 'Use a new output directory for each take.' }
if ($Output.StartsWith($GameRoot + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Output must be outside the game.' }
if (Get-Process KSP2_x64 -ErrorAction SilentlyContinue) { throw 'Close KSP2 before capturing.' }
if (Get-NetTCPConnection -LocalPort 28542 -State Listen -ErrorAction SilentlyContinue) { throw 'Harness port is in use.' }
[void](Get-Command $FFmpeg -ErrorAction Stop)
[void](Get-Command $FFprobe -ErrorAction Stop)
New-Item -ItemType Directory -Path $Output | Out-Null
$profile = Join-Path $env:USERPROFILE 'AppData\LocalLow\Intercept Games\Kerbal Space Program 2'
$backup = $profile + '.video-' + [Guid]::NewGuid().ToString('N')
$evidence = $backup + '-results'
$parent = [IO.Path]::GetFullPath((Split-Path $profile)) + '\'
foreach ($path in @($profile, $backup, $evidence)) {
    $absolute = [IO.Path]::GetFullPath($path)
    if (-not $absolute.StartsWith($parent, [StringComparison]::OrdinalIgnoreCase)) { throw 'Profile path escaped its parent.' }
    if ((Test-Path -LiteralPath $path) -and ((Get-Item -LiteralPath $path).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Linked profiles are not supported.' }
}
$settings = Get-Content -LiteralPath (Join-Path $profile 'Global\Settings.json') -Raw | ConvertFrom-Json -AsHashtable
$seed = @{}
foreach ($key in @('VersionString','LanguageKey','PlayerEULALegalAcceptanceUTCTime','PlayerEULALegalAcceptanceVersion','PlayerPPLegalAcceptanceUTCTime','PlayerPPLegalAcceptanceVersion','PlayerTOSLegalAcceptanceUTCTime','PlayerTOSLegalAcceptanceVersion')) {
    if ($settings.ContainsKey($key)) { $seed[$key] = $settings[$key] }
}
# Use the reviewed complete-package validator before extracting anything.
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $ReleaseZip).Path)
try {
    $reader = [IO.StreamReader]::new($archive.GetEntry('BetterAA-release-manifest.json').Open())
    try { $manifest = $reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
} finally { $archive.Dispose() }
& python -X utf8 (Join-Path $BetterAaRoot 'tools\package-complete.py') --verify $ReleaseZip --commit $manifest.sourceCommit --version $manifest.version
if ($LASTEXITCODE -ne 0) { throw 'Release ZIP failed validation.' }
$playerVersion = (Get-Item -LiteralPath (Join-Path $GameRoot 'UnityPlayer.dll')).VersionInfo.ProductVersion
if (-not ($playerVersion -eq $manifest.unityVersion -or $playerVersion.StartsWith($manifest.unityVersion + ' ', [StringComparison]::Ordinal))) {
    throw "Release targets Unity $($manifest.unityVersion), but the player is $playerVersion."
}
& (Join-Path $PSScriptRoot 'install-mod.ps1') -GameRoot $GameRoot -UnityRoot $UnityRoot
if ($LASTEXITCODE -ne 0) { throw 'Harness build/install failed.' }
$retiredMod = Join-Path $GameRoot 'mods\ReduxBetterAA'
$modBackup = Join-Path $Output 'previous-mod'
if (Test-Path -LiteralPath $retiredMod) {
    $resolved = (Resolve-Path -LiteralPath $retiredMod).Path
    if (-not $resolved.StartsWith($GameRoot + '\mods\', [StringComparison]::OrdinalIgnoreCase) -or
        ((Get-Item -LiteralPath $resolved).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Unsafe mod backup path.' }
    Move-Item -LiteralPath $resolved -Destination $modBackup
}
Expand-Archive -LiteralPath $ReleaseZip -DestinationPath $GameRoot -Force
foreach ($entry in $manifest.files) {
    if ((Get-FileHash -LiteralPath (Join-Path $GameRoot $entry.path)).Hash -ine $entry.sha256) { throw "Installed hash mismatch: $($entry.path)" }
}
$fixtureRoot = Join-Path $Output 'fixtures'
New-Item -ItemType Directory -Path $fixtureRoot | Out-Null
Copy-Item -LiteralPath $Fixture -Destination (Join-Path $fixtureRoot 'launchpad.json')
$scriptPath = Join-Path $Output 'capture.lua'
$invariant = [Globalization.CultureInfo]::InvariantCulture
$prefix = "capture_preview = $($Preview.IsPresent.ToString().ToLowerInvariant())`ncapture_start_yaw = $($StartYaw.ToString($invariant))`ncapture_distance = $($Distance.ToString($invariant))`n"
$prefix + (Get-Content -LiteralPath (Join-Path $harness 'tests\media\betteraa-launchpad-pan.lua') -Raw) |
    Set-Content -LiteralPath $scriptPath -Encoding utf8NoBOM
$reduxConfig = Join-Path $GameRoot 'Redux\config.json'
Copy-Item -LiteralPath $reduxConfig -Destination (Join-Path $Output 'redux-config-original.json')
$moved = $false
try {
    Copy-Item -LiteralPath (Join-Path $BetterAaRoot 'tests\Release\offline-redux-config.json') -Destination $reduxConfig -Force
    Move-Item -LiteralPath $profile -Destination $backup
    $moved = $true
    New-Item -ItemType Directory -Path (Join-Path $profile 'Global') | Out-Null
    $seed | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $profile 'Global\Settings.json') -Encoding utf8NoBOM
    & (Join-Path $harness 'redux-test.ps1') run $scriptPath -Launch -StartupSettleSeconds 20 -ResponseTimeoutSeconds 120 -Timeout 7200 -GameRoot $GameRoot -Fixtures $fixtureRoot -Results (Join-Path $Output 'results')
    if ($LASTEXITCODE -ne 0) { throw "Player capture failed ($LASTEXITCODE)." }
} finally {
    foreach ($process in @(Get-Process KSP2_x64 -ErrorAction SilentlyContinue | Where-Object Path -eq (Join-Path $GameRoot 'KSP2_x64.exe'))) {
        if (-not $process.WaitForExit(10000)) { $process.Kill(); [void]$process.WaitForExit(10000) }
    }
    if ($moved) {
        if (Get-Process KSP2_x64 -ErrorAction SilentlyContinue) { throw "Player still running. Original profile is at $backup" }
        if (Test-Path -LiteralPath $profile) { Move-Item -LiteralPath $profile -Destination $evidence }
        Move-Item -LiteralPath $backup -Destination $profile
        Write-Host 'Original player profile restored.'
    }
    Copy-Item -LiteralPath (Join-Path $Output 'redux-config-original.json') -Destination $reduxConfig -Force
}
Copy-Item -LiteralPath (Join-Path $GameRoot 'mods\ReduxBetterAA\diagnostics') -Destination (Join-Path $Output 'diagnostics') -Recurse
$sequences = @(Get-ChildItem -LiteralPath (Join-Path $Output 'results') -Filter capture.json -File -Recurse)
if ($sequences.Count -ne $(if ($Preview) { 1 } else { 4 })) { throw 'Incomplete capture set.' }
$validationArgs = @((Join-Path $PSScriptRoot 'validate-pan-captures.py'), (Join-Path $Output 'results'), '--output', (Join-Path $Output 'alignment.json'))
if ($Preview) { $validationArgs += '--preview' }
& python -X utf8 @validationArgs
if ($LASTEXITCODE -ne 0) { throw 'Camera capture alignment failed.' }
$videos = @()
foreach ($sequence in $sequences) {
    $capture = Get-Content -LiteralPath $sequence.FullName -Raw | ConvertFrom-Json
    $frames = @(Get-ChildItem -LiteralPath $sequence.DirectoryName -Filter '*.png' -File)
    if ($frames.Count -ne $capture.frames) { throw 'Missing captured frames.' }
    for ($i = 1; $i -lt $capture.poses.Count; ++$i) {
        if ($capture.poses[$i].unityFrame -ne ($capture.poses[$i-1].unityFrame + 1)) { throw 'Capture skipped a rendered frame.' }
    }
    $video = Join-Path $Output ($sequence.Directory.Name + '.mp4')
    & $FFmpeg -hide_banner -loglevel error -n -framerate $capture.fps -start_number 0 -i (Join-Path $sequence.DirectoryName '%06d.png') -frames:v $capture.frames -c:v libx264 -preset slow -crf 10 -pix_fmt yuv420p -movflags +faststart $video
    if ($LASTEXITCODE -ne 0) { throw 'Video encoding failed.' }
    $probe = (& $FFprobe -v error -count_frames -select_streams v:0 -show_entries stream=width,height,nb_read_frames,r_frame_rate,duration -of json $video) | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0 -or [int]$probe.streams[0].nb_read_frames -ne $capture.frames -or $probe.streams[0].r_frame_rate -ne '60/1' -or [int]$probe.streams[0].width -ne $capture.width -or [int]$probe.streams[0].height -ne $capture.height -or [Math]::Abs([double]::Parse($probe.streams[0].duration, $invariant) - $capture.frames / $capture.fps) -gt 0.001) { throw 'Encoded video failed verification.' }
    $videos += @{ name = [IO.Path]::GetFileName($video); sha256 = (Get-FileHash -LiteralPath $video).Hash.ToLowerInvariant(); video = $probe.streams[0]; capture = $sequence.FullName }
}
@{ release = $manifest; fixtureSha256 = (Get-FileHash -LiteralPath $Fixture).Hash.ToLowerInvariant(); videos = $videos; profileEvidence = $evidence } |
    ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $Output 'videos.json') -Encoding utf8NoBOM
Write-Host "Verified videos: $Output"
