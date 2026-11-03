import { Link, useLocation } from 'react-router-dom'
import { cn } from '../../../shared/utils/cn'

/**
 * `exact` paths light the tab only on an exact match; `prefix` paths also light it for anything
 * nested beneath them. The distinction matters for `/team`, which is the Users landing route but
 * is also the prefix of every other tab here — treating it as a prefix kept Users highlighted on
 * every page in the section.
 */
interface TeamTab {
  to: string
  label: string
  exact?: string[]
  prefix: string[]
}

const tabs: TeamTab[] = [
  { to: '/team/users', label: 'Users', exact: ['/team'], prefix: ['/team/users'] },
  { to: '/team/capabilities', label: 'Roles & Permissions', prefix: ['/team/capabilities', '/team/roles'] },
  { to: '/team/permission-groups', label: 'Permission Groups', prefix: ['/team/permission-groups'] },
  { to: '/team/access-scopes', label: 'Access Scopes', prefix: ['/team/access-scopes'] },
  { to: '/team/approval-limits', label: 'Approval Limits', prefix: ['/team/approval-limits'] },
  { to: '/team/audit-history', label: 'Audit History', prefix: ['/team/audit-history'] },
  // Skills, Assignment Groups and the whole Time & Attendance section already rendered this nav
  // but were absent from it, so their routes existed with nothing anywhere linking to them.
  { to: '/team/skills', label: 'Skills', prefix: ['/team/skills'] },
  { to: '/team/groups', label: 'Assignment Groups', prefix: ['/team/groups'] },
  { to: '/team/time-attendance', label: 'Time & Attendance', prefix: ['/team/time-attendance'] },
]

export function TeamTabNav() {
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
