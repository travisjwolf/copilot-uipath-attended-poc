Set-StrictMode -Version Latest

function Get-BridgeApiKey {
    $apiKey = $env:COPILOT_UIPATH_BRIDGE_API_KEY
    if ([string]::IsNullOrWhiteSpace($apiKey)) {
        throw "Set COPILOT_UIPATH_BRIDGE_API_KEY before running this script."
    }

    return $apiKey
}

function New-BridgeHeaders {
    param(
        [switch]$ForStartJob,
        [string]$IdempotencyKey
    )

    $headers = @{
        "X-Api-Key" = Get-BridgeApiKey
    }

    if ($ForStartJob) {
        if ([string]::IsNullOrWhiteSpace($IdempotencyKey)) {
            $IdempotencyKey = [guid]::NewGuid().ToString("N")
        }

        $headers["Idempotency-Key"] = $IdempotencyKey
        $headers["X-Request-Timestamp"] = [DateTimeOffset]::UtcNow.ToString("O")
    }

    return $headers
}
