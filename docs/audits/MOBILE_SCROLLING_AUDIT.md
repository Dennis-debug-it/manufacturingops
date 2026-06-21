# Mobile Scrolling Audit — Ecosys ServiceOps Frontend

Date: 2026-05-26
Auditor: Automated inspection (audit-only pass)
Scope: `C:\ecosys\ecosys-serviceops-frontend` — app shell, navigation, work-order
flows, list pages, settings, modals, and global CSS.
Status: AUDIT ONLY. No files were modified.

---

## A. Executive summary

The mobile scrolling problem is primarily a **shell + per-page layer cake** issue,
not a single bug. The findings fall into four layers in roughly decreasing order
of impact:

1. **Auth pages (Login, Signup, Forgot/Reset/Change Password)** combine
   `min-h-screen` + `overflow-hidden` + `flex items-center` on the outermost
   wrappers. When the form is taller than the viewport on a small phone, the
   form is vertically centered and the top of it is pushed off-screen with no
   way to scroll up. This is the **most reproducible "cannot scroll" symptom**
   on small devices.
2. **Modals and the Drawer use `vh` units (`max-h-[90vh]`, `max-h-screen`,
   `max-h-[calc(100vh-88px)]`)** rather than `dvh`/`svh`. On iOS Safari and
   recent Android Chrome the dynamic browser chrome makes `100vh` taller than
   the visible viewport, so the bottom of long modals/drawers (form bottoms,
   action buttons) drops below the address bar and is unreachable. Combined
   with `overflow-hidden` on the modal shell, only the inner scroller can be
   moved — when that scroller is wrongly sized, the user cannot reach the
   primary action.
3. **In-content tables that are not properly responsive**. The "Recorded
   materials" table inside `WorkOrderDetailPage` and the shared `MaterialsUsedTable`
   in `workOrderWorkflowUi` both wrap a `min-w-[640px]` / `min-w-[680px]` `<table>`
   in a parent that has `overflow-hidden` (not `overflow-x-auto`). On a 360–412
   px phone the rightmost columns are simply **clipped**, and the table
   contributes nothing useful below `md`. The page's `AppShell` then applies
   `overflow-x-hidden` to the main column, which hides this overflow from sight
   without fixing it — so the user sees a "stuck" or jittery scroll feel and
   missing data, depending on the browser.
4. **The mobile sidebar overlay does not lock body scroll.** `Sidebar.tsx` has
   no `document.body.style.overflow = 'hidden'` when the drawer opens, only the
   per-route `cleanupBodyInteractivity()` call. The drawer panel is `fixed
   inset-0` with an inner scroller at `h-[calc(100%-60px)] overflow-y-auto`,
   so touch-scrolls on the panel can scroll-chain to the body underneath. The
   page beneath also keeps scrolling, which produces the familiar "screen
   jumps when I close the menu" feel.

There is **no shell-level scroll prison** (the app does not use the classic
`h-screen + overflow-auto` trapped-scroll pattern on the main route container —
scroll happens on the document body as it should). The shell is mostly correct;
the felt-mobile-pain is concentrated in (1) auth, (2) modals/drawers, (3) a few
tables, and (4) the mobile sidebar overlay.

---

## B. Confirmed problems

### B1. Auth pages trap content above the fold — Severity: **Critical** on mobile

- Files:
  - `src/modules/auth/LoginPage.tsx:84,101`
  - `src/modules/auth/SignupPage.tsx:81,83`
  - `src/modules/auth/ResetPasswordPage.tsx:63,64`
  - `src/modules/auth/ForgotPasswordPage.tsx:35,36`
  - `src/modules/auth/ChangePasswordPage.tsx:87,89`
- Pattern (LoginPage example):

```
<div className="relative min-h-screen overflow-hidden bg-app">
  ...
  <div className="relative mx-auto flex min-h-screen max-w-[1280px] items-center px-4 py-8 sm:px-6 lg:px-8">
```

- Behaviour observed: When the auth card grows taller than the viewport (which
  happens easily on 360×800 with the soft keyboard up, an inline error, or
  Signup's longer form), the outer `overflow-hidden` blocks document scroll
  and `flex … items-center` vertically centers the card — pushing its top
  off-screen. The user cannot reach the top of the form (heading, error
  banner) and on Signup the bottom CTA can also be cut.
- Why this is the most common "I cannot scroll on mobile" complaint: it is
  literally the first page every user sees.

### B2. Modal uses 90vh + clipped inner scroller — Severity: **High**

- File: `src/components/ui/Modal.tsx:57,67`
- Pattern:

```
<div className="glass-panel w-[95vw] ${maxWidth} max-h-[90vh] overflow-hidden rounded-[24px]">
  <div className="border-app flex … border-b px-4 py-4 sm:px-6"> ... header (no fixed height) ... </div>
  <div className="max-h-[calc(90vh-88px)] overflow-y-auto px-4 py-5 sm:px-6">
```

- Issues:
  - `90vh` and `calc(90vh-88px)` use static `vh`. On iOS Safari and Android
    Chrome when the address bar is visible, the visual viewport is shorter
    than `100vh`, so `90vh` overflows the screen and the inner scroll area
    plus the header become taller than what is visible. The user cannot reach
    the bottom of the modal body (action buttons, submit) without first
    collapsing the URL bar.
  - The header height is assumed to be 88 px, but with long titles + descriptions
    it wraps to two or three lines, breaking the math and pushing the body
    further down.
  - Modal lacks any `safe-area-inset-bottom` padding, so on iPhones with the
    home indicator the last row of action buttons can land under the indicator.
- Severity is **High** because all create/edit dialogs (work orders, clients,
  sites, assets, users, PM schedules, bulk import) go through this component.

### B3. Drawer uses 100vh / 94vh — Severity: **High**

- File: `src/components/ui/Drawer.tsx:55,65`
- Pattern:

```
<div className="glass-panel h-full max-h-screen w-full max-w-3xl overflow-hidden rounded-none sm:max-h-[94vh] sm:rounded-[24px]">
  <div className="… border-b px-4 py-4 …"> ... header ... </div>
  <div className="max-h-[calc(100vh-88px)] overflow-y-auto px-4 py-5 sm:max-h-[calc(94vh-88px)] sm:px-6">
```

- Same root cause as B2: `100vh` does not equal the visual viewport on mobile
  browsers. On full-screen mode (`rounded-none` mobile variant) this means the
  bottom of the drawer (and any submit / cancel buttons) is hidden behind the
  browser chrome.
- Same `safe-area-inset-bottom` gap. The internal `overflow-y-auto` is the
  only scroll surface — when it is sized wrong, the form bottom is genuinely
  unreachable, not merely "ugly".

### B4. "Recorded materials" table is clipped on mobile — Severity: **High**

- Files:
  - `src/modules/work-orders/WorkOrderDetailPage.tsx:721`
  - `src/modules/work-orders/workOrderWorkflowUi.tsx:591` (shared
    `MaterialsUsedTable`)
- Pattern:

```
<div className="mt-4 overflow-hidden rounded-[20px] border border-app">
  <table className="w-full min-w-[680px]">  <!-- WorkOrderDetailPage -->
  <table className="w-full min-w-[640px]">  <!-- workOrderWorkflowUi -->
```

- The wrapper is `overflow-hidden`, not `overflow-x-auto`. The child table is
  forced to a minimum of 640–680 px wide. On a 360–412 px viewport this
  silently clips the "Source / store" and "Notes" columns — users can't see
  them and can't horizontally scroll to reach them.
- The two work-order tables that ARE properly mobile-handled (WorkOrdersPage
  list at `min-w-[1320px]` inside `hidden lg:block`, and DataTable at
  `min-w-[900px]` inside `hidden md:block`) prove the team knows the pattern
  — these two materials tables are the leak.

### B5. Mobile sidebar overlay never locks body scroll — Severity: **High**

- File: `src/components/layout/Sidebar.tsx:87-100`
- Pattern:

```
{mobileOpen ? (
  <div data-ui-overlay="true" className="bg-overlay fixed inset-0 z-40 p-3 lg:hidden" onClick={onCloseMobile}>
    <div className="glass-panel h-full w-full max-w-[320px] overflow-hidden rounded-[30px] p-5" onClick={(event) => event.stopPropagation()}>
      ...
      <div className="h-[calc(100%-60px)] overflow-y-auto pr-1">
```

- Issues:
  - No `document.body.style.overflow = 'hidden'` (or matching cleanup) when
    the drawer opens. Compare with `Modal.tsx` and `Drawer.tsx`, which both
    set/restore `body.style.overflow` correctly. The Settings layout drawer
    (`SettingsLayout.tsx:124`) and the platform settings drawer
    (`SettingsMiniSidebar.tsx:65`) share the same omission.
  - On iOS, scroll on the panel "chains" to the body underneath; on close,
    the underlying page can be at an unexpected scroll position. This is the
    "after I close the menu, the page jumps / I cannot scroll properly" feel
    the audit was launched to find.
  - The inner scroller uses `h-[calc(100%-60px)]` — assumes the close-button
    row is exactly 60 px. With dynamic content above (logo, tenant name) the
    last nav links can fall under the visible window.
  - The overlay has `p-3` outer padding but the inner panel is `h-full`, so on
    very short phones (≤ 700 px tall in landscape) the panel can grow taller
    than the available height before the inner scroller activates.
- The shell DOES call `cleanupBodyInteractivity()` on route changes, which
  resets `body.style.overflow`, but the sidebar never set it in the first
  place — the cleanup is mismatched with what's missing.

### B6. Settings page mobile drawer also lacks body scroll lock — Severity: **Medium**

- Files:
  - `src/pages/settings/SettingsLayout.tsx:123-140`
  - `src/pages/platform/settings/components/SettingsMiniSidebar.tsx:64-81`
- Same omission as B5. Plus both layouts wrap the desktop sidebar + content in
  `flex w-full min-w-0 gap-0 overflow-hidden rounded-[32px]` (SettingsLayout:113)
  and `flex min-h-[680px] w-full min-w-0 flex-col overflow-hidden …`
  (PlatformSettingsPage:58). The `min-h-[680px]` is fine on mobile, but
  `overflow-hidden` plus a nested panel form that grows taller than the
  declared min height can clip — needs verification with full panel content.

### B7. Fixed mobile action bar lacks safe-area padding — Severity: **Medium**

- File: `src/modules/work-orders/workOrderWorkflowUi.tsx:772`
- Pattern:

```
<div className="fixed inset-x-0 bottom-0 z-40 border-t border-app bg-[rgba(255,255,255,0.94)] px-4 py-3 backdrop-blur lg:hidden" data-testid="tech-bottom-bar">
```

- The bar is the technician primary-action bar (Start / Hold / Resume /
  Complete). It is `fixed bottom-0` with no `pb-[env(safe-area-inset-bottom)]`,
  so on iPhones with a home indicator the buttons sit partly under the
  indicator (lower tap area, looks cramped).
- Consumers (`TechJobPage.tsx:267`, work-order detail) add `pb-24` /`pb-28` to
  their containers to clear the bar — that part is correct.

### B8. KipButton fixed bottom collides with the action bar — Severity: **Low / Medium**

- File: `src/components/kip/KipButton.tsx:7`
- The "Ask Fundi" floating action button is `fixed bottom-5 right-5 z-40`.
  When a page also renders the `MobilePrimaryActionBar` (also `z-40`,
  `inset-x-0 bottom-0`), the FAB lands inside the action bar's strip and can
  cover the right-most action button (Hold / Complete). No safe-area padding
  either.

### B9. Topbar is heavy on mobile and uses `flex-wrap` — Severity: **Medium**

- File: `src/components/layout/Topbar.tsx:69-275`
- The sticky topbar contains the menu button, tenant header, branch select,
  theme toggle, bell, avatar, and (xl:hidden) a second mobile search bar
  underneath. On mobile, `flex-wrap` causes these to stack into 3–4 rows, so
  the sticky header can occupy 220–260 px of the 800 px viewport. That eats a
  large portion of available vertical room and pushes page content far below
  the fold. The header itself does not block scrolling, but it makes scroll
  *feel* mandatory just to reach the page title.
- A search result dropdown (`max-h-[320px] overflow-y-auto`) sits inside the
  sticky header — a nested scroll container with up to 5 results. Harmless but
  adds another row to the sticky region.

### B10. AppShell uses overflow-x-hidden on main — Severity: **Low**

- File: `src/components/layout/AppShell.tsx:147` (`<main className="flex-1 min-w-0 overflow-x-hidden">`)
- This is correct *defensively* (it absorbs accidental horizontal overflow
  from a child) but it also hides bugs like B4 from being obvious during
  development. With B4 in place, the main column quietly clips columns rather
  than producing a horizontal scrollbar that the team would catch.

### B11. `body { overscroll-behavior: none }` — Severity: **Low**

- File: `src/index.css:178`
- This disables the rubber-band overscroll and the "swipe-to-go-back" gesture
  on iOS Safari. Not a scroll *bug* but worth confirming the team intended
  this; users often perceive it as "scrolling feels wrong" on iOS.

---

## C. Suspicious areas that need careful review (not yet broken, but risky)

- **`Sidebar.tsx:82` — desktop-only sticky sidebar** uses
  `sticky top-0 h-screen w-full overflow-y-auto`. Hidden on mobile by the
  `aside hidden lg:flex` wrapper, so it does not affect mobile today. Risky
  because any future refactor that drops the `hidden lg:flex` gate would
  reintroduce a full-viewport-tall sticky column on mobile.
- **`PlatformSettingsPage.tsx:58`** — `flex min-h-[680px] … overflow-hidden`.
  Safe today because the panels fit, but tall sub-forms (e.g. SMTP, branding
  uploads) could be clipped without warning. Worth verifying each panel
  doesn't exceed 680 px on mobile.
- **`TechJobPage.tsx:341`** — `flex gap-2 overflow-x-auto pb-1` for the
  Overview / Checklist / Evidence / Closure pills. Correct pattern, but when
  the user finger-pans horizontally, iOS sometimes treats the gesture as a
  page scroll. Confirm `overscroll-x-contain` is added; today it isn't.
- **`DataTable.tsx:67`** — `overflow-x-auto overscroll-x-contain` is present
  here (good), inside a `hidden md:block` (good). Listed as a positive
  reference rather than a problem.
- **Modal/Drawer body click handling** uses `event.stopPropagation()` on
  the inner panel and `onClick={onClose}` on the outer overlay. Touch-drag on
  the overlay (when trying to scroll the modal) can register as a tap and
  close the modal. Verify on real mobile.
- **`auth-showcase` / login background** — `absolute inset-0` decorative
  layer combined with `overflow-hidden` parent. Decorative only, but layered
  with `flex items-center` it is part of the B1 root cause.

---

## D. Pages most affected (ranked, worst first)

1. **Login / Signup / Forgot / Reset / Change Password** — B1. Critical user
   flow, easy to reproduce, blocks people from logging in on small screens
   with the keyboard up.
2. **Work Order Detail Page** — B2 (every edit modal it opens), B4 (recorded
   materials table clipped), B7 (mobile action bar). Lots of long forms; this
   is the daily-driver screen.
3. **Tech Job Page** — B7 (fixed action bar safe-area), B8 (Kip FAB overlap),
   long single-column scrolling so users feel every shell flaw.
4. **Any page with create / edit modals** — Clients, Sites, Assets, Users,
   PM Schedules, Materials, Settings sub-pages. All share Modal.tsx → B2.
5. **Settings pages** — B6 (mobile drawer body lock), plus the desktop side
   panel that hides behind a "Sections" button on mobile. The mobile section
   pills at the top scroll horizontally, which is fine, but combined with the
   sticky topbar eats a lot of vertical space.
6. **Tables backed by `DataTable` (Clients, Sites, Assets, Materials,
   Finance, Reports)** — fine on mobile thanks to the mobile-card branch.
   Listed here only because users *expect* the desktop columns and may not
   realize they are intentionally hidden.
7. **AppShell topbar** — B9. Affects every page but is cosmetic, not
   functional.

---

## E. Recommended fix plan (do NOT implement in this pass)

### Phase 1 — App shell & global

1. Replace `min-h-screen` and `100vh` in `AppShell.tsx`, `Sidebar.tsx`, and
   global CSS where they describe "the viewport" with `100dvh` (`min-h-dvh`,
   `h-dvh`). Keep `100vh` only where a static fallback is intentional.
2. In `Sidebar.tsx` mobile branch, add a `useEffect` that sets
   `document.body.style.overflow = 'hidden'` on open and restores it on close,
   mirroring `Modal.tsx` / `Drawer.tsx`. Same for `SettingsLayout.tsx` and
   `SettingsMiniSidebar.tsx`.
3. Reconsider `body { overscroll-behavior: none }` in `index.css`. Either
   document why it's there or downgrade to `overscroll-behavior-y: contain`.
4. Audit the topbar (B9) to collapse aux controls into a single overflow menu
   on `< sm`, reducing sticky height from ~240 px to ~64 px.

### Phase 2 — Work-order mobile fixes

5. Replace the `overflow-hidden` wrappers on the two materials tables
   (`WorkOrderDetailPage.tsx:721` and `workOrderWorkflowUi.tsx:591`) with
   `overflow-x-auto overscroll-x-contain`, OR add a `lg:hidden` mobile
   card alternative the way `WorkOrdersPage.tsx:436` does.
6. Add `pb-[env(safe-area-inset-bottom)]` and `data-safe-area` to
   `MobilePrimaryActionBar` and shift the per-page `pb-28` accordingly.
7. Reposition `KipButton` so it does not stack with the action bar — either
   raise its `bottom` to `bottom-[calc(5rem+env(safe-area-inset-bottom))]`
   when an action bar is present, or hide it on routes that render the bar.

### Phase 3 — Tables / list mobile fixes

8. Audit every `min-w-[XXXpx]` (~25 occurrences) and ensure each one is
   wrapped in `overflow-x-auto overscroll-x-contain` AND has a `md:hidden`
   mobile card alternative. The recorded materials tables are the only known
   misses; verify systematically.
9. Add `overscroll-x-contain` to the work-order detail's `flex gap-2
   overflow-x-auto pb-1` tab pill bars and the Settings mobile section pills.

### Phase 4 — Modal / form fixes

10. Switch `Modal.tsx` and `Drawer.tsx` to `dvh`-based heights:
    - Modal panel: `max-h-[90dvh]` (with `100dvh` fallback for older browsers).
    - Modal body: replace `max-h-[calc(90vh-88px)]` with a `flex` column
      layout that has a flex-1 scrollable body, eliminating the magic
      `88px` math.
    - Same approach for Drawer.
11. Add `pb-[max(1.25rem,env(safe-area-inset-bottom))]` to the modal body
    on its mobile branch so the last button row clears the home indicator.
12. Confirm modal close-on-tap-outside doesn't fire on a vertical drag (use
    `onPointerUp` with movement guard, or trap touch).

### Phase 5 — Auth pages

13. Remove `overflow-hidden` and `items-center` from the outer auth wrappers
    on small screens. Replace with `min-h-dvh items-start pt-10 pb-10 sm:items-center`
    so the form scrolls naturally on mobile and is centered on tablet+.
14. Verify Signup with all fields + an error message scrolls to the bottom
    on a 360×800 viewport.

### Phase 6 — Playwright regression

15. Add to `playwright.config.ts` a `Mobile Chrome (Pixel 5)` and
    `Mobile Safari (iPhone 13)` project. The existing
    `tests/e2e/responsive-layouts.spec.ts` already iterates 360, 430, 768,
    1024, 1366 widths; expand its `assertNoHorizontalOverflow` helper with
    `assertCanScrollToBottom(page, locatorOfBottomElement)` and call it on:
    login submit button, modal submit, drawer submit, tech bottom bar,
    work-order recorded materials last column.

---

## F. Do-not-touch list (already correct, leave alone)

- `src/components/ui/DataTable.tsx` — mobile-card / desktop-table split
  with `overflow-x-auto overscroll-x-contain` is the reference pattern for
  the rest of the codebase. Do not refactor.
- `src/modules/work-orders/WorkOrdersPage.tsx:386-462` — mobile cards
  (`lg:hidden`) plus desktop table (`hidden lg:block`) is correctly
  implemented. Do not touch.
- `src/components/layout/AppShell.tsx:135-184` — body-scroll architecture
  (no `h-screen` prison on the main content area, `min-w-0` on flex child) is
  correct. Only address the children mentioned in B1–B9.
- `src/utils/appCleanup.ts` — `cleanupBodyInteractivity` and the UI reset
  event are working as designed; the sidebar omission (B5) is the caller's
  fault, not this helper's.
- `src/index.css` — typography, theme tokens, and most of the base layer
  are unrelated to the scrolling issue. The only line worth revisiting is
  the `body { overscroll-behavior: none }` rule.
- `src/modules/tech/TechLayout.tsx` — a clean `min-h-screen` + sticky header
  + body-scroll page. Reference implementation.

---

## G. Test evidence and observations

No Playwright run was executed in this audit pass. The existing test
inventory was inspected:

- `playwright.config.ts` only declares a `Desktop Chrome` project, so no
  automated mobile regression runs in CI today.
- `tests/e2e/responsive-layouts.spec.ts` does iterate `{360x800, 430x932,
  768x1024, 1024x768, 1366x768}` viewports and calls
  `assertNoHorizontalOverflow` (defined in `tests/e2e/helpers/layout.ts`)
  after each navigation. This helper *would* catch B4-like horizontal
  overflow on mobile — but only if `AppShell` did not have
  `overflow-x-hidden` on `<main>` masking the bug at the document level. The
  helper measures `documentElement.scrollWidth` vs `clientWidth`, which is
  exactly what the shell suppresses.
- `tests/e2e/mobile-technician.spec.ts` uses a 412×915 viewport and
  exercises the tech flow; it does not assert scroll reachability of the
  fixed bottom bar.
- No assertion verifies modal/drawer body scrolls to the action button on
  mobile.

Recommended additions are in section E, phase 6.

Static behaviour predictions (to verify with real devices in a follow-up
session):

| Viewport      | Page                  | Expected symptom                                                                    |
| ------------- | --------------------- | ----------------------------------------------------------------------------------- |
| 360×800       | `/login`              | Form vertically centered; top of card off-screen after focusing email + error shown |
| 360×800       | `/work-orders/:id`    | "Recorded materials" columns 4–5 clipped; rest of page scrolls normally             |
| 390×844 (iOS) | any modal             | Bottom action row sits below visual viewport when URL bar is visible                |
| 412×915       | `/tech/jobs/:id`      | Bottom action bar overlaps home-indicator area; "Ask Fundi" FAB overlaps it         |
| 360×800       | sidebar opened        | Touch-drag on drawer scrolls underlying page; page lands at new scroll on close     |
| 360×800       | `/settings/*`         | Mobile section pills are reachable; section-drawer scroll chains to body            |

---

## Appendix — Pattern occurrence counts (grep)

```
overflow-hidden   27 lines
overflow-y-auto    8 lines
overflow-x-auto   13 lines
overflow-scroll    0 lines
h-screen          21 lines
min-h-screen      19 lines
max-h-screen       1 line
fixed (class)     10 lines
sticky (class)     5 lines
min-h-0            4 lines
flex-1            27 lines
w-screen           0 lines
max-w-screen       0 lines
min-w-0           61 lines
```

Classification summary:

- **Safe** (correct usage): all `min-w-0` (61), all `flex-1`, the desktop
  `sticky` topbar/sidebar/TechLayout headers, the global `html/body/#root`
  `overflow-x: hidden` and `min-height: 100dvh` rules, the
  `DataTable`/`WorkOrdersPage` mobile-card patterns, all `Modal.tsx` and
  `Drawer.tsx` `useEffect`-driven body locks.
- **Suspicious** (works today, may break later): the `min-h-[680px]
  overflow-hidden` settings shells; the `overflow-hidden` on
  WorkOrdersPage's outer card (radius mask, but blocks future bug
  discovery); the `overflow-x-hidden` on `AppShell` main.
- **Likely causing mobile scroll issue**: items B1–B7 above. All five auth
  pages, both materials tables, `Modal.tsx`, `Drawer.tsx`, the sidebar mobile
  overlay, the two settings mobile drawers, and `MobilePrimaryActionBar`.

End of audit.
