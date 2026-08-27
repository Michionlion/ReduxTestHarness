[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateRange(1, 65535)]
    [int] $Port
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, $Port)
$listener.Start(4)
$running = $true
$runId = $null
$runStatus = 'passed'

try {
    while ($running) {
        $client = $listener.AcceptTcpClient()
        try {
            $stream = $client.GetStream()
            $utf8 = [Text.UTF8Encoding]::new($false)
            $reader = [IO.StreamReader]::new($stream, $utf8, $false, 4096, $true)
            $writer = [IO.StreamWriter]::new($stream, $utf8, 4096, $true)
            $writer.AutoFlush = $true
            try {
                $request = $reader.ReadLine() | ConvertFrom-Json
                $response = switch ($request.command) {
                    'ping' {
                        [ordered]@{
                            ok = $true
                            ready = $true
                            gameState = 'MainMenu'
                            testStatus = if ($runId) { 'passed' } else { 'idle' }
                            protocolVersion = 1
                            reduxCliIntegration = [ordered]@{ available = $false }
                        }
                    }
                    'run_script' {
                        $runId = [string]$request.runId
                        $runStatus = if ([string]$request.script -match 'MOCK_FAIL') {
                            'failed'
                        } else {
                            'passed'
                        }
                        [ordered]@{
                            ok = $true
                            accepted = $true
                            runId = $runId
                            artifactDirectory = 'mock-artifacts'
                        }
                    }
                    'get_status' {
                        [ordered]@{
                            ok = $true
                            runId = $runId
                            name = 'Mock bridge pass'
                            status = $runStatus
                            reportPath = 'mock-artifacts\report.json'
                            error = if ($runStatus -eq 'failed') { 'Intentional mock failure.' } else { $null }
                            screenshots = @()
                        }
                    }
                    'cancel_test' {
                        [ordered]@{ ok = $true; status = 'cancelled' }
                    }
                    'shutdown' {
                        $running = $false
                        [ordered]@{ ok = $true }
                    }
                    default {
                        [ordered]@{
                            ok = $false
                            code = 'unknown_command'
                            error = "Unknown mock command: $($request.command)"
                        }
                    }
                }
                $writer.WriteLine(($response | ConvertTo-Json -Compress -Depth 10))
            }
            finally {
                $writer.Dispose()
                $reader.Dispose()
            }
        }
        finally {
            $client.Dispose()
        }
    }
}
finally {
    $listener.Stop()
}
