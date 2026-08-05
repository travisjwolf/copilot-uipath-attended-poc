# Mock Agent Responses

Use synthetic identifiers in demonstrations. `DesktopUnavailable` is a
flow-level presentation state for an unreachable gateway, stopped Assistant,
locked/disconnected desktop, or equivalent launch failure; it is not currently
a bridge job-status enum.

| State | Conversational response |
| --- | --- |
| Accepted | "Your attended desktop demonstration was accepted. Request ID: `{requestId}`." |
| Running | "The synthetic UiPath demonstration is running in the active desktop session." |
| Completed | "The desktop demonstration completed: `{out_Result}`" |
| Failed | "The desktop demonstration could not complete. No sensitive input was logged. Request ID: `{requestId}`." |
| Cancelled | "The desktop demonstration was cancelled. Request ID: `{requestId}`." |
| TimedOut | "The desktop demonstration is still not complete after the 12-minute monitoring window. Request ID: `{requestId}`." |
| DesktopUnavailable | "The attended desktop is unavailable. Unlock the workstation, confirm UiPath Assistant and the local bridge are running, then try again." |

## Adaptive Card Template

Populate placeholders in the flow only with safe bridge outputs:

```json
{
  "type": "AdaptiveCard",
  "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
  "version": "1.5",
  "body": [
    {
      "type": "TextBlock",
      "weight": "Bolder",
      "size": "Medium",
      "text": "UiPath attended demonstration"
    },
    {
      "type": "FactSet",
      "facts": [
        {
          "title": "Status",
          "value": "${status}"
        },
        {
          "title": "Request ID",
          "value": "${requestId}"
        }
      ]
    },
    {
      "type": "TextBlock",
      "wrap": true,
      "text": "${message}"
    }
  ]
}
```

Do not include submitted input values, local file paths, exception details, API
keys, gateway names, or UiPath tenant identifiers in cards or chat responses.
