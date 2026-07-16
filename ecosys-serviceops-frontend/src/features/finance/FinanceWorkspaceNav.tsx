import { NavLink, useLocation } from 'react-router-dom'
import { cn } from '../../shared/utils/cn'

interface FinanceNavItem {
  to: string
  label: string
}

interface FinanceNavGroup {
  label: string
  /** Where the section tab lands (its first item). */
  to: string
  items: FinanceNavItem[]
}

const overview: FinanceNavItem = { to: '/finance', label: 'Overview' }

const financeNavGroups: FinanceNavGroup[] = [
  {
    label: 'Documents',
    to: '/finance/documents',
    items: [
      { to: '/finance/documents', label: 'Documents' },
      { to: '/finance/quotes', label: 'Quotes' },
      { to: '/finance/invoices', label: 'Invoices' },
      { to: '/finance/receipts', label: 'Receipts' },
    ],
  },
  {
    label: 'Pricing',
    to: '/finance/rate-cards',
    items: [
      { to: '/finance/rate-cards', label: 'Rate Cards' },
      { to: '/finance/preferential-rates', label: 'Preferential Rates' },
      { to: '/finance/exchange-rates', label: 'Exchange Rates' },
    ],
  },
  {
    label: 'Control',
    to: '/finance/billable-work',
    items: [
      { to: '/finance/billable-work', label: 'Billable Work' },
      { to: '/finance/invoice-approvals', label: 'Approvals' },
      { to: '/finance/credit-control', label: 'Credit Control' },
      { to: '/finance/aging', label: 'Aging' },
    ],
  },
  {
    label: 'Reports',
    to: '/finance/reports',
    items: [
      { to: '/finance/reports', label: 'Reports' },
      { to: '/finance/expenses', label: 'Expenses' },
      { to: '/finance/payables', label: 'Payables' },
    ],
  },
]

/** True when `pathname` is exactly `to` or a nested route beneath it. */
function matchesPath(pathname: string, to: string): boolean {
  return pathname === to || pathname.startsWith(`${to}/`)
}

/** The group that owns the current route, chosen by the longest matching item path. */
function activeGroupFor(pathname: string): FinanceNavGroup | null {
  let best: { group: FinanceNavGroup; length: number } | null = null
  for (const group of financeNavGroups) {
    for (const item of group.items) {
      if (matchesPath(pathname, item.to) && (!best || item.to.length > best.length)) {
        best = { group, length: item.to.length }
      }
    }
  }
  return best?.group ?? null
}

export function FinanceWorkspaceNav() {
  const { pathname } = useLocation()
  const activeGroup = activeGroupFor(pathname)
  const overviewActive = pathname === overview.to

  return (
    <nav className="space-y-2">
      {/* Primary: Overview + section tabs. */}
      <div className="flex flex-wrap items-center gap-1.5 rounded-2xl border border-slate-200 bg-white p-1.5 shadow-sm">
        <NavLink
          to={overview.to}
          end
          className={cn(
            'inline-flex h-9 items-center rounded-xl px-3.5 text-sm font-semibold transition',
            overviewActive ? 'bg-slate-900 text-white shadow-sm' : 'text-slate-600 hover:bg-slate-100 hover:text-slate-900',
          )}
        >
          {overview.label}
        </NavLink>
        <span aria-hidden className="mx-0.5 h-5 w-px bg-slate-200" />
        {financeNavGroups.map((group) => {
          const isActive = group === activeGroup
          return (
            <NavLink
              key={group.label}
              to={group.to}
              className={cn(
                'inline-flex h-9 items-center rounded-xl px-3.5 text-sm font-semibold transition',
                isActive ? 'bg-slate-900 text-white shadow-sm' : 'text-slate-600 hover:bg-slate-100 hover:text-slate-900',
              )}
            >
              {group.label}
            </NavLink>
          )
        })}
      </div>

      {/* Secondary: items within the active section. Hidden on Overview. */}
      {activeGroup ? (
        <div className="flex flex-wrap items-center gap-1 px-1">
          {activeGroup.items.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              className={({ isActive }) =>
                cn(
                  'inline-flex h-8 items-center rounded-lg px-3 text-[13px] font-medium transition',
                  isActive
                    ? 'bg-slate-100 text-slate-900 ring-1 ring-inset ring-slate-200'
                    : 'text-slate-500 hover:bg-slate-50 hover:text-slate-800',
                )
              }
            >
              {item.label}
            </NavLink>
          ))}
        </div>
      ) : null}
    </nav>
  )
}
