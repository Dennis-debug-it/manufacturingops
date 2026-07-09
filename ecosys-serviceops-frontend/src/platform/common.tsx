import type { ReactNode } from 'react'
import { NavLink } from 'react-router-dom'
import { StatusBadge } from '../shared/components/StatusBadge'
import type { LicenseStatus, TenantStatus, TrialStatus } from './types'

export function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <label className="space-y-2">
      <span className="text-xs font-semibold uppercase tracking-[0.16em] text-slate-500">{label}</span>
      {children}
    </label>
  )
}

export function SectionTitle({
  title,
  description,
  action,
}: {
  title: string
  description: string
  action?: ReactNode
}) {
  return (
    <div className="flex flex-wrap items-start justify-between gap-3">
      <div>
        <p className="text-lg font-semibold text-slate-900">{title}</p>
        <p className="mt-1 text-sm text-slate-600">{description}</p>
      </div>
      {action}
    </div>
  )
}

export function tenantStatusBadge(status: TenantStatus | string) {
  const normalized = String(status)
  return <StatusBadge status={normalized} />
}

export function licenseStatusBadge(status: LicenseStatus | string) {
  const normalized = String(status)
  return <StatusBadge status={normalized} />
}

export function trialStatusBadge(status: TrialStatus | string) {
  const normalized = String(status)
  return <StatusBadge status={normalized} />
}

export function miniSidebarLinkClass(isActive: boolean) {
  return isActive
    ? 'rounded-lg bg-slate-900 px-3 py-2 text-sm font-medium text-white'
    : 'rounded-lg px-3 py-2 text-sm font-medium text-slate-600 hover:bg-slate-100 hover:text-slate-900'
}

export function MiniSidebar({
  items,
}: {
  items: Array<{ label: string; to: string }>
}) {
  return (
    <div className="flex flex-wrap gap-1 rounded-2xl border border-slate-200 bg-white p-2 xl:flex-col">
      {items.map((item) => (
        <NavLink
          key={item.to}
          to={item.to}
          end={item.to === '/platform/settings'}
          className={({ isActive }) => miniSidebarLinkClass(isActive)}
        >
          {item.label}
        </NavLink>
      ))}
    </div>
  )
}
