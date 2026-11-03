import { Link, useLocation } from 'react-router-dom'
import { cn } from '../../../shared/utils/cn'

/**
 * Sub-navigation for the Time & Attendance section. Its five pages (live, timesheets, timesheet
 * detail, leave, reports) were each routed but only cross-linked between timesheets and timesheet
 * detail, so the section had no entry point and no way to move around inside it.
 *
 * `/team/time-attendance` and `/team/time-attendance/live` render the same page, so the Live tab
 * matches the bare path exactly rather than as a prefix — as a prefix it would match every tab.
 */
interface TimeAttendanceTab {
  to: string
  label: string
  exact?: string[]
  prefix: string[]
}

const tabs: TimeAttendanceTab[] = [
  { to: '/team/time-attendance/live', label: 'Live', exact: ['/team/time-attendance'], prefix: ['/team/time-attendance/live'] },
  { to: '/team/time-attendance/timesheets', label: 'Timesheets', prefix: ['/team/time-attendance/timesheets'] },
  { to: '/team/time-attendance/leave', label: 'Leave', prefix: ['/team/time-attendance/leave'] },
  { to: '/team/time-attendance/reports', label: 'Reports', prefix: ['/team/time-attendance/reports'] },
]

export function TimeAttendanceTabNav() {
  const location = useLocation()
  return (
    <div className="flex flex-wrap gap-2">
      {tabs.map((tab) => {
        const active =
          (tab.exact ?? []).includes(location.pathname) ||
          tab.prefix.some((prefix) => location.pathname === prefix || location.pathname.startsWith(`${prefix}/`))
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
