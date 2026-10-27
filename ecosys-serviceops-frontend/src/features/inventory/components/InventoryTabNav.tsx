import { Link, useLocation } from 'react-router-dom'
import { cn } from '../../../shared/utils/cn'

const tabs: Array<{ to: string; label: string; match: string[]; exact?: string[] }> = [
  { to: '/inventory/stock', label: 'Stock', match: ['/inventory/stock'], exact: ['/inventory'] },
  { to: '/inventory/parts', label: 'Parts', match: ['/inventory/parts'] },
  { to: '/inventory/issues', label: 'Issues', match: ['/inventory/issues'] },
  { to: '/inventory/restock-requests', label: 'Restock Requests', match: ['/inventory/restock-requests'] },
  { to: '/inventory/parts-requests', label: 'Parts Requests', match: ['/inventory/parts-requests'] },
  { to: '/inventory/parts-returns', label: 'Parts Returns', match: ['/inventory/parts-returns'] },
  { to: '/inventory/purchase-orders', label: 'Purchase Orders', match: ['/inventory/purchase-orders'] },
  { to: '/inventory/suppliers', label: 'Suppliers', match: ['/inventory/suppliers'] },
  { to: '/inventory/movements', label: 'Movements', match: ['/inventory/movements'] },
  { to: '/inventory/stock-counts', label: 'Stock Counts', match: ['/inventory/stock-counts'] },
  { to: '/inventory/locations', label: 'Locations', match: ['/inventory/locations'] },
  { to: '/inventory/vans', label: 'Vans', match: ['/inventory/vans'] },
  { to: '/inventory/tools', label: 'Tools', match: ['/inventory/tools'] },
  { to: '/inventory/alerts', label: 'Alerts', match: ['/inventory/alerts'] },
  { to: '/inventory/reports', label: 'Reports', match: ['/inventory/reports'] },
]

export function InventoryTabNav() {
  const location = useLocation()
  return (
    <div className="flex flex-wrap gap-2">
      {tabs.map((tab) => {
        // '/inventory' itself is the Stock page; as a prefix it would light Stock up on every tab.
        const active =
          (tab.exact ?? []).includes(location.pathname) ||
          tab.match.some((prefix) => location.pathname === prefix || location.pathname.startsWith(`${prefix}/`))
        return (
          <Link
            key={tab.to}
            to={tab.to}
            className={cn(
              'inline-flex h-10 items-center gap-2 rounded-full border px-3.5 text-sm font-medium transition',
              active ? 'border-[var(--app-primary)] bg-[var(--app-primary)] text-white' : 'border-slate-200 bg-white text-slate-700 hover:bg-slate-50',
            )}
          >
            {tab.label}
          </Link>
        )
      })}
    </div>
  )
}
