# Copilot UiPath Attended PoC

This proof of concept provides a localhost-only ASP.NET Core bridge that will
eventually let Microsoft Copilot Studio request a UiPath attended process in
the currently logged-in Windows user's interactive session.

The current increment implements the secured API, its default fake Robot
client, a compiled `UiPath.Robot.Api` adapter, and a synthetic attended
workflow. Package version `1.0.6` is published to the authenticated user's
staging personal workspace. Real local RobotAPI and Power Platform gateway
requests completed in the interactive Windows session and returned
`out_Result`.

## Safety Boundary

Use synthetic mock data only. Do not use or store Bank of America customer,
employee, account, credential, authentication, or production information.
Never commit secrets, certificates, generated credentials, `bin/`, or `obj/`.
The bridge binds to `127.0.0.1` by default and must not be exposed through a
public tunnel.

## Workstation Validation

Validated on July 31, 2026:

| Prerequisite | Observed state |
| --- | --- |
| Git | Installed: `2.52.0.windows.1` |
| .NET 8 SDK | Installed: `8.0.423` |
| PowerShell | Windows PowerShell `5.1.26100.8875` installed; PowerShell 7 absent |
| UiPath CLI | Installed: `1.196.4` |
| UiPath Studio | Installed: `24.10.22.0` |
| UiPath Robot | Installed: `24.10.22.0` |
| UiPath Assistant | Installed: `2024.10.22.0` |
| UiPath User Mode host | Installed: `24.10.22.0` |

The desktop components match the target `2024.10.x` baseline. The bridge uses
`UiPath.Robot.Api` `24.10.5`, the newest stable package in that compatibility
family. Its exact local API surface was inspected before implementation, and
the adapter is covered by tests using the real interfaces.

See `docs/environment-validation.md` for paths and detailed findings.

## Run Locally

The Development environment enables `FakeRobotClient`. Set a synthetic local
API key in the current shell before starting the bridge:

```powershell
$env:COPILOT_UIPATH_BRIDGE_API_KEY = "choose-a-local-development-value"
dotnet restore .\CopilotUiPathAttendedPoc.sln
dotnet build .\CopilotUiPathAttendedPoc.sln --no-restore
dotnet test .\CopilotUiPathAttendedPoc.sln --no-build
.\scripts\start-bridge.ps1
```

The API listens only on `http://127.0.0.1:5211`. In a second shell with the
same environment variable, try:

```powershell
.\scripts\test-health.ps1
$job = .\scripts\start-test-job.ps1
.\scripts\get-job-status.ps1 -RequestId $job.requestId
```

Job endpoints require `X-Api-Key` for direct callers. The on-premises gateway
uses Basic authentication with the fixed username `CopilotGateway` and the same
API key as its password. Starts also require `Idempotency-Key` and an ISO-8601
`X-Request-Timestamp`. The bridge validates the allowlist and input sizes,
expires stale requests, generates correlation/request IDs, and permits one
attended job at a time. Job and idempotency state are in memory and reset when
the bridge restarts.

## UiPath Workflow

`uipath/CopilotAttendedPoC/` accepts `in_RequestId`, `in_Message`, and
`in_RequestedBy`, and returns `out_Result`. It uses only synthetic data, writes
the message to a GUID-named file under `%TEMP%`, opens that file in Notepad,
and verifies its window title. Native UiPath 24.10 package execution completed
successfully with output arguments.

Validate and build workflow changes with the UiPath CLI:

```powershell
Push-Location .\uipath\CopilotAttendedPoC
uip rpa validate
uip rpa build .
Pop-Location
```

The current `uip` CLI requires a newer Studio for desktop debug and selector
indication. Do not add preview UI Automation packages or hand-write selectors.
Use Studio 24.10 indication later to replace the safe file-display step with a
true Notepad `Type Into` demonstration.

## Configuration

`appsettings.Development.json` enables the fake client. The allowlist maps the
public alias `CopilotAttendedPoC` to a fixed Robot process name and approved
inputs. Store the key in `COPILOT_UIPATH_BRIDGE_API_KEY` or .NET user secrets,
never `appsettings.json`. Setting `UiPath:UseFakeRobotClient` to `false` selects
the real local RobotAPI client.

Live RobotAPI discovery returns `CopilotAttendedPoC`. The real launch path was
verified with synthetic data on August 3, 2026. See
`docs/uipath-integration.md` for deployment and execution evidence.

## Power Platform Artifacts

The now-unblocked Phase 7 artifacts are under `docs/power-platform/`:

- `custom-connector.swagger.json` defines the three OpenAPI 2.0 connector
  actions with gateway-compatible Basic authentication.
- `on-premises-data-gateway.md` preserves the localhost-only gateway boundary.
- `copilot-studio-agent-flow.md` describes the idempotent flow and polling loop.
- `tool-descriptions.md` and `mock-responses.md` provide safe agent guidance.

Validated on August 4, 2026, in `UiPath (default) (Upgrade)`:

- Unmanaged solution: `Copilot UiPath Attended PoC`
- Connector: `Copilot UiPath Attended Bridge`
- Gateway: `CopilotUiPathAttendedPoc-Gateway-01` in North Europe
- Synthetic request `829ab915-b780-4cda-88b8-4f10d30beac5`: `Completed`

No Copilot Studio flow, public endpoint, or production integration has been
provisioned.
