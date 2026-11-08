import { useState } from 'react'
import { Link, useLocation } from 'react-router-dom'
import { ChevronDown } from 'lucide-react'
import { cn } from '../utils/cn'
import { Logo } from '../components/Logo'
import { navGroups, tenantNavItems, type NavGroup, type NavItem } from './navigation'
import { useAuth } from '../hooks/useAuth'
import { hasPermission } from '../hooks/usePermission'
import { useEntitlements } from '../hooks/useEntitlements'

const COLLAPSED_KEY = 'ecosys.sidebar.collapsed'

function isActive(pathname: string, to: string) {
  return pathname === to || pathname.startsWith(`${to}/`)
}

function readCollapsed(): NavGroup[] {
  try {
    const value: unknown = JSON.parse(window.localStorage.getItem(COLLAPSED_KEY) ?? '[]')
    return Array.isArray(value) ? value.filter((x): x is NavGroup => navGroups.includes(x as NavGroup)) : []
  } catch {
    return []
  }
}

function writeCollapsed(groups: NavGroup[]) {
  try {
    window.localStorage.setItem(COLLAPSED_KEY, JSON.stringify(groups))
  } catch {
    // Private mode or blocked storage: the sidebar still works, it just forgets.
  }
}

function NavLink({ item, active }: { item: NavItem; active: boolean }) {
  const Icon = item.icon
  return (
    <Link
      to={item.to}
      aria-current={active ? 'page' : undefined}
      className={cn(
        'relative flex items-center gap-3 rounded-xl px-3 py-2 text-sm font-medium transition',
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

/** The items this person may open, grouped by section in display order; empty sections are left out. */
export function useVisibleNavGroups(): { group: NavGroup; items: NavItem[] }[] {
  const { user } = useAuth()
  const { canAccessPath } = useEntitlements()
  const visible = tenantNavItems.filter(
    (item) => item.roles.includes(user?.role ?? 'viewer') && canAccessPath(item.to) && hasPermission(user, item.permission ?? 'dashboard.view'),
  )
  return navGroups
    .map((group) => ({ group, items: visible.filter((item) => item.group === group) }))
    .filter((section) => section.items.length > 0)
}

export function Sidebar() {
  const { user, tenant } = useAuth()
  const location = useLocation()
  const sections = useVisibleNavGroups()
  const [collapsed, setCollapsed] = useState<NavGroup[]>(readCollapsed)

  const toggle = (group: NavGroup) => {
    const next = collapsed.includes(group) ? collapsed.filter((x) => x !== group) : [...collapsed, group]
    setCollapsed(next)
    writeCollapsed(next)
  }

  return (
    <aside className="hidden h-screen w-72 shrink-0 bg-gradient-to-b from-[#073F43] to-[#052D30] lg:flex lg:flex-col">
      <div className="border-b border-white/10 px-6 py-5">
        <Logo tone="dark" className="h-9 w-auto" />
        <div className="mt-3 text-sm font-medium text-white">{tenant?.companyName ?? 'Tenant'}</div>
      </div>
      <nav className="flex-1 space-y-3 overflow-y-auto px-3 py-4" aria-label="Main">
        {sections.map(({ group, items }) => {
          const hasActive = items.some((item) => isActive(location.pathname, item.to))
          // The section holding the current page stays open even if it was collapsed.
          const open = hasActive || !collapsed.includes(group)
          const listId = `nav-${group.replace(/\s+/g, '-').toLowerCase()}`
          return (
            <div key={group}>
              {sections.length > 1 ? (
                <button
                  type="button"
                  onClick={() => toggle(group)}
                  aria-expanded={open}
                  aria-controls={listId}
                  className="flex w-full items-center justify-between rounded-lg px-3 py-1 text-[11px] font-semibold uppercase tracking-wider text-white/45 transition hover:text-white/80"
                >
                  {group}
                  <ChevronDown className={cn('h-3.5 w-3.5 transition-transform', open ? '' : '-rotate-90')} />
                </button>
              ) : null}
              {open ? (
                <div id={listId} className="mt-1 space-y-0.5">
                  {items.map((item) => <NavLink key={item.to} item={item} active={isActive(location.pathname, item.to)} />)}
                </div>
              ) : null}
            </div>
          )
        })}
      </nav>
      <div className="border-t border-white/10 px-6 py-4 text-xs text-white/60">
        <div className="font-medium text-white">{user?.fullName}</div>
        <div>{user?.email}</div>
      </div>
    </aside>
  )
}
