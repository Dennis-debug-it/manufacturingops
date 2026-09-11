import { Link, useLocation } from 'react-router-dom'
import { cn } from '../../../shared/utils/cn'
import { useEntitlements } from '../../../shared/hooks/useEntitlements'

const tabs = [
  { to: '/clients', label: 'Clients' },
  { to: '/sites', label: 'Sites' },
  { to: '/assets', label: 'Assets' },
]

export function TriTabNav() {
  const location = useLocation()
  const { canAccessPath } = useEntitlements()
  return (
    <div className="flex flex-wrap gap-2">
      {tabs.filter((tab) => canAccessPath(tab.to)).map((tab) => {
        const active = location.pathname === tab.to
        return (
          <Link
            key={tab.to}
            to={tab.to}
            className={cn(
              'inline-flex h-11 items-center gap-2 rounded-full border px-4 text-sm font-medium transition',
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
