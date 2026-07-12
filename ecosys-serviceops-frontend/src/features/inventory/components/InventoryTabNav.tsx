import { Link, useLocation } from 'react-router-dom'
import { cn } from '../../../shared/utils/cn'

const tabs = [
  { to: '/inventory/stock', label: 'Stock', match: ['/inventory', '/inventory/stock'] },
  { to: '/inventory/parts', label: 'Parts', match: ['/inventory/parts'] },
  { to: '/inventory/issues', label: 'Issues', match: ['/inventory/issues'] },
  { to: '/inventory/restock-requests', label: 'Restock Requests', match: ['/inventory/restock-requests'] },
  { to: '/inventory/purchase-orders', label: 'Purchase Orders', match: ['/inventory/purchase-orders'] },
  { to: '/inventory/suppliers', label: 'Suppliers', match: ['/inventory/suppliers'] },
  { to: '/inventory/movements', label: 'Movements', match: ['/inventory/movements'] },
  { to: '/inventory/vans', label: 'Vans', match: ['/inventory/vans'] },
  { to: '/inventory/alerts', label: 'Alerts', match: ['/inventory/alerts'] },
  { to: '/inventory/reports', label: 'Reports', match: ['/inventory/reports'] },
]

export function InventoryTabNav() {
  const location = useLocation()
  return (
    <div className="flex flex-wrap gap-2">
      {tabs.map((tab) => {
        const active = tab.match.some((prefix) => location.pathname === prefix || location.pathname.startsWith(`${prefix}/`))
        return (
          <Link
            key={tab.to}
            to={tab.to}
            className={cn(
              'inline-flex h-10 items-center gap-2 rounded-full border px-3.5 text-sm font-medium transition',
              active ? 'border-slate-900 bg-slate-900 text-white' : 'border-slate-200 bg-white text-slate-700 hover:bg-slate-50',
            )}
          >
            {tab.label}
          </Link>
        )
      })}
    </div>
  )
}
