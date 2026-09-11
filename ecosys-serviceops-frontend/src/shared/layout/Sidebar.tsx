import { Link, useLocation } from 'react-router-dom'
import { cn } from '../utils/cn'
import { Logo } from '../components/Logo'
import { tenantNavItems, type NavItem } from './navigation'
import { useAuth } from '../hooks/useAuth'
import { usePermission } from '../hooks/usePermission'
import { useEntitlements } from '../hooks/useEntitlements'

function NavLink({ item, active }: { item: NavItem; active: boolean }) {
  const allowed = usePermission(item.permission ?? 'dashboard.view')
  if (!allowed) {
    return null
  }
  const Icon = item.icon
  return (
    <Link
      to={item.to}
      className={cn(
        'flex items-center gap-3 rounded-xl px-3 py-2.5 text-sm font-medium transition',
        active ? 'bg-slate-900 text-white shadow-sm' : 'text-slate-700 hover:bg-slate-100 hover:text-slate-900',
      )}
    >
      <Icon className="h-4 w-4" />
      {item.label}
    </Link>
  )
}

export function Sidebar() {
  const { user, tenant } = useAuth()
  const { canAccessPath } = useEntitlements()
  const location = useLocation()

  return (
    <aside className="hidden h-screen w-72 shrink-0 border-r border-slate-200 bg-white lg:flex lg:flex-col">
      <div className="border-b border-slate-200 px-6 py-5">
        <Logo className="h-9 w-auto" />
        <div className="mt-3 text-sm font-medium text-slate-900">{tenant?.companyName ?? 'Tenant'}</div>
      </div>
      <nav className="flex-1 space-y-1 overflow-y-auto px-3 py-4">
        {tenantNavItems
          .filter((item) => item.roles.includes(user?.role ?? 'viewer') && canAccessPath(item.to))
          .map((item) => {
            const active = location.pathname === item.to || location.pathname.startsWith(`${item.to}/`)
            return <NavLink key={item.to} item={item} active={active} />
          })}
      </nav>
      <div className="border-t border-slate-200 px-6 py-4 text-xs text-slate-500">
        <div className="font-medium text-slate-900">{user?.fullName}</div>
        <div>{user?.email}</div>
      </div>
    </aside>
  )
}
