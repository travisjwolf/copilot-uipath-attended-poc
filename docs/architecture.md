# Local Bridge Architecture

The default Development path remains fully local and deterministic:

```text
HTTP request on 127.0.0.1
  -> API key + request size filters
  -> server-generated correlation ID
  -> minimal API endpoints
  -> AttendedJobService
  -> allowlist + idempotency + expiration + single-job gate
  -> in-memory status store
  -> IRobotProcessProvider / IAttendedJobLauncher
  -> FakeRobotClient
```

`AttendedJobService` generates each request ID, overwrites `in_RequestId`,
records status transitions, and coordinates cancellation. Starts require an
idempotency key and timestamp; matching replays return the original request ID.
API responses never return submitted input values. Logging contains request
IDs, correlation IDs, process aliases, statuses, and failure types only.

`UiPathRobotClient` implements the same interfaces using the verified
`UiPath.Robot.Api` 24.10 contracts. It refreshes local process discovery,
resolves only the fixed configured process name, submits allowlisted inputs,
tracks Robot status events, returns output arguments, and passes cancellation
to `RunJob`. Configuration selects it only when
`UiPath:UseFakeRobotClient` is `false`.

The eventual transport remains:

```text
Copilot Studio -> Power Automate -> on-premises data gateway
  -> localhost bridge -> UiPath.Robot.api -> User Mode Robot
  -> attended process in the logged-in desktop session
```

The workflow package runs through the native 24.10 Robot CLI and is published
to the authenticated staging user's personal workspace. Live RobotAPI discovery
returns the approved process, and a bridge request completed through the local
User Mode Robot with an output argument. Power Platform design artifacts are
now available under `docs/power-platform/`; provisioning remains intentionally
out of scope.
