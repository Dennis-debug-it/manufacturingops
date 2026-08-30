import { Link, useLocation } from 'react-router-dom'
import { cn } from '../utils/cn'
import { Logo } from '../components/Logo'
import { tenantNavItems, type NavItem } from './navigation'
import { useAuth } from '../hooks/useAuth'
import { usePermission } from '../hooks/usePermission'

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
        'relative flex items-center gap-3 rounded-xl px-3 py-2.5 text-sm font-medium transition',
        active
          ? 'bg-white/10 text-white before:absolute before:left-0 before:top-1/2 before:h-5 before:w-[3px] before:-translate-y-1/2 before:rounded-full before:bg-[var(--app-lime)] before:content-[""]'
          : 'text-white/70 hover:bg-white/5 hover:text-white',
      )}
    >
      <Icon className="h-4 w-4" />
      {item.label}
    </Link>
  )
}

export function Sidebar() {
  const { user, tenant } = useAuth()
  const location = useLocation()

  return (
    <aside className="hidden h-screen w-72 shrink-0 bg-gradient-to-b from-[#073F43] to-[#052D30] lg:flex lg:flex-col">
      <div className="border-b border-white/10 px-6 py-5">
        <Logo tone="dark" className="h-9 w-auto" />
        <div className="mt-3 text-sm font-medium text-white">{tenant?.companyName ?? 'Tenant'}</div>
      </div>
      <nav className="flex-1 space-y-1 overflow-y-auto px-3 py-4">
        {tenantNavItems
          .filter((item) => item.roles.includes(user?.role ?? 'viewer'))
          .map((item) => {
            const active = location.pathname === item.to || location.pathname.startsWith(`${item.to}/`)
            return <NavLink key={item.to} item={item} active={active} />
          })}
      </nav>
      <div className="border-t border-white/10 px-6 py-4 text-xs text-white/60">
        <div className="font-medium text-white">{user?.fullName}</div>
        <div>{user?.email}</div>
      </div>
    </aside>
  )
}
