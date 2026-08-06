import { Link, useLocation } from 'react-router-dom'
import { cn } from '../../../shared/utils/cn'

const tabs = [
  { to: '/team/users', label: 'Users', match: ['/team', '/team/users'] },
  { to: '/team/capabilities', label: 'Roles & Permissions', match: ['/team/capabilities', '/team/roles'] },
  { to: '/team/audit-history', label: 'Audit History', match: ['/team/audit-history'] },
]

export function TeamTabNav() {
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
