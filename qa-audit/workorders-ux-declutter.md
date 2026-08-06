# Work Orders — UX Declutter Advice

Grounded in `src/features/work-orders/WorkOrdersPage.tsx` + `components/FilterPanel.tsx`, 2026-08-02.

## Root causes of "busy"
1. **12-column table** (WorkOrdersPage.tsx:59-126): WO No, Client, Site, Asset, Type, Priority, Status, SLA, Assigned To, Due Date, Last Update, actions. ~100px/col at 1280px.
   - Client repeats every row; Site/Asset/Last Update often `—`; three colored pills per row (Priority+Status+SLA) compete; Type is raw CamelCase.
2. **Four stacked chrome bands** before the table (header → tab pills → search-card+view switcher → filter bar), `space-y-6` each.
   - Search wrapped in `rounded-2xl … p-4 shadow-sm` card (:184) = panel-weight for one input.

## Already good (keep)
- FilterPanel is collapsed-by-default behind a Filters button w/ active-filter chips + Clear all.
- Row actions in a kebab QuickActionsMenu.
- Alternate low-density views exist (Kanban/Calendar/Map).

## Priority recommendations
1. **Trim table to ~7 default columns + a "Columns ▾" toggle.** Merge Client+Site+Asset → one two-line Asset/Location cell; fold Last Update into detail/tooltip; merge Due+SLA. Let power users re-enable the rest.
2. **Collapse 4 bands → 1 toolbar row.** Search + ViewSwitcher + Filters on one line; de-card the search (plain h-10 bordered input); page spacing space-y-6 → space-y-4.
3. **One strong color signal per row.** Status = full badge; Priority = colored dot+label; SLA colored only when at-risk/breached. Space out Type ("New Installation"), muted text.

## Lower-priority polish
- Auto-hide 100%-empty columns in the current result set.
- Group rows by Client/Site with sticky group headers instead of repeating client.
- Compact/comfortable density toggle.

## Lowest-risk first change
Recommendation #2 touches only WorkOrdersPage.tsx (toolbar consolidation + de-card search). Highest visibility, no data/logic change.
