import { Link, useLocation } from 'react-router-dom'
import { cn } from '../../../shared/utils/cn'

const tabs = [
  { to: '/clients', label: 'Clients' },
  { to: '/sites', label: 'Sites' },
  { to: '/assets', label: 'Assets' },
]

export function TriTabNav() {
  const location = useLocation()
  return (
    <div className="flex flex-wrap gap-2">
      {tabs.map((tab) => {
        const active = location.pathname === tab.to
        return (
          <Link
            key={tab.to}
            to={tab.to}
            className={cn(
              'inline-flex h-11 items-center gap-2 rounded-full border px-4 text-sm font-medium transition',
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
