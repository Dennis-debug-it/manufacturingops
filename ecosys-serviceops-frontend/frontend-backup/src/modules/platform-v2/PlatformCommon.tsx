import type { ReactNode } from 'react'
import { NavLink } from 'react-router-dom'
import { Badge } from '../../components/ui/Badge'
import type { LicenseStatus, PlatformUserStatus, TenantStatus } from '../../types/platform'

export function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <label className="space-y-2">
      <span className="text-xs font-semibold uppercase tracking-[0.16em] text-muted">{label}</span>
      {children}
    </label>
  )
}

export function SectionTitle({ title, description, action }: { title: string; description: string; action?: ReactNode }) {
  return (
    <div className="flex flex-wrap items-start justify-between gap-3">
      <div>
        <p className="text-lg font-semibold text-app">{title}</p>
        <p className="mt-1 text-sm text-muted">{description}</p>
      </div>
      {action}
    </div>
  )
}

export function tenantStatusBadge(status: TenantStatus) {
  if (status === 'Active') return <Badge tone="success">{status}</Badge>
  if (status === 'Trial') return <Badge tone="info">{status}</Badge>
  if (status === 'Suspended') return <Badge tone="warning">{status}</Badge>
  return <Badge tone="neutral">{status}</Badge>
}

export function licenseStatusBadge(status: LicenseStatus) {
  if (status === 'Active') return <Badge tone="success">{status}</Badge>
  if (status === 'Trial') return <Badge tone="info">{status}</Badge>
  if (status === 'Expired') return <Badge tone="danger">{status}</Badge>
  return <Badge tone="warning">{status}</Badge>
}

export function userStatusBadge(status: PlatformUserStatus) {
  return status === 'Active' ? <Badge tone="success">Active</Badge> : <Badge tone="neutral">Disabled</Badge>
}

export function TabLinks({
  links,
}: {
  links: Array<{ label: string; to: string }>
}) {
  return (
    <div className="flex gap-2 overflow-x-auto rounded-2xl border border-app bg-app/30 p-2">
      {links.map((link) => (
        <NavLink
          key={link.to}
          to={link.to}
          end={link.to.endsWith('/finance') || link.to.endsWith('/settings')}
          className={({ isActive }) =>
            `shrink-0 rounded-xl px-3 py-2 text-sm font-semibold transition ${isActive ? 'bg-accent-strong text-black' : 'text-muted hover:text-app'}`
          }
        >
          {link.label}
        </NavLink>
      ))}
    </div>
  )
}
