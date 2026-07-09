import { Fragment } from 'react'
import { NavLink } from 'react-router-dom'
import { cn } from '../../shared/utils/cn'

interface FinanceNavItem {
  to: string
  label: string
  exact?: boolean
}

interface FinanceNavGroup {
  label: string
  items: FinanceNavItem[]
}

const financeNavGroups: FinanceNavGroup[] = [
  {
    label: 'Documents',
    items: [
      { to: '/finance', label: 'Overview', exact: true },
      { to: '/finance/documents', label: 'Documents' },
      { to: '/finance/quotes', label: 'Quotes' },
      { to: '/finance/invoices', label: 'Invoices' },
      { to: '/finance/receipts', label: 'Receipts' },
    ],
  },
  {
    label: 'Pricing',
    items: [
      { to: '/finance/rate-cards', label: 'Rate Cards' },
      { to: '/finance/preferential-rates', label: 'Preferential Rates' },
      { to: '/finance/exchange-rates', label: 'Exchange Rates' },
    ],
  },
  {
    label: 'Control',
    items: [
      { to: '/finance/billable-work', label: 'Billable Work' },
      { to: '/finance/invoice-approvals', label: 'Approvals' },
      { to: '/finance/credit-control', label: 'Credit Control' },
      { to: '/finance/aging', label: 'Aging' },
    ],
  },
  {
    label: 'Reports',
    items: [
      { to: '/finance/reports', label: 'Reports' },
      { to: '/finance/expenses', label: 'Expenses' },
      { to: '/finance/payables', label: 'Payables' },
    ],
  },
]

export function FinanceWorkspaceNav() {
  return (
    <nav className="overflow-x-auto rounded-2xl border border-slate-200 bg-white p-2 shadow-sm">
      <div className="flex min-w-max items-center gap-2">
        {financeNavGroups.map((group, groupIndex) => (
          <Fragment key={group.label}>
            {groupIndex > 0 ? <span aria-hidden className="mx-1 h-6 w-px shrink-0 bg-slate-200" /> : null}
            <span className="hidden select-none pl-1 pr-0.5 text-[10px] font-semibold uppercase tracking-[0.16em] text-slate-400 xl:inline">
              {group.label}
            </span>
            {group.items.map((item) => (
              <NavLink
                key={item.to}
                to={item.to}
                {...(item.exact ? { end: true } : {})}
                className={({ isActive }) =>
                  cn(
                    'inline-flex h-10 items-center rounded-xl px-3 text-sm font-medium transition',
                    isActive
                      ? 'bg-slate-900 text-white'
                      : 'text-slate-600 hover:bg-slate-100 hover:text-slate-900',
                  )
                }
              >
                {item.label}
              </NavLink>
            ))}
          </Fragment>
        ))}
      </div>
    </nav>
  )
}
