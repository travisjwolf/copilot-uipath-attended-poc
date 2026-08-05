# On-Premises Data Gateway Setup

## Safety Boundary

Use the standard on-premises data gateway, not personal mode. Installation,
service-account changes, firewall changes, and Power Platform authentication
require administrator approval and are not performed by this repository. Do not
create a public tunnel or change the bridge from its default
`http://127.0.0.1:5211` binding.

The gateway must run on this workstation for the proof of concept. Because the
connector host is `127.0.0.1`, installing it elsewhere would address that other
gateway host instead of the Windows session running UiPath Assistant.

## Prerequisites

- The bridge and UiPath Assistant run under the currently logged-in attended
  user.
- `GET http://127.0.0.1:5211/health` succeeds locally.
- A gateway administrator has installed and registered a standard-mode gateway
  in the same Power Platform environment and region as the flow.
- The API key is stored in `COPILOT_UIPATH_BRIDGE_API_KEY` for the bridge and as
  the password in the custom connector connection. Never place it in Swagger or
  source control.

## Connector Connection

1. Import `custom-connector.swagger.json` into an unmanaged solution in Power
   Apps or Power Automate.
2. Confirm the connector uses HTTP, host `127.0.0.1:5211`, base URL `/`, the
   on-premises data gateway option, and Basic authentication.
3. Create a connector connection, select the registered on-premises gateway,
   enter `CopilotGateway` as the username, and use the local bridge API key as
   the password.
4. Start the real bridge with `scripts/start-real-bridge.ps1` from a shell that
   contains `COPILOT_UIPATH_BRIDGE_API_KEY`.
5. Test `StartUiPathAttendedJob` using synthetic data only. Do not save test
   payloads containing real customer, employee, account, or authentication data.

The gateway uses outbound cloud connectivity; do not open inbound firewall
ports for the bridge. Keep the gateway current and restrict gateway
administration and connector sharing to PoC participants.

Power Platform does not support API-key authentication for custom connectors
using an on-premises gateway. The bridge therefore accepts the same secret by
either the local `X-Api-Key` header or a gateway-only Basic credential. The
password is still compared in constant time and is never logged.

## Validated PoC Resources

On August 4, 2026, the `Copilot UiPath Attended Bridge` connector was added to
the unmanaged `Copilot UiPath Attended PoC` solution. Its connection used
`CopilotUiPathAttendedPoc-Gateway-01` in North Europe. Synthetic request
`829ab915-b780-4cda-88b8-4f10d30beac5` completed and returned `out_Result`
through the full gateway-to-RobotAPI path.

References: [manage gateways](https://learn.microsoft.com/en-us/power-automate/gateway-manage),
[gateway architecture](https://learn.microsoft.com/en-us/power-platform/admin/wp-onpremises-gateway),
[custom connector authentication limitations](https://learn.microsoft.com/en-us/connectors/custom-connectors/faq),
and [import an OpenAPI connector](https://learn.microsoft.com/en-us/connectors/custom-connectors/define-openapi-definition).
