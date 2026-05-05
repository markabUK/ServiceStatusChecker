# Service Status Checker

A configurable .NET background service that monitors HTTP endpoints, tracks service state, and sends notifications when services are unhealthy (or recover), plus a scheduled morning status report.

## What this project does

`ServiceStatusChecker`:

- Runs scheduled health checks for multiple services.
- Supports per-monitor HTTP options (headers, response body capture, insecure HTTPS toggle).
- Persists monitor state to disk so state changes survive restarts.
- Sends notifications through:
    - Email
    - Webhooks (e.g., Google Chat, Teams, Slack, Discord, or custom endpoints)
- Sends a scheduled morning report summarizing previous known states.

---

## High-level architecture

Core components:

- `Program.cs`
    - Registers configuration, DI services, HTTP clients, Quartz jobs.
- `Services/ServiceMonitor.cs`
    - Executes health checks and triggers notifications.
- `Jobs/MonitorJob.cs`
    - Quartz job wrapper for monitor execution.
- `Services/MorningReportService.cs`
    - Builds/sends morning report and manages morning-report state.
- `Jobs/MorningReportJob.cs`
    - Quartz job wrapper for morning report execution.
- `Notifiers/`
    - `EmailNotifier.cs`
    - `WebhookNotifier.cs`
    - Formatter implementations (`DefaultWebhookBodyFormatter`, `GoogleChatWebhookBodyFormatter`)
- `Services/`
    - Morning report formatter implementations (`DefaultMorningReportFormatter`, `GoogleChatMorningReportFormatter`)
- `State/`
    - `JsonStateStore.cs` (service state persistence)
    - `MorningReportStateStore.cs` (morning report sent-date persistence)
- `Models/`
    - Config and message contracts (`MonitorConfig`, `NotificationConfig`, etc.)

---

## Requirements

- .NET SDK compatible with the project target framework (see `ServiceStatusChecker/ServiceStatusChecker.csproj`).
- Network access from the runtime host to:
    - Monitored services
    - SMTP server (if email is enabled)
    - Webhook endpoints (if webhooks are enabled)

---

## Quick start

1. Copy and edit `ServiceStatusChecker/appsettings.json`.
2. Add monitor definitions under `Monitors.MonitorConfig`.
3. Configure notification channels under `Notifications`.
4. Run the service.

### Run (example)

```bash
cd /home/mbrou/git/ServiceStatusChecker/ServiceStatusChecker
dotnet run
```

### Build (example)
``` bash
cd /home/mbrou/git/ServiceStatusChecker/ServiceStatusChecker
dotnet build
```

## Configuration (appsettings.json)

### Full sanitized example
``` json
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

### Monitors section

Path: Monitors.MonitorConfig[]
Each monitor object:
- Name (string, required) <br/>
  Human-friendly monitor name used in logs and notifications.
- Url (string, required) <br/>
  Endpoint to call with HTTP GET.
- Cron (string, required) <br/>
  Quartz cron expression controlling schedule.
- Notify (string[], optional) <br/>
  Channels to notify (must match notifier handles, e.g., "Email" or webhook key names).
- IncludeResponseBody (bool, optional, default false) <br/>
  Whether response body should be included in notification payloads.
- AllowInsecureHttps (bool, optional, default false) <br/>
  If true, uses an HTTP client that bypasses certificate validation.
- Headers (object, optional) <br/>
  Arbitrary request headers as key/value pairs.

#### Header notes
Define custom headers in Headers.
Accept is valid for GET and recommended.
Content-Type on a GET request is often invalid as a request header and may be skipped by TryAddWithoutValidation.
If a header is invalid, the service logs a warning and continues.

### Notifications section
Email
Path: Notifications.Email
Fields:
- From
- To
- SmtpServer
- Port
- UseSsl
- UserName
- Password

Used by Notifiers/EmailNotifier.cs.
Webhooks
Path: Notifications.Webhooks
A dictionary where each key is a channel name (e.g., GoogleChatTest) and value is:
- WebhookUrl (string, required)
- Formatter (string, optional)

Used by Notifiers/WebhookNotifier.cs.
If Formatter is omitted, the notifier falls back to the default formatter.

### Formatter behavior
Webhook formatters (IWebhookBodyFormatter)
Registered formatters include:
- default → Notifiers/DefaultWebhookBodyFormatter.cs
- google-chat → Notifiers/GoogleChatWebhookBodyFormatter.cs

- Selection logic:
1. If webhook config has Formatter, resolve by name.
2. If not found or omitted, use default.

This enables per-webhook formatting (for example, short link labels in Google Chat).
Morning report formatters (IMorningReportFormatter)

Registered formatters include:
- default → Services/DefaultMorningReportFormatter.cs
- google-chat → Services/GoogleChatMorningReportFormatter.cs

Morning report channel formatting follows similar formatter-name selection/fallback behavior.

### MorningReport section
Path: MorningReport
Fields:
- Enabled (bool) <br/>
  Enables/disables morning report workflow.
- Cron (string) <br/>
  Quartz cron expression for scheduled report.
- Notify (string[]) <br/>
  Channels used for morning report delivery.

Behavior notes:
- Tracks last sent report date in morning_report_state.json.
- Can send report after startup/day rollover when needed based on stored report state and current date logic in Services/MorningReportService.cs.

### Scheduling notes (Quartz cron)
Cron expressions in this project follow Quartz format (supports ? and day-of-week names).
Examples:
- Every 5 minutes, weekdays during working hours:<br/>
  0 */5 8-17 ? * MON-FRI
- Every day at 06:00:<br/>
  0 0 6 * * ?

## Runtime behavior summary
- Each scheduled monitor executes an HTTP GET.
- Retries are applied according to the configured policy in ServiceMonitor.
- Logs identify monitor context during health checks/retries.
- Notifications are sent through configured channels.
- State transitions are persisted in state.json.
- Morning reports summarize persisted monitor states.

## Security recommendations
- Do not commit real WebhookUrl, SMTP credentials, or tokens.
- Move secrets to environment variables or secret stores for production.
- Keep AllowInsecureHttps set to false in production unless absolutely required.
- If you must use insecure HTTPS in non-prod, scope it to specific monitors only.
- Avoid enabling IncludeResponseBody for endpoints that may return sensitive data.

## Troubleshooting
- Skipping invalid header 'X' for monitor Y
  - Header is not valid in the context of the request.
  - Common case: Content-Type on GET.
- Webhook sends fail
  - Verify WebhookUrl format and outbound connectivity.
  - Confirm formatter name exists (default, google-chat, etc.).
- No morning report sent
  - Check MorningReport.Enabled, MorningReport.Cron, and MorningReport.Notify.
  - Check write permissions for morning_report_state.json.
- State not retained after restart
  - Confirm State.FilePath is writable and persistent on disk.

## File references

- ServiceStatusChecker/Program.cs
- ServiceStatusChecker/Services/ServiceMonitor.cs
- ServiceStatusChecker/Services/MorningReportService.cs
- ServiceStatusChecker/Notifiers/WebhookNotifier.cs
- ServiceStatusChecker/Notifiers/DefaultWebhookBodyFormatter.cs
- ServiceStatusChecker/Notifiers/GoogleChatWebhookBodyFormatter.cs
- ServiceStatusChecker/Services/DefaultMorningReportFormatter.cs
- ServiceStatusChecker/Services/GoogleChatMorningReportFormatter.cs
- ServiceStatusChecker/State/JsonStateStore.cs
- ServiceStatusChecker/State/MorningReportStateStore.cs
- ServiceStatusChecker/Models/MonitorConfig.cs
- ServiceStatusChecker/Models/NotificationConfig.cs
- ServiceStatusChecker/Models/WebhookConfig.cs