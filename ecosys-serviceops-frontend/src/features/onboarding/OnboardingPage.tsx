import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Link, Navigate } from 'react-router-dom'
import { ArrowRight, Boxes, CalendarCheck, ClipboardCheck, GitBranch, Users, type LucideIcon } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { useAuth } from '../../shared/hooks/useAuth'
import { usePermission } from '../../shared/hooks/usePermission'
import { useEntitlements } from '../../shared/hooks/useEntitlements'
import { api } from '../../shared/api/apiClient'
import { endpoints } from '../../shared/api/endpoints'
import { NewUserFormDrawer } from '../team/components/NewUserFormDrawer'
import { StarterPacks } from '../settings/components/StarterPacks'

interface SetupStep {
  icon: LucideIcon
  title: string
  text: string
  to?: string
  onClick?: () => void
}

function Step({ step, index }: { step: SetupStep; index: number }) {
  const Icon = step.icon
  const body = (
    <>
      <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-[var(--app-primary-soft)] text-sm font-semibold text-[var(--app-primary)]">{index + 1}</span>
      <span className="min-w-0 flex-1">
        <span className="flex items-center gap-2 text-sm font-semibold text-slate-900"><Icon className="h-4 w-4 text-slate-500" />{step.title}</span>
        <span className="mt-0.5 block text-sm text-slate-600">{step.text}</span>
      </span>
      <ArrowRight className="h-4 w-4 shrink-0 text-slate-400" />
    </>
  )
  const className = 'flex w-full items-center gap-4 rounded-xl border border-slate-200 bg-white p-4 text-left transition hover:border-slate-300 hover:bg-slate-50'
  return step.to ? <Link to={step.to} className={className}>{body}</Link> : <button type="button" onClick={step.onClick} className={className}>{body}</button>
}

/**
 * Where a new workspace admin lands after accepting the invitation: five steps that take a factory
 * from empty to running planned maintenance, in the order the data depends on each other.
 */
export function OnboardingPage() {
  const { tenant, user } = useAuth()
  const isAdmin = user?.role === 'admin'
  const { hasModule, planName, isLoading } = useEntitlements()
  const canInviteTeam = usePermission('team.manage')
  const [inviting, setInviting] = useState(false)
  const profile = useQuery({
    queryKey: ['tenant-profile', tenant?.tenantId],
    queryFn: () => api.get<{ serviceOpsEmail?: string | null }>(endpoints.tenant.profile),
    // The workspace profile is an admin-only service.
    enabled: Boolean(tenant?.tenantId) && isAdmin,
  })

  // Workspace setup is the admin's job; anyone else who lands here goes to their own home screen.
  if (user && !isAdmin) return <Navigate to="/dashboard" replace />

  const maintenance = hasModule('maintenance')
  const steps: SetupStep[] = [
    ...(maintenance ? [
      { icon: GitBranch, title: 'Lay out your factory', text: 'Factories and their sections (withering, rolling, drying, boilers…), and the weekly maintenance window.', to: '/settings/organisation' },
      { icon: Boxes, title: 'Add your assets', text: 'Import the asset register from a spreadsheet, or add machines one by one.', to: '/assets?import=1' },
      { icon: ClipboardCheck, title: 'Set up running checks', text: 'The shift and daily checks operators and technicians tick off on each machine.', to: '/settings/running-checks' },
      { icon: CalendarCheck, title: 'Plan preventive maintenance', text: 'Schedules by calendar, running hours or kilometres, so jobs raise themselves.', to: '/maintenance/pm' },
    ] : []),
    ...(canInviteTeam ? [{ icon: Users, title: 'Invite your team', text: 'Engineers, supervisors, technicians and the stores team, each with their own role.', onClick: () => setInviting(true) }] : []),
  ]

  return (
    <div className="space-y-4">
      <PageHeader title="Welcome to Ecosys Factory Maintenance" subtitle="Your workspace is ready. These steps get your factory running; do them in any order and come back any time." />
      <section className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
        <div className="flex flex-wrap items-start justify-between gap-4">
          <div>
            <h2 className="text-xl font-semibold text-slate-900">{tenant?.companyName}</h2>
            <p className="mt-1 text-sm text-slate-600">{planName ?? (isLoading ? 'Loading plan…' : 'Ecosys')}</p>
          </div>
          {!isLoading ? <Link to="/dashboard" className="inline-flex min-h-11 items-center justify-center rounded-lg bg-slate-900 px-4 py-3 text-sm font-medium text-white hover:bg-slate-800">Go to Dashboard</Link> : null}
        </div>
        <div className="mt-5 rounded-xl border border-emerald-200 bg-emerald-50 p-4">
          <p className="text-xs font-semibold uppercase tracking-wider text-emerald-800">Workspace email</p>
          <p className="mt-1 break-all text-sm font-medium text-emerald-950">{profile.data?.serviceOpsEmail ?? (profile.isLoading ? 'Loading workspace email…' : 'Contact your platform administrator for your workspace email.')}</p>
        </div>
      </section>
      {steps.length > 0 ? (
        <section aria-labelledby="setup-steps" className="space-y-2">
          <h2 id="setup-steps" className="text-sm font-semibold uppercase tracking-wider text-slate-500">Set up your factory</h2>
          {steps.map((step, index) => <Step key={step.title} step={step} index={index} />)}
        </section>
      ) : null}
      {maintenance ? (
        <section className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
          <StarterPacks compact />
        </section>
      ) : null}
      <p className="text-sm text-slate-500">Company details, branding and preferences are in Settings whenever you need them.</p>
      {inviting && canInviteTeam ? <NewUserFormDrawer open onClose={() => setInviting(false)} /> : null}
    </div>
  )
}
