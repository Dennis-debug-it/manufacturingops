# Ecosys ServiceOps Delivery Readiness Audit

## Audit Update - 2026-05-13
- Public lead submission is no longer reproducing the earlier `500`. The failure was traced to ASP.NET validation metadata on `CreatePlatformLeadRequest`, fixed in code, covered by a backend regression test, and re-verified locally against `POST /api/public/leads`.
- The SLA enforcement background worker no longer fails its startup query after replacing a non-translatable EF Core `StringComparison` filter.
- Frontend lint is still failing with 34 errors, so the product remains below pilot-ready despite the backend stabilization progress.

## Executive Summary
Overall judgement:
- Not ready

Ecosys ServiceOps has real progress and a usable backbone, but it is not yet safe to put in front of pilot clients without a hardening sprint. The backend compiles, the frontend production build completes, backend unit tests pass, and several platform endpoints smoke-test successfully. That is the good news.

The bad news is more important. The public workspace-request/lead intake flow is currently broken with a live `500` on `POST /api/public/leads`. Tenant-side browser smoke flows are still unstable. The frontend lint gate fails with 34 errors. Several tenant-detail settings sections are still explicit placeholders. Site management is expected by the domain model and work-order linkage but has no routed frontend workspace. Email delivery exposes an API mode that is not implemented in the backend. This is not a polish gap only; it is a delivery-readiness gap.

## Overall Health Score
Overall score: **54 / 100**

| Area | Score | Notes |
| --- | ---: | --- |
| Backend health | 67 | Builds and unit tests pass; core APIs respond; some real workflow failures remain |
| Frontend health | 56 | Production build passes, but lint fails badly and tenant runtime flows are fragile |
| Database / migrations | 50 | App starts and migrates, but migration history is messy and runtime evidence suggests schema drift risk |
| UI / UX readiness | 52 | Platform shell is usable; tenant settings and some admin areas remain rough or placeholder-heavy |
| Security / auth | 72 | JWT auth, password change/reset, role policies, and session middleware exist |
| Multi-tenancy | 68 | Tenant-aware patterns are present; branch scoping is tested; factory/contractor split is not yet cleanly expressed |
| Email / notification readiness | 45 | SMTP path exists, queue/logging exists, but API mode is not implemented and real delivery needs live validation |
| Testing | 58 | Unit coverage exists for auth/email safety; smoke/e2e exists but key suites fail |
| Deployment readiness | 47 | Env-based API URL exists, but CORS, runtime assumptions, and migration confidence are not pilot-safe yet |
| Pilot readiness | 40 | Too many known gaps in lead intake, settings completeness, email readiness, and UX clarity |

## Feature Status Matrix
| Module | Backend | Frontend | Tests | UX | Tenant isolation | Status | Notes | Priority |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Authentication & password flows | Implemented | Implemented | Unit tests strong; tenant browser flow weak | Adequate | Good | PARTIAL | Login, logout, change password, forgot/reset exist; tenant runtime smoke remains unstable | High |
| Command Centre overview | Implemented | Implemented | 1/2 targeted smoke tests passed | Good shell, incomplete hardening | N/A platform | PARTIAL | Navigation works, but dashboard smoke expected marker missing and polish is incomplete | High |
| Platform tenant lifecycle | Implemented | Implemented | Unit tests around create/trial logic; UI smoke partial | Mixed | N/A platform | PARTIAL | Activation/suspension/deactivation exists; tenant drawer has many placeholder sections | High |
| Platform users | Implemented | Implemented | Covered indirectly; no dedicated end-to-end pass completed | Good enough | N/A platform | PARTIAL | Roles are clearer than before, but still need pilot cleanup and credential resend real-email validation | Medium |
| Platform settings | Implemented | Implemented | No strong end-to-end evidence | Mixed | N/A platform | PARTIAL | Structured settings UI exists, but several panels rely on fragile effect/state patterns flagged by lint | High |
| Public leads / workspace requests | Implemented | Landing/signup surfaces exist | Backend smoke failed | Broken workflow | N/A platform | BROKEN | `POST /api/public/leads` returns `500`, breaking public enquiry flow and lead-management smoke | Critical |
| Tenant dashboard | Implemented | Implemented | Indirect smoke only | Acceptable | Good | PARTIAL | Exists, but tenant runtime instability blocks clean pilot confidence | Medium |
| Clients | Implemented | Implemented | Included in backend smoke only | Mixed | Good | PARTIAL | CRUD appears present, but copy exposes `SLA plan` terminology in tenant UI | High |
| Sites / client sites | Implemented | No routed tenant page found | No meaningful coverage | Missing | Unknown in practice | MISSING | Backend `SitesController` exists but no visible tenant route/page for site operations | Critical |
| Assets | Implemented | Implemented | Included in backend smoke | Acceptable | Good | PARTIAL | Assets are functional, but site linkage and category/schema maturity still need validation | High |
| Work orders | Implemented | Implemented | Backend smoke only | Strongest tenant module so far, still not fully hardened | Good | PARTIAL | Creation, assignment, PM attachment, arrival/departure, comments, completion exist; needs broader end-to-end proof | Critical |
| Preventive maintenance & PM templates | Implemented | Implemented | Limited | Moderate | Good | PARTIAL | Good structural depth, but execution and schedule confidence are not yet pilot-grade | High |
| Assignment groups | Implemented | Implemented | Backend smoke indirect | Acceptable | Good | PARTIAL | Present in settings and work-order flow; more validation needed around routing and membership management | Medium |
| Users & roles | Implemented | Implemented | Unit coverage around credential delivery; no clean tenant UI smoke | Mixed | Good | PARTIAL | Core capability exists, but pilot confidence is reduced by tenant runtime instability | High |
| Branches / outlets | Implemented | Implemented | Indirect coverage | Acceptable | Good | PARTIAL | Branch-aware scoping is a strength, but UI/runtime validation is incomplete | Medium |
| Numbering rules | Implemented | Implemented | Smoke script exists but was not run end-to-end | Acceptable | Good | PARTIAL | Looks viable; needs direct branch-specific validation in a live environment | Medium |
| Email notifications | Implemented | Implemented | Unit coverage around outbox/logging; no real SMTP validation | Dense | Good | PARTIAL | SMTP path exists; Email API mode is exposed but not implemented | Critical |
| Email intake | Implemented | Implemented | Targeted e2e failed before proving flow | Better organized than old settings pages | Good | PARTIAL | Workflow-first tenant UI exists, but pilot confidence is low until runtime smoke passes | Critical |
| Monitoring intake | Implemented | Implemented | Minimal | Technical and admin-heavy | Good | PARTIAL | Exists, but still feels like a configuration console rather than a refined pilot workflow | Medium |
| Audit logs & activity | Implemented | Implemented | Limited | Platform logs are compact enough; tenant proof is weaker | Mixed | PARTIAL | Strong conceptually, but public lead failure suggests audit schema/runtime mismatch risk | High |
| Reports & logs | Implemented | Implemented | Limited | Basic | Good | PARTIAL | Reporting exists but still feels summary-level rather than decision-grade | Medium |
| Branding & theme | Implemented | Implemented | Copy audit suite failed before completion | Mixed | Good | PARTIAL | Branding support exists, but copy polish and some text encoding issues remain | High |
| Platform finance mapping | Implemented | Implemented | Limited | Not core for pilot | N/A platform | DEFERRED | Useful platform finance work exists, but it should not block ServiceOps pilot delivery | Low |
| Factory / internal maintenance fit | Partial architecture support | No dedicated mode | None | Not packaged | Partial | PARTIAL | Current client/site/billing assumptions need abstraction for department/internal-only deployments | High |
| Contractor / service-provider fit | Partial architecture support | Current default shape is closer to this model | None | Usable with cleanup | Partial | PARTIAL | This is the nearer-term fit, but still needs customer-safe copy, sites, email, and workflow hardening | High |

## Critical Blockers
1. Public lead submission is broken. Backend smoke consistently failed on `POST /api/public/leads` with HTTP `500`, which also broke lead listing/update smoke.
2. Tenant browser flows are not reliable enough for pilot confidence. Tenant-targeted Playwright runs failed before exercising email intake, responsive layouts, and customer-facing copy checks.
3. Frontend quality gate is failing. `npm run lint` returned 34 errors across state/effect patterns, hook ordering, refresh boundaries, and purity rules.
4. Tenant settings are incomplete in key places. In the tenant detail drawer under platform tenants, branding, users, modules, numbering, templates, audit trail, and danger zone still show follow-up-sprint placeholders.
5. Sites are missing from the actual tenant workspace despite being part of the backend model and required for client/site/asset linkage.
6. Email delivery readiness is overstated. The UI exposes an Email API delivery mode, but the backend throws `NotSupportedException` for that mode.

## High Priority Fixes
1. Repair the public lead/workspace-request flow and confirm it works end-to-end from public form to platform lead management.
2. Stabilize tenant browser runtime flows so signup/login and protected tenant pages can pass smoke tests consistently.
3. Clear the frontend lint backlog before pilot; do not ship with 34 known code-quality failures.
4. Replace placeholder tenant-detail sections with working panels or explicitly defer/remove them from pilot-facing navigation.
5. Add a real tenant site management page or remove site references from pilot promises.
6. Validate outbound email with real SMTP credentials, including invites, password reset, work-order notifications, and failure logging.
7. Remove or hide Email API mode until it is implemented.
8. Clean customer-facing copy that leaks internal terms such as `SLA plan`.
9. Tighten Command Centre overview/dashboard semantics and test hooks so smoke coverage matches the actual UI.
10. Reconcile migration history and confirm the live schema matches the current EF model.

## Medium Priority Improvements
1. Compress and simplify some admin/settings panels that still read like configuration dumps.
2. Improve mobile readability for operational tables and detail screens once tenant runtime smoke is stable.
3. Add explicit empty/loading/error states everywhere a list or dashboard can be empty or slow.
4. Reduce hard-coded Kenya-specific finance language in areas intended to be globally reusable.
5. Standardize wording across platform roles: `Platform Owner`, `Platform Admin`, `Support Admin`.
6. Remove visible encoding artifacts such as `Â·` and `tenantâ€™s`.

## Deferred Items
1. Phase 2 finance depth beyond light billing mapping.
2. CRM-like lead enrichment beyond simple enquiry capture and follow-up.
3. Broader platform finance analytics refinement.
4. Optional KIP/assistant surfaces that are still clearly non-core.

## Factory Version Gap Analysis
The current architecture can support a factory/internal maintenance edition without cloning the whole system, but it is not a clean switch yet.

What already helps:
- Multi-tenant model is already in place.
- Branch and assignment-group concepts can map to departments, plants, sections, or lines.
- Assets, PM templates, work orders, materials, and users are all reusable for internal maintenance.

What still blocks a clean factory edition:
- The primary tenant model still assumes external clients and commercial/account lifecycle concepts.
- Site/client vocabulary is too contractor-oriented for internal-only operations.
- Customer/commercial concepts are still embedded in UI copy and data relationships.
- There is no first-class operating mode that hides billing/contracts/client-facing wording.

Recommended path:
- Add a tenant operating mode such as `contractor` vs `internal-maintenance`.
- Swap labels and hide irrelevant modules by mode instead of forking the app.
- Make `client` optional and allow `department/section/requesting team` as the first-class origin for internal work orders.

## Contractor Version Gap Analysis
The current product is closer to a contractor/service-provider edition than to a factory edition.

What is already aligned:
- Clients, assets, work orders, assignment groups, PM, email notifications, and tenant branding all fit the contractor model.
- Platform tenant onboarding and license lifecycle are already contractor-friendly.

What still needs work:
- Site management is missing from the tenant workspace.
- Public lead/workspace request flow is broken.
- Customer-facing copy still exposes internal terms in some places.
- Email and intake flows are not yet proven with real-world runtime tests.

## Email and Notification Readiness
What works:
- SMTP-oriented backend settings exist.
- Email outbox and delivery logs exist.
- Unit tests cover credential resend safety, reset-link handling, and outbox/log transitions.
- Tenant and platform email settings UIs both exist.

What is broken or risky:
- Email API delivery mode is exposed in the UI, but backend send/verify explicitly says it is not implemented.
- Real SMTP delivery was not validated in this audit.
- Some notification toggles are ahead of the actual workflow wiring.
- The public lead flow likely intersects with audit/email side effects and currently fails hard.

What must be tested with real SMTP before pilot:
- New user credential email
- Resend credentials
- Password reset
- Work-order assignment
- Work-order status changes
- Tenant override vs platform-default sender behavior
- Failure logging when delivery is rejected or times out

## UI/UX Critique
- The settings workspace shell is directionally good. The mini-sidebar layout is the right choice and should be retained.
- The tenant detail drawer inside platform tenants is not pilot-ready. Too many sections still say they will be enabled in a follow-up sprint.
- Email intake is much better organized than a flat technical dump. This is one of the stronger admin UX areas, but it still depends on runtime stability before it can be trusted.
- Some pages still expose internal wording such as `SLA plan`, which is not safe to leave visible in customer-facing tenant operations.
- Platform and tenant tables are generally compact enough, which is good. Logs and audit tables are more delivery-appropriate than oversized card layouts.
- There are text encoding issues in the UI (`Â·`, `tenantâ€™s`) that make the product feel unfinished.
- The sign-in experience is serviceable, but overall confidence drops because tenant smoke flows do not reliably complete.

## Technical Risk Register
| Risk | Impact | Likelihood | Area | Mitigation | Priority |
| --- | --- | --- | --- | --- | --- |
| Public lead submission returns `500` | Blocks demand capture and pilot onboarding | High | Backend / onboarding | Reproduce locally, inspect DB/audit side effects, add regression test | Critical |
| Schema drift between current model and live DB | Breaks runtime unexpectedly | High | Database / migrations | Rebuild migration confidence, verify applied migrations, test clean DB upgrade | Critical |
| Frontend lint backlog hides real defects | Medium to high | High | Frontend | Fix lint errors before pilot branch cut | High |
| Tenant-side smoke flows remain unstable | High | High | Full stack | Stabilize signup/login/runtime tests and rerun key specs | Critical |
| Email API mode exposed but not implemented | High confusion and failed setup | High | Email | Hide/remove API mode until implemented | High |
| Placeholder-heavy settings create false expectations | High | High | UX / product | Remove or complete placeholder sections before pilot demos | High |
| Missing site workspace | High for contractor workflows | High | Product / frontend | Add tenant UI for sites or reduce scope explicitly | Critical |
| Customer-facing copy leaks internal jargon | Medium | Medium | UX / product | Copy review pass on tenant pages and templates | High |
| Hardcoded environment assumptions | Medium | Medium | Deployment | Tighten env docs, origin handling, and deployment config | Medium |
| Weak feature-level automated coverage | Medium | High | Testing | Add end-to-end coverage for work orders, settings, sites, and email | High |

## Recommended Delivery Plan
### What to fix before pilot
- Repair public leads/workspace-request flow.
- Stabilize tenant login/signup/browser smoke.
- Resolve frontend lint failures.
- Replace or remove placeholder tenant-detail settings sections.
- Add or explicitly defer site management.
- Validate SMTP delivery and failure logging.
- Hide unsupported Email API mode.
- Clean customer-facing copy and text encoding issues.

### What can be tested with pilot clients
- Core work-order lifecycle after hardening
- Asset and branch management
- Assignment groups and dispatch workflows
- PM templates and preventive schedules with a controlled pilot group
- Tenant branding and command-centre onboarding once stable

### What to defer
- Deep finance mapping
- CRM-like follow-up enrichment
- Optional assistant surfaces
- Factory-specific mode switching if contractor pilot comes first

### What to monitor after deployment
- API 500 rates on onboarding, settings, and work-order actions
- Email delivery failures and retry volume
- Tenant session/auth anomalies
- Work-order completion and assignment workflow errors
- Long-running migrations or startup initialization warnings
