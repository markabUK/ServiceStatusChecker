# Service Status Checker

A configurable .NET background service that monitors HTTP endpoints, tracks service health state, and sends notifications when services go down (or recover) — plus a scheduled morning status report.

---

## Overview

`ServiceStatusChecker`:

- Runs scheduled health checks against multiple configured services.
- Supports per-monitor HTTP options (custom headers, response body capture, insecure HTTPS toggle).
- Persists monitor state to disk so health status survives restarts.
- Delivers notifications via:
  - **Email**
  - **Webhooks** (Google Chat, Teams, Slack, Discord, or custom endpoints)
- Sends a scheduled morning report summarising previously known service states.

---

## Architecture

### Core components

| Component | Description |
| :--- | :--- |
| `Program.cs` | Registers configuration, DI services, HTTP clients, and Quartz jobs. |
| `Services/ServiceMonitor.cs` | Executes health checks and triggers notifications. |
| `Jobs/MonitorJob.cs` | Quartz job wrapper for monitor execution. |
| `Services/MorningReportService.cs` | Builds and sends morning reports; manages report state. |
| `Jobs/MorningReportJob.cs` | Quartz job wrapper for morning report execution. |
| `Notifiers/EmailNotifier.cs` | Email notification delivery. |
| `Notifiers/WebhookNotifier.cs` | Webhook notification delivery. |
| `Notifiers/DefaultWebhookBodyFormatter.cs` | Default webhook payload formatter. |
| `Notifiers/GoogleChatWebhookBodyFormatter.cs` | Google Chat-specific webhook formatter. |
| `Services/DefaultMorningReportFormatter.cs` | Default morning report formatter. |
| `Services/GoogleChatMorningReportFormatter.cs` | Google Chat-specific morning report formatter. |
| `State/JsonStateStore.cs` | Service state persistence. |
| `State/MorningReportStateStore.cs` | Morning report sent-date persistence. |
| `Models/` | Configuration and message contracts (`MonitorConfig`, `NotificationConfig`, etc.). |

---

## Requirements

- .NET SDK compatible with the project target framework (see `ServiceStatusChecker/ServiceStatusChecker.csproj`).
- Network access from the runtime host to:
  - All monitored service endpoints.
  - An SMTP server (if email notifications are enabled).
  - Webhook endpoints (if webhook notifications are enabled).

---

## Quick Start

1. Copy and edit `ServiceStatusChecker/appsettings.json`.
2. Add monitor definitions under `Monitors.MonitorConfig`.
3. Configure notification channels under `Notifications`.
4. Run the service.

**Run:**
```bash
cd /home/mbrou/git/ServiceStatusChecker/ServiceStatusChecker
dotnet run
```

**Build:**
```bash
cd /home/mbrou/git/ServiceStatusChecker/ServiceStatusChecker
dotnet build
```

---

## Configuration (`appsettings.json`)

### Full sanitised example

```json
{
  "Monitors": {
    "MonitorConfig": [
      {
        "Name": "Example API - UAT",
        "Url": "https://example.internal/api/health?check=light",
        "Cron": "0 */5 8-17 ? * MON-FRI",
        "Notify": [ "Email", "GoogleChatTest" ],
        "IncludeResponseBody": true,
        "AllowInsecureHttps": false,
        "Headers": {
          "Accept": "application/json",
          "X-Correlation-Source": "ServiceStatusChecker"
        }
      }
    ]
  },
  "MorningReport": {
    "Enabled": true,
    "Cron": "0 0 6 * * ?",
    "Notify": [ "GoogleChatTest" ]
  },
  "Notifications": {
    "Email": {
      "From": "monitor@your-domain.example",
      "To": "alerts@your-domain.example",
      "SmtpServer": "smtp.your-domain.example",
      "Port": 587,
      "UseSsl": true,
      "UserName": "smtp-user",
      "Password": "smtp-password"
    },
    "Webhooks": {
      "GoogleChatTest": {
        "WebhookUrl": "https://chat.googleapis.com/v1/spaces/.../messages?key=REDACTED&token=REDACTED",
        "Formatter": "google-chat"
      },
      "OpsDefault": {
        "WebhookUrl": "https://hooks.example.internal/alerts",
        "Formatter": "default"
      }
    }
  },
  "State": {
    "FilePath": "state.json"
  }
}
```

---

### Monitors

**Path:** `Monitors.MonitorConfig[]`

| Field | Type | Required | Default | Description |
| :--- | :--- | :---: | :--- | :--- |
| `Name` | `string` | ✅ | — | Human-friendly name used in logs and notifications. |
| `Url` | `string` | ✅ | — | Endpoint to poll with an HTTP GET request. |
| `Cron` | `string` | ✅ | — | Quartz cron expression controlling the check schedule. |
| `Notify` | `string[]` | | — | Notification channels to use. Must match a key in `Notifications` (e.g., `"Email"` or a webhook name). |
| `IncludeResponseBody` | `bool` | | `false` | When enabled, includes the raw response body in notification payloads. |
| `AllowInsecureHttps` | `bool` | | `false` | When enabled, bypasses TLS certificate validation for this monitor. |
| `Headers` | `object` | | — | Arbitrary request headers as key/value pairs. |

**Header notes:**
- `Accept` is valid on GET requests and is recommended.
- `Content-Type` on a GET request is typically invalid and will be silently skipped via `TryAddWithoutValidation`.
- Invalid headers are logged as warnings and skipped; the check continues normally.

---

### Notifications

#### Email

**Path:** `Notifications.Email`

| Field | Description |
| :--- | :--- |
| `From` | Sender email address. |
| `To` | Recipient email address. |
| `SmtpServer` | Hostname of the SMTP server. |
| `Port` | SMTP port (e.g., `587`). |
| `UseSsl` | Whether to use TLS/SSL. |
| `UserName` | SMTP authentication username. |
| `Password` | SMTP authentication password. |

Used by `Notifiers/EmailNotifier.cs`.

#### Webhooks

**Path:** `Notifications.Webhooks`

A dictionary where each key is the channel name (e.g., `GoogleChatTest`) and the value contains:

| Field | Required | Description |
| :--- | :---: | :--- |
| `WebhookUrl` | ✅ | The target webhook URL. |
| `Formatter` | | Formatter name to use (e.g., `default`, `google-chat`). Falls back to `default` if omitted. |

Used by `Notifiers/WebhookNotifier.cs`.

---

### Formatters

#### Webhook formatters (`IWebhookBodyFormatter`)

| Name | Implementation |
| :--- | :--- |
| `default` | `Notifiers/DefaultWebhookBodyFormatter.cs` |
| `google-chat` | `Notifiers/GoogleChatWebhookBodyFormatter.cs` |

**Selection logic:**
1. If the webhook config specifies a `Formatter`, resolve it by name.
2. If the name is not found or `Formatter` is omitted, fall back to `default`.

#### Morning report formatters (`IMorningReportFormatter`)

| Name | Implementation |
| :--- | :--- |
| `default` | `Services/DefaultMorningReportFormatter.cs` |
| `google-chat` | `Services/GoogleChatMorningReportFormatter.cs` |

Morning report channel formatting follows the same selection and fallback logic as webhook formatters.

---

### Morning Report

**Path:** `MorningReport`

| Field | Type | Description |
| :--- | :--- | :--- |
| `Enabled` | `bool` | Enables or disables the morning report workflow. |
| `Cron` | `string` | Quartz cron expression for scheduled report delivery. |
| `Notify` | `string[]` | Notification channels to use for the morning report. |

**Behaviour notes:**
- Tracks the last sent report date in `morning_report_state.json`.
- Can send a report shortly after startup or on day rollover based on stored state and current date logic in `Services/MorningReportService.cs`.

---

### Scheduling (Quartz cron)

Cron expressions follow Quartz format, which supports `?` and day-of-week names.

| Example | Expression |
| :--- | :--- |
| Every 5 minutes, weekdays, 08:00–17:00 | `0 */5 8-17 ? * MON-FRI` |
| Every day at 06:00 | `0 0 6 * * ?` |

---

## Runtime Behaviour

1. Each scheduled monitor executes an HTTP GET against its configured URL.
2. Retries are applied according to the retry policy configured in `ServiceMonitor`.
3. Log entries include monitor context during health checks and retry attempts.
4. Notifications are sent via all configured channels on state change.
5. State transitions are persisted to `state.json`.
6. Morning reports summarise all persisted monitor states.

---

## Security Recommendations

- **Do not commit** real webhook URLs, SMTP credentials, or tokens to source control.
- Move secrets to environment variables or a dedicated secrets store in production.
- Keep `AllowInsecureHttps` set to `false` in production unless absolutely necessary.
- If insecure HTTPS is required in non-production environments, scope it to specific monitors only — never globally.
- Avoid enabling `IncludeResponseBody` for endpoints that may return sensitive data.

---

## Troubleshooting

**`Skipping invalid header 'X' for monitor Y`**
The header is not valid in the context of a GET request (e.g., `Content-Type`). The check still runs normally.

**Webhook sends failing**
Verify that `WebhookUrl` is correctly formatted and that the host has outbound connectivity to the target. Confirm the `Formatter` value matches a registered name (`default`, `google-chat`, etc.).

**No morning report sent**
Check that `MorningReport.Enabled` is `true`, the `Cron` expression is valid, and `MorningReport.Notify` contains at least one channel. Also verify write permissions on `morning_report_state.json`.

**State not retained after restart**
Confirm `State.FilePath` points to a writable, persistent location on disk.

---

## Custom JavaScript Formatters

ServiceStatusChecker supports dynamic webhook formatting via JavaScript using the **Jint** engine. This lets you craft exact payloads for any provider — Slack, Teams, Discord, and more — without recompiling the application.

### Getting started

1. Create a `/Scripts` directory in the application root.
2. Add a `.js` file named after your notification channel (e.g., `slack.js` for the `slack` channel).
3. Implement a `format(context)` function that returns a serialised JSON string.

### The `context` object

| Property | Type | Description |
| :--- | :--- | :--- |
| `ServiceName` | `string` | Monitor name as defined in configuration. |
| `Url` | `string` | The monitored endpoint URL. |
| `IsUp` | `bool` | `true` if the service is currently healthy. |
| `StatusCode` | `string` | HTTP status code and reason phrase (e.g., `"200 - OK"`). |
| `Error` | `string` | Exception message if the check failed. |
| `ResponseBody` | `string` | Raw response body (requires `IncludeResponseBody: true`). |
| `Timestamp` | `string` | UTC timestamp of the check event. |

### Example: Slack Block Kit formatter (`Scripts/slack.js`)

```javascript
/**
 * Custom Webhook Formatter for Slack
 * @param {object} context - The NotificationContext object
 * @returns {string} - Serialised JSON payload for the Slack webhook
 */
function format(context) {
  const statusEmoji = context.IsUp ? "✅" : "🚨";
  const statusText  = context.IsUp ? "SERVICE RESTORED" : "SERVICE DOWN";
  const color       = context.IsUp ? "#2eb886" : "#a30200";

  const payload = {
    attachments: [
      {
        color,
        blocks: [
          {
            type: "header",
            text: {
              type: "plain_text",
              text: `${statusEmoji} ${statusText}: ${context.ServiceName}`
            }
          },
          {
            type: "section",
            fields: [
              { type: "mrkdwn", text: `*URL:*\n${context.Url}` },
              { type: "mrkdwn", text: `*Timestamp:*\n${context.Timestamp}` }
            ]
          }
        ]
      }
    ]
  };

  if (!context.IsUp) {
    payload.attachments[0].blocks.push({
      type: "section",
      text: {
        type: "mrkdwn",
        text: `*Details:* \`${context.StatusCode || "N/A"}\`\n> ${context.Error || "No specific error captured."}`
      }
    });
  }

  return JSON.stringify(payload);
}
```

---

## Morning Report Scripts

A script file ending in `-morning.js` (e.g., `slack-morning.js`) is automatically registered as a **Morning Report Formatter**, letting you fully customise the daily summary layout per provider.

### The `context` object

#### `MorningReportMessageContext` (root)

| Property | Type | Description |
| :--- | :--- | :--- |
| `ReportDate` | `string` | Report date in ISO format (`yyyy-MM-dd`). |
| `GeneratedAtUtc` | `string` | UTC timestamp of report generation. |
| `Monitors` | `array` | List of `MorningReportMonitorSnapshot` objects. |

#### `MorningReportMonitorSnapshot` (per item in `Monitors`)

| Property | Description |
| :--- | :--- |
| `Name` | Friendly service name from configuration. |
| `Url` | Monitored endpoint URL. |
| `State` | Current status: `"Up"`, `"Down"`, or `"Unknown"`. |

### Tips

- **Aggregate totals:** Use `.filter()` or `.reduce()` on `context.Monitors` to produce summary counts (e.g., *"9 of 10 services operational"*).
- **Contextual tone:** Parse `GeneratedAtUtc` to detect the day of the week and vary your greeting accordingly.
- **Payload validity:** Always return a valid JSON string that satisfies the schema requirements of your target provider.

### Example: Slack morning report formatter (`Scripts/slack-morning.js`)

```javascript
/**
 * Custom Morning Report Formatter for Slack
 * @param {object} context - The MorningReportMessageContext object
 * @returns {string} - Serialised JSON payload
 */
function format(context) {
  const total     = context.Monitors.length;
  const upCount   = context.Monitors.filter(m => m.State === "Up").length;
  const downCount = total - upCount;

  const statusEmoji = downCount === 0 ? "✅" : "⚠️";

  let message = `${statusEmoji} *Daily Status Report — ${context.ReportDate}*\n`;
  message    += `Total: ${total} | Up: ${upCount} | Down: ${downCount}\n\n`;
  message    += `*Current Status:*\n`;

  context.Monitors.forEach(monitor => {
    const indicator = monitor.State === "Up" ? "🟢" : "🔴";
    message += `${indicator} *${monitor.Name}* (${monitor.Url})\n`;
  });

  message += downCount > 0
    ? "\n❗ *Action Required:* One or more services are currently offline."
    : "\n✨ All systems operational.";

  return JSON.stringify({ text: message });
}
```

---

## File References

- `ServiceStatusChecker/Program.cs`
- `ServiceStatusChecker/Services/ServiceMonitor.cs`
- `ServiceStatusChecker/Services/MorningReportService.cs`
- `ServiceStatusChecker/Notifiers/WebhookNotifier.cs`
- `ServiceStatusChecker/Notifiers/DefaultWebhookBodyFormatter.cs`
- `ServiceStatusChecker/Notifiers/GoogleChatWebhookBodyFormatter.cs`
- `ServiceStatusChecker/Services/DefaultMorningReportFormatter.cs`
- `ServiceStatusChecker/Services/GoogleChatMorningReportFormatter.cs`
- `ServiceStatusChecker/State/JsonStateStore.cs`
- `ServiceStatusChecker/State/MorningReportStateStore.cs`
- `ServiceStatusChecker/Models/MonitorConfig.cs`
- `ServiceStatusChecker/Models/NotificationConfig.cs`
- `ServiceStatusChecker/Models/WebhookConfig.cs`