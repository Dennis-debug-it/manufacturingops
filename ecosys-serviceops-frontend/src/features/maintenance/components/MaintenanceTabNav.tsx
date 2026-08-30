import { Link, useLocation } from 'react-router-dom'
import { cn } from '../../../shared/utils/cn'

/**
 * Section navigation for Maintenance. The sidebar only ever pointed at the calendar; schedules
 * were reachable through a card on a calendar day, PM compliance only through a prose link on the
 * Reports page, and Engine Status not at all.
 */
const tabs = [
  { to: '/maintenance/calendar', label: 'Calendar', prefix: ['/maintenance/calendar'] },
  { to: '/maintenance/schedules', label: 'PM Schedules', prefix: ['/maintenance/schedules'] },
  { to: '/maintenance/compliance', label: 'PM Compliance', prefix: ['/maintenance/compliance'] },
  { to: '/maintenance/engine', label: 'Engine Status', prefix: ['/maintenance/engine'] },
]

export function MaintenanceTabNav() {
  const location = useLocation()
  return (
    <div className="flex flex-wrap gap-2">
      {tabs.map((tab) => {
        const active = tab.prefix.some((prefix) => location.pathname === prefix || location.pathname.startsWith(`${prefix}/`))
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
