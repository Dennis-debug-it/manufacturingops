# Design: CSAT & Configurable Approval Matrix

Status: APPROVED 2026-07-12 — CSAT: 1–5 stars via email link + portal prompt.
Approval matrix phase 1: Purchase Orders + Expenses. Escalation: reminder
notifications only (requests never move levels automatically).

Both designs reuse existing infrastructure: the email outbox + template system, the
hashed single-use token pattern from password reset, the public rate-limited endpoint
pattern from `/api/public/leads`, and the background polling loop that already drives
scheduled reports and the email outbox.

---

## 1. CSAT (customer satisfaction)

### Recommended flow
1. **Trigger** — when a work order transitions to `Completed`, enqueue a survey email
   to the client contact via the existing email outbox (new template key
   `csat-survey`, editable per tenant like every other template). Optional per-tenant
   delay (send immediately, or N hours after completion).
2. **Token** — `CsatSurveyToken` row: hashed token (same pattern as password reset),
   single-use, 14-day expiry, linked to work order + client.
3. **Survey page** — anonymous public route `POST /api/public/csat/{token}` behind the
   `PublicEndpoints` rate limit. Frontend page `/csat/{token}`: **1–5 star rating +
   optional comment**, one screen, no login. Portal users additionally get a "Rate
   this job" prompt on completed work orders in the client portal (same endpoint).
4. **Storage** — `CsatResponse`: tenantId, workOrderId, clientId, technicianId
   (lead), score (1–5), comment, respondedAt, channel (Email | Portal).
5. **Reporting** — wire the existing CSAT tab in Reports to: average score, response
   rate, monthly trend, breakdown by technician and by client. Score also shown on
   the work order detail once submitted.
6. **Settings** — Notification settings gains: CSAT enabled (default off until the
   tenant opts in), send delay, reminder after N days (max 1 reminder).

### Why 1–5 stars over NPS
Per-job CSAT measures the service visit; NPS measures brand loyalty and needs volume
to be meaningful. Field-service platforms (ServiceTitan, Jobber) use per-job star
ratings. NPS can be layered on later as a quarterly campaign if wanted.

Estimate: 2–3 days (backend entity/endpoints/email template + survey page + report tab).

---

## 2. Configurable approval matrix

### Scope (phase 1 recommendation)
Documents with money attached and an existing approve action to hook:
**Purchase Orders** and **Expenses/Payables**. Phase 2: Quotations, Contracts,
Material Requests. (Contracts already have a single-approver lifecycle; the matrix
replaces the "who" decision, not the lifecycle.)

### Model
```
ApprovalMatrix        (per tenant + documentType, active flag)
 └─ ApprovalLevel     (ordered): minAmount, maxAmount (null = unbounded),
                      approverType (Permission | SpecificUsers),
                      requiredPermission (e.g. finance.approvals.manage)
                      or userIds[], escalateAfterHours (null = no escalation)
ApprovalRequest       (documentType, documentId, matrixLevelIndex, status
                      Pending|Approved|Rejected|Escalated, assignedSummary,
                      decidedByUserId, decidedAt, dueAt, note)
```

### Engine
- A single `IApprovalWorkflowService` with `StartAsync(docType, docId, amount)` and
  `DecideAsync(requestId, approve/reject, note)`.
- Existing module endpoints keep their routes; their approve actions delegate to the
  service. If no matrix is configured for a document type, behavior is unchanged
  (current single-permission approval) — **zero migration risk at launch**.
- Amount routing picks the ONE level whose [min, max) range contains the document
  amount; that level's approvers must approve sequentially from level 1 up to and
  including the matched level (standard threshold-chain semantics).
- Overdue handling (per decision): the existing background polling loop gains a
  check for `Pending` requests past `dueAt` → send reminder notifications to the
  assigned approvers (and repeat daily). Requests never move levels automatically.

### UI
- **Settings → Approval Matrix**: per document type, a level editor (amount ranges,
  approver permission or named users, escalation hours).
- **My Approvals** page: cross-module inbox of pending approval requests assigned to
  the signed-in user, with approve/reject + note. Badge count in the sidebar.
- Document detail pages show the approval chain status.

Estimate: 4–5 days phased — engine + PO/Expenses + settings UI + approvals inbox
first; quotes/contracts/material-requests as a follow-up.

---

## Decision checklist
1. CSAT scale: 1–5 stars (recommended) / NPS 0–10 / both.
2. CSAT channel: email link + portal prompt (recommended) / email only / portal only.
3. Matrix phase-1 documents: PO + Expenses (recommended) — add others now?
4. Escalation default: auto-escalate after 24h (recommended) / reminder only, no escalation.
