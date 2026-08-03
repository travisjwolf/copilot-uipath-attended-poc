# Environment Validation

Validation date: 2026-07-31

## Repository

- Repository root: `C:\uipath-projects\copilot-uipath-attended-poc`
- Initial contents: `AGENTS.md`
- Git repository: initialized on branch `main`
- Git author identity: not configured; commits require a user-provided name
  and email

## Developer Tools

| Tool | Version | Path |
| --- | --- | --- |
| Git | `2.52.0.windows.1` | `C:\Program Files\Git\cmd\git.exe` |
| .NET SDK | `8.0.423` | `C:\Program Files\dotnet\dotnet.exe` |
| ASP.NET Core runtime | `8.0.29` | `C:\Program Files\dotnet\shared\Microsoft.AspNetCore.App` |
| Windows PowerShell | `5.1.26100.8875` | Windows built-in |
| PowerShell 7 | Not installed | `pwsh` not found |
| UiPath CLI | `1.196.4` | `%APPDATA%\npm\uip.ps1` |

Windows PowerShell 5.1 is sufficient for the planned scripts. PowerShell 7 is
optional and must not be installed without approval.

## UiPath Installation

| Component | Observed version | Path |
| --- | --- | --- |
| Studio | `24.10.22.0` | `C:\Program Files\UiPath\Studio\UiPath.Studio.exe` |
| Robot | `24.10.22.0` | `C:\Program Files\UiPath\Studio\UiRobot.exe` |
| Assistant | `2024.10.22.0` | `C:\Program Files\UiPath\Studio\UiPathAssistant\UiPath.Assistant.exe` |
| User Mode host | `24.10.22.0` | `C:\Program Files\UiPath\Studio\UiPath.Service.UserHost.exe` |
| Executor | `24.10.22.0` | `C:\Program Files\UiPath\Studio\UiPath.Executor.exe` |

Assistant, Executor, RobotJS UserHost, and UiPath Service UserHost processes
were running during validation. A synthetic package was executed successfully
through `UiRobot.exe` in the current user session.

## Robot API Findings

`C:\Program Files\UiPath\Studio\UiPath.Robot.Api.dll` is installed with product
version `24.10.22`. A framework-specific copy under the Studio installation has
assembly and file version `24.10.22`.

NuGet.org does not list `UiPath.Robot.Api`. UiPath's documented `UiPath-Official`
feed currently indexes RobotAPI only through `22.10.3`. Read-only inspection of
the unauthenticated `Public.Feeds` endpoint named `UiPath-Internal` found stable
`24.10.0`, `24.10.1`, and `24.10.5`; `24.10.5` is the newest stable package in
that compatibility family. The repository-scoped `NuGet.config` pins this
confirmed source; it contains no credentials.

The API and test projects reference `UiPath.Robot.Api` `24.10.5`. The adapter
was implemented only after inspecting the local assembly and package methods,
constructors, properties, events, cancellation token, and output types. Live
`GetProcesses` completed successfully, but the approved process was absent.

Studio reports no development license on this workstation, so its local
publisher cannot complete. The native Robot pack and file execution commands
do work. No administrator action, new UiPath authentication, or external
endpoint creation was performed. A temporary copy of the synthetic package was
placed in the documented Robot default feed while testing local installation;
the connected Robot did not register it, and command policy prevented automated
cleanup of that exact file.
