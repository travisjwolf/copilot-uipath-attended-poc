[CmdletBinding()]
param(
    [string]$BaseUrl = "http://127.0.0.1:5211",
    [string]$RequestedBy = "synthetic-test-user",
    [string]$Message = "This synthetic attended automation was requested locally.",
    [string]$IdempotencyKey = ([guid]::NewGuid().ToString("N"))
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot "common.ps1")

$body = @{
    processAlias = "CopilotAttendedPoC"
    requestedBy = $RequestedBy
    inputs = @{
        in_Message = $Message
        in_RequestedBy = $RequestedBy
    }
} | ConvertTo-Json -Depth 4

Invoke-RestMethod `
    -Method Post `
    -Uri "$($BaseUrl.TrimEnd('/'))/api/jobs" `
    -Headers (New-BridgeHeaders -ForStartJob -IdempotencyKey $IdempotencyKey) `
    -ContentType "application/json" `
    -Body $body
