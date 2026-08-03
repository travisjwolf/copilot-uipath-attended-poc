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
- The API key is stored in `COPILOT_UIPATH_BRIDGE_API_KEY` for the bridge and in
  the custom connector connection. Never place it in Swagger or source control.

## Connector Connection

1. Import `custom-connector.swagger.json` into an unmanaged solution in Power
   Apps or Power Automate.
2. Confirm the connector uses HTTP, host `127.0.0.1:5211`, base URL `/`, and
   header API-key authentication named `X-Api-Key`.
3. Create a connector connection, select the registered on-premises gateway,
   and enter the local bridge API key when prompted.
4. Start the bridge with `scripts/start-bridge.ps1`. For the real client, set
   `UiPath__UseFakeRobotClient=false` in that process environment.
5. Test `StartUiPathAttendedJob` using synthetic data only. Do not save test
   payloads containing real customer, employee, account, or authentication data.

The gateway uses outbound cloud connectivity; do not open inbound firewall
ports for the bridge. Keep the gateway current and restrict gateway
administration and connector sharing to PoC participants.

References: [manage gateways](https://learn.microsoft.com/en-us/power-automate/gateway-manage),
[gateway architecture](https://learn.microsoft.com/en-us/power-platform/admin/wp-onpremises-gateway),
and [import an OpenAPI connector](https://learn.microsoft.com/en-us/connectors/custom-connectors/define-openapi-definition).
