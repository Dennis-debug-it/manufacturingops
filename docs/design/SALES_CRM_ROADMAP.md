# Sales CRM roadmap

Closes the gaps found by comparing the Sales module with Zoho CRM, BluWave, Salesforce, Dynamics 365,
HubSpot, Pipedrive, Odoo and the field-service platforms ServiceTitan, Simpro and Jobber (October 2026).

Ecosys's edge is the hand-off from sales into service work: won deal → client → quote → work order,
plus RFP/RFQ bid tracking. The plan builds on that first, then on CRM basics.

Sizes are rough, for one developer: **S** ≈ 1 day, **M** ≈ 2–4 days, **L** ≈ 1–2 weeks.

## Decisions (agreed)

| # | Question | Decision |
|---|---|---|
| 1 | Prospect companies (2.1) | A **Client marked as a prospect**, not a separate record. Converting a prospect is changing the flag. |
| 2 | Default owner for system-made deals | **Automatic, no setup needed.** The first person in this order who can still manage the pipeline: a preferred person (a contract's renewal owner, then its account manager); the owner of the client's latest open deal, else latest won deal; the account manager, then renewal owner, on the client's newest live contract; the workspace default owner (Settings › Sales); an admin. See `ISalesOwnerResolver`. |
| 3 | Deal visibility (4.1) | **Market practice** (HubSpot's "all / owned only" record access): the workspace chooses. Default is everyone sees all deals, as before. "Own deals" limits people to what they own; admins and holders of `crm.view_all` (managers by default, adjustable per person or group) see everything. |
| 4 | Stages and pipelines (4.2) | **Flexible, built for selling modules separately:** several pipelines per workspace, each with its own editable stages, optionally tied to a module. See 4.2 below. |

## Status

### Phase 0: Check what's shipped (done)
- [x] Backend tests run on .NET 10 with a local Postgres: the full suite passes, including a Postgres test
  that runs every CRM query (`SalesCrmFlowTests.Postgres_CrmQueriesTranslate`).

### Phase 1: Connect Sales to the service side (done)
- [x] **1.1 An accepted quote marks the deal Won.** This works whether the client accepts in the portal
  or staff record it. A declined quote leaves the deal open, logs the reason, and adds a follow-up for
  the next day (`SalesQuotationSync`).
- [x] **1.2 Unanswered quotes are chased.** Quotes now record when and by whom they were sent. The
  Follow-ups page lists sent quotes still waiting for an answer. The deal owner (else whoever sent the
  quote) is reminded after 3 days, and again 2 days before the quote expires. Quotes not raised from a
  deal are listed only for people who can see finance.
- [x] **1.3 Sales tab on the client page:** the client's deals and RFPs/RFQs, plus "New deal for this client".
- [x] **1.4 Renewal deals from contracts.** A live contract entering its renewal notice period gets a
  renewal deal, with a follow-up due at once. Renewing the contract wins the deal; letting it expire, or
  terminating or cancelling it, loses it. This can be turned off in Settings › Sales.
- [x] **1.5 Technicians flag leads.** The job screen has a "Spotted more work for this client?" card,
  which works offline too. It creates a Lead for the client and site, linked to the work order. No CRM
  access is needed, only jobs assigned to the technician can be used, and only on plans that include the CRM.
- [x] **Settings › Sales:** deal visibility, default owner, renewal deals on or off.
- [x] **4.1 Deal visibility (own vs all)**, applied to every CRM endpoint, including reports and follow-ups.

### Phase 2: Contacts and companies (done)
- [x] **2.1 Prospects as clients** (decision 1): `Client.IsProspect`, hidden from operational lists and
  shown in Sales and under a "Prospects" filter in the client register. A deal's prospect details are kept
  as a prospect client; winning the deal, or its first quote or work order, makes it a client. Existing
  prospect deals were moved over by a migration matching on company name.
- [x] **2.2 Contact people** with a role, several per client and per deal.
- [x] **2.3 Duplicate checks and merge** on normalised email, phone and company name.
- [x] **2.4 CSV import and export** for deals and contacts, with a preview before importing.
- [x] **2.5 Assignment rules** (one person, or several in turn; by source, service type or location),
  slotted into the owner order above the workspace default.

### Phase 3: Pipeline insight (done)
- [x] **3.1 Stage history:** every stage change saved with its date; past changes rebuilt from the activity log.
- [x] **3.2 Stalled-deal alerts:** a workspace idle limit (14 days by default), a badge, a list filter and a reminder.
- [x] **3.3 Conversion and time-in-stage report** per pipeline.
- [x] **3.4 Sales targets** per owner per month, with results against target in Reports.
- [x] **3.5 Weekly summary email** on Monday mornings (team figures for managers).

### Phase 4: Access and configuration (done)
- [x] 4.1 Deal visibility (done above).
- [x] **4.2 Pipelines and stages** (decision 4):
  - `SalesPipeline` (name, order, optional `Module`, default, active) with `SalesPipelineStage` (key,
    name, order, probability). A stage's key never changes, so renaming is free; every pipeline ends in
    Won and Lost, so reports, renewals and the hand-off keep working.
  - Each workspace gets a "Sales" pipeline with the original seven stages on first use; existing deals
    were mapped across by migration.
  - A pipeline tied to a module only shows when the plan has that module. Renewal deals go to a
    Maintenance pipeline where there is one; technician leads and imports go to the default pipeline
    (imports can name another).
  - The board, list, summary, reports and export take a pipeline; deals move between pipelines from the
    edit form. Settings › Sales has the pipeline editor.
- [x] **4.3 Custom fields:** text, number, date, dropdown and yes/no fields, for every pipeline or one.
  On the deal form and page, as a list filter, and as columns in the export and import.
- [x] **4.4 Stage rules:** each stage (Won included) can require built-in details (value, close date,
  contact, type of work, site, scope, a quotation) or custom fields. A stage's rules apply to every
  later stage, so skipping ahead skips nothing; Lost has none. Only moves people make are checked, never
  a client accepting a quote or a contract renewing. The deal page marks stages it cannot enter yet.
  - Quotes have no discount line, so the approval half is **quote approval before sending**: the
    approval matrix (Settings › Approvals) has a Quotations type; a quote in a band is held until approved
    at its current total, and changing the total afterwards asks again.

### Phase 5: Communication and lead capture (done)
> **Switched off for now (October 2026):** WhatsApp and email intake are disabled platform-wide
> (`Features:WhatsApp` / `Features:EmailIntake` in the API's appsettings, and `WHATSAPP_ENABLED` /
> `EMAIL_INTAKE_ENABLED` in the frontend's `shared/config/features.ts`). Deal emails still go out; replies
> go to the sender's own email. The website enquiry form is unaffected.

- [x] **5.1 Email and WhatsApp on deals.**
  - Email is written on the deal, sent from the workspace's address with the sender's name, and logged.
    Its subject carries the deal's tag ("[Deal D-1A2B3C4D]"). With the intake mailbox on, replies are
    addressed to it; a reply carrying the tag goes back on the deal when it comes from someone at the
    client (a contact's address or the same company domain), so a copied tag cannot write on a deal.
  - WhatsApp opens the salesperson's own WhatsApp with the message ready and logs it. The platform's
    WhatsApp number is shared by every workspace and only sends approved templates, so it cannot carry
    free-text sales chat, and its replies cannot be tied to one workspace. The workspace's own WhatsApp
    inbox conversations with the deal's contacts (matched on phone number) are shown on the deal.
- [x] **5.2 Inbound lead capture.** A sales email address (an alias of the intake mailbox, set in
  Settings › Sales) turns mail into leads instead of work orders; the website enquiry form has a public
  link and an embed snippet (the only page other sites may frame). Leads are matched to an existing
  client or become a prospect, get an owner by the usual rules and a follow-up due now; a repeat within a
  week joins the open lead. WhatsApp from unknown numbers is not possible on the shared number (above).
- [x] **5.3 Calendar feed:** "My calendar" and its private feed gain a Sales source (follow-ups on open
  deals and unsubmitted RFP/RFQ deadlines, for their owner).
- [x] **5.4 Signed acceptance on portal quotes:** the client types their name (and position) and ticks
  that they accept; the name, time, network address and portal user are stored and shown on the quote.
- [x] **5.5 Sequences:** task and email steps with waits between them, run hourly. Started on a deal;
  a client reply (if the sequence says so) or closing the deal stops it and closes what it left open.

### Phase 6: Field sales and polish (done)
- [x] **6.1 Field visit reports with GPS:** a site visit logged from the phone can carry its location
  (asked for only when the person taps for it), shown on the deal with a map link.
- [x] **6.2 Drag-and-drop pipeline board** (`@dnd-kit`): drag a card to a stage; Lost asks for the
  reason; stage rules still apply. The stage menu on each card remains the keyboard way to move a deal.
- [x] **6.3 Saved views** (the pipeline's filters, kept in the address so they can be bookmarked too;
  managers can share them) **and documents on deals.**
- [x] **6.5 Win-probability scoring:** from the last 12 months of closed deals in the same pipeline: the
  smoothed win rate of deals that reached the deal's stage, adjusted for the lead source's record, for
  going quiet and for an overdue close date. Shown once a pipeline has 10 closed deals, beside the
  owner's own probability, with the reasons.

### Not planned
Territories, team forecasting and opportunity splits; website chatbots and prospect databases; AI agents that write emails.
