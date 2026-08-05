[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot "common.ps1")

$null = Get-BridgeApiKey
$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "src\AttendedLaunchBridge.Api"

$env:ASPNETCORE_ENVIRONMENT = "Production"
$env:DOTNET_ENVIRONMENT = "Production"
$env:UiPath__UseFakeRobotClient = "false"

& dotnet run --project $projectPath --no-launch-profile
exit $LASTEXITCODE
