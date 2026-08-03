[CmdletBinding()]
param(
    [string]$BaseUrl = "http://127.0.0.1:5211"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot "common.ps1")

Invoke-RestMethod `
    -Method Get `
    -Uri "$($BaseUrl.TrimEnd('/'))/health" `
    -Headers (New-BridgeHeaders)
