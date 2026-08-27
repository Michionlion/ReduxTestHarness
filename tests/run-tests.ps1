[CmdletBinding()]
param(
    [string] $GameRoot = $env:KSP2_ROOT
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$pwsh = (Get-Command pwsh -ErrorAction Stop).Source

function Assert-True {
    param([bool] $Condition, [string] $Message)
    if (-not $Condition) { throw $Message }
}

function Send-BridgeRequest {
    param([int] $Port, [hashtable] $Request)
    $client = [Net.Sockets.TcpClient]::new()
    try {
        $client.Connect([Net.IPAddress]::Loopback, $Port)
        $stream = $client.GetStream()
        $utf8 = [Text.UTF8Encoding]::new($false)
        $writer = [IO.StreamWriter]::new($stream, $utf8, 4096, $true)
        $reader = [IO.StreamReader]::new($stream, $utf8, $false, 4096, $true)
        try {
            $writer.WriteLine(($Request | ConvertTo-Json -Compress))
            $writer.Flush()
            return $reader.ReadLine() | ConvertFrom-Json
        }
        finally {
            $reader.Dispose()
            $writer.Dispose()
        }
    }
    finally {
        $client.Dispose()
    }
}

Write-Host 'Checking PowerShell syntax...'
$powershellFiles = Get-ChildItem -LiteralPath $repoRoot -Recurse -File |
    Where-Object Extension -In @('.ps1', '.psm1')
foreach ($file in $powershellFiles) {
    $tokens = $null
    $errors = $null
    [void][Management.Automation.Language.Parser]::ParseFile(
        $file.FullName,
        [ref]$tokens,
        [ref]$errors)
    Assert-True ($errors.Count -eq 0) "PowerShell parse errors in $($file.FullName): $errors"
}

Write-Host 'Checking JSON metadata and schema documents...'
$jsonFiles = Get-ChildItem -LiteralPath $repoRoot -Recurse -Filter '*.json' -File |
    Where-Object FullName -NotMatch '[\\/]build[\\/]'
foreach ($file in $jsonFiles) {
    # KSP2 save fixtures legitimately contain property names that differ only
    # by casing (for example partGuid and PartGuid). A hashtable preserves both
    # while still making malformed JSON fail this validation pass.
    $document = Get-Content -LiteralPath $file.FullName -Raw | ConvertFrom-Json -AsHashtable
    Assert-True ($null -ne $document) "JSON document did not parse: $($file.FullName)"
}

Write-Host 'Compiling the in-game mod...'
$buildArguments = @('-NoProfile', '-File', (Join-Path $repoRoot 'scripts\build-mod.ps1'))
if ($GameRoot) { $buildArguments += @('-GameRoot', $GameRoot) }
& $pwsh @buildArguments
Assert-True ($LASTEXITCODE -eq 0) "build-mod.ps1 exited with $LASTEXITCODE"
$builtDll = Join-Path $repoRoot 'build\ReduxTestHarness\ReduxTestHarness.dll'
Assert-True (Test-Path -LiteralPath $builtDll) "Built DLL is missing: $builtDll"

Write-Host 'Exercising CLI status and run against a loopback mock...'
$portProbe = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
$portProbe.Start()
$port = ([Net.IPEndPoint]$portProbe.LocalEndpoint).Port
$portProbe.Stop()

$mock = Start-Process `
    -FilePath $pwsh `
    -ArgumentList @('-NoProfile', '-File', (Join-Path $PSScriptRoot 'mock-bridge.ps1'), '-Port', $port) `
    -WindowStyle Hidden `
    -PassThru
try {
    $ready = $false
    for ($attempt = 0; $attempt -lt 40; $attempt++) {
        if ($mock.HasExited) {
            throw "Mock bridge exited early with code $($mock.ExitCode)."
        }
        try {
            $ping = Send-BridgeRequest -Port $port -Request @{ command = 'ping' }
            if ($ping.ok -and $ping.ready) {
                $ready = $true
                break
            }
        }
        catch {
            Start-Sleep -Milliseconds 100
        }
    }
    Assert-True $ready 'Mock bridge did not become ready.'

    $cli = Join-Path $repoRoot 'redux-test.ps1'
    & $pwsh -NoProfile -File $cli status -Port $port
    Assert-True ($LASTEXITCODE -eq 0) "redux-test status exited with $LASTEXITCODE"

    & $pwsh -NoProfile -File $cli run `
        (Join-Path $repoRoot 'tests\smoke\orbit-render.lua') `
        -Port $port `
        -Timeout 10 `
        -Results (Join-Path $repoRoot '.test-results\mock')
    Assert-True ($LASTEXITCODE -eq 0) "redux-test run exited with $LASTEXITCODE"

    $failedOutput = & $pwsh -NoProfile -File $cli run `
        (Join-Path $repoRoot 'tests\mock-fail.lua') `
        -Port $port `
        -Timeout 10 `
        -Results (Join-Path $repoRoot '.test-results\mock') 2>&1
    $failedExit = $LASTEXITCODE
    Assert-True ($failedExit -eq 1) "redux-test failed run exited with $failedExit instead of 1: $failedOutput"

    $shutdown = Send-BridgeRequest -Port $port -Request @{ command = 'shutdown' }
    Assert-True ($shutdown.ok -eq $true) 'Mock bridge rejected shutdown.'
    [void]$mock.WaitForExit(5000)
}
finally {
    if (-not $mock.HasExited) {
        Stop-Process -Id $mock.Id -Force
    }
    $mock.Dispose()
}

$unavailableProbe = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
$unavailableProbe.Start()
$unavailablePort = ([Net.IPEndPoint]$unavailableProbe.LocalEndpoint).Port
$unavailableProbe.Stop()
$unavailableOutput = & $pwsh -NoProfile -File (Join-Path $repoRoot 'redux-test.ps1') `
    status -Port $unavailablePort 2>&1
$unavailableExit = $LASTEXITCODE
Assert-True ($unavailableExit -eq 2) `
    "redux-test unavailable status exited with $unavailableExit instead of 2: $unavailableOutput"

Write-Host 'PASS - static checks, compilation, and CLI mock integration succeeded.'
