# Scripts

These Windows PowerShell 5.1-compatible scripts operate the localhost bridge.
Set a synthetic development API key in the current shell first:

```powershell
$env:COPILOT_UIPATH_BRIDGE_API_KEY = "choose-a-local-development-value"
```

Start the API with `./scripts/start-bridge.ps1`, then use
`./scripts/test-health.ps1` and `./scripts/start-test-job.ps1`. Pass the
returned request ID to `./scripts/get-job-status.ps1 -RequestId <guid>` or
`./scripts/cancel-test-job.ps1 -RequestId <guid>`. Job-start requests generate
an idempotency key unless `-IdempotencyKey` is supplied explicitly.

Use `./scripts/start-real-bridge.ps1` when testing the published attended
process. It runs in Production, explicitly enables `UiPathRobotClient`, and
retains the localhost-only endpoint.

The scripts read the API key only from the environment and do not persist it.

After building the solution, run `./scripts/validate-real-robotapi.ps1` for a
one-time real RobotAPI smoke test. It starts a temporary production-mode bridge,
discovers the allowlisted process, launches one synthetic attended request, and
verifies the output and Notepad evidence. If no API key environment variable is
present, the script generates an ephemeral key that exists only for that run.
