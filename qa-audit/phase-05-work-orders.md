# Phase 5 — Work Orders (core module)

Tested live + source review, 2026-08-02. 8 WOs in tenant (WO-001-0000xx).

## Positives (verified, strong)
- **Formal state machine** (`WorkOrderStatusTransitions.BaseTransitions`): terminal states (Closed/Cancelled/Rejected) are dead-ends; `Assigned` cannot jump straight to `Completed` (must pass `InProgress`); `Completed → {ClientAccepted, Reopened, Closed}` only.
- **Central guard** `TransitionAsync` → `CanTransition(current, target, admin)`; invalid transitions throw `BusinessRuleException("Cannot transition … from X to Y")`. Re-completing fails (no double-complete).
- **Start guard**: requires `CanCompleteWorkOrders` permission, assigned-execution access, branch scope, AND at least one technician accepted before `In Progress`.
- **Complete guard**: requires work-done notes and a recorded arrival before completion.
- **Admin-only transitions** (e.g. reopen from Closed) gated by `CanUseAdminOnlyLifecycleTransitions`.
- Multi-view UI (Table/Kanban/Calendar/Map), Report Queue, bulk assign/close, duplicate, job card, PM template attach, events/audit trail, notes/comments — all present.
- Branch-level access scoping (`WhereAccessible(scope)`) layered on top of tenant scoping.

## Findings
- No correctness defects found in the state machine on review. (No Critical/High here.)

### F5-01 — LOW — Verify concurrency on parallel status transitions
- **Category**: Concurrency (to confirm)
- The transition reloads the WO then checks `CanTransition`; two simultaneous transitions could both pass the check before either saves if there is no row version/optimistic concurrency token. Verify `WorkOrder` has a `xmin`/rowversion concurrency token, else a double-submit race could double-fire side effects (emails, CSAT).
- **Fix**: Ensure optimistic concurrency (EF `IsRowVersion`/PostgreSQL xmin) on `WorkOrder.Status`.

## Not exhaustively tested live
- Evidence/photos/signatures upload validation (file-upload security) — carry to a dedicated pass.
- SLA deadline calculation accuracy across hold windows.
