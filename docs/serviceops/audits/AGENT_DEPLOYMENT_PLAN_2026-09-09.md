# Agent deployment plan for critical audit closure

Date: 9 September 2026. Status: ready-to-dispatch plan; implementation agents have not been launched by this planning task.

Authority: [critical-items closure plan](CRITICAL_ITEMS_CLOSURE_PLAN_2026-09-09.md). Preserve its acceptance criteria and consolidated audit scope. Resolve CL-01 through CL-10; do not assume historical findings still reproduce or classify existing code as verified.

## Token-efficient execution adjustment — 10 September

Objective: maximize verified closure per token. This changes dispatch granularity, not closure criteria. Preserve completed and partial work; do not repeat Wave 0 or regenerate the full audit.

Use three implementation lanes only when three independent tickets are ready. Otherwise run fewer workers. For new specialist assignments, use a fresh subagent with `fork_turns: none` and a compact task packet, rather than copying the whole conversation. Reuse an existing agent for a directly related continuation when its retained context is useful; do not repeatedly send it the same source files or plan. Keep the configured model unless the user explicitly requests a model change.

Each packet contains only: CL ID, one concrete outcome, exact owned files, current implementation state, acceptance checks, dependencies, and evidence destination. Target a 300–600-word packet. Agents read the relevant code and closure criteria, not every audit and every plan. Read shared instructions once, narrow searches to owned areas, and return changed facts rather than full logs. Target a handoff under 200 words plus evidence paths; these are communication targets, not limits on necessary reasoning or implementation.

### Next dispatch queue

| Order | Lane / bounded ticket | Starting state to preserve | Required output |
|---|---|---|---|
| 1 | Data: finish isolated PostgreSQL startup and migration rehearsal | Migration metadata and discovery test exist; local cluster/config files exist under tmp/critical-closure-postgres; readiness unverified | Connection-ready local DB, discovery/upgrade results or exact blocker; never print credentials |
| 1 | Access: finish portal hardening verification | Portal controller/service and PortalAuthSecurityTests already changed | Review partial edits, prove origin and token rejection; relational single-use test once DB ready; active-contact guard integration request |
| 1 | Inventory: finish material lifecycle patch | MaterialRequestsController and MaterialIssuesController have partial changes | Complete quantity/state/branch/replay tests and check required frontend return payload; no rewrite of finished changes |
| 2 | Coordinator: integrate and run one backend batch | Dashboard active-order filter and regression test already added | One build and focused test batch for all three lanes plus dashboard; route failures only to the owner |
| 3 | Access: intake ownership and replay | Source finding recorded, implementation not started | One isolated intake ticket; no additional broad auth audit |
| 3 | Delivery: recover stranded outbox sends | Source finding recorded; intended retry semantics need inspection | Bounded recovery/idempotence implementation and crash/retry tests |
| 3 | Operations: verify work-order creation and execution | Historical placeholders largely replaced | Reproduce exact remaining failure, then fix only that workflow; evidence if already working |
| 4 | Offline: split CL-08 into queue storage, replay, and UI integration | Queue writer/replay absent in baseline inspection | Sequential independently testable slices; shared auth/work-order integration by coordinator |
| 5 | Module gaps and final verification | Use existing route inventory | Only confirmed missing release-critical behaviours, then independent verification and release evidence |

One worker owns each changed file at a time. The coordinator maintains the compact register and combines compatible tests into one run. Do not ask every agent to build the whole solution, run lint, or restate all findings. Use focused tests after each meaningful fix and the full suite at the integrated candidate gate. Test failures return as a short reproduction with relevant output only. Independent verification remains required, but reviewers inspect the diff and acceptance evidence rather than repeat the initial audit.

When a usage limit interrupts work, checkpoint changed files, unrun tests and the exact next action. Resume that ticket once capacity is available; do not repeatedly relaunch failing agents or purchase/redeem usage without explicit authorization. Production-dependent checks remain pending until access is available.

## Operating model

Use one coordinator and at most three worker agents concurrently, matching the four available agent slots. Specialist names below describe assignments across waves, not ten simultaneous agents. Use subagents within this task; no separate user-owned tasks are needed.

The coordinator owns the finding register, dependency ordering, shared-file integration, environment access, final regression and release evidence. Workers reproduce findings, implement bounded fixes, run focused tests and hand back evidence. A different worker or the coordinator verifies each implementation before it progresses to staging verification. No agent closes its own finding based solely on its implementation report.

The existing workspace contains extensive uncommitted work. Before edits, capture file status and a recoverable baseline of existing changes, including untracked source. Do not reset, discard, blindly overwrite, or commit unrelated work. All agents share this directory: exclusive file ownership is mandatory. If isolated worktrees become necessary, they must include the required existing changes; a clean checkout of HEAD alone is not a valid baseline.

## Dispatch waves

| Wave | Worker slot 1 | Worker slot 2 | Worker slot 3 | Coordinator work and exit gate |
|---|---|---|---|---|
| 0 — baseline | **Environment auditor:** CL-01; inspect runtime, PostgreSQL, migration history and recovery requirements. | **Access auditor:** CL-02/03/07; identify current auth, portal, entitlement and email gaps. | **Operations auditor:** CL-04/05/06/08/09; inventory workflows, placeholders, PWA and API contracts. | Preserve working tree, map every closure criterion to a ticket, discover test entry points and record baseline results. Read-only worker assignments. Exit: reproduced gaps, precise file ownership, environment blockers and estimated implementation batches. |
| 1 — foundations | **Data engineer:** CL-01; repair demonstrated migration/startup issues and prepare repeatable non-production upgrade/restore checks. | **Access engineer:** CL-02/03; auth, tenant isolation, entitlements, public leads and portal sessions. | **Dashboard engineer:** CL-04; correct metrics, query behaviour and visible error states. | Integrate shared contracts, prepare two-tenant/role/plan fixtures and run baseline checks on one controlled test environment. Exit: foundation tests pass; unfinished external checks explicitly blocked. |
| 2 — operations and delivery | **Work-order engineer:** CL-05; client/site/asset links, creation, dispatch and execution/report workflow. | **Inventory engineer:** CL-06; material requests, approval, issue/return and concurrency protection. | **Delivery engineer:** CL-07; outbox, notifications, intake, recipient rules and financial document dispatch. | Freeze interfaces between the three lanes before edits. Integrate work-order materials UI and other shared files. Exit: focused real-backend tests pass and complete operational journey is testable. |
| 3 — offline and remaining scope | **Offline engineer:** CL-08; shipped worker, queue persistence/replay, conflicts, session and cache isolation. | **Tenant-module engineer:** CL-09 tenant settings, PM, reports, team and remaining tenant workflows. | **Platform/portal engineer:** CL-09 platform settings, administration and remaining portal workflows. | Divide route inventory into small tickets; integrate router, navigation and shared components. Exit: every promised route/action has implementation and evidence; offline behaviour is tested using built assets. |
| 4 — independent verification | **Security verifier:** CL-02/03/07/08 negative authorization, token, recipient and cache-isolation tests. | **Workflow verifier:** CL-04/05/06/09 desktop/mobile journeys, counts, stock and persistence. | **Release verifier:** CL-01/10 migration/recovery evidence, test completeness and configuration review. | Freeze candidate; run complete backend/frontend/browser gates sequentially where they share resources. Assign verifiers to code they did not implement. Exit: all required checks pass on the same candidate; no unresolved release-critical defects. |
| 5 — release and closure | **Deployment assistant:** prepare/review release artifacts and execute the established deployment process when authorized and accessible. | **Production verifier:** safe-account smoke tests against the deployed candidate. | **Evidence reviewer:** reconcile each closure criterion with production results and outstanding dependencies. | Coordinate release owner, backup, health checks and recovery readiness; arrange the closure plan's 24–48-hour observation with an actual owner or separately authorized monitoring. Close findings individually after evidence passes. |

Waves are dependency gates, not calendar promises. Within a wave, dispatch only three independent bounded assignments. Start an unblocked ticket early when its prerequisites and file ownership are settled. An unavailable production target must not stop useful local fixes; it does prevent production closure. Re-estimate the original three-week planning envelope after Wave 0, based on remaining work and external access rather than assumed agent speed.

## Ownership and integration rules

Before each dispatch, resolve actual current paths and attach an exact editable-file list. Folder names below describe scope, not unrestricted write access.

- **Data lane:** migrations, startup and database tooling. Historical applied migrations remain immutable; review existing modifications and use new corrective migrations when appropriate.
- **Access lane:** authentication, permissions/entitlements, onboarding, leads and portal login. Queue email-service changes for the delivery lane and shared auth-state changes for the coordinator.
- **Dashboard lane:** dashboard controller, page and dedicated tests.
- **Work-order lane:** work-order, client/site/asset creation and execution components, controllers and dedicated tests. Inventory owns stock/request rules; delivery owns sending reports/documents.
- **Inventory lane:** material requests, approvals, issue/return and stock services. Hand shared WorkOrderDetailPage integration to the coordinator while the work-order lane edits it.
- **Delivery lane:** outbox, sender settings, intake and financial recipients/documents. Do not concurrently edit auth-token lifecycle or inventory services.
- **Offline lane:** PWA configuration, worker, local database and queue processor. Work-order hooks and authentication cache cleanup are coordinator-integrated if shared.
- **Module lanes:** exact tenant or platform/portal pages assigned from the Wave 0 inventory. No blanket licence to rewrite a whole module or remove existing functionality.

Coordinator-only shared files include application startup/DI, AppDbContext and model snapshot, route tables, API endpoint catalog, global auth/permission/entitlement hooks, shared navigation, dependency manifests/lockfiles and test configuration. A worker can request a temporary exclusive handoff for a specific shared file; otherwise it supplies the required change description for coordinator implementation. Maintain one migration author at a time.

No concurrent tests against the same mutable database or fixed-port application instance. Use separate databases/ports where practical; otherwise the coordinator schedules runs. Focused tests accompany each fix; full-suite runs occur at integration gates rather than being duplicated by every worker.

## Worker assignment template

Each dispatch must include:

1. **Finding and outcome:** closure IDs, bounded defect and exact acceptance criteria copied from the closure plan.
2. **Starting state:** current baseline reference, existing relevant changes and prerequisite contracts.
3. **Ownership:** exact editable files, coordinator-owned files and prohibited overlap with active workers.
4. **Method:** reproduce first; preserve working behaviour; implement the smallest complete fix; follow applicable repository instructions; avoid unrelated refactors.
5. **Validation:** focused tests including failure/authorization cases; specify whether a real backend, PostgreSQL, built browser assets or controlled mailbox is required.
6. **Handoff:** changed files, root cause, behaviour before/after, test command/results, environment, remaining risks, dependencies and evidence paths. Separate implemented from verified.
7. **Escalation:** report contract conflicts or unavailable external access immediately; continue independent authorized work. Never invent credentials, waive closure criteria or silently replace a real integration check with a mock.

Keep tickets small enough for one independently reviewable outcome. Split CL-05/07/09 by workflow after inventory; do not dispatch “fix all remaining frontend issues” as one assignment.

## Evidence and progress control

The coordinator maintains a register with: CL ID, sub-ticket, agent, owned files, status, prerequisite, candidate reference, evidence path, independent verifier and next action. Preserve the closure plan's statuses: Open → In progress → Implemented / awaiting verification → Verified in staging → Closed after release verification. Blocked entries state the exact missing dependency and owner.

Store evidence under `docs/audits/evidence/critical-closure/`, grouped by finding and candidate; redact secrets and personal data. Link reproducible results rather than copying large raw logs into the register. Record the commit plus a diff/artifact identity while work remains uncommitted; final release evidence must identify the exact candidate.

After each wave, report completed implementations, independently verified findings, failed checks, external blockers and the next three assignments. If verification fails, return a minimal reproduction to the owning worker, then have the independent verifier rerun the affected checks. Do not restart the entire audit after each fix.

## Completion and external dependencies

The agents can complete source fixes, regression coverage, local environment setup where available, release preparation and documented evidence. Actual database/host access, controlled email recipients, provider configuration and release responsibility must be available for live checks. Missing access is tracked without exposing secrets in prompts or evidence.

This request prepares the deployment plan; it does not launch implementation workers or deploy the application. When execution is requested, start Wave 0 with the three bounded audits above. Proceed through unblocked implementation and verification work without repeated permission requests. Follow existing authorization for external changes; if production deployment is not yet authorized, present the tested release candidate and recovery evidence as the final decision point.

Success means every CL-01–CL-10 criterion has evidence, all required tests pass, an independent verifier has accepted each item, and release verification/observation is complete. Locally implemented work awaiting production remains open at its correct verification status.
