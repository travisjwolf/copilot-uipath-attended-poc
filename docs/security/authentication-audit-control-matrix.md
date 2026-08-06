# Authentication and End-to-End Audit Control Matrix

- **Assessment date:** August 6, 2026
- **Scope:** Synthetic attended-automation proof of concept only
- **Overall status:** Functional execution demonstrated; end-to-end
  authenticated user attribution and unified audit evidence are blocked.

## Decision Summary

The POC proves that a Power Platform request can traverse the on-premises data
gateway, reach the localhost bridge, and start an allowlisted UiPath process.
It does **not** prove that the Microsoft 365 user who requested the action is the
same identity that the bridge authorized or the Windows/UiPath identity that
executed it.

The identity chain currently collapses to one stored gateway credential. The
`requestedBy` value is caller-supplied text, not an authenticated claim. Logs
exist in several components, but no durable record joins the Copilot activity,
flow run, gateway call, bridge request, Robot job, and Windows session. The POC
must therefore not be represented as providing end-to-end authentication,
non-repudiation, or audit-ready user attribution.

## Identity Path

```mermaid
flowchart LR
    U["Microsoft 365 user"] -->|"Entra/M365 session"| A["Copilot agent"]
    A -->|"Agent flow invocation; not yet validated"| F["Power Automate"]
    F -->|"Stored connector connection"| G["On-premises gateway"]
    G -->|"Fixed Basic credential"| B["Local launch bridge"]
    B -->|"Local RobotAPI plus unverified requestedBy"| R["UiPath User Mode Robot"]
    R -->|"Logged-in Windows session"| P["Attended process"]
```

## Control Matrix

| Boundary or asset | Current authentication and visible identity | Available evidence | Blocker or control gap | Priority |
|---|---|---|---|---|
| User to Copilot agent | Microsoft Entra ID/Microsoft 365 authenticates the user. Copilot can know the conversational user. | Copilot Studio activity and interaction records may identify the user, agent, operation, and time when tenant licensing, auditing, and retention are configured. | The current private Microsoft 365 agent cannot yet invoke the flows. Missing `agent author` access prevents full Copilot Studio deployment and validation. | P1 |
| Agent to Power Automate | The platform mediates tool invocation. The flow execution and connector identity depend on the trigger, sharing, and connection configuration. | Agent activity, conversation records, flow run history, and solution-aware Dataverse run records can provide separate evidence. | No trusted user claim, agent activity ID, conversation ID, or flow run ID is forwarded to the bridge. The actual tool invocation path is untested. | P0 |
| Flow to connector connection | The current custom connector uses a gateway-backed connection with fixed username `CopilotGateway` and the bridge API key as its password. | Flow action inputs/outputs and the connection reference are available to authorized Power Platform administrators. | All callers using this connection collapse to the same workload credential. Microsoft documents that a gateway normally uses its stored data-source credential regardless of the invoking user. | P0 |
| Gateway cloud service to local gateway | The registered gateway and outbound TLS/Azure Relay channel authenticate the gateway service. The local service runs under its configured Windows service account. | Gateway configuration, cluster membership, service logs, diagnostics, and cloud activity records provide operational evidence. | This authenticates the gateway, not the Copilot user. The current custom-connector path provides no delegated user token or Windows identity to the bridge. | P0 |
| Gateway to launch bridge | The bridge accepts either `X-Api-Key` or Basic authentication. Both methods validate the same shared secret; Basic also validates the fixed username. | ASP.NET logs record HTTP method, path, status, request ID, and a bridge-generated correlation ID. | The bridge creates no authenticated `ClaimsPrincipal`, validates no Entra token or client certificate, and cannot distinguish users, flows, or connector connections holding the secret. The local hop is HTTP, so secret confidentiality relies on workstation and loopback isolation rather than transport encryption. | P0 |
| Bridge authorization and job state | Configuration allowlists the process alias and permitted input names. Timestamp, size, concurrency, and idempotency controls limit replay and misuse. | The bridge records job lifecycle metadata and returns a server-generated request ID. | These are resource and replay controls, not user authorization. Any caller with the shared secret and a request ID can read status or request cancellation. Job state and idempotency history are memory-only and disappear on restart. | P0 |
| Bridge to UiPath RobotAPI | `UiPathRobotClient` selects the fixed local process and calls `RobotClient.RunJob`. It passes the bridge request ID and the unverified `requestedBy` text as workflow inputs. | Bridge lifecycle logs, RobotAPI status events, output arguments, and UiPath execution logs can show local execution. | This implementation passes no upstream token or signed identity claim. It does not persist a UiPath execution/job identifier that can be joined automatically to the Power Platform run. | P0 |
| Robot to attended Windows process | The User Mode Robot executes in the currently logged-in interactive Windows session. | UiPath Robot logs and Windows workstation telemetry may identify the machine and session account. | The desktop account is an execution identity, not proof of the remote initiating user. The initiating and executing people may differ. | P0 |
| Cross-system audit record | Each platform has its own timestamps, identifiers, permissions, and retention controls. | Purview/Copilot logs, Power Automate run records, gateway diagnostics, bridge logs, UiPath audit/Robot logs, and Windows logs are potential evidence sources. | There is no immutable transaction record or common propagated identifier joining all sources. Current reconstruction is manual and timestamp-based. | P0 |

## Existing Controls and Their Limits

| Current control | What it supports | What it does not prove |
|---|---|---|
| Localhost-only bridge and outbound gateway transport | Reduces network exposure and avoids an inbound public endpoint. | The identity of the human requester. |
| Shared API key with constant-time comparison | Authenticates possession of one POC secret. | Which user, agent, flow, or connector used it. |
| Process and input allowlists | Prevents arbitrary process names and unexpected arguments. | Whether a particular user is authorized to run the approved process. |
| Request timestamp and idempotency key | Limits stale requests and duplicate execution while the bridge remains running. | Non-repudiation or durable replay protection after restart. |
| Server-generated request and correlation IDs | Links bridge HTTP and lifecycle logs for one local request. | Upstream Copilot/flow activity or downstream UiPath/Windows execution. |
| Metadata-only logging and synthetic-data policy | Reduces sensitive-data exposure in source and logs. | Completeness, immutability, retention, or centralized audit availability. |

## Blocking Decisions and Acceptance Gates

1. **Choose the identity assurance model.** Sponsors and security must decide
   whether true delegated end-user authentication is required, or whether an
   authenticated workload identity plus cryptographically protected user
   attribution is acceptable. The current stored-credential gateway path does
   neither end to end.
2. **Establish trusted bridge ingress.** Replace or supplement the shared secret
   with an approved workload-authentication design, such as validated Entra
   tokens, mTLS, or a controlled token-exchange/broker pattern. Do not expose
   RobotAPI or the bridge publicly to accomplish this.
3. **Derive identity from trusted claims.** Do not authorize or audit from the
   request body's `requestedBy` field. Bind the authoritative user and workload
   identities to the request on the server side.
4. **Add user-level authorization and ownership.** Define which users or groups
   may start each alias and enforce that only the owning principal or an
   authorized operator can read or cancel a request.
5. **Persist an audit envelope.** Store an append-only transaction containing
   the user ID, agent/conversation ID, flow run ID, connection reference,
   gateway/bridge correlation IDs, bridge request ID, UiPath execution ID,
   machine ID, Windows session identity, timestamps, status, and policy result.
   Store no message body or sensitive input values.
6. **Propagate correlation.** Preserve an approved upstream correlation value
   rather than replacing it at every boundary, while retaining each native ID
   in the audit envelope.
7. **Define retention and monitoring.** Set access, retention, clock
   synchronization, export, alerting, and SIEM requirements across Microsoft,
   gateway, bridge, UiPath, and Windows evidence sources.
8. **Complete a controlled evidence test.** After Copilot Studio access is
   resolved, run one synthetic transaction and demonstrate a single evidence
   package linking the authenticated user to the completed attended process.

## Ownership

| Owner | Required action |
|---|---|
| POC sponsor and security architecture | Approve the identity assurance model and evidence standard. |
| Power Platform administrator | Assign agent author access, configure flow/connection ownership, and enable appropriate Copilot, Dataverse, Purview, and run-history controls. |
| Identity and access management | Approve workload credentials, claim issuance, rotation, revocation, and user/group authorization. |
| Gateway administrator | Control gateway membership, service identity, connector sharing, diagnostics, and credential rotation. |
| Bridge engineering | Implement trusted authentication, principal-bound authorization, durable state, propagated correlation, and audit export. |
| UiPath administrator | Confirm Robot/Orchestrator audit sources, execution identifiers, access, retention, and export for the 2024.10 target. |
| Endpoint operations | Define workstation, interactive-session, Windows logging, lock-state, and time-synchronization controls. |

## UiPath Audit Verification

The installed `uip` CLI version `1.198.0` exposes tenant and organization audit
source discovery, event query, and export. After obtaining an authorized UiPath
session and the required audit permissions, use synthetic test windows only:

```powershell
uip login status --output json
uip admin audit tenant sources --output table
uip admin audit tenant events --from-date <ISO-8601> --to-date <ISO-8601> --limit 200 --output json
uip admin audit tenant export --from-date <ISO-8601> --to-date <ISO-8601> --file-format csv --output-file <approved-path>
```

This assessment verified the local CLI command surface, not the availability or
retention of specific tenant events. Those must be confirmed with UiPath and
enterprise audit administrators before the POC can claim joined evidence.

## Evidence References

Repository evidence:

- [Bridge shared-secret authentication](../../src/AttendedLaunchBridge.Api/Middleware/ApiKeyEndpointFilter.cs#L9)
- [Bridge-generated correlation ID](../../src/AttendedLaunchBridge.Api/Middleware/CorrelationIdMiddleware.cs#L11)
- [Request validation, idempotency, and job metadata](../../src/AttendedLaunchBridge.Api/Application/Services/AttendedJobService.cs#L62)
- [Memory-only job store](../../src/AttendedLaunchBridge.Api/Infrastructure/InMemoryJobStatusStore.cs#L7)
- [Local RobotAPI launch](../../src/AttendedLaunchBridge.Api/Infrastructure/UiPath/UiPathRobotClient.cs#L41)
- [Copilot Studio access blocker](../power-platform/copilot-studio-agent.md#full-studio-blocker)
- [Gateway connection design](../power-platform/on-premises-data-gateway.md#connector-connection)

Platform references:

- [Microsoft gateway architecture and stored credentials](https://learn.microsoft.com/en-us/data-integration/gateway/service-gateway-onprem-indepth)
- [Power Automate flow connections and run-only users](https://learn.microsoft.com/en-us/power-automate/create-team-flows)
- [Copilot Studio activities in Microsoft Purview](https://learn.microsoft.com/en-us/microsoft-copilot-studio/admin-logging-copilot-studio)
- [Power Automate activity and run logging](https://learn.microsoft.com/en-us/power-platform/admin/activity-logging-auditing/activity-logs-power-automate)
- [UiPath RobotAPI behavior and compatibility](https://docs.uipath.com/robot/standalone/latest/admin-guide/robot-api)
- [UiPath audit log capabilities and retention](https://docs.uipath.com/automation-cloud/automation-cloud/latest/admin-guide/about-logs)
