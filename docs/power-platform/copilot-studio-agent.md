# Copilot Studio Agent

## Current Agent

`UiPath Attended PoC Agent` was created privately in Microsoft 365 Agent
Builder on August 5, 2026. It is dedicated to the synthetic attended automation
POC and is not shared or published.

The agent configuration:

- Starts automation only after an explicit user request.
- Prohibits customer, employee, account, credential, authentication, and
  production data.
- Treats `Accepted` as queued, not completed, and treats `requestId` as opaque.
- Checks status no more than every 15 seconds, capped at 48 checks or 12 minutes.
- Reports only safe status and result text and never exposes infrastructure or
  raw error details.
- Disables document, code, chart, image, and open-web capabilities.

Suggested prompts cover starting the synthetic demonstration, checking its
status, and explaining desktop prerequisites.

## Full Studio Blocker

The Microsoft 365 builder does not expose Power Automate actions. Its **Copy to
Copilot Studio** environment picker remains at **Loading environments**, the
standalone Copilot Studio authoring page renders blank, and the POC solution
still shows `Agents (0)`. Power Apps also reports that some commands are
unavailable for the current environment privileges.

An environment administrator must assign the current maker the **agent author**
security role in environment
`Default-d8353d2a-b153-4d17-8827-902c51f72357`. Microsoft documents this role
as required to create an agent in an existing environment:
[environment access guidance](https://learn.microsoft.com/en-us/microsoft-copilot-studio/environments-first-run-experience#create-an-agent-in-an-existing-environment-where-you-dont-have-access).

Also confirm that workstation/network policy permits HTTPS access to
`copilotstudio.microsoft.com`; the current connection closes before the
authoring application loads.

## Resume Steps

1. Sign out and back in after the role assignment, then open Copilot Studio.
2. Select `UiPath (default) (Upgrade)` and copy the private agent into it.
3. Add the copied agent to the `Copilot UiPath Attended PoC` solution.
4. Add `Run UiPath Attended PoC` and `Check UiPath Attended Job Status` as
   separate tools using `tool-descriptions.md`.
5. Test `Accepted`, `Running`, `Completed`, cancellation, timeout, and desktop
   unavailable responses with synthetic data before publishing.
