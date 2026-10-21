# Ecosys Projects — Module Design

**Status:** Draft for review. No code has been written against this design.
**Scope:** A new, tenant-configurable **Projects** module for finite work (installations, fit-outs,
rollouts, engagements) that sits beside the existing **Contracts (AMC)** module and reuses the
existing clients, work orders, inventory, attendance, finance, approvals and notification plumbing.

---

## 0. Decisions needed before coding

These are the questions that change the data model. Each has a recommended default; the rest of the
document assumes the default.

| # | Decision | Recommended default | Alternative |
|---|---|---|---|
| D1 | Which record is the project's "client"? | **`Client`** (tenant-scoped, already used by contracts, work orders, invoices). The Platform CRM (`PlatformLead`, `CrmContact`) has no `TenantId` and is Ecosys's own sales CRM — it must not hold tenants' customers. | Make the Platform CRM multi-tenant (not recommended: mixes Ecosys sales data with tenant data). |
| D2 | How are consultants modelled? | A consultant is a **`Client` with `ClientType = "Consultant"`**, linked to projects through `ProjectParty`. No new organisation table; consultants get sites/contacts/portal for free. | A separate `Consultant` entity. |
| D3 | Licensing | New commercial module key **`projects`**, sold on its own or with Operations/Finance. Tabs that touch inventory/work orders require `operations`; financial tabs require `finance` (same pattern as `WorkOrderBilling` requiring both today). | Bundle Projects into `operations`. |
| D4 | Progress method | **Weighted milestones** (weights default to equal), with a permission-gated manual override that is recorded and shown as "overridden". | Task-count based; or manual only. |
| D5 | Variation approval | Reuse the **Approval Matrix** with a new document type `ProjectVariation` (amount bands, permission/user approvers, reminders already exist). | A simple single-approver flag on the variation. |
| D6 | Labour cost | Labour cost = attendance hours on the project × a **cost rate per technician** (new nullable `Technician.HourlyCostRate`). Without a rate, labour is shown as hours only. | Labour entered manually as a cost line. |
| D7 | Material requests for projects | Allow `MaterialRequest` to target a project directly (make `WorkOrderId` nullable, add `ProjectId`, require exactly one). | Force every project material request through a work order. |

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
                    ┌──────────── Client (+ Sites, Consultants as Clients) ────────────┐
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
- New controllers (`Projects`, `ProjectSettings`) go in a new `ProjectsControllers` set, so
  `ServiceOpsModuleClassificationTests` keeps catching unclassified controllers.
- Route-level dependencies (in `RequiredModules`):
  - `/api/projects/{id}/materials*`, `/work-orders*`, `/attendance*` → `[projects, operations]`
  - `/api/projects/{id}/financials*`, `/invoices*` → `[projects, finance]`

### 4.2 Feature toggles

Stored as `TenantModuleSetting` with `ModuleKey = "projects"`:

```json
{
  "milestones": true,
  "tasks": true,
  "variations": true,
  "siteDiary": false,
  "materials": true,
  "attendance": true,
  "financials": true,
  "documents": true,
  "progressMethod": "WeightedMilestones",
  "allowManualProgressOverride": true
}
```

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

### 5.3 People and parties

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

// External organisations on the project besides the client (D2).
public sealed class ProjectParty : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid ClientId { get; set; }                 // a Client with ClientType Consultant/Subcontractor/…
    public string Role { get; set; } = "Consultant";   // Consultant | MainContractor | Subcontractor | Other
    public string? ContactName { get; set; }
    public string? ContactPhone { get; set; }
    public string? ContactEmail { get; set; }
}
```

Project-scoped access: a user with `projects.view` sees all projects unless the tenant enables
"members only" visibility, in which case non-managers see only projects where they are a `ProjectMember`.

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
- Only `Approved` variations change `ApprovedAdditions` / `ApprovedOmissions`, inside the same
  transaction as the status change.
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
    public int? HeadcountOverride { get; set; }         // otherwise derived from attendance for the date
    public DateTime? SubmittedAt { get; set; }          // locked once submitted; corrections are new entries
}
```

Photos are `Attachment` rows with `EntityType = "ProjectDiaryEntry"`. Workers present and materials
used that day are **derived** from `AttendanceEntry` and `MaterialIssue` for the project and date, not
re-entered.

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
| `AttendanceEntry` | `+ ProjectId` | Clock in "on site" for a project (alongside the existing `CurrentWorkOrderId`) |
| `Technician` | `+ HourlyCostRate` (decimal?) | Labour costing (D6); visible only with finance permission |
| `Expense` | `+ ProjectId` | Staff claims charged to a project |
| `SupplierBill` | `+ ProjectId` | Subcontractor and supplier costs |
| `PurchaseOrder` | `+ ProjectId` | Committed (not yet billed) cost |
| `TenantQuotation` | `+ ConvertedToProjectId` | "Award as project", mirroring `ConvertedToWorkOrderId` |
| `TenantInvoice` | `+ ProjectId` | Progress / milestone / final invoices against a project |
| `Contract` | `+ SourceProjectId` | AMC created at handover |
| `ApprovalDocumentTypes` | `+ ProjectVariation` | Variation approvals |
| `NumberingSetting` defaults | `+ Project (PRJ)`, `+ ProjectVariation (VO)` | Document numbers |

---

## 7. Commercial calculations

All amounts exclude tax, in the project currency. Each figure names its source, so numbers can be
traced on screen.

| Figure | Formula / source |
|---|---|
| Revised value | `OriginalValue + ApprovedAdditions − ApprovedOmissions` |
| Pending variations | Sum of `Submitted` variations (shown separately, never added to the revised value) |
| Materials cost | Σ `MaterialIssue.TotalIssuedCost` (status Issued, `ProjectId` = p) − value of `PartsReturn` for p |
| Labour cost | Σ attendance hours on p (clock-in to clock-out minus breaks) × `Technician.HourlyCostRate` |
| Subcontractor / supplier cost | Σ `SupplierBill.Amount` with `ProjectId` = p |
| Expenses | Σ `Expense.Amount` with status Approved or Reimbursed and `ProjectId` = p |
| Other costs | Σ `ProjectCostEntry.Amount` |
| **Actual cost** | Sum of the five lines above |
| Committed cost | Open `PurchaseOrder` value for p not yet billed |
| Invoiced | Σ `TenantInvoice.TotalAmount − TaxAmount` (not Void) with `ProjectId` = p |
| Received | Σ allocated `TenantPayment` against those invoices |
| Outstanding | Σ `TenantInvoice.Balance` for those invoices; overdue = past `DueDate` |
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

### 8.2 Award from quotation

On an Accepted `TenantQuotation`: "Award as project" creates the project with client, site, original
value (quotation subtotal) and `SourceQuotationId`, and sets `ConvertedToProjectId` on the quotation.
This follows the existing convert-to-work-order pattern and requires `[projects, finance]`.

### 8.3 Handover → AMC

"Create maintenance contract" opens the existing contract form, prefilled with: client, project site(s),
assets created during the project (assets whose `SiteId` matches and `CreatedAt` falls within the
project), start date = handover date. It saves a **Draft** `Contract` with `SourceProjectId`; everything
after that is the existing contract workflow. Requires `operations` and contract permissions.

### 8.4 Site attendance

Technicians clock in through the existing attendance flow, with an optional "Project" picker (projects
where they are a member and the stage category is Active or Handover). `AttendanceAutoClockOutWorker`
is unchanged.

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
GET|POST|PUT|DELETE /api/projects/{id}/parties[/{partyId}]
GET|POST|PUT|DELETE /api/projects/{id}/milestones[/{milestoneId}]
POST                /api/projects/{id}/milestones/{milestoneId}/complete | /verify
GET|POST|PUT|DELETE /api/projects/{id}/tasks[/{taskId}]

GET|POST|PUT        /api/projects/{id}/variations[/{variationId}]
POST                /api/projects/{id}/variations/{variationId}/submit | /approve | /reject | /void

GET  /api/projects/{id}/work-orders          existing work orders filtered by project   [projects, operations]
GET  /api/projects/{id}/materials            issues, requests, returns for the project  [projects, operations]
GET  /api/projects/{id}/attendance?from&to   sessions on the project                    [projects, operations]
GET  /api/projects/{id}/financials           section 7 breakdown                        [projects, finance]
GET|POST|PUT /api/projects/{id}/budget-lines | /cost-entries                            [projects, finance]  (R2)
GET|POST|PUT /api/projects/{id}/diary[/{entryId}], POST …/{entryId}/submit             (R2)

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
| `projects.manage` | Create/edit projects, members, parties, milestones, tasks, stage changes |
| `projects.variations.submit` | Draft and submit variations |
| `projects.variations.approve` | Direct approval when no approval matrix applies |
| `projects.financials.view` | Financials tab, budget, margins, labour rates |
| `projects.financials.manage` | Budget lines, cost entries |
| `projects.close` | Close / reopen |
| `projects.settings` | Types, stages, templates, toggles |

Default role mapping on seed: admin = all; manager = all except settings; finance = view + financials;
dispatcher/viewer = view; technician = none (field staff reach projects through attendance, work orders
and the diary, gated by membership).

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
    WorkOrdersTab, MaterialsTab, AttendanceTab, FinancialsTab, DiaryTab, DocumentsTab
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
- **Mobile:** the diary and attendance flows are built mobile-first (supervisors on site). The rest is
  responsive but office-first.

---

## 13. Releases

### Release 1: Projects core
Types, stages, templates, project CRUD, members, parties (consultants), milestones, tasks, weighted
progress, variations with approval, documents (attachments), `ProjectId` on `WorkOrder` and
`MaterialIssue`, register and workspace (Overview, Milestones, Tasks, Team, Variations, Work Orders,
Materials, Documents), permissions, licensing, notifications for members, tasks and variations.

*Accepted when:* a tenant can award a project to a client, plan weighted milestones, approve an
addition and an omission and see the revised value change only on approval, link work orders and
material issues, and a tenant without the `projects` module gets a 403 on every Projects endpoint.

### Release 2: Field and commercial
Site attendance against projects, material requests/returns for projects, site diary, budget lines,
cost entries, `ProjectId` on expenses, supplier bills, POs and invoices, the financials tab (section 7),
award-from-quotation, handover → maintenance contract, closure checks, milestone/deadline reminders.

*Accepted when:* the financial breakdown for a seeded project reconciles line by line against the
underlying issues, returns, attendance, bills, expenses and invoices.

### Release 3: Shared platform (separate design)
In-app notification inbox, Calendar & Activities (meetings, minutes, action items), WhatsApp project
reminders, cross-module role dashboards.

---

## 14. Testing

- **Tenant isolation:** for every new entity and every new `ProjectId` FK, cross-tenant IDs are
  rejected on write and invisible on read (existing tenant-isolation test patterns).
- **Licensing:** `ServiceOpsModuleClassificationTests` covers the new controllers. Route tests cover the
  `[projects, operations]` and `[projects, finance]` dependencies.
- **Domain:** variation state machine (only Approved moves values; void reverses; no negative revised
  value); progress calculation (weights, override, verified-only mode); closure checks.
- **Financials:** a fixture project with known issues, returns, attendance, bills, expenses and invoices
  must produce exact expected figures.
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
| Project-specific attendance, material, invoice or expense tables | Each would duplicate an existing ledger; use `ProjectId` on the existing rows |
| Gantt charts, dependencies, resource levelling | Primavera territory; revisit after real usage |
| Arbitrary typed-in progress percentage | Replaced by weighted milestones + audited override (D4) |
| Building on the `Ecosys.ServiceOps` project (`Customer`, its own `WorkOrder`) | Not referenced by the API or Infrastructure; appears to be legacy |
