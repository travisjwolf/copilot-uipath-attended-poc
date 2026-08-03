# Copilot Studio Agent-Flow Setup

## Import The Connector

1. In the target Power Platform environment, create or open an unmanaged
   solution.
2. Import `custom-connector.swagger.json` as a custom connector named
   `Copilot UiPath Attended Bridge`.
3. Review API-key authentication, create a gateway-backed connection, and test
   with synthetic values. Do not paste the API key into a flow action or agent
   instruction.

## Build The Agent Flow

Create an agent flow with **When an agent calls the flow** and **Respond to the
agent**. Accept only `message` and `requestedBy` string inputs.

1. Add a Compose action named `IdempotencyKey` with expression `guid()`.
2. Add a Compose action named `RequestTimestamp` with expression `utcNow()`.
3. Call `StartUiPathAttendedJob`. Set the alias to `CopilotAttendedPoC`, map the
   two synthetic inputs, and use the Compose outputs for both headers.
4. Save the returned `requestId`; never accept a request ID from the agent.
5. Poll `GetUiPathAttendedJobStatus` with a short Delay inside a bounded Do Until
   loop. Stop on `Completed`, `Failed`, or `Cancelled`, and enforce a flow timeout.
6. Optionally call `CancelUiPathAttendedJob` when the user explicitly cancels or
   the bounded wait expires.
7. Respond to the agent with `requestId`, `status`, `out_Result`, and a safe
   display message. Do not return bridge exception details.

Disable connector automatic retries for non-idempotent starts only if the flow
cannot guarantee reuse of the composed idempotency key. A retry must reuse the
same key and request body.

## Add The Tool

Publish the flow, then add it from the agent's **Tools** page. Use the tool name
and description in `tool-descriptions.md`. Configure both inputs explicitly and
test Accepted, Running, Completed, Failed, cancellation, gateway unavailable,
and desktop unavailable paths before publishing the agent.

Reference: [call an agent flow from an agent](https://learn.microsoft.com/en-us/microsoft-copilot-studio/advanced-use-flow).
