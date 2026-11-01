import type { ReactNode } from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { cn } from '../../../shared/utils/cn'
import { useInventoryRequests } from '../requestsApi'

interface NavItem {
  to: string
  label: string
  /** Other paths that belong to this item (detail pages, old addresses). */
  match?: string[]
}

interface NavGroup {
  label: string
  items: NavItem[]
}

export const inventoryNav: NavGroup[] = [
  {
    label: 'Overview',
    items: [
      { to: '/inventory', label: 'Overview' },
      { to: '/inventory/stock', label: 'Stock levels' },
    ],
  },
  {
    label: 'Parts',
    items: [{ to: '/inventory/parts', label: 'Parts catalogue' }],
  },
  {
    label: 'Requests',
    items: [
      { to: '/inventory/requests', label: 'Requests', match: ['/inventory/issues', '/inventory/parts-requests', '/inventory/restock-requests'] },
      { to: '/inventory/parts-returns', label: 'Returns' },
      { to: '/inventory/faulty-parts', label: 'Faulty parts' },
    ],
  },
  {
    label: 'Purchasing',
    items: [
      { to: '/inventory/alerts', label: 'Reorder' },
      { to: '/inventory/purchase-orders', label: 'Purchase orders' },
      { to: '/inventory/suppliers', label: 'Suppliers' },
    ],
  },
  {
    label: 'Stock control',
    items: [
      { to: '/inventory/movements', label: 'Movements & transfers' },
      { to: '/inventory/stock-counts', label: 'Stock counts' },
      { to: '/inventory/locations', label: 'Locations' },
      { to: '/inventory/vans', label: 'Vans' },
    ],
  },
  {
    label: 'Tools & reports',
    items: [
      { to: '/inventory/tools', label: 'Tools' },
      { to: '/inventory/reports', label: 'Reports' },
    ],
  },
]

const allItems = inventoryNav.flatMap((group) => group.items)

function isActive(item: NavItem, pathname: string) {
  // '/inventory' is the Overview; as a prefix it would match every page.
  if (item.to === '/inventory') return pathname === '/inventory'
  return [item.to, ...(item.match ?? [])].some((prefix) => pathname === prefix || pathname.startsWith(`${prefix}/`))
}

/**
 * Inventory's own menu, grouped by job, beside every inventory page. It replaces the row of fifteen
 * pill tabs each page used to repeat. On a phone it collapses to a single "Go to" list.
 */
export function InventoryShell({ children }: { children: ReactNode }) {
  const { pathname } = useLocation()
  const navigate = useNavigate()
  const { data: requests } = useInventoryRequests()
  const openRequests = (requests ?? []).filter((request) => request.stage === 'Requested' || request.stage === 'Approved').length
  const current = allItems.find((item) => isActive(item, pathname))

  return (
    <div className="lg:grid lg:grid-cols-[12.5rem_minmax(0,1fr)] lg:gap-6">
      <nav aria-label="Inventory" className="mb-4 lg:mb-0">
        <label className="block lg:hidden">
          <span className="sr-only">Go to an inventory page</span>
          <select
            value={current?.to ?? ''}
            onChange={(event) => navigate(event.target.value)}
            className="h-11 w-full rounded-lg border border-slate-300 bg-white px-3 text-sm font-medium text-slate-800 outline-none focus:border-[var(--app-primary)]"
          >
            {current ? null : <option value="">Inventory</option>}
            {inventoryNav.map((group) => (
              <optgroup key={group.label} label={group.label}>
                {group.items.map((item) => (
                  <option key={item.to} value={item.to}>
                    {item.label}
                    {item.to === '/inventory/requests' && openRequests > 0 ? ` (${openRequests} open)` : ''}
                  </option>
                ))}
              </optgroup>
            ))}
          </select>
        </label>

        <div className="sticky top-4 hidden space-y-4 lg:block">
          {inventoryNav.map((group) => (
            <div key={group.label}>
              <p className="px-2 pb-1 text-[11px] font-semibold uppercase tracking-[0.12em] text-slate-500">{group.label}</p>
              <ul className="space-y-0.5">
                {group.items.map((item) => {
                  const active = isActive(item, pathname)
                  return (
                    <li key={item.to}>
                      <Link
                        to={item.to}
                        aria-current={active ? 'page' : undefined}
                        className={cn(
                          'flex items-center justify-between rounded-lg px-2 py-1.5 text-sm transition',
                          active ? 'bg-[var(--app-primary)] font-medium text-white' : 'text-slate-700 hover:bg-slate-100',
                        )}
                      >
                        <span>{item.label}</span>
                        {item.to === '/inventory/requests' && openRequests > 0 ? (
                          <span className={cn('rounded-full px-1.5 text-[11px] font-semibold tabular-nums', active ? 'bg-white/20 text-white' : 'bg-amber-100 text-amber-800')}>
                            {openRequests}
                          </span>
                        ) : null}
                      </Link>
                    </li>
                  )
                })}
              </ul>
            </div>
          ))}
        </div>
      </nav>
      <div className="min-w-0">{children}</div>
    </div>
  )
}
