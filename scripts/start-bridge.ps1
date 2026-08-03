[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot "common.ps1")

$null = Get-BridgeApiKey
$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "src\AttendedLaunchBridge.Api"

$env:ASPNETCORE_ENVIRONMENT = "Development"
& dotnet run --project $projectPath --launch-profile http
exit $LASTEXITCODE
