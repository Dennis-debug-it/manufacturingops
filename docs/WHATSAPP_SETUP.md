# WhatsApp reminders: getting the shared Ecosys number live

Release 3 step 3d sends WhatsApp template messages from **one WhatsApp Business number owned by Ecosys**
(design decision R5). Tenants never configure WhatsApp themselves. This is the checklist for Ecosys to
take the number live. Until every step is done the feature is safe to deploy: with no number configured,
queued messages are marked "Not sent" in each tenant's log and email carries on as before.

Meta's business verification and template review take days and are outside our control. Start steps 1–4
early.

## 1. Meta Business verification

1. Create (or use) the **Ecosys Meta Business portfolio** at <https://business.facebook.com>.
2. Business settings → Security centre → **Start verification**. Upload the company registration
   certificate and a utility bill or bank letter showing the same legal name and address.
3. Wait for approval (usually 2–5 working days). Without it the number is limited to 250 conversations a day.

## 2. WhatsApp Business account and app

1. At <https://developers.facebook.com> create an app of type **Business**, linked to the Ecosys portfolio.
2. Add the **WhatsApp** product. This creates a WhatsApp Business account (WABA).
3. Note the **WhatsApp Business Account ID** (WhatsApp → API setup).
4. App settings → Basic: copy the **App secret**. It signs every webhook call.

## 3. Phone number

1. WhatsApp → API setup → **Add phone number**. Use a number that is not registered on the WhatsApp or
   WhatsApp Business app (or delete it from the app first).
2. Display name: **Ecosys**. Meta reviews the display name against the business name.
3. Verify the number by SMS or voice call.
4. Note the **Phone number ID** (not the phone number itself).
5. Set the two-step verification PIN and keep it in the password manager.

## 4. Permanent access token

The temporary token in API setup expires after 24 hours. Create a permanent one:

1. Business settings → Users → **System users** → Add, role **Admin**, name e.g. `ecosys-whatsapp`.
2. **Add assets**: the app (full control) and the WhatsApp Business account (full control).
3. **Generate new token** for the app, expiry **Never**, permissions `whatsapp_business_messaging` and
   `whatsapp_business_management`.
4. Store the token in the password manager. It is shown only once.

## 5. Message templates

Submit each template in WhatsApp Manager → **Message templates** → Create. Language **English (en)**.
Use the names exactly as below; Ecosys sends them by name. `{{1}}` is always the tenant's company name,
so every message says who it is from. Add a sample value for each variable when Meta asks.

The texts below are also in code: `WhatsAppTemplateCatalogue.cs`. Keep the two the same; if Meta asks for
a wording change, change both.

### Staff reminders (category: Utility)

| Name | Notification key | Body | Footer |
|---|---|---|---|
| `checks_missed` | `factory-check.missed` | `*{{1}}*: running checks in {{2}} were not done in shift {{3}}. Please see Ecosys.` | `Ecosys reminders. Reply STOP to stop.` |
| `meeting_reminder` | `activity.reminder` | `*{{1}}*: reminder, "{{2}}" starts at {{3}}. Location: {{4}}.` | `Ecosys reminders. Reply STOP to stop.` |
| `action_item_due` | `action-item.due` | `*{{1}}*: your action item "{{2}}" is due on {{3}}.` | `Ecosys reminders. Reply STOP to stop.` |
| `work_order_assigned` | `work-order.assigned` | `*{{1}}*: work order {{2}} "{{3}}" ({{4}} priority) has been assigned to you. Open the Ecosys technician app for details.` | `Ecosys reminders. Reply STOP to stop.` |

### Client updates (category: Utility)

| Name | Key | Body | Footer |
|---|---|---|---|
| `technician_on_the_way` | `technician-on-the-way` | `*{{1}}*: {{2}} is on the way for job {{3}}. Track the visit here: {{4}}` | `Sent by Ecosys for the business named above. Reply STOP to stop these messages.` |
| `work_order_completed` | `work-order-completed` | `*{{1}}*: job {{2}} has been completed by {{3}}. Details: {{4}}` | same |

### Number confirmation (category: Authentication)

| Name | Settings |
|---|---|
| `ecosys_verification_code` | Code delivery: **Copy code** button. Tick "Add security recommendation". Code expiration: **10 minutes**. Meta fixes the body text ("{{1}} is your verification code. For your security, do not share this code."). |

Ecosys sends the code as the body parameter and again as the button parameter, as Meta requires for
copy-code templates.

Templates are usually approved within minutes to a day. A rejected template shows the reason in
WhatsApp Manager; fix the wording in both places and resubmit.

## 6. Webhook

1. Pick a long random **verify token** (e.g. `openssl rand -hex 24`).
2. In the app: WhatsApp → Configuration → Webhook → **Edit**.
   - Callback URL: `https://<your API host>/api/whatsapp/webhook`
   - Verify token: the value from step 1.
   Meta calls `GET` with `hub.mode=subscribe`; the API echoes `hub.challenge` only when the token matches.
3. **Subscribe** to the `messages` field. That one field carries delivery statuses (sent, delivered, read,
   failed) and replies.
4. Every `POST` is checked against `X-Hub-Signature-256` (HMAC-SHA256 of the raw body with the app
   secret). A call without a valid signature gets 401 and changes nothing.

Replies: **STOP** (also STOP ALL, UNSUBSCRIBE) blocks that phone number for every tenant; **START** (or
UNSTOP) lifts it. Confirming the number again in Ecosys with a code also lifts it. Any other reply is
logged on the platform side only (masked number, no content) and never shown to a tenant, because one
number serves every tenant.

## 7. Environment variables

Set these on the API (docker-compose maps the `ECOSYS_WHATSAPP_*` names in `.env` to them):

| Variable | `.env` name | Value |
|---|---|---|
| `WhatsApp__Enabled` | `ECOSYS_WHATSAPP_ENABLED` | `true` |
| `WhatsApp__PhoneNumberId` | `ECOSYS_WHATSAPP_PHONE_NUMBER_ID` | the phone number ID (step 3) |
| `WhatsApp__BusinessAccountId` | `ECOSYS_WHATSAPP_BUSINESS_ACCOUNT_ID` | the WABA ID (step 2), for reference |
| `WhatsApp__AccessToken` | `ECOSYS_WHATSAPP_ACCESS_TOKEN` | the permanent system-user token (step 4) |
| `WhatsApp__AppSecret` | `ECOSYS_WHATSAPP_APP_SECRET` | the app secret (step 2) |
| `WhatsApp__WebhookVerifyToken` | `ECOSYS_WHATSAPP_WEBHOOK_VERIFY_TOKEN` | the verify token (step 6) |
| `WhatsApp__DisplayPhoneNumber` | `ECOSYS_WHATSAPP_DISPLAY_NUMBER` | the number as people see it, e.g. `+254 700 000 000` |
| `WhatsApp__MonthlyCapPerTenant` | `ECOSYS_WHATSAPP_MONTHLY_CAP` | messages per tenant per month, default `1000` |
| `WhatsApp__GraphApiVersion` | — | default `v21.0`; raise when Meta retires it |

These are platform secrets. They are never stored in the database or shown to tenants.

## 8. Turn it on for a tenant

WhatsApp is a tenant feature flag on top of the plan (design section 6), off by default because Ecosys pays
Meta per message:

1. Platform → Tenants → the tenant → **Users** tab → **WhatsApp reminders: On**
   (`PUT /api/platform/whatsapp/tenants/{tenantId}`).
2. The tenant admin chooses which notifications may use WhatsApp under **Settings → WhatsApp Reminders**.
   Defaults: running checks missed, work order assigned, meeting reminder, technician on the way.
3. Staff opt in under **Settings → My Notifications** (office) or **Account** (technician app): enter the
   number, type the code sent on WhatsApp. Client contacts opt in from the portal **Profile**, or office
   staff record their agreement on the client's **Client Portal** tab (who, when, how).

## 9. Check it works

1. Opt yourself in on a test tenant; the code should arrive within about 10 seconds (the outbox worker
   runs every 10 s).
2. Trigger a reminder (e.g. assign yourself a work order). The tenant's message log shows Sent, then
   Delivered and Read as the webhook reports them.
3. Reply STOP from the phone; the next message to that number shows "Not sent: The number replied STOP".
   Reply START to restore.

Failures: a message Meta refuses (unknown template, bad number) fails at once with Meta's reason in the
log. Network errors, 429 and 5xx retry after 1, 5, 15 and 60 minutes, then fail for good.
