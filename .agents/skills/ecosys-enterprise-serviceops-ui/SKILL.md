# Ecosys Enterprise ServiceOps UI Skill

## Purpose

Use this skill when improving, reviewing, or refactoring Ecosys UI/UX.

Ecosys is an enterprise ServiceOps platform for contractors and internal maintenance/service teams. The UI must feel serious, compact, operational, and trustworthy — closer to ServiceMax-style field service software than a generic AI-generated SaaS dashboard.

## Product Context

Ecosys supports:

- Clients
- Sites
- Assets
- Work Orders
- Preventive Maintenance
- Contracts
- Teams
- Assignment Groups
- Materials
- Evidence
- Signatures
- Reports
- Settings

Work Orders are the core of the product.

## Design Direction

Prefer:

- compact layouts
- dense but readable tables
- master-detail layouts
- side drawers for details
- sticky action bars
- consistent spacing
- consistent typography
- calm enterprise colors
- clear hierarchy
- minimal decorative UI
- mobile-first technician execution screens
- excellent empty states
- excellent loading states
- excellent error states

Avoid:

- too many cards
- oversized dashboard blocks
- random gradients
- decorative badges
- inconsistent button styles
- repeated boxed sections
- multiple competing primary actions
- modals for everything
- forms that overflow on mobile
- hidden sticky buttons
- clipped tables

## Work Order UX Rules

The Work Order lifecycle should be obvious:

1. Create
2. Assign
3. Schedule/Dispatch
4. Start Work
5. Add updates
6. Use materials
7. Add evidence
8. Capture signatures
9. Complete
10. Generate report

Primary actions should be limited and contextual:

- Start Work
- Put On Hold
- Resume
- Complete

Do not add unnecessary actions like Reassign, Escalate, Change Priority, or Cancel unless the existing product already supports them and the screen truly needs them.

## Layout Rules

For list-heavy pages, prefer:

- compact filter bar
- searchable table
- saved views where useful
- status chips
- row actions menu
- detail drawer or split panel

For detail pages, prefer:

- concise header
- status/lifecycle indicator
- tabs for major sections
- sticky primary action
- audit/timeline section
- related records shown compactly

For mobile technician screens, prefer:

- single-column layout
- large primary action
- sticky bottom action bar
- quick update/comment entry
- evidence upload
- materials used
- signatures
- offline/error-friendly states

## Refactoring Rules

When modifying UI:

1. Preserve existing functionality.
2. Do not remove business logic.
3. Do not break routes.
4. Reuse existing components where possible.
5. Consolidate duplicated layout patterns.
6. Keep the UI compact.
7. Update tests where needed.
8. Run build and tests before final response.

## Final Review Checklist

Before finishing, verify:

- The page is visually calmer.
- Cards/boxes have been reduced.
- Tables are readable and compact.
- Buttons have clear hierarchy.
- Mobile layout works.
- Empty/loading/error states exist.
- The work order flow remains intact.
- Existing tests pass or failures are explained.