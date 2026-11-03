# Critical closure execution register

Started 9 September 2026; resumed 10 September 2026 after usage interruption.

10 September checkpoint: all three worker agents were interrupted again by the account usage limit. Partial edits are preserved and unverified; use the token-efficient queue in the deployment plan to resume without repeating discovery. Migration discovery tests, portal tests, portal controller/service edits, material controller edits, and dashboard filter/test edits are present. Local PostgreSQL setup files exist, but readiness and rehearsal results have not yet been verified by the coordinator.

Scope: [closure plan](CRITICAL_ITEMS_CLOSURE_PLAN_2026-09-09.md) and [agent deployment plan](AGENT_DEPLOYMENT_PLAN_2026-09-09.md).

Baseline commit: `799551a82634e5093b3f1ad714451ea04e6624aa`, plus pre-existing uncommitted changes. Recoverable local baseline: `tmp/critical-closure-baseline-20260909` (patches, status and copies of changed/untracked files). This is not a production backup.

## Verified baseline

- Frontend lint passed.
- Backend: 246 passed, 1 failed, 0 skipped. Failure: `Swagger_IsNotExposedOutsideDevelopment` could not connect to PostgreSQL at loopback port 5432.
- Results: `tmp/critical-closure-tests/closure-baseline.trx`.
- No production deployment or live email verification has occurred.

## Active assignments

| Finding | Agent | Current status | Owned implementation | Next gate |
|---|---|---|---|---|
| CL-01 | environment | In progress | September finance/email migration metadata; MigrationDiscoveryTests; isolated local PostgreSQL setup | Discovery/SQL tests and fresh-database migration rehearsal |
| CL-02/03 | access | In progress | PortalAuthController, PortalAuthService and dedicated portal tests | Trusted link origin, atomic consumption and active-contact validation |
| CL-04 | coordinator | In progress | DashboardController and dashboard regression coverage | Active workload excludes terminal orders and other tenants |
| CL-05 | operations audit / coordinator integration | Open | No new assigned edits | Revalidate complete creation/execution journey |
| CL-06 | operations | In progress | MaterialRequestsController, MaterialIssuesController and dedicated inventory tests | Valid transitions, scoped writes, no repeated deduction/return, usage reconciliation |
| CL-07 | access audit | Open | No new delivery edits assigned | Validate intake ownership/replay and real delivery |
| CL-08 | operations audit | Open | No offline edits assigned | Persistent scoped queue and replay currently absent in source audit |
| CL-09 | operations audit | Open | No broad module edits assigned | Finish route/action inventory and validate remaining report gaps |
| CL-10 | coordinator | In progress | Shared test orchestration and independent verification | Full required checks on integrated candidate |

Only the coordinator edits shared startup, DI, model/snapshot, routing, auth-state and test configuration unless explicitly handing off exclusive ownership. Backend build/test runs are serialized; environment agent currently owns the test slot for migration discovery.

## Dependencies and evidence limits

- Installed PostgreSQL 18 binaries are available, but no database was listening during the baseline. An isolated workspace cluster is being prepared; existing installed database data is untouched.
- Actual staging/production target, provider configuration and controlled live mailbox details remain unprovided. Local fixes and tests continue; production closure remains pending.
- Source audit findings are not runtime proof. No finding is Closed at this point.
