# ServiceOps provenance

This repository is a fork of [Ecosys ServiceOps](https://github.com/Dennis-debug-it/ecosys-serviceops), merged with its full git history so platform fixes can be ported with `git cherry-pick`. See [scope.md §1.4](scope.md#14-how-the-code-is-shared) for the fork rules.

## Fork point

| | |
|---|---|
| Source | `Dennis-debug-it/ecosys-serviceops` `main` |
| Commit | `248e221f421fe12fdeff0cc8365d8d80524f5d71` (2026-11-01, "Merge pull request #17") |
| Forked | 2026-11-03 |

ServiceOps design, audit and status documents that don't describe this product were moved to [`docs/serviceops/`](serviceops/) for reference. Deployment, VPS and WhatsApp setup guides stay in `docs/` because they still apply here.

## Ported fixes

Record each fix brought across from ServiceOps (or sent back to it) after the fork.

| Date | Direction | ServiceOps commit | This repo commit | What |
|---|---|---|---|---|
| | | | | |

## Pruned modules

Updated as each out-of-scope module is removed in Phase 0.

| Module | Removed in commit | Notes |
|---|---|---|
| Technician location (GPS on arrival/departure, location prompt) | `58e574c` | Migration `RemoveTechnicianLocationTracking`. Patrol and fleet GPS kept. |
| Clients, sites, contracts, client wizard, client/site imports | `2c949a3` | Screens and APIs gone. |
| Rate cards, preferential rates | `2c949a3` | Exchange rates kept (spares purchasing). |
| Client ETA emails, public job tracking page | `2c949a3` | Migration `RemoveServiceOpsLeftovers` drops `eta_notification_settings` and `work_orders.TrackingToken`. |
| Technician travel steps (On My Way / Arrived / Departure), site visits | `2c949a3` | Arrival no longer required to complete. |
| Work order map view, inventory Locations page, "Waiting on client" hold | `2c949a3` | |
| Client, site, system and contract tables and columns | `61fd40e` | Migration `RemoveClientsSitesContracts`. Work orders, assets, PM plans and material issues no longer carry client/site/system ids. |
| Email intake (mailbox, intake rules, licence feature) | `61fd40e` | Monitoring webhooks kept. |
| Client sign-off, report acknowledgement and emailing reports to clients, client-visible notes, client contacts on meetings | `61fd40e` | Only the technician signs a job; reports go Draft → Under Review → Approved. |
| Site visits, arrival/departure stamps, timesheet travel breakdown | `61fd40e` | `work_order_visits` and the arrival/departure columns dropped. |
