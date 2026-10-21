# Ecosys Projects — Module Design

**Status:** Approved. **Release 1 is implemented** (see "Release 1: what was built" at the end); Release 2 and 3 are not started.
**Scope:** A new, tenant-configurable **Projects** module for finite work (installations, fit-outs,
rollouts, engagements) that sits beside the existing **Contracts (AMC)** module and reuses the
existing clients, work orders, inventory, attendance, finance, approvals and notification plumbing.

---

## 0. Decisions

These are the decisions that shape the data model. All are confirmed. The existing hourly fields on
contract rate cards (`ContractRateRule`, `RateCard`) are out of scope and stay as they are.

| # | Decision | Agreed approach | Alternative considered |
|---|---|---|---|
| D1 | Which record is the project's "client"? | **Confirmed.** **`Client`** (tenant-scoped, already used by contracts, work orders, invoices). The Platform CRM (`PlatformLead`, `CrmContact`) has no `TenantId` and is Ecosys's own sales CRM — it must not hold tenants' customers. | Make the Platform CRM multi-tenant (not recommended: mixes Ecosys sales data with tenant data). |
| D2 | How are consultants modelled? | **Confirmed.** A new tenant-scoped **`Consultant` directory**: firm name and contact details only, with no sites, assets, invoices or portal. Projects link to consultants through `ProjectConsultant`. Consultants are not `Client` records. | — |
| D3 | Licensing | **Confirmed.** New commercial module key **`projects`**, sold on its own or with Operations/Finance. Tabs that touch inventory/work orders require `operations`; financial tabs require `finance` (same pattern as `WorkOrderBilling` requiring both today). | Bundle Projects into `operations`. |
| D4 | Progress method | **Confirmed.** **Weighted milestones** (weights default to equal), with a permission-gated manual override that is recorded and shown as "overridden". | Task-count based; or manual only. |
| D5 | Variation approval | **Confirmed.** Reuse the **Approval Matrix** with a new document type `ProjectVariation` (amount bands, permission/user approvers, reminders already exist). | A simple single-approver flag on the variation. |
| D6 | Labour unit and cost | **Confirmed.** Labour is measured in **man-days, never hours**: work in Kenya isn't billed or costed per hour. Casual site workers have an agreed **daily rate**, **entered by the office, never by the site supervisor**, so labour cost = days worked × that rate. Permanent staff are not costed automatically (no salary data in Ecosys); a tenant that wants them costed adds a manual "Labour" cost entry. | Labour as a cost entry only, with no daily rates at all. |
| D7 | Material requests for projects | **Confirmed.** Allow `MaterialRequest` to target a project directly (make `WorkOrderId` nullable, add `ProjectId`, require exactly one). | Force every project material request through a work order. |
| D8 | Site attendance for casual workers | **Confirmed.** Most people on site (fundis, helpers, casuals) have no Ecosys login and aren't `Technician` records, so they can't clock in. The supervisor marks a **daily muster roll** per project (Present / Half day / Absent) against a project **worker roster**. Technicians with logins can still clock in as they do today; the muster roll is what counts man-days. Supervisors record **presence only**; they never enter or see rates. | Require every worker to be a `Technician` with a login. |
| D9 | How projects are billed | **Confirmed.** **Valuations (interim payment certificates)**, recorded as **totals, not BQ line items**: the QS works the valuation from the bill of quantities outside Ecosys and attaches it; Ecosys records work done to date and materials on site, deducts **retention** and **advance recovery** and previously certified amounts, and the certified amount becomes a `TenantInvoice`. Retention is released in two parts (practical completion and end of defects liability). Projects with simple milestone or lump-sum billing skip retention. | Hold the full BQ in Ecosys and value line by line (rejected: much larger model for the same certificate). |
| D10 | Withholding VAT / tax | **Confirmed.** Some clients withhold tax (e.g. 2% VAT) when paying a certificate. A **withholding certificate** settles that part of the invoice exactly like a payment does, so project receivables reconcile. This is a Finance-wide change: it works on every `TenantInvoice`, not just project invoices. | Leave invoices showing a permanent shortfall. |
| D11 | Client portal | **Confirmed.** Clients see their projects in the existing client portal: stage, progress (planned vs actual), milestones, approved variations, certified valuations and project invoices. They never see costs, budget, margins, the muster roll, rates, internal tasks or the diary. | No portal access. |
| D12 | Deleting projects | **Confirmed.** Projects are never deleted; they are **cancelled** (stage category Cancelled), which keeps linked work orders, issues and invoices traceable. | Allow deleting projects with nothing linked. |

---

## 1. Design principles

1. **Reference, don't duplicate.** A project points at existing `Client`, `Site`, `User`/`Technician`,
   `MaterialIssue`, `WorkOrder`, `TenantInvoice`, `Expense`, `SupplierBill`, `PurchaseOrder`. There is no
   second client master, stock ledger, invoice ledger or work-order engine.
2. **One nullable foreign key per existing record.** Most integration is `ProjectId` on an existing
   table, so the existing screens, permissions and reports keep working and simply gain a project filter.
3. **Projects ≠ AMC.** Projects own finite delivery (start, end, milestones, variations, handover).
   `Contract` keeps owning recurring obligations (coverage, PM entitlements, renewals, SLA). The only
   link is optional: a project's handover can create a draft contract.
4. **Configurable per tenant, not per customer request.** Project types, stages and enabled features are
   tenant data. The contractor who asked for this gets a template, not special code.
5. **Turning a feature off hides it; it never deletes data.**
6. **Follow the repo's existing conventions** — `Ecosys.Domain/Entities`, `AuditableEntity` +
   `ITenantEntity`, `ApplyTenantFilter`, string status constants, `api/<resource>` routes,
   `ServiceOpsPlans` module gating, `NumberingSetting` for document numbers. No folder restructure, no
   `/api/v1`, no separate worker process.

---

## 2. What already exists and gets reused

| Need | Existing piece | Location |
|---|---|---|
| Client & sites | `Client`, `Site` | `Ecosys.Domain/Entities/Client.cs`, `Site.cs` |
| AMC / service contracts | `Contract` (+ sites, assets, PM entitlements, renewals, reminder rules) | `ContractManagement.cs`, `ContractServices.cs` |
| Recurring maintenance | `PreventiveMaintenancePlan`, `PmSchedulerWorker` | `PreventiveMaintenancePlan.cs` |
| Field work | `WorkOrder` (+ assignments, time logs, photos, signatures, reports) | `WorkOrder*.cs` |
| Staff | `User`, `Technician`, `AssignmentGroup`, `UserCertification` | |
| Attendance | `AttendanceEntry` (clock sessions), `LeaveRequest`, `AttendanceAutoClockOutWorker` | `AttendanceEntry.cs` |
| Materials | `MaterialRequest`, `MaterialIssue`, `PartsReturn`, `StockMovement`, `StockTransfer`, branch/van stock | |
| Purchasing | `PurchaseOrder`, `Supplier`, `SupplierBill` | `PurchaseOrder.cs`, `ExpenseAndPayable.cs` |
| Sales docs | `TenantQuotation` (already converts to invoice / work order) | `TenantQuotation.cs` |
| Billing | `TenantInvoice`, `TenantPayment`, `PaymentAllocation`, `CreditNote` | |
| Expenses | `Expense` (claims) | `ExpenseAndPayable.cs` |
| Approvals | `ApprovalMatrix` / `ApprovalLevel` / `ApprovalRequest`, `ApprovalReminderWorker` | `ApprovalMatrix.cs` |
| Numbering | `NumberingSetting` (prefix, padding, year, reset) | `NumberingSetting.cs` |
| Files | `Attachment` (`EntityType` + `EntityId`) | `Attachment.cs` |
| History | `AuditLog` (`EntityName` + `EntityId`) | `AuditLog.cs` |
| Licensing | `ServiceOpsPlans` (module keys + controller classification) | `Ecosys.Infrastructure/Services/ServiceOpsPlans.cs` |
| Feature settings | `TenantModuleSetting` (JSON per module key) | `TenantModuleSetting.cs` |
| Notifications | `NotificationKeyDescriptor`, email outbox, web push queue, SignalR hub, WhatsApp, `UserNotificationPreference` | `Ecosys.Infrastructure/Services`, `Ecosys.Api/Hubs` |

---

## 3. Module boundaries

```
                    ┌──────────── Client (+ Sites) ── Consultant directory  ────────────┐
                    │                                                                  │
        ┌───────────▼───────────┐     handover (optional)      ┌──────────────────────▼─┐
        │       PROJECTS        │ ───────────────────────────▶ │   CONTRACTS (AMC)      │
        │ finite: milestones,   │      creates draft Contract   │ recurring: coverage,   │
        │ variations, diary,    │                               │ PM plans, renewals,SLA │
        │ budget vs actual      │                               └──────────┬─────────────┘
        └───┬───────┬───────┬───┘                                          │ generates
            │       │       │ ProjectId on…                                ▼
            │       │       └──────────────▶ WorkOrder  ◀──────────── PM work orders
            │       └──────────────────────▶ MaterialRequest / MaterialIssue / PartsReturn
            └──────────────────────────────▶ AttendanceEntry, Expense, SupplierBill,
                                             PurchaseOrder, TenantQuotation, TenantInvoice
```

- **Projects owns:** project record, type/stage config, team, milestones, tasks, variations, budget,
  diary, closure.
- **Projects reads but never writes:** stock balances, invoice balances, attendance sessions. Writes to
  those happen through their own services (e.g. issuing material still goes through the material-issue
  service, which now accepts a `ProjectId`).

---

## 4. Licensing and tenant configuration

### 4.1 Module key

Add to `ServiceOpsPlans`:

```csharp
public const string Projects = "projects";
```

- `Modules(plan)` accepts `projects` in `ModulesIncluded`. Canonical plans (`operations`, `finance`,
  `suite`) are **unchanged** — existing tenants do not silently gain or lose anything. Projects is enabled
  by adding the key to a plan (or a new plan code, decided commercially).
- New controllers (`Projects`, `ProjectSettings`, `Consultants`) go in a new `ProjectsControllers` set, so
  the controller-classification check in `TenantLifecycleConsistencyTests` keeps catching unclassified controllers.
- Route-level dependencies (in `RequiredModules`):
  - `/api/projects/{id}/materials*`, `/work-orders*`, `/attendance*` → `[projects, operations]`
  - `/api/projects/{id}/financials*`, `/invoices*`, `/valuations*` → `[projects, finance]`
  - `/api/portal/projects*` → `[projects]`; `/api/portal/projects/{id}/valuations*`, `/invoices*` → `[projects, finance]`
  - Withholding certificates sit with the existing finance controllers → `[finance]`

### 4.2 Feature toggles

Stored as `TenantModuleSetting` with `ModuleKey = "projects"`:

```json
{
  "milestones": true,
  "tasks": true,
  "variations": true,
  "siteDiary": false,
  "materials": true,
  "musterRoll": true,
  "valuations": true,
  "financials": true,
  "documents": true,
  "progressMethod": "WeightedMilestones",
  "allowManualProgressOverride": true,
  "visibility": "AllProjects",
  "clientPortal": true
}
```

`visibility` is `AllProjects` (anyone with `projects.view`) or `MembersOnly` (non-managers see only
projects where they are a `ProjectMember`). `clientPortal` switches the portal Projects page on for the
whole tenant; each project can also be hidden individually (`Project.VisibleInClientPortal`).

Toggles hide tabs, nav entries and endpoints (the API returns 404/403 for disabled features); they
never delete rows. A toggle that depends on an unlicensed module (e.g. `materials` without `operations`)
is shown disabled with an upgrade hint.

---

## 5. Data model

All new entities derive from `AuditableEntity`, implement `ITenantEntity`, and are registered with
`ApplyTenantFilter`. Status/category values are string constants (as with `ApprovalDocumentTypes`) so new
values don't need migrations. Every unique index includes `TenantId`.

### 5.1 Configuration entities

```csharp
// Tenant-defined classification: "New Installation", "Service Project", "Fit-out", "IT Rollout"…
public sealed class ProjectType : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? DefaultTemplateId { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}

// Tenant-defined workflow stage. Dashboards and rules use Category, never the display name.
public sealed class ProjectStage : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;          // "Awarded", "Mobilising", "Snagging"…
    public string Category { get; set; } = ProjectStageCategories.Planning;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public static class ProjectStageCategories
{
    public const string Planning   = "Planning";    // awarded, not started
    public const string Active     = "Active";      // execution
    public const string OnHold     = "OnHold";
    public const string Handover   = "Handover";    // testing, commissioning, snagging
    public const string Completed  = "Completed";   // delivered, financially open
    public const string Closed     = "Closed";      // reconciled and locked
    public const string Cancelled  = "Cancelled";
}

// Reusable starting point: milestones (with weights) and tasks copied into a new project.
public sealed class ProjectTemplate : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ItemsJson { get; set; } = "[]";  // [{ milestone, weight, offsetDays, tasks:[…] }]
    public bool IsActive { get; set; } = true;
}
```

New tenants get seeded defaults (one type "General Project", stages for each category). A starter pack
for contractors adds "New Installation", "Service Project" and a contractor milestone template, using
the existing `SystemTemplatePack` / starter-pack mechanism.

### 5.2 Project

```csharp
public sealed class Project : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid? BranchId { get; set; }
    public string ProjectNumber { get; set; } = string.Empty;   // NumberingSetting DocumentType "Project", prefix "PRJ"
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public Guid ClientId { get; set; }                 // D1
    public Guid? SiteId { get; set; }                  // must belong to ClientId
    public Guid ProjectTypeId { get; set; }
    public Guid StageId { get; set; }
    public Guid? ProjectManagerUserId { get; set; }

    // Commercial
    public string Currency { get; set; } = "KES";
    public decimal OriginalValue { get; set; }
    public decimal ApprovedAdditions { get; set; }     // maintained by the variation service
    public decimal ApprovedOmissions { get; set; }     // positive number
    // RevisedValue = OriginalValue + ApprovedAdditions - ApprovedOmissions (computed, not stored)
    public Guid? SourceQuotationId { get; set; }       // awarded from a TenantQuotation

    // Schedule
    public DateOnly? PlannedStartDate { get; set; }
    public DateOnly? PlannedEndDate { get; set; }
    public DateOnly? ActualStartDate { get; set; }
    public DateOnly? ActualEndDate { get; set; }

    // Progress (D4)
    public decimal ProgressPercent { get; set; }       // cached result of the configured method
    public decimal? ProgressOverridePercent { get; set; }
    public Guid? ProgressOverrideByUserId { get; set; }
    public string? ProgressOverrideReason { get; set; }

    // Billing terms (D9, Release 2)
    public decimal? RetentionPercent { get; set; }     // null = no retention
    public decimal? RetentionCapAmount { get; set; }   // optional ceiling on total retention held
    public decimal RetentionReleaseAtCompletionPercent { get; set; } = 50; // share released at practical completion; 0 = all at end
    public int? DefectsLiabilityMonths { get; set; }   // final retention release due HandedOverAt + this period
    public decimal? AdvancePaymentAmount { get; set; } // paid up front, recovered across valuations
    public decimal? AdvanceRecoveryPercent { get; set; } // % of each valuation's work done this period, until fully recovered

    // Client portal (D11)
    public bool VisibleInClientPortal { get; set; } = true;

    // Handover / closure
    public Guid? MaintenanceContractId { get; set; }   // Contract created at handover
    public DateTime? HandedOverAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public Guid? ClosedByUserId { get; set; }
    public string? ClosureNotes { get; set; }
}
```

Unique index: `(TenantId, ProjectNumber)`. Indexes: `(TenantId, ClientId)`, `(TenantId, StageId)`,
`(TenantId, ProjectManagerUserId)`.

### 5.3 People and consultants

```csharp
// Internal team. Either a User (office) or a Technician (field), or both when linked.
public sealed class ProjectMember : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? UserId { get; set; }
    public Guid? TechnicianId { get; set; }
    public string Role { get; set; } = ProjectMemberRoles.Member;  // ProjectManager | Supervisor | Member | Viewer
    public string? Trade { get; set; }                             // from tenant trade list (5.7)
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
}

// Consultant directory (D2): a contact list, nothing more. Shared by all of the tenant's projects.
public sealed class Consultant : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string FirmName { get; set; } = string.Empty;      // "XYZ Engineering"
    public string? Discipline { get; set; }                   // optional free text: QS, M&E, Architect, Structural…
    public string? ContactPerson { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? PhysicalAddress { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;                // deactivate rather than delete once used on a project
}

// Which consultants are on which project.
public sealed class ProjectConsultant : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid ConsultantId { get; set; }
    public string? RoleOnProject { get; set; }                // optional: "Lead consultant", "QS"…
}
```

Project-scoped access: see the `visibility` setting in 4.2.

Consultant firm names are unique per tenant (case-insensitive). A consultant linked to any project can
be deactivated but not deleted.

### 5.4 Milestones and tasks

```csharp
public sealed class ProjectMilestone : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public decimal Weight { get; set; } = 1;           // relative; normalised when computing progress
    public DateOnly? PlannedDate { get; set; }
    public DateOnly? ForecastDate { get; set; }
    public string Status { get; set; } = "NotStarted"; // NotStarted | InProgress | Completed | Verified
    public DateTime? CompletedAt { get; set; }
    public Guid? CompletedByUserId { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public Guid? VerifiedByUserId { get; set; }
    public decimal? BillingPercent { get; set; }       // optional milestone-billing hint (Release 2)
}

public sealed class ProjectTask : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? MilestoneId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? AssigneeUserId { get; set; }
    public DateOnly? DueDate { get; set; }
    public string Priority { get; set; } = "Medium";   // Low | Medium | High
    public string Status { get; set; } = "Open";       // Open | InProgress | Done | Cancelled
    public DateTime? CompletedAt { get; set; }
    public int SortOrder { get; set; }
}
```

**Progress (weighted milestones):** `Σ(weight of Completed or Verified milestones) / Σ(all weights) × 100`.
A milestone counts as complete when `Completed`; a stricter tenant setting can require `Verified`.
Recomputed on every milestone change and cached on `Project.ProgressPercent`. The dashboard shows
*planned* progress too (share of milestone weight whose `PlannedDate` has passed), so "behind schedule"
is a computed comparison, not a status someone types.

### 5.5 Variations

```csharp
public sealed class ProjectVariation : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ProjectId { get; set; }
    public string VariationNumber { get; set; } = string.Empty;   // NumberingSetting "ProjectVariation", prefix "VO"
    public string Kind { get; set; } = "Addition";               // Addition | Omission
    public decimal Amount { get; set; }                          // always positive
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ClientReference { get; set; }                 // client/consultant instruction number
    public string Status { get; set; } = "Draft";                // Draft | Submitted | Approved | Rejected | Void
    public DateTime? SubmittedAt { get; set; }
    public DateTime? DecidedAt { get; set; }
    public Guid? DecidedByUserId { get; set; }
    public string? DecisionNote { get; set; }
}
```

Rules:
- Only `Approved` variations change `ApprovedAdditions` / `ApprovedOmissions`. On every approve or
  void, the totals are **recomputed from the variation rows** (`SUM` of Approved additions and omissions)
  inside the same transaction as the status change, never incremented, so two decisions landing
  together can't leave the totals wrong. Approval goes through a new `ProjectVariation` case in
  `ApprovalWorkflowService.ApplyOutcomeAsync`, alongside the existing `Expense` and `PurchaseOrder` cases.
- Submit creates an `ApprovalRequest` with `DocumentType = ApprovalDocumentTypes.ProjectVariation`
  (added to `ApprovalDocumentTypes.All`). If the tenant has no active matrix for it, a holder of
  `projects.variations.approve` approves directly.
- An approved variation is never edited. It is voided (reverses its effect, audited) and replaced.
- Supporting documents (signed instruction, drawings) are `Attachment` rows with
  `EntityType = "ProjectVariation"`.
- Omissions cannot take the revised value below zero.

### 5.6 Release 2 entities

```csharp
// Budget by cost category; actuals are derived (section 7), not typed in here.
public sealed class ProjectBudgetLine : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ProjectId { get; set; }
    public string Category { get; set; } = "Materials";   // Materials | Labour | Subcontractor | Equipment | Other
    public string? Description { get; set; }
    public decimal Amount { get; set; }
}

// Costs with no natural home elsewhere (equipment hire, one-off site costs). Kept small on purpose.
public sealed class ProjectCostEntry : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ProjectId { get; set; }
    public string Category { get; set; } = "Other";
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateOnly IncurredOn { get; set; }
    public Guid RecordedByUserId { get; set; }
}

// Daily site diary.
public sealed class ProjectDiaryEntry : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ProjectId { get; set; }
    public DateOnly EntryDate { get; set; }
    public Guid AuthorUserId { get; set; }
    public string? WorkCompleted { get; set; }
    public string? IssuesAndDelays { get; set; }
    public string? InstructionsReceived { get; set; }   // feeds variations
    public string? Weather { get; set; }
    public DateTime? SubmittedAt { get; set; }          // locked once submitted; corrections are new entries
}

// Project worker roster (D8): casuals and anyone else on site without an Ecosys login.
public sealed class ProjectWorker : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? TechnicianId { get; set; }             // set when the worker is also a Technician
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }                  // M-Pesa number for wage payments
    public string? Trade { get; set; }                  // from the tenant trade list (5.7)
    public string EngagementType { get; set; } = "Casual"; // Casual | Permanent | Subcontractor
    public bool IsActive { get; set; } = true;
    // No rate here: the supervisor can create a worker on site; the office sets the rate (ProjectWorkerRate).
}

// Daily rate, entered by the office. Rates are dated, so a new rate never rewrites earlier days.
public sealed class ProjectWorkerRate : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ProjectWorkerId { get; set; }
    public DateOnly EffectiveFrom { get; set; }        // applies to work dates on or after this, until the next rate
    public decimal DailyRate { get; set; }             // KES per full day
    public Guid SetByUserId { get; set; }
    public string? Note { get; set; }
}

// Daily muster roll: one row per worker per day, marked by the site supervisor.
public sealed class ProjectMusterEntry : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid ProjectWorkerId { get; set; }
    public DateOnly WorkDate { get; set; }
    public string Status { get; set; } = "Present";     // Present | HalfDay | Absent
    public decimal DayFraction { get; set; } = 1;       // 1, 0.5 or 0, derived from Status
    public Guid MarkedByUserId { get; set; }
    public string? Note { get; set; }
}

// Valuation / interim payment certificate (D9).
public sealed class ProjectValuation : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ProjectId { get; set; }
    public string ValuationNumber { get; set; } = string.Empty;  // NumberingSetting "ProjectValuation", prefix "VAL"
    public DateOnly PeriodEnd { get; set; }
    // Totals only (D9). The QS's BQ workings are an Attachment on the valuation, not rows in Ecosys.
    public decimal WorkDoneToDate { get; set; }         // measured work to date, incl. approved variations
    public decimal MaterialsOnSite { get; set; }        // unfixed materials on site, valued this period
    public decimal GrossValueToDate { get; set; }       // WorkDoneToDate + MaterialsOnSite
    public decimal RetentionToDate { get; set; }        // GrossValueToDate × RetentionPercent, capped
    public decimal AdvanceRecoveredToDate { get; set; } // cumulative; see 8.5
    public decimal PreviouslyCertified { get; set; }    // net amount of the previous certified valuation
    public decimal AmountDue { get; set; }              // Gross − RetentionToDate − AdvanceRecoveredToDate − PreviouslyCertified
    public string Status { get; set; } = "Draft";       // Draft | Submitted | Certified | Invoiced | Void
    public Guid? CertifiedByConsultantId { get; set; }  // which project consultant certified it
    public string? CertificateReference { get; set; }   // consultant / QS certificate number
    public DateOnly? CertifiedOn { get; set; }
    public decimal? CertifiedAmount { get; set; }       // may differ from AmountDue; the certified figure is what gets invoiced
    public Guid? InvoiceId { get; set; }                // TenantInvoice raised from this valuation
}

// Retention release (D9): normally two per project.
public sealed class ProjectRetentionRelease : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ProjectId { get; set; }
    public string Stage { get; set; } = "PracticalCompletion";  // PracticalCompletion | EndOfDefectsLiability
    public decimal Amount { get; set; }
    public DateOnly ReleasedOn { get; set; }
    public Guid? InvoiceId { get; set; }                // the release invoice
    public Guid ReleasedByUserId { get; set; }
}
```

**Finance-wide addition (D10)**, in the existing finance module rather than Projects:

```csharp
// A withholding certificate issued by the client. Settles part of an invoice like a payment does.
public sealed class WithholdingCertificate : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ClientId { get; set; }
    public Guid InvoiceId { get; set; }
    public string TaxType { get; set; } = "VAT";        // VAT | IncomeTax
    public decimal? RatePercent { get; set; }           // e.g. 2; informational
    public decimal Amount { get; set; }
    public string CertificateNumber { get; set; } = string.Empty;  // the client's / KRA certificate number
    public DateOnly CertificateDate { get; set; }
    public string Status { get; set; } = "Confirmed";   // Confirmed | Reversed (same as TenantPayment)
}
```

It allocates through the existing `PaymentAllocation` table with a new
`AllocationSourceTypes.Withholding = "Withholding"` (the column allows 20 characters), so
`InvoiceBalanceCalculator`, overdue status and client statements treat it as settlement with no special
cases. The certificate scan is an `Attachment`. Ecosys does not file anything with KRA.

Photos are `Attachment` rows with `EntityType = "ProjectDiaryEntry"`, and the consultant's signed
certificate is an `Attachment` on the valuation. Workers present and materials used on a given day are
**derived** from the muster roll and `MaterialIssue` for the project and date, not re-entered in the diary.

### 5.7 Trades

Add a tenant trade list (`TenantModuleSetting` key `projects`, field `trades: ["HVAC","Electrical",…]`)
and use it to populate `Technician.SkillCategory` and `ProjectMember.Trade`. No new table unless trades
later need certifications per trade (which `CertificationDefinition` / `UserCertification` already cover).

---

## 6. Changes to existing tables

All additive, nullable, indexed `(TenantId, ProjectId)`. Each service that writes the row validates that
the project belongs to the tenant and (where the row has a `ClientId`) to the same client.

| Table | Change | Purpose |
|---|---|---|
| `WorkOrder` | `+ ProjectId` | Snag lists, commissioning visits, project call-outs |
| `MaterialIssue` | `+ ProjectId` | Issue stock to a project/site directly (`WorkOrderId` already nullable) |
| `MaterialRequest` | `+ ProjectId`, `WorkOrderId` → nullable, check: exactly one set (D7) | Project material requisitions |
| `PartsReturn` | `+ ProjectId` | Returns reduce project material cost |
| `AttendanceEntry` | `+ ProjectId` | Technicians with logins clock in "on site" for a project (alongside the existing `CurrentWorkOrderId`) |
| `Expense` | `+ ProjectId` | Staff claims charged to a project |
| `SupplierBill` | `+ ProjectId` | Subcontractor and supplier costs |
| `PurchaseOrder` | `+ ProjectId` | Committed (not yet billed) cost |
| `TenantQuotation` | `+ ConvertedToProjectId` | "Award as project", mirroring `ConvertedToWorkOrderId` |
| `TenantInvoice` | `+ ProjectId`, `+ ProjectValuationId` | Valuation, milestone, final and retention-release invoices against a project |
| `Contract` | `+ SourceProjectId` | AMC created at handover |
| `ApprovalDocumentTypes` | `+ ProjectVariation` | Variation approvals |
| `PaymentAllocation` | `+ AllocationSourceTypes.Withholding` | Withholding certificates settle invoices (D10) |
| `Attachment` | `+ SharedWithClient` (bool, default false) | Documents shown in the client portal (D11) |
| `NumberingSetting` defaults | `+ Project (PRJ)`, `+ ProjectVariation (VO)`, `+ ProjectValuation (VAL)` | Document numbers |

---

## 7. Commercial calculations

All amounts exclude tax, in the project currency. Each figure names its source, so numbers can be
traced on screen.

| Figure | Formula / source |
|---|---|
| Revised value | `OriginalValue + ApprovedAdditions − ApprovedOmissions` |
| Pending variations | Sum of `Submitted` variations (shown separately, never added to the revised value) |
| Materials cost | Σ `MaterialIssue.TotalIssuedCost` (status Issued, `ProjectId` = p) − value of `PartsReturn` for p |
| Man-days | Σ `ProjectMusterEntry.DayFraction` for p (shown by trade and by week) |
| Labour cost | Σ `DayFraction ×` the worker's `ProjectWorkerRate` in effect on `WorkDate`, for p (casual workers only; permanent staff via manual cost entries, D6) |
| Uncosted days | Man-days with no rate in effect yet, shown as a count with a prompt to the office. They're excluded from labour cost until a rate is set |
| Subcontractor / supplier cost | Σ `SupplierBill.Amount` with `ProjectId` = p |
| Expenses | Σ `Expense.Amount` with status Approved or Reimbursed and `ProjectId` = p |
| Other costs | Σ `ProjectCostEntry.Amount` |
| **Actual cost** | Materials cost + labour cost + subcontractor/supplier cost + expenses + other costs (man-days and uncosted days are counts, not costs) |
| Committed cost | Open `PurchaseOrder` value for p not yet billed |
| Invoiced | Σ `TenantInvoice.TotalAmount − TaxAmount` (not Void) with `ProjectId` = p |
| Received | Σ allocations against those invoices from payments **and** withholding certificates (shown split: cash received / tax withheld) |
| Outstanding | Σ `TenantInvoice.Balance` for those invoices; overdue = past `DueDate` |
| Certified to date | `GrossValueToDate` of the latest Certified valuation |
| Retention held | `RetentionToDate` of the latest Certified valuation − Σ `ProjectRetentionRelease.Amount` |
| Retention due | First part: `RetentionReleaseAtCompletionPercent` of retention held, once `HandedOverAt` is set. Balance: once `HandedOverAt + DefectsLiabilityMonths` has passed |
| Advance outstanding | `AdvancePaymentAmount − AdvanceRecoveredToDate` of the latest Certified valuation |
| Unbilled value | Revised value − certified to date |
| Indicative margin | `(Revised value − Actual cost) / Revised value` |
| Forecast margin | `(Revised value − (Actual cost + Committed cost)) / Revised value` |

The financials tab labels margins "indicative" and lists what is excluded (tax, accruals, unrecorded
commitments). A material is a cost when *issued*, net of returns. Stock still in the store is not a cost.

---

## 8. Workflows

### 8.1 Lifecycle

The stage *categories* drive behaviour. The display names are tenant-defined.

```
Planning ──▶ Active ◀──▶ OnHold
               │
               ▼
           Handover ──▶ Completed ──▶ Closed
   (any non-Closed) ──▶ Cancelled
```

| Transition | Checks / side effects |
|---|---|
| Create | Client required; site must belong to the client; number allocated; template milestones/tasks copied; PM added as `ProjectMember` |
| → Active | Sets `ActualStartDate` if empty |
| → Handover | Warns if milestones are incomplete (does not block) |
| → Completed | Sets `ActualEndDate`, `HandedOverAt`; offers "Create maintenance contract" |
| → Closed | **Blocks** if: variations still Submitted, material issues not Issued/Rejected, unbilled revised value > tolerance (tenant setting), or invoices outstanding (warning only). Requires `projects.close`. Project becomes read-only except attachments. |
| Reopen | `projects.close` holder, reason required, audited |
| Delete | Not available (D12). Cancel instead; a cancelled project stays visible under a "Cancelled" filter |

### 8.2 Award from quotation

On an Accepted `TenantQuotation`: "Award as project" creates the project with client, site, original
value (quotation subtotal) and `SourceQuotationId`, and sets `ConvertedToProjectId` on the quotation.
This follows the existing convert-to-work-order pattern and requires `[projects, finance]`.

### 8.3 Handover → AMC

"Create maintenance contract" opens the existing contract form, prefilled with: client, project site(s),
assets created during the project (assets whose `SiteId` matches and `CreatedAt` falls within the
project), start date = handover date. It saves a **Draft** `Contract` with `SourceProjectId`; everything
after that is the existing contract workflow. Requires `operations` and contract permissions.

### 8.4 Site attendance (muster roll)

- The supervisor opens the project on their phone each day and sees the active roster with yesterday's
  marks pre-filled. They tap Present / Half day / Absent for each worker and submit. A new worker can be
  added to the roster from the same screen.
- One mark per worker per project per day (unique index). Marks can be corrected until the tenant's
  lock point (default: end of the following day). After that a correction needs `projects.manage` and
  is audited.
- **Supervisors mark presence only.** The muster screen shows no rates and has no rate field. A worker
  added on site starts with no rate.
- **The office sets rates** (`projects.financials.manage`) from the project's Workers screen, before or
  after the days are worked. Each rate has an effective-from date. Days already marked are costed as soon
  as a rate covers them, and a later rate change only affects days from its own effective date.
- Weekly **wage sheet** (office only): man-days × rate per worker for a date range, exportable for paying casuals
  (cash or M-Pesa). Workers with uncosted days are flagged at the top. Ecosys records what's owed; it doesn't pay anyone or run payroll.
- Technicians with logins can still clock in through the existing attendance flow with an optional
  "Project" picker. If a technician is also on the roster (`ProjectWorker.TechnicianId`), their clock-in
  pre-fills their muster mark, so they're never counted twice.

### 8.5 Valuations, advance and retention

**Advance (optional).** "Raise advance invoice" invoices `AdvancePaymentAmount` at the start. It is then
recovered from each valuation:

```
work this period         = WorkDoneToDate − previous valuation's WorkDoneToDate
recovery this valuation  = min(work this period × AdvanceRecoveryPercent, advance still outstanding)
AdvanceRecoveredToDate   = previous AdvanceRecoveredToDate + recovery this valuation
```

Recovery applies to work done, not to materials on site.

**Each valuation:**
1. The PM or QS enters work done to date and materials on site. Work done defaults to
   progress % × revised value as a starting point only; the QS overwrites it with the BQ figure and
   attaches the BQ workings.
2. Ecosys calculates retention (capped), advance recovery and the amount due.
3. Submit → the consultant certifies outside Ecosys. The user records the certifying consultant, the
   certificate number and date, the **certified amount**, and attaches the signed certificate.
4. "Raise invoice" creates a `TenantInvoice` for the certified amount plus VAT, linked by
   `ProjectValuationId`. From there it's the existing invoice, payment and overdue flow. If the client
   withholds tax, finance records the withholding certificate against that invoice (D10).

**Retention, released in two parts:**
1. At practical completion (`HandedOverAt` set), "Release retention" offers
   `RetentionReleaseAtCompletionPercent` (default 50%) of the retention held and raises an invoice.
2. When the defects liability period ends, it offers the balance. A `project.retention-due` reminder
   fires at each point.

A tenant can set the completion share to 0% (all retention released at the end) or 100% (all at
completion). Each release is a `ProjectRetentionRelease` row, so retention held is always traceable.

Projects without retention or valuations skip all of this and invoice milestones or the lump sum directly.

### 8.6 Client portal (D11)

Portal users already see their own client's work orders, invoices, quotations and contracts
(`PortalController`, scoped by `IPortalContext.ClientId`). A **Projects** page is added on the same basis:

| Portal shows | Portal never shows |
|---|---|
| Project name, number, site, stage, planned and actual dates | Costs, budget, margins, committed cost |
| Progress: planned vs actual | Muster roll, workers, rates, wage sheet |
| Milestones: name, planned date, status | Tasks, diary, internal notes |
| Approved variations (number, title, addition/omission, amount) | Draft, submitted or rejected variations |
| Certified valuations and their certificates | Draft or submitted valuations |
| Project invoices and retention held (needs `finance`) | Consultants' contact details |
| Project documents marked "Share with client" | All other attachments |

- Shown only when the tenant's `clientPortal` setting is on **and** the project's
  `VisibleInClientPortal` is true.
- Documents are internal by default. An `Attachment` on a project is shared only when someone ticks
  "Share with client" (new `Attachment.SharedWithClient` flag, default false).
- Read-only in Release 1 and 2. The client can't approve variations or certificates through the portal.

---

## 9. API

Matching existing style (`[Authorize]`, `[Route("api/...")]`, tenant-aware base controller).

```
GET    /api/projects                         list (filters: stage category, type, client, PM, search)
POST   /api/projects                         create
GET    /api/projects/{id}                    detail + summary figures
PUT    /api/projects/{id}                    update header
POST   /api/projects/{id}/stage              change stage   { stageId, note }
POST   /api/projects/{id}/progress-override  { percent | null, reason }
POST   /api/projects/{id}/close | /reopen

GET|POST|PUT|DELETE /api/projects/{id}/members[/{memberId}]
GET|POST|DELETE     /api/projects/{id}/consultants[/{projectConsultantId}]
GET|POST|PUT        /api/consultants[/{consultantId}]        directory (search, deactivate)
GET|POST|PUT|DELETE /api/projects/{id}/milestones[/{milestoneId}]
POST                /api/projects/{id}/milestones/{milestoneId}/complete | /verify
GET|POST|PUT|DELETE /api/projects/{id}/tasks[/{taskId}]

GET|POST|PUT        /api/projects/{id}/variations[/{variationId}]
POST                /api/projects/{id}/variations/{variationId}/submit | /approve | /reject | /void

GET  /api/projects/{id}/work-orders          existing work orders filtered by project   [projects, operations]
GET  /api/projects/{id}/materials            issues, requests, returns for the project  [projects, operations]
GET  /api/projects/{id}/financials           section 7 breakdown                        [projects, finance]
GET|POST|PUT /api/projects/{id}/budget-lines | /cost-entries                            [projects, finance]  (R2)
GET|POST|PUT /api/projects/{id}/diary[/{entryId}], POST …/{entryId}/submit             (R2)

GET|POST|PUT /api/projects/{id}/workers[/{workerId}]        roster                     (R2)
GET|POST     /api/projects/{id}/workers/{workerId}/rates  daily rates (office)        [projects, finance] (R2)
GET          /api/projects/{id}/muster?date=YYYY-MM-DD      day sheet, pre-filled      (R2)
PUT          /api/projects/{id}/muster?date=YYYY-MM-DD      submit marks for the day   (R2)
GET          /api/projects/{id}/wage-sheet?from&to          man-days × rate, CSV export [projects, finance] (R2)

GET|POST|PUT /api/projects/{id}/valuations[/{valuationId}]                              [projects, finance]  (R2)
POST         /api/projects/{id}/valuations/{valuationId}/submit | /certify | /raise-invoice | /void
POST         /api/projects/{id}/advance-invoice                                         [projects, finance]  (R2)
POST         /api/projects/{id}/retention-releases     { stage }                        [projects, finance]  (R2)

GET|POST     /api/tenant-invoices/{id}/withholding-certificates                         [finance]            (R2)
POST         /api/withholding-certificates/{id}/reverse                                 [finance]            (R2)

GET  /api/portal/projects                    the client's visible projects              [projects]
GET  /api/portal/projects/{id}               header, progress, milestones, approved variations, shared documents
GET  /api/portal/projects/{id}/valuations    certified valuations, invoices, retention  [projects, finance]  (R2)

POST /api/tenant-quotations/{id}/award-as-project                                      [projects, finance]
POST /api/projects/{id}/create-maintenance-contract                                    [projects, operations]

GET|PUT        /api/project-settings                 feature toggles, trades, visibility
GET|POST|PUT   /api/project-settings/types | /stages | /templates
```

Creating a work order, material request or invoice *for* a project reuses the existing endpoints with
a new optional `projectId` field. No duplicate create endpoints.

---

## 10. Permissions

New constants in `PermissionNames` (and `All`), mapped to roles through the existing
`RolePermission` / `UserPermission` admin UI:

| Permission | Grants |
|---|---|
| `projects.view` | Register and workspace (non-financial tabs) |
| `projects.manage` | Create/edit projects, members, consultant links and the consultant directory, milestones, tasks, stage changes |
| `projects.muster.mark` | Add workers to the roster and mark the daily muster roll (site supervisors). No access to rates |
| `projects.variations.submit` | Draft and submit variations |
| `projects.variations.approve` | Direct approval when no approval matrix applies |
| `projects.financials.view` | Financials tab, budget, margins, workers' daily rates and ID numbers, wage sheet |
| `projects.financials.manage` | Budget lines, cost entries, workers' daily rates, valuations, advance invoice, retention releases |
| `projects.close` | Close / reopen |
| `projects.settings` | Types, stages, templates, toggles |

Withholding certificates use the existing `finance.manage` permission. Portal access uses the existing
client-portal login; no new portal permissions.

Default role mapping on seed: admin = all; manager = all except settings; finance = view + financials;
dispatcher/viewer = view; technician = none (field staff reach projects through attendance, work orders
and the diary, gated by membership). The role-permission system is tenant-wide, so project-level actions use **two checks**: the user holds
the permission **and** has the right role on that project. For example, marking a project's muster roll
needs `projects.muster.mark` **and** a `Supervisor` (or `ProjectManager`) membership on that project. This
check is built once (`ProjectAccess.RequireRole(projectId, roles)`) and reused by every project
endpoint. Rates are never shown on the muster screen, whatever the user's permissions.

---

## 11. Notifications

New keys in `NotificationKeyDescriptor` (category "Projects"). They flow through the **existing**
channels: email outbox, web push queue, SignalR (`ServiceOpsHub`) and, for client-facing keys, WhatsApp
templates. Users control them through the existing `UserNotificationPreference` screen.

| Key | Recipients | Trigger |
|---|---|---|
| `project.member-added` | The member | Added to a project |
| `project.task-assigned` | Assignee | Task assigned |
| `project.task-due` | Assignee | 1 day before due (tenant-configurable) |
| `project.milestone-due` | PM | N days before planned date |
| `project.milestone-overdue` | PM, managers | Planned date passed, not complete |
| `project.variation-submitted` | Approvers (via the approval matrix, so existing reminders apply) | Submit |
| `project.variation-decided` | Submitter, PM | Approve / reject |
| `project.end-date-approaching` | PM | N days before planned end |
| `project.muster-missing` | Supervisor, PM | Muster roll not submitted by a configured time on an active working day |
| `project.valuation-certified` | PM, finance | Certified amount recorded (prompt to raise the invoice) |
| `project.retention-due` | PM, finance | Practical completion reached, and again when the defects liability period ends, with retention still held |

Date-based reminders run in a new `ProjectReminderWorker` using the existing `BackgroundWorkLease`
(the same pattern as `ApprovalReminderWorker`), with a sent-marker per (entity, key, date) for idempotency.

**Out of scope for this document:** a persisted in-app notification inbox (the bell) and a shared
Calendar/Meetings capability. Neither exists today (`InAppEnabled` is only a preference flag, and SignalR
is live-only). Both serve every module, not just Projects, and get their own design (Release 3).

---

## 12. Frontend

New feature folder `ecosys-serviceops-frontend/src/features/projects/`, following `features/contracts/`:

```
features/projects/
  api.ts, types.ts
  ProjectsPage.tsx            register: KPI strip + table + filters
  ProjectDetailPage.tsx       workspace with tabs
  components/
    OverviewTab, MilestonesTab, TasksTab, TeamTab, VariationsTab,
    WorkOrdersTab, MaterialsTab, MusterRollTab, ValuationsTab, FinancialsTab, DiaryTab, DocumentsTab
    ProjectForm, VariationForm, StageBadge, ProgressBar
features/settings/…/ProjectSettingsPage.tsx   types, stages, templates, toggles, trades
```

- **Nav:** "Projects" item in `shared/layout/navigation.ts`, `permission: 'projects.view'`, shown only
  when the tenant's modules include `projects`. It sits next to "Contracts", which keeps its current name.
- **Register KPIs:** active projects (stage category Active/Handover), total revised value, behind
  schedule (actual < planned progress), pending variation value, outstanding receivables (finance only).
- **Workspace header:** number, name, client · site, stage, revised vs. original value, progress
  (planned vs. actual), days remaining, PM, consultant.
- **Tabs** render only when their feature toggle is on **and** the tenant holds the module the tab needs.
- **Existing screens gain a Project field:** the work-order form, material request/issue form, attendance
  clock-in, expense form, supplier bill form, PO form, invoice form, and a "Project" column/filter on
  their lists.
- **Mobile:** the muster roll and diary are built mobile-first (supervisors on site, often on patchy
  data). The muster screen is one list with large Present / Half day / Absent buttons. Offline marking
  reuses the technician app's existing outbox (`features/tech/offline`), which already queues job
  actions and replays them with `Idempotency-Key` and `X-Occurred-At`; the muster save is one more kind
  of queued action, and the server upserts one mark per worker per day, so a replay is never duplicated.
  Offline is scoped to the muster roll only.
  The rest is responsive but office-first.

---

## 13. Releases

### Release 1: Projects core
Types, stages, templates, project CRUD, members, consultant directory and project links, milestones, tasks, weighted
progress, variations with approval, documents (attachments), `ProjectId` on `WorkOrder` and
`MaterialIssue`, register and workspace (Overview, Milestones, Tasks, Team, Variations, Work Orders,
Materials, Documents), permissions and project-role checks, licensing, notifications for members, tasks
and variations, client portal Projects page (progress, milestones, approved variations, shared documents).

*Accepted when:* a tenant can award a project to a client, plan weighted milestones, approve an
addition and an omission and see the revised value change only on approval, link work orders and
material issues, and a tenant without the `projects` module gets a 403 on every Projects endpoint.

### Release 2: Field and commercial
Worker roster and daily muster roll, wage sheet, technician clock-in against projects, material
requests/returns for projects, site diary, budget lines, cost entries, `ProjectId` on expenses, supplier
bills, POs and invoices, valuations (totals) with advance recovery, two-part retention release,
withholding certificates (finance-wide), portal valuations and invoices, offline muster marking, the
financials tab (section 7), award-from-quotation, handover → maintenance contract, closure checks,
milestone/deadline/muster/retention reminders.

*Accepted when:* the financial breakdown for a seeded project reconciles line by line against the
underlying issues, returns, muster roll, bills, expenses and invoices; and a valuation sequence
(advance, two interim certificates with materials on site, half retention at practical completion,
the balance at end of defects liability) produces the same amounts as a hand-worked example; and an
invoice part-settled by a 2% withholding certificate shows as fully paid.

### Release 3: Shared platform (separate design)
In-app notification inbox, Calendar & Activities (meetings, minutes, action items), WhatsApp project
reminders, cross-module role dashboards.

---

## 14. Testing

- **Tenant isolation:** for every new entity and every new `ProjectId` FK, cross-tenant IDs are
  rejected on write and invisible on read (existing tenant-isolation test patterns).
- **Licensing:** the controller-classification check (`TenantLifecycleConsistencyTests`, via `ServiceOpsPlans.IsClassified`) covers the new controllers. Route tests cover the
  `[projects, operations]` and `[projects, finance]` dependencies.
- **Domain:** variation state machine (only Approved moves values; void reverses; no negative revised
  value); progress calculation (weights, override, verified-only mode); closure checks.
- **Financials:** a fixture project with known issues, returns, muster marks, bills, expenses and
  invoices must produce exact expected figures. A new rate with a later effective date must not change
  earlier days' cost; days with no rate show as uncosted and are excluded from labour cost.
- **Valuations:** retention cap, advance recovery (stops at the advance amount, ignores materials on
  site), "previously certified" chaining, a certified amount that differs from the calculated one, and
  voiding a valuation that has already been invoiced (blocked).
- **Retention:** 50/50, 0/100 and 100/0 splits; releases never exceed retention held.
- **Withholding:** a certificate reduces the invoice balance like a payment; reversing it restores the
  balance; it can't exceed the invoice balance.
- **Variations:** two approvals at the same moment leave the totals equal to the `SUM` of approved rows.
- **Portal:** a portal user sees only their own client's visible projects; costs, rates, muster data,
  unapproved variations and unshared documents never appear in any portal response.
- **Muster roll:** one mark per worker per day; the muster API never returns or accepts rates; the lock point is enforced; a technician who clocked in
  and is also on the roster counts once.
- **Migrations:** additive only, nullable columns, applied against a copy of production-shaped data.
  Existing work-order, material and invoice tests pass unchanged.

---

## 15. Deliberately not doing

These ideas came up during brainstorming. They don't fit the existing codebase, so they are left out:

| Idea | Why not |
|---|---|
| Link projects to the Platform CRM | It isn't tenant-scoped; it's Ecosys's own sales pipeline (D1) |
| Rebuild AMC as a new module | `Contract`, PM plans and renewals already are the AMC module |
| Restructure into `Ecosys.Modules/*` | A rewrite with no user value; follow the existing layering |
| `/api/v1/...` routes, a separate worker process | Not the repo's conventions; hosted services + `BackgroundWorkLease` already cover jobs |
| Project-specific material, invoice or expense tables | Each would duplicate an existing ledger; use `ProjectId` on the existing rows. (The muster roll is the one exception: `AttendanceEntry` only covers technicians with logins, and casual workers don't have them, D8.) |
| Hourly labour costing; salaries for permanent staff | Work in Kenya isn't billed or costed per hour. The only rates in Ecosys are casual workers' daily rates, entered by the office (D6) |
| Payroll, statutory deductions, paying workers | Ecosys records what casuals are owed (wage sheet); paying them and payroll (PAYE, SHIF, NSSF, housing levy) stay outside |
| Bill of quantities held in Ecosys | Valuations are recorded as totals with the QS's BQ attached (D9) |
| eTIMS / KRA filing | Not needed for this module; withholding certificates are recorded, not filed (D10) |
| National ID numbers for workers | Dropped: no purpose that justifies holding them under the Data Protection Act |
| Gantt charts, dependencies, resource levelling | Primavera territory; revisit after real usage |
| Arbitrary typed-in progress percentage | Replaced by weighted milestones + audited override (D4) |
| Building on the `Ecosys.ServiceOps` project (`Customer`, its own `WorkOrder`) | Not referenced by the API or Infrastructure; appears to be legacy |

---

## Release 1: what was built

Built to sections 4–12 for Release 1, with these implementation choices:

- **Permissions** are capabilities (`projects.view`, `.manage`, `.variations.submit`, `.variations.approve`,
  `.close`, `.settings`) rather than new role-matrix columns. Role defaults live in code
  (`CapabilityRoleDefaults`: Manager gets all but settings; Dispatcher, Finance and Viewer get view;
  Admin gets everything). Tenants grant or deny them per user or per permission group in Access Control.
  Login and `/api/auth/me` return them as `user.capabilities`.
- **Licensing:** `projects` is an add-on key in `LicensePlan.ModulesIncluded`, usable on any plan
  (including Operations, Finance and Suite). No existing plan includes it.
- **Work orders and material issues** are linked to a project from the project workspace (link and
  unlink, same client only). The existing create forms don't have a Project field yet.
- **People picker:** `GET /api/projects/people` lists active users and technicians for team building,
  because the full user list is admin-only.
- **Project settings** live at `/projects/settings` (not under `/settings`, which is admin-only), so a
  manager granted `projects.settings` can use them.
- **Client portal:** `/api/portal/settings` gains `showProjects` (module licensed and portal page on);
  the portal Projects page and document download are in `PortalProjectsController`.
- **Notifications:** email and web push for member added, task assigned, variation submitted and
  variation decided. Decisions made from the Approvals inbox (matrix approvals) update the project but
  don't send the "decided" notification.

Not in Release 1, as planned: date-based reminders (`ProjectReminderWorker`), award from quotation,
handover → maintenance contract, and everything listed under Release 2.

---

## Release 2: what was built

Built to sections 5.6, 7, 8.2–8.5 and 11, with these implementation choices:

- **Site work** is a separate controller, `api/project-site/{projectId}/…` (workers, muster, diary) plus
  `GET api/project-site/mine` for the technician app. Writing needs `projects.manage`, or
  `projects.muster.mark` **and** a Supervisor or Project Manager place on that project's team. A caller
  with neither sees "not found", not the project. Technicians get `projects.muster.mark` by default.
- **No rates on site.** Roster and muster responses carry no rate or cost field (a test pins this).
  Daily rates are set by the office (`projects.financials.manage`) on the project's Labour & wages tab;
  a rate applies from its effective date until the next one, so a new rate never rewrites earlier days.
  Days with no rate in effect are counted as uncosted and flagged until a rate covers them.
- **Muster lock:** supervisors can correct marks up to `MusterLockDays` (default 1) after the work date.
  Later corrections need `projects.manage` and are audited as `projects.muster.corrected`.
- **Technician clock-in** accepts an optional project (the technician must be on its team and it must be
  open). A rostered technician who clocked in on the project is *suggested* Present on the day sheet; it
  counts only once the supervisor saves the sheet, so nobody is counted twice.
- **The money side** (`ProjectCommercialController`, `[projects, finance]`): billing terms, worker rates,
  wage sheet (JSON or CSV, up to three months, formula-safe), budget lines, cost entries, financials,
  valuations, advance invoice and retention releases. Reading needs `projects.financials.view`;
  changing needs `projects.financials.manage` (Finance and Manager roles by default).
- **D7 (materials)** is implemented through `MaterialIssue.ProjectId` (issued cost less returns on those
  issues). `PartsReturn` gets no `ProjectId`; `MaterialRequest` is unchanged.
- **Valuations:** one in progress at a time; each builds on the last certified one. Retention and
  advance terms lock once a valuation is certified. The certified amount is what's invoiced; "raise
  invoice" creates a **Draft** tax invoice (tenant VAT, client credit term) for finance to send. Only
  the latest valuation can be voided, and an invoiced one only after its invoice is voided; a
  valuation whose invoice was voided can raise a new one.
- **Retention:** two releases at most (unique per stage), each raising a draft invoice; a release whose
  invoice was voided can raise a fresh one. Releases are allowed after closure, since the money is owed
  whatever the stage.
- **Withholding certificates** work on every tax invoice (not only projects), from the invoice page.
- **Award from quotation** is `POST /api/projects/from-quotation/{quotationId}` (Accepted quotations
  only; the quotation becomes Converted). **Handover → AMC** is
  `POST /api/projects/{id}/create-maintenance-contract`: a Draft contract covering the project site and
  the assets created there during the project.
- **Closure** is also blocked by a valuation in progress or a certified valuation not yet invoiced, and,
  when the tenant sets `ClosureUnbilledTolerancePercent`, by uninvoiced value above that share. The
  tolerance is off by default, for tenants that bill projects outside Ecosys.
- **Reminders:** `ProjectReminderWorker` (every 15 minutes, under the background-work lease) sends
  task-due, milestone-due, milestone-overdue, end-date-approaching, muster-missing (Monday to Saturday,
  after `MusterReminderHour`, active projects with workers) and retention-due, once per entity, key and
  date (`ProjectReminderLog`). `project.valuation-certified` is sent when a certificate is recorded.
- **Existing forms** (work order, material issue, expense claim, supplier bill, purchase order, invoice)
  take an optional Project, checked to be open and, where the record has a client, the same client.
- **Portal:** `GET /api/portal/projects/{id}/valuations` returns certified valuations, the project's
  non-draft invoices and retention held, when Finance is licensed and the portal shows invoices.

Known limits: voiding an invoice doesn't reverse its allocations (existing behaviour, unchanged); the
muster reminder doesn't know about public holidays.
