# Copilot Studio Agent-Flow Setup

## Import The Connector

1. In the target Power Platform environment, create or open an unmanaged
   solution.
2. Import `custom-connector.swagger.json` as a custom connector named
   `Copilot UiPath Attended Bridge`.
3. Create a gateway-backed Basic authentication connection. Use the fixed
   username `CopilotGateway` and the bridge API key as the password; do not paste
   the key into a flow action or agent instruction.

## Build The Start Flow

Create `Run UiPath Attended PoC` with **When an agent calls the flow** and
**Respond to the agent**. Accept only `message` and `requestedBy` strings.

1. Compose `guid()` as `IdempotencyKey` and `utcNow()` as `RequestTimestamp`.
2. Call `StartUiPathAttendedJob` for alias `CopilotAttendedPoC` with the two
   synthetic inputs and both composed headers.
3. Immediately return `requestId`, `status`, and `message`. Do not poll or wait
   for desktop completion in this flow.

Generate the idempotency key once per invocation and reuse it with the same body
for every retry. Never accept a request ID from agent text.

## Build The Status Flow

Create `Check UiPath Attended Job Status` with one `requestId` string input.
Call `GetUiPathAttendedJobStatus`, then return `requestId`, `status`,
`outResult`, and `error`. Treat the request ID as an opaque value originally
returned by the start flow. Do not return bridge exception details.

Use this channel-independent polling policy:

- Query at most once every 15 seconds.
- Stop on `Completed`, `Failed`, or `Cancelled`.
- Stop after 48 checks or 12 minutes and present a safe timeout message.
- Call `CancelUiPathAttendedJob` only for an explicit user cancellation or a
  policy decision to cancel after the bounded wait.
- Do not keep one gateway HTTP request open for the attended job runtime.

For conversational channels, return `Accepted` immediately and let the agent
call the status tool on a later turn or from a bounded coordinator topic. If
automatic completion notifications are required, Copilot Studio asynchronous
flow responses may be used only after validating the selected channel and the
new agent infrastructure. They are supported in Teams but not Microsoft 365
Copilot or telephony channels.

The bridge permits 90 seconds of clock skew for Power Automate timestamps.
Keep the gateway workstation time synchronized; verify drift with `w32tm
/stripchart /computer:time.windows.com /dataonly /samples:3` before increasing
this tolerance.

Validated on August 5, 2026: the start flow returned `Accepted` in three seconds.
Status-flow run `08584156819349106950074449764CU245` returned `Running` for a
delayed fake job, and run `08584156822002924605222324752CU150` returned
`Completed`. A separate real RobotAPI run also returned the expected synthetic
`out_Result`.

## Add The Tool

Publish both flows, then add them from the agent's **Tools** page. Use the names
and descriptions in `tool-descriptions.md`. Test Accepted, Running, Completed,
Failed, cancellation, bounded timeout, gateway unavailable, and desktop
unavailable paths before publishing the agent.

References: [call an agent flow from an agent](https://learn.microsoft.com/en-us/microsoft-copilot-studio/advanced-use-flow)
and [asynchronous flow responses](https://learn.microsoft.com/en-us/microsoft-copilot-studio/flow-asynchronous-response).
