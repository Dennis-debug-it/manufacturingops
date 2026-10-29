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

### Phase 2: Contacts and companies (next)
- [ ] **2.1 Prospects as clients** (decision 1): add `Client.IsProspect`, hidden from operational lists
  (work orders, dispatch, invoicing) and shown in Sales and the client register under a filter. A deal's
  "prospect" text fields become a prospect client. Winning a deal, or creating its first quote or work
  order, clears the flag. Existing prospect deals are moved over by a migration that matches on company name.
- [ ] **2.2 Contact people** with a role (decision-maker, procurement, technical), several per deal and per client.
- [ ] **2.3 Duplicate checks and merge** on normalised email, phone and company name.
- [ ] **2.4 CSV import and export** for deals and contacts, with a preview before importing.
- [ ] **2.5 Assignment rules** (round-robin, or by service type or region). These slot into the owner order (decision 2) above the workspace default.

### Phase 3: Pipeline insight
- [ ] **3.1 Stage history:** each stage change saved with its date, rebuilding past changes from creation, won and lost dates.
- [ ] **3.2 Stalled-deal alerts:** an idle limit per stage, with a badge and a reminder.
- [ ] **3.3 Conversion and time-in-stage reports** (needs 3.1).
- [ ] **3.4 Sales targets** per owner per month or quarter, with reports against target.
- [ ] **3.5 Weekly report email.**

### Phase 4: Access and configuration
- [x] 4.1 Deal visibility (done above).
- [ ] **4.2 Pipelines and stages** (decision 4). Design:
  - `SalesPipeline` (name, order, optional `Module`, active) and `SalesPipelineStage` (name, order,
    default probability, kind `Open | Won | Lost`). Every pipeline has exactly one Won and one Lost
    stage, so reports, renewals and the hand-off keep working.
  - Each workspace is seeded with one pipeline holding today's seven stages. Existing deals map
    across, so nothing changes for current users.
  - Deals get `PipelineId` + `StageId`. The `Stage` text stays as the stage name, so existing code keeps working while it is moved over.
  - A pipeline tied to a module ("Maintenance contracts" → Maintenance, "Installations" → Operations,
    "Spares" → Inventory) only shows when the workspace's plan has that module, so selling modules
    separately decides which pipelines appear. Renewal deals go to the Maintenance pipeline if one
    exists, technician leads to the pipeline for the work's type.
  - The board, reports and forecast gain a pipeline switch. Win rate and forecast are per pipeline, with an "All pipelines" total.
- [ ] **4.3 Custom fields** (per-workspace field definitions on forms, filters and exports).
- [ ] **4.4 Stage rules:** fields required before a deal can move on; discount approvals reuse the finance approval matrix.

### Phase 5: Communication and lead capture
- [ ] **5.1 WhatsApp and email on deals:** send from the deal; incoming messages matched by contact (needs 2.2).
- [ ] **5.2 Inbound lead capture:** a website form per workspace, a sales email address handled by `EmailIntakeService`, and WhatsApp messages from unknown numbers.
- [ ] **5.3 Calendar feed** for follow-ups.
- [ ] **5.4 Online signature on portal quotes** (typed name, time and IP stored with the acceptance).
- [ ] **5.5 Sequences** (automated multi-step follow-ups).

### Phase 6: Field sales and polish
- [ ] **6.1 Field visit reports with GPS**, reusing the technician app's location and offline saving.
- [ ] **6.2 Drag-and-drop pipeline board** (`@dnd-kit` is already a dependency).
- [ ] **6.3 Saved views; documents on deals.**
- [ ] **6.5 Win-probability scoring**, after about 6 months of stage history.

### Not planned
Territories, team forecasting and opportunity splits; website chatbots and prospect databases; AI agents that write emails.
