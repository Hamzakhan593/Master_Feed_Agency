# Master Feed Agency — WhatsApp Setup

The code is already wired for the official WhatsApp Cloud API. The normal sales/payment system keeps working even while WhatsApp is disabled.

## 1) Edit this file

`Master_Feed_Agency/whatsappsettings.json`

Fill:

- `Enabled`: `true`
- `PhoneNumberId`: WhatsApp Cloud API phone-number ID
- `AccessToken`: API access token
- `ApiVersion`: use the Graph API version shown in your Meta setup
- `OwnerPhone`: owner WhatsApp number, e.g. `923001234567` or `03001234567`

Keep this file private. The patch adds it to `.gitignore` so credentials are not committed.

## 2) Create/approve these three templates

The names can be changed in `whatsappsettings.json`, but the parameter order must match.

### `owner_daily_report` — 7 body variables

Suggested body:

Master Feed Agency - {{1}}
Bills: {{2}}
Aaj ki total sale: Rs. {{3}}
Counter par wasool: Rs. {{4}}
Aaj naya udhaar: Rs. {{5}}
Customer wasooli: Rs. {{6}}
Total customer baqaya: Rs. {{7}}

### `customer_due_reminder` — 4 body variables

Suggested body:

Assalam-o-Alaikum {{1}}, aap ki Rs. {{2}} payment {{3}} ko due hai. Ref: {{4}}. Barah-e-karam waqt par payment kar dein. - Master Feed Agency

### `payment_received` — 4 body variables

Suggested body:

Assalam-o-Alaikum {{1}}, aap ki Rs. {{2}} payment receive ho gayi. Receipt: {{3}}. {{4}} - Master Feed Agency

The fourth payment variable automatically becomes either remaining balance text or “Aap ka khata clear ho gaya hai.”

## 3) Default schedules

- Owner daily business brief: 10:00 PM Pakistan time (`DailyOwnerReportHour: 22`)
- Customer due reminder: 2 days before due date, checked after 10:00 AM (`DueReminderDaysBefore: 2`, `DueReminderHour: 10`)
- Automatic worker checks every 5 minutes and duplicate messages are blocked by a unique message key.

The application/server must be running for scheduled messages to send.
