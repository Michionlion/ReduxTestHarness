[CmdletBinding()]
param(
    [Parameter(Position = 0, Mandatory = $true)]
    [ValidateSet('status', 'run')]
    [string] $Command,

    [Parameter(Position = 1)]
    [string] $Script,

    [switch] $Launch,
    [switch] $KeepOpen,
    [switch] $KeepStartupWarning,
    [ValidateRange(1, 86400)]
    [int] $Timeout = 180,
    [ValidateRange(0, 60)]
    [double] $StartupSettleSeconds = 2,
    [string] $Results,
    [string] $Fixtures,
    [string] $GameRoot,
    [Alias('host')]
    [string] $Address = '127.0.0.1',
    [ValidateRange(1, 65535)]
    [int] $Port = 28542
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Write-CliError {
    param([Parameter(Mandatory = $true)] [string] $Message)
    [Console]::Error.WriteLine($Message)
}

function Resolve-AbsolutePath {
    param(
        [Parameter(Mandatory = $true)] [string] $Path,
        [switch] $AllowMissing
    )

    if ([IO.Path]::IsPathRooted($Path)) {
        $candidate = [IO.Path]::GetFullPath($Path)
    }
    else {
        $candidate = [IO.Path]::GetFullPath((Join-Path (Get-Location) $Path))
    }

    if (-not $AllowMissing -and -not (Test-Path -LiteralPath $candidate)) {
        throw "Path does not exist: $candidate"
    }
    return $candidate
}

function Resolve-KspRoot {
    param([string] $RequestedRoot)

    $candidates = [Collections.Generic.List[string]]::new()
    if ($RequestedRoot) { $candidates.Add($RequestedRoot) }
    if ($env:KSP2_ROOT) { $candidates.Add($env:KSP2_ROOT) }
    $candidates.Add('G:\SteamLibrary\steamapps\common\Kerbal Space Program 2')
    $candidates.Add('C:\Program Files (x86)\Steam\steamapps\common\Kerbal Space Program 2')

    foreach ($candidate in $candidates) {
        if (-not $candidate) { continue }
        $root = Resolve-AbsolutePath -Path $candidate -AllowMissing
        if (Test-Path -LiteralPath (Join-Path $root 'KSP2_x64.exe')) {
            return $root
        }
    }
    throw 'KSP2 was not found. Pass --GameRoot or set KSP2_ROOT.'
}

function Invoke-BridgeRequest {
    param(
        [Parameter(Mandatory = $true)] [hashtable] $Request,
        [int] $ConnectTimeoutMilliseconds = 2000
    )

    $client = [Net.Sockets.TcpClient]::new()
    try {
        $connect = $client.ConnectAsync($Address, $Port)
        if (-not $connect.Wait($ConnectTimeoutMilliseconds)) {
            throw "Timed out connecting to $Address`:$Port"
        }
        if ($connect.IsFaulted) {
            throw $connect.Exception.GetBaseException()
        }

        $stream = $client.GetStream()
        $stream.ReadTimeout = [Math]::Max(5000, $ConnectTimeoutMilliseconds)
        $stream.WriteTimeout = [Math]::Max(5000, $ConnectTimeoutMilliseconds)
        $utf8 = [Text.UTF8Encoding]::new($false)
        $writer = [IO.StreamWriter]::new($stream, $utf8, 4096, $true)
        $reader = [IO.StreamReader]::new($stream, $utf8, $false, 4096, $true)
        try {
            $writer.WriteLine(($Request | ConvertTo-Json -Compress -Depth 20))
            $writer.Flush()
            $line = $reader.ReadLine()
            if ([string]::IsNullOrWhiteSpace($line)) {
                throw 'The Redux test bridge closed the connection without a response.'
            }
            return $line | ConvertFrom-Json
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

function Test-BridgeReady {
    try {
        $response = Invoke-BridgeRequest -Request @{ command = 'ping' } -ConnectTimeoutMilliseconds 750
        return $response.ok -eq $true -and $response.ready -eq $true
    }
    catch {
        return $false
    }
}

function Wait-BridgeReady {
    param(
        [Parameter(Mandatory = $true)] [Diagnostics.Stopwatch] $Clock,
        [Parameter(Mandatory = $true)] [int] $TimeoutSeconds,
        [Diagnostics.Process] $Process
    )

    while ($Clock.Elapsed.TotalSeconds -lt $TimeoutSeconds) {
        if ($Process -and $Process.HasExited) {
            throw "KSP2 exited before the Redux test bridge became ready (exit code $($Process.ExitCode))."
        }
        if (Test-BridgeReady) { return }
        Start-Sleep -Milliseconds 500
    }
    throw "Timed out waiting for the Redux test bridge at $Address`:$Port."
}

function Wait-StartupMenuReady {
    param(
        [Parameter(Mandatory = $true)] [Diagnostics.Stopwatch] $Clock,
        [Parameter(Mandatory = $true)] [int] $TimeoutSeconds,
        [Parameter(Mandatory = $true)] [double] $SettleSeconds,
        [Diagnostics.Process] $Process
    )

    $readyAtSeconds = $null
    $lastState = 'unknown'
    while ($Clock.Elapsed.TotalSeconds -lt $TimeoutSeconds) {
        if ($Process -and $Process.HasExited) {
            throw "KSP2 exited before the startup menu became ready (exit code $($Process.ExitCode))."
        }

        try {
            $status = Invoke-BridgeRequest -Request @{ command = 'ping' } -ConnectTimeoutMilliseconds 750
            $lastState = [string] $status.gameState
            if ($status.ok -eq $true -and $lastState -eq 'MainMenu' -and
                $status.startupWarningVisible -ne $true) {
                if ($null -eq $readyAtSeconds) {
                    $readyAtSeconds = $Clock.Elapsed.TotalSeconds
                }
                if (($Clock.Elapsed.TotalSeconds - $readyAtSeconds) -ge $SettleSeconds) {
                    return
                }
            }
            else {
                $readyAtSeconds = $null
            }
        }
        catch {
            $readyAtSeconds = $null
        }
        Start-Sleep -Milliseconds 250
    }
    throw "Timed out waiting for a settled KSP2 main menu (last game state: $lastState)."
}

function Write-RunResult {
    param([Parameter(Mandatory = $true)] $Status)

    $label = if ($Status.status -eq 'passed') { 'PASS' } else { 'FAIL' }
    Write-Host "$label $($Status.name)"
    if ($Status.reportPath) {
        Write-Host "`nReport:`n$($Status.reportPath)"
    }
    if ($Status.screenshots -and $Status.screenshots.Count -gt 0) {
        Write-Host "`nScreenshots:"
        foreach ($path in $Status.screenshots) { Write-Host $path }
    }
    if ($Status.error) {
        Write-Host "`nError:`n$($Status.error)" -ForegroundColor Red
    }
}

if ($Command -eq 'status') {
    try {
        $status = Invoke-BridgeRequest -Request @{ command = 'ping' }
        if (-not $status.ok) { throw $status.error }
        Write-Host "Redux test bridge: ready"
        Write-Host "Game state: $($status.gameState)"
        Write-Host "Test state: $($status.testStatus)"
        if ($null -ne $status.reduxCliIntegration) {
            $nativeState = if ($status.reduxCliIntegration.available) { 'available' } else { 'missing from player build' }
            Write-Host "Redux CliIntegration: $nativeState"
        }
        exit 0
    }
    catch {
        Write-CliError "Redux test bridge is unavailable at $Address`:$Port. $($_.Exception.Message)"
        exit 2
    }
}

if (-not $Script) {
    Write-CliError 'The run command requires a Lua script path.'
    exit 2
}

$launchedProcess = $null
$launchedByThisCommand = $false
$clock = [Diagnostics.Stopwatch]::StartNew()

try {
    $scriptPath = Resolve-AbsolutePath -Path $Script
    $scriptText = [IO.File]::ReadAllText($scriptPath)
    $repoRoot = $PSScriptRoot
    $resultsRoot = if ($Results) {
        Resolve-AbsolutePath -Path $Results -AllowMissing
    }
    else {
        Join-Path $repoRoot '.test-results'
    }
    $fixturesRoot = if ($Fixtures) {
        Resolve-AbsolutePath -Path $Fixtures -AllowMissing
    }
    else {
        Join-Path $repoRoot 'fixtures'
    }

    if (-not (Test-BridgeReady)) {
        if (-not $Launch) {
            throw "Redux test bridge is unavailable at $Address`:$Port. Start KSP2 or pass --launch."
        }

        $resolvedGameRoot = Resolve-KspRoot -RequestedRoot $GameRoot
        $marker = Join-Path $resolvedGameRoot 'mods\ReduxTestHarness\test-mode.enabled'
        if (-not (Test-Path -LiteralPath $marker)) {
            throw "ReduxTestHarness is not installed/enabled. Run scripts\install-mod.ps1 first (missing $marker)."
        }
        $launchEnvironment = @{
            REDUX_TEST_ENABLE = '1'
            REDUX_TEST_PORT = $Port.ToString([Globalization.CultureInfo]::InvariantCulture)
            REDUX_TEST_DISMISS_PHOTOSENSITIVITY = $(if ($KeepStartupWarning) { '0' } else { '1' })
        }
        $launchedProcess = Start-Process `
            -FilePath (Join-Path $resolvedGameRoot 'KSP2_x64.exe') `
            -WorkingDirectory $resolvedGameRoot `
            -Environment $launchEnvironment `
            -PassThru
        $launchedByThisCommand = $true
        Wait-BridgeReady -Clock $clock -TimeoutSeconds $Timeout -Process $launchedProcess
        Wait-StartupMenuReady `
            -Clock $clock `
            -TimeoutSeconds $Timeout `
            -SettleSeconds $StartupSettleSeconds `
            -Process $launchedProcess
    }

    $runId = [Guid]::NewGuid().ToString('N')
    $response = Invoke-BridgeRequest -Request @{
        command = 'run_script'
        runId = $runId
        script = $scriptText
        scriptPath = $scriptPath
        resultsRoot = $resultsRoot
        fixturesRoot = $fixturesRoot
        timeoutSeconds = $Timeout
    } -ConnectTimeoutMilliseconds 10000

    if (-not $response.ok) {
        throw $response.error
    }

    while ($clock.Elapsed.TotalSeconds -lt $Timeout) {
        if ($launchedProcess -and $launchedProcess.HasExited) {
            throw "KSP2 exited while the test was running (exit code $($launchedProcess.ExitCode))."
        }

        Start-Sleep -Milliseconds 350
        $status = Invoke-BridgeRequest -Request @{
            command = 'get_status'
            runId = $runId
        }
        if (-not $status.ok) { throw $status.error }
        if ($status.status -in @('passed', 'failed', 'cancelled')) {
            Write-RunResult -Status $status
            if ($status.status -eq 'passed') { exit 0 }
            exit 1
        }
    }

    try {
        [void](Invoke-BridgeRequest -Request @{ command = 'cancel_test'; runId = $runId })
    }
    catch { }
    throw "Test exceeded the overall timeout of $Timeout seconds."
}
catch {
    Write-CliError $_.Exception.Message
    exit 2
}
finally {
    if ($launchedByThisCommand -and -not $KeepOpen) {
        try {
            [void](Invoke-BridgeRequest -Request @{ command = 'shutdown'; quitGame = $true })
        }
        catch {
            if ($launchedProcess -and -not $launchedProcess.HasExited) {
                Write-Warning 'KSP2 was launched by this command but could not be asked to shut down cleanly.'
            }
        }
    }
}
