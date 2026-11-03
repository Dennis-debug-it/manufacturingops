# PM Auto-Scheduling — Verified Status

**Date verified:** 2026-06-03  
**Audit finding:** Complete and operational (Phase 4 audit)

---

## Summary

PM auto-scheduling is **fully implemented and registered**. The background service runs
automatically once the API starts, targeting the current `AppDbContext` and
`Ecosys.Domain.Entities` domain model.

---

## Active implementation

| Item | Detail |
|------|--------|
| **File** | `backend/src/Ecosys.Infrastructure/Services/PmSchedulerWorker.cs` |
| **Class** | `PmSchedulerWorker : BackgroundService` |
| **Registered in DI** | Yes — `services.AddHostedService<PmSchedulerWorker>()` in `DependencyInjection.cs` line 68 |
| **Schedule** | Waits until 06:00 UTC on first run, then every 24 hours via `PeriodicTimer` |
| **Trigger condition** | `PreventiveMaintenancePlan.Status == "Active"` AND `NextPmDate` is within `DaysBeforeDue` days |
| **Duplicate guard** | Skips plan if an open (non-Completed/non-Cancelled) WO already exists for that plan |
| **Checklist copy** | Copies all `PmTemplate.Questions` as `WorkOrderChecklistItem` rows on the generated WO |
| **WO number** | Uses `NumberingSettings` per tenant, falls back to `WO-000001` format |

---

## What triggers generation

```
plan.NextPmDate.HasValue
  AND today >= (plan.NextPmDate - plan.DaysBeforeDue)
  AND no open WO exists for plan.Id
```

---

## Stale files (dead code, do not register)

Two old-domain stubs exist in `backend/src/Ecosys.Infrastructure/Hosted/` targeting
the deprecated `EcosysDbContext`. They are wrapped in `#if NEVER` and are **not**
registered in DI. Run `docs/CLEANUP_MANUAL_STEPS.ps1` to delete them.

| Stale file | Replacement |
|-----------|-------------|
| `Hosted/PreventiveMaintenanceHostedService.cs` | `Services/PmSchedulerWorker.cs` |
| `Hosted/SlaMonitorHostedService.cs` | `Services/SlaEnforcementWorker.cs` |

---

## Known gap

`PmSchedulerWorker` updates `plan.LastGeneratedAt` but does **not** automatically
advance `plan.NextPmDate` after generation. The manual "Generate work order" action
in `PreventiveMaintenancePage.tsx` → `PreventiveMaintenanceController.cs` advances
`NextPmDate` based on `ServiceIntervalMonths`. The background worker depends on the
plan's existing `NextPmDate` — if nobody calls the manual endpoint to advance it,
the same plan will be skipped on subsequent daily runs (because the open WO check
prevents duplicates, but after the WO is closed, `NextPmDate` remains in the past
and a new WO will be generated immediately on the next cycle).

**Recommended fix (Phase 2):** After generating a WO, advance `plan.NextPmDate` by
`plan.ServiceIntervalMonths` inside `PmSchedulerWorker.RunCycleAsync()`.
