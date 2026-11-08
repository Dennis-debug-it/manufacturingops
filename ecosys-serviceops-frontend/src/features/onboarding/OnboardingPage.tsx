import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Link, Navigate } from 'react-router-dom'
import { PageHeader } from '../../shared/components/PageHeader'
import { useAuth } from '../../shared/hooks/useAuth'
import { usePermission } from '../../shared/hooks/usePermission'
import { useEntitlements } from '../../shared/hooks/useEntitlements'
import { api } from '../../shared/api/apiClient'
import { endpoints } from '../../shared/api/endpoints'
import { ClientFormDrawer } from '../clients/components/ClientFormDrawer'
import { NewWorkOrderForm } from '../work-orders/components/NewWorkOrderForm'
import { NewUserFormDrawer } from '../team/components/NewUserFormDrawer'
import { StarterPacks } from '../settings/components/StarterPacks'

export function OnboardingPage() {
  const { tenant, user } = useAuth()
  const isAdmin = user?.role === 'admin'
  const { hasModule, planName, isLoading } = useEntitlements()
  const canAddCustomer = usePermission('clients.edit')
  const canCreateWorkOrder = usePermission('work-orders.create')
  const canInviteTeam = usePermission('team.manage')
  const [action, setAction] = useState<'customer' | 'work-order' | 'team' | null>(null)
  const profile = useQuery({
    queryKey: ['tenant-profile', tenant?.tenantId],
    queryFn: () => api.get<{ serviceOpsEmail?: string | null }>(endpoints.tenant.profile),
    // The workspace profile is an admin-only service.
    enabled: Boolean(tenant?.tenantId) && isAdmin,
  })
  const buttonClass = 'inline-flex min-h-11 items-center justify-center rounded-lg border border-slate-200 bg-white px-4 py-3 text-sm font-medium text-slate-900 transition hover:bg-slate-50'

  // Workspace setup is the admin's job; anyone else who lands here goes to their own home screen.
  if (user && !isAdmin) return <Navigate to="/dashboard" replace />

  return (
    <div className="space-y-4">
      <PageHeader title="Welcome to Ecosys ServiceOps" subtitle="Your workspace is ready. Start with what you need today." />
      <section className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
        <h2 className="text-xl font-semibold text-slate-900">{tenant?.companyName}</h2>
        <p className="mt-1 text-sm text-slate-600">{planName ?? (isLoading ? 'Loading plan…' : 'ServiceOps')}</p>
        <div className="mt-5 rounded-xl border border-emerald-200 bg-emerald-50 p-4">
          <p className="text-xs font-semibold uppercase tracking-wider text-emerald-800">Workspace email</p>
          <p className="mt-1 break-all text-sm font-medium text-emerald-950">{profile.data?.serviceOpsEmail ?? (profile.isLoading ? 'Loading workspace email…' : 'Contact your platform administrator for your workspace email.')}</p>
        </div>
        <div className="mt-6 flex flex-wrap gap-3">
          {canAddCustomer ? <button className={buttonClass} onClick={() => setAction('customer')}>Add Customer</button> : null}
          {hasModule('operations') && canCreateWorkOrder ? <button className={buttonClass} onClick={() => setAction('work-order')}>Create Work Order</button> : null}
          {canInviteTeam ? <button className={buttonClass} onClick={() => setAction('team')}>Invite Team</button> : null}
          {!isLoading ? <Link to={hasModule('operations') ? '/dashboard' : '/finance'} className="inline-flex min-h-11 items-center justify-center rounded-lg bg-slate-900 px-4 py-3 text-sm font-medium text-white hover:bg-slate-800">Go to Dashboard</Link> : null}
        </div>
        <p className="mt-5 text-sm text-slate-500">You can complete company details and preferences in Settings whenever you need them.</p>
      </section>
      {hasModule('operations') ? (
        <section className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
          <StarterPacks compact />
        </section>
      ) : null}
      {action === 'customer' && canAddCustomer ? <ClientFormDrawer open onClose={() => setAction(null)} /> : null}
      {action === 'work-order' && hasModule('operations') && canCreateWorkOrder ? <NewWorkOrderForm open onClose={() => setAction(null)} /> : null}
      {action === 'team' && canInviteTeam ? <NewUserFormDrawer open onClose={() => setAction(null)} /> : null}
    </div>
  )
}
