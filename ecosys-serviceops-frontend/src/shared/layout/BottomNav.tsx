import { useEffect, useState } from 'react'
import { Link, useLocation } from 'react-router-dom'
import { Bell, CalendarDays, ClipboardList, MoreHorizontal, UserCircle2, Watch, X } from 'lucide-react'
import { cn } from '../utils/cn'
import { useAuth } from '../hooks/useAuth'
import { tenantNavItems, type NavItem } from './navigation'

const technicianNav = [
  { label: 'Jobs', to: '/work-orders', icon: ClipboardList },
  { label: 'My Time', to: '/me/time', icon: Watch },
  { label: 'Today', to: '/maintenance/calendar', icon: CalendarDays },
  { label: 'Alerts', to: '/reports', icon: Bell },
  { label: 'Profile', to: '/settings/account', icon: UserCircle2 },
]

function isActive(pathname: string, to: string) {
  return pathname === to || pathname.startsWith(`${to}/`)
}

function MoreSheet({ items, onClose }: { items: Pick<NavItem, 'label' | 'to' | 'icon'>[]; onClose: () => void }) {
  const location = useLocation()

  return (
    // z-[1100]: Leaflet panes/controls (used by the Live Map and Work Orders Map views)
    // default to z-index 1000, so this sheet must clear that to stay on top of the map.
    <div className="fixed inset-0 z-[1100] flex items-end lg:hidden">
      <button type="button" aria-label="Close menu" className="absolute inset-0 bg-slate-900/40" onClick={onClose} />
      <div className="relative w-full rounded-t-2xl border-t border-slate-200 bg-white p-4 pb-24 shadow-2xl">
        <div className="flex items-center justify-between">
          <span className="text-sm font-semibold text-slate-900">All modules</span>
          <button type="button" onClick={onClose} className="rounded-lg p-2 text-slate-500 transition hover:bg-slate-100 hover:text-slate-900">
            <X className="h-4 w-4" />
          </button>
        </div>
        <div className="mt-3 grid grid-cols-3 gap-2 sm:grid-cols-4">
          {items.map((item) => {
            const Icon = item.icon
            const active = isActive(location.pathname, item.to)
            return (
              <Link
                key={item.to}
                to={item.to}
                onClick={onClose}
                className={cn(
                  'flex flex-col items-center gap-1.5 rounded-xl px-2 py-3 text-xs font-medium transition',
                  active ? 'bg-slate-900 text-white' : 'text-slate-600 hover:bg-slate-100 hover:text-slate-900',
                )}
              >
                <Icon className="h-5 w-5" />
                <span className="truncate">{item.label}</span>
              </Link>
            )
          })}
        </div>
      </div>
    </div>
  )
}

export function BottomNav() {
  const { user } = useAuth()
  const location = useLocation()
  const [showMore, setShowMore] = useState(false)

  useEffect(() => {
    setShowMore(false)
  }, [location.pathname])

  const allItems = user?.role === 'technician'
    ? technicianNav
    : tenantNavItems.filter((item) => item.roles.includes(user?.role ?? 'viewer'))

  if (allItems.length === 0) {
    return null
  }

  const hasOverflow = allItems.length > 5
  const primaryItems = hasOverflow ? allItems.slice(0, 4) : allItems
  const overflowItems = hasOverflow ? allItems.slice(4) : []

  return (
    <>
      {showMore && overflowItems.length > 0 ? <MoreSheet items={overflowItems} onClose={() => setShowMore(false)} /> : null}
      <nav className="fixed inset-x-0 bottom-0 z-30 border-t border-slate-200 bg-white shadow-[0_-1px_3px_rgba(15,23,42,0.06)] px-2 py-2 lg:hidden">
        <div className="grid grid-cols-5 gap-1">
          {primaryItems.map((item) => {
            const active = isActive(location.pathname, item.to)
            const Icon = item.icon
            return (
              <Link
                key={item.to}
                to={item.to}
                className={cn(
                  'flex flex-col items-center gap-1 rounded-xl px-2 py-2 text-[11px] font-medium transition',
                  active ? 'bg-slate-900 text-white' : 'text-slate-500 hover:bg-slate-100 hover:text-slate-900',
                )}
              >
                <Icon className="h-4 w-4" />
                <span className="truncate">{item.label}</span>
              </Link>
            )
          })}
          {hasOverflow ? (
            <button
              type="button"
              onClick={() => setShowMore((current) => !current)}
              className={cn(
                'flex flex-col items-center gap-1 rounded-xl px-2 py-2 text-[11px] font-medium transition',
                showMore || overflowItems.some((item) => isActive(location.pathname, item.to))
                  ? 'bg-slate-900 text-white'
                  : 'text-slate-500 hover:bg-slate-100 hover:text-slate-900',
              )}
            >
              <MoreHorizontal className="h-4 w-4" />
              <span className="truncate">More</span>
            </button>
          ) : null}
        </div>
      </nav>
    </>
  )
}
