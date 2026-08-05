# Copilot Studio Tool Descriptions

## Start Tool

**Name:** `Start attended desktop demonstration`

**Description:** Use this tool only when the user explicitly asks to run the
approved synthetic UiPath desktop demonstration on their currently available
Windows workstation. It returns an `Accepted` status and request ID immediately;
it does not wait for completion. Never use it for real customer, employee,
account, credential, authentication, or production data.

## Status Tool

**Name:** `Check attended desktop demonstration status`

**Description:** Use this tool with the request ID returned by the start tool to
check the current attended demonstration state. Call it no more than once every
15 seconds. Report completion only when status is `Completed`; otherwise report
`Running`, `Failed`, or `Cancelled` accurately without exposing raw errors.

### Start Inputs

- `message`: A short synthetic message to display. Reject or replace content
  that appears to contain sensitive or production information.
- `requestedBy`: A synthetic caller label supplied by the flow or agent context;
  do not use an email address, employee ID, or authentication identifier.

### Status Input

- `requestId`: An opaque bridge-generated identifier returned by the start tool;
  never invent one or accept executable, package, XAML, or process identifiers.

## Connector Actions

- `StartUiPathAttendedJob`: Start the allowlisted process once. The flow, not the
  agent, generates the idempotency key and timestamp.
- `GetUiPathAttendedJobStatus`: Check a server-generated request ID at most once
  every 15 seconds, stopping after 48 checks or 12 minutes.
- `CancelUiPathAttendedJob`: Use only for an explicit cancellation request or a
  bounded flow timeout.

The tool must explain that the user needs an active, unlocked desktop session.
It must not expose API keys, local paths, package identifiers, Robot process
keys, gateway details, or raw errors.
