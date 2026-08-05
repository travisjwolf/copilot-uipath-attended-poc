[CmdletBinding()]
param(
    [string]$BaseUrl = "http://127.0.0.1:5211",
    [int]$StartupTimeoutSeconds = 45,
    [int]$JobTimeoutSeconds = 90
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot
$bridgeDll = Join-Path $repoRoot "src\AttendedLaunchBridge.Api\bin\Debug\net8.0\AttendedLaunchBridge.Api.dll"
if (-not (Test-Path -LiteralPath $bridgeDll)) {
    throw "Build the solution before running the real RobotAPI validation."
}

$apiKey = $env:COPILOT_UIPATH_BRIDGE_API_KEY
if ([string]::IsNullOrWhiteSpace($apiKey)) {
    $keyBytes = New-Object byte[] 48
    $random = [Security.Cryptography.RandomNumberGenerator]::Create()
    try {
        $random.GetBytes($keyBytes)
    }
    finally {
        $random.Dispose()
    }

    $apiKey = [Convert]::ToBase64String($keyBytes)
}

$stdoutPath = Join-Path $env:TEMP ("bridge-real-{0}.out.log" -f [guid]::NewGuid().ToString("N"))
$stderrPath = Join-Path $env:TEMP ("bridge-real-{0}.err.log" -f [guid]::NewGuid().ToString("N"))
$bridge = $null
$stdoutTask = $null
$stderrTask = $null
$testSucceeded = $false

try {
    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = "dotnet.exe"
    $startInfo.Arguments = ('"{0}"' -f $bridgeDll)
    $startInfo.WorkingDirectory = Split-Path -Parent $bridgeDll
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.EnvironmentVariables["ASPNETCORE_ENVIRONMENT"] = "Production"
    $startInfo.EnvironmentVariables["DOTNET_ENVIRONMENT"] = "Production"
    $startInfo.EnvironmentVariables["COPILOT_UIPATH_BRIDGE_API_KEY"] = $apiKey
    $startInfo.EnvironmentVariables["UiPath__UseFakeRobotClient"] = "false"

    $bridge = New-Object System.Diagnostics.Process
    $bridge.StartInfo = $startInfo
    if (-not $bridge.Start()) {
        throw "The real-client bridge process could not be started."
    }

    $stdoutTask = $bridge.StandardOutput.ReadToEndAsync()
    $stderrTask = $bridge.StandardError.ReadToEndAsync()

    $health = $null
    $healthDeadline = [DateTimeOffset]::UtcNow.AddSeconds($StartupTimeoutSeconds)
    do {
        Start-Sleep -Milliseconds 500
        if ($bridge.HasExited) {
            throw "The real-client bridge exited before becoming healthy."
        }

        try {
            $health = Invoke-RestMethod -Method Get -Uri "$BaseUrl/health" -TimeoutSec 3
        }
        catch {
            $health = $null
        }
    } while ($null -eq $health -and [DateTimeOffset]::UtcNow -lt $healthDeadline)

    if ($null -eq $health) {
        throw "The real-client bridge did not become healthy within $StartupTimeoutSeconds seconds."
    }

    $processes = @(Invoke-RestMethod -Method Get -Uri "$BaseUrl/api/processes" -TimeoutSec 45)
    $aliases = @($processes | ForEach-Object { $_.processAlias })
    if ($aliases.Count -ne 1 -or $aliases[0] -ne "CopilotAttendedPoC") {
        throw "RobotAPI discovery did not return exactly the approved CopilotAttendedPoC alias."
    }

    $requestedBy = "synthetic-local-validation"
    $message = "Synthetic Copilot-to-UiPath attended validation after gateway registration."
    $headers = @{
        "X-Api-Key" = $apiKey
        "Idempotency-Key" = [guid]::NewGuid().ToString("N")
        "X-Request-Timestamp" = [DateTimeOffset]::UtcNow.ToString("O")
        "X-Correlation-ID" = [guid]::NewGuid().ToString()
    }
    $body = @{
        processAlias = "CopilotAttendedPoC"
        requestedBy = $requestedBy
        inputs = @{
            in_Message = $message
            in_RequestedBy = $requestedBy
        }
    } | ConvertTo-Json -Depth 4

    $accepted = Invoke-RestMethod `
        -Method Post `
        -Uri "$BaseUrl/api/jobs" `
        -Headers $headers `
        -ContentType "application/json" `
        -Body $body `
        -TimeoutSec 30
    $requestId = [guid]$accepted.requestId

    $statusHeaders = @{ "X-Api-Key" = $apiKey }
    $status = $null
    $statusDeadline = [DateTimeOffset]::UtcNow.AddSeconds($JobTimeoutSeconds)
    do {
        Start-Sleep -Milliseconds 500
        $status = Invoke-RestMethod `
            -Method Get `
            -Uri "$BaseUrl/api/jobs/$requestId" `
            -Headers $statusHeaders `
            -TimeoutSec 10
    } while (
        $status.status -notin @("Completed", "Failed", "Cancelled") -and
        [DateTimeOffset]::UtcNow -lt $statusDeadline
    )

    if ($status.status -ne "Completed") {
        throw "The attended job ended with status '$($status.status)'."
    }

    $expectedResult = "Synthetic Notepad demonstration completed for request $requestId."
    if ($status.result.out_Result -ne $expectedResult) {
        throw "The UiPath output argument did not match the expected synthetic result."
    }

    $evidencePath = Join-Path $env:TEMP ("CopilotAttendedPoC-{0}.txt" -f $requestId.ToString("N"))
    $evidenceMatches =
        (Test-Path -LiteralPath $evidencePath) -and
        ((Get-Content -LiteralPath $evidencePath -Raw) -eq $message)
    $notepadWindows = @(
        Get-Process -Name notepad -ErrorAction SilentlyContinue |
            Where-Object {
                $_.MainWindowTitle -like "*CopilotAttendedPoC-$($requestId.ToString("N"))*"
            }
    )

    if (-not $evidenceMatches -or $notepadWindows.Count -ne 1) {
        throw "The attended desktop evidence did not match the accepted request."
    }

    $testSucceeded = $true
    [pscustomobject]@{
        Health = $health.status
        DiscoveredAlias = $aliases[0]
        RequestId = $requestId
        FinalStatus = $status.status
        Output = $status.result.out_Result
        EvidenceFileMatched = $evidenceMatches
        MatchingNotepadWindows = $notepadWindows.Count
    }
}
finally {
    if ($null -ne $bridge -and -not $bridge.HasExited) {
        $bridge.Kill()
        $bridge.WaitForExit(10000) | Out-Null
    }

    if ($null -ne $bridge) {
        if ($null -ne $stdoutTask) {
            $stdoutTask.GetAwaiter().GetResult() | Set-Content -LiteralPath $stdoutPath
        }
        if ($null -ne $stderrTask) {
            $stderrTask.GetAwaiter().GetResult() | Set-Content -LiteralPath $stderrPath
        }
    }

    if (-not $testSucceeded) {
        Write-Output "BRIDGE_STDOUT_TAIL"
        if (Test-Path -LiteralPath $stdoutPath) {
            Get-Content -LiteralPath $stdoutPath -Tail 30
        }
        Write-Output "BRIDGE_STDERR_TAIL"
        if (Test-Path -LiteralPath $stderrPath) {
            Get-Content -LiteralPath $stderrPath -Tail 30
        }
    }

    if ([System.IO.File]::Exists($stdoutPath)) {
        [System.IO.File]::Delete($stdoutPath)
    }
    if ([System.IO.File]::Exists($stderrPath)) {
        [System.IO.File]::Delete($stderrPath)
    }
}
