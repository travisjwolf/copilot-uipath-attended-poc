# Copilot Studio Tool Descriptions

## Flow Tool

**Name:** `Run attended desktop demonstration`

**Description:** Use this tool only when the user explicitly asks to run the
approved synthetic UiPath desktop demonstration on their currently available
Windows workstation. It displays a mock message in Notepad and waits for a
completion result. Never use it for real customer, employee, account,
credential, authentication, or production data. Do not claim completion until
the returned status is `Completed`.

## Inputs

- `message`: A short synthetic message to display. Reject or replace content
  that appears to contain sensitive or production information.
- `requestedBy`: A synthetic caller label supplied by the flow or agent context;
  do not use an email address, employee ID, or authentication identifier.

## Connector Actions

- `StartUiPathAttendedJob`: Start the allowlisted process once. The flow, not the
  agent, generates the idempotency key and timestamp.
- `GetUiPathAttendedJobStatus`: Poll a server-generated request ID until a
  terminal state is reached.
- `CancelUiPathAttendedJob`: Use only for an explicit cancellation request or a
  bounded flow timeout.

The tool must explain that the user needs an active, unlocked desktop session.
It must not expose API keys, local paths, package identifiers, Robot process
keys, gateway details, or raw errors.
