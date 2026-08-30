import { useEffect, useState } from 'react'
import toast from 'react-hot-toast'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Plus } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { FormDrawer } from '../../shared/components/FormDrawer'
import { usePermission } from '../../shared/hooks/usePermission'
import { api } from '../../shared/api/apiClient'

interface PlatformLicensePlan {
  id: string
  name: string
  code: string
  monthlyPrice: number
  annualPrice: number
  maxUsers: number | null
  maxAssets: number | null
  maxWorkOrders: number | null
  modulesIncluded: string[]
  isActive: boolean
}

interface PlanFormState {
  id: string | null
  name: string
  code: string
  monthlyPrice: number
  annualPrice: number
  maxUsers: number | null
  maxAssets: number | null
  maxWorkOrders: number | null
  modulesIncluded: string
  isActive: boolean
}

const emptyForm: PlanFormState = { id: null, name: '', code: '', monthlyPrice: 0, annualPrice: 0, maxUsers: null, maxAssets: null, maxWorkOrders: null, modulesIncluded: '', isActive: true }

function PlanDrawer({ open, plan, onClose }: { open: boolean; plan: PlatformLicensePlan | null; onClose: () => void }) {
  const queryClient = useQueryClient()
  const [form, setForm] = useState<PlanFormState>(emptyForm)

  useEffect(() => {
    if (!open) return
    if (plan) {
      setForm({ id: plan.id, name: plan.name, code: plan.code, monthlyPrice: plan.monthlyPrice, annualPrice: plan.annualPrice, maxUsers: plan.maxUsers, maxAssets: plan.maxAssets, maxWorkOrders: plan.maxWorkOrders, modulesIncluded: plan.modulesIncluded.join(', '), isActive: plan.isActive })
    } else {
      setForm(emptyForm)
    }
  }, [open, plan])

  const save = useMutation({
    mutationFn: () => {
      const payload = {
        name: form.name,
        code: form.code || null,
        monthlyPrice: form.monthlyPrice,
        annualPrice: form.annualPrice,
        maxUsers: form.maxUsers,
        maxAssets: form.maxAssets,
        maxWorkOrders: form.maxWorkOrders,
        modulesIncluded: form.modulesIncluded.split(',').map((m) => m.trim()).filter(Boolean),
        isActive: form.isActive,
      }
      return form.id ? api.put<PlatformLicensePlan>(`/api/platform/licensing/plans/${form.id}`, payload) : api.post<PlatformLicensePlan>('/api/platform/licensing/plans', payload)
    },
    onSuccess: () => {
      toast.success(form.id ? 'Plan updated' : 'Plan created')
      void queryClient.invalidateQueries({ queryKey: ['platform', 'licensing', 'plans'] })
      onClose()
    },
  })

  return (
    <FormDrawer open={open} title={plan ? `Edit ${plan.name}` : 'New License Plan'} onClose={onClose}>
      <div className="space-y-4">
        <div>
          <label className="mb-1.5 block text-sm font-medium text-slate-700">Name</label>
          <input value={form.name} onChange={(event) => setForm((current) => ({ ...current, name: event.target.value }))} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]" />
        </div>
        <div>
          <label className="mb-1.5 block text-sm font-medium text-slate-700">Code</label>
          <input value={form.code} onChange={(event) => setForm((current) => ({ ...current, code: event.target.value }))} placeholder="Auto-generated from name if blank" className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]" />
        </div>
        <div className="grid grid-cols-2 gap-4">
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Monthly Price</label>
            <input type="number" value={form.monthlyPrice} onChange={(event) => setForm((current) => ({ ...current, monthlyPrice: Number(event.target.value) }))} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]" />
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Annual Price</label>
            <input type="number" value={form.annualPrice} onChange={(event) => setForm((current) => ({ ...current, annualPrice: Number(event.target.value) }))} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]" />
          </div>
        </div>
        <div className="grid grid-cols-3 gap-4">
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Max Users</label>
            <input type="number" value={form.maxUsers ?? ''} onChange={(event) => setForm((current) => ({ ...current, maxUsers: event.target.value ? Number(event.target.value) : null }))} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]" />
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Max Assets</label>
            <input type="number" value={form.maxAssets ?? ''} onChange={(event) => setForm((current) => ({ ...current, maxAssets: event.target.value ? Number(event.target.value) : null }))} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]" />
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Max WOs / mo</label>
            <input type="number" value={form.maxWorkOrders ?? ''} onChange={(event) => setForm((current) => ({ ...current, maxWorkOrders: event.target.value ? Number(event.target.value) : null }))} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]" />
          </div>
        </div>
        <div>
          <label className="mb-1.5 block text-sm font-medium text-slate-700">Modules Included (comma-separated)</label>
          <input value={form.modulesIncluded} onChange={(event) => setForm((current) => ({ ...current, modulesIncluded: event.target.value }))} placeholder="EmailIngestion, MonitoringIntegration, AdvancedReports, ClientPortal" className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]" />
        </div>
        <label className="flex items-center gap-2 text-sm text-slate-700">
          <input type="checkbox" checked={form.isActive} onChange={(event) => setForm((current) => ({ ...current, isActive: event.target.checked }))} className="h-4 w-4 rounded border-slate-300" />
          Active
        </label>
        <button type="button" disabled={!form.name.trim() || save.isPending} onClick={() => save.mutate()} className="inline-flex h-11 w-full items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white hover:bg-[var(--app-secondary)] disabled:opacity-60">
          {save.isPending ? 'Saving…' : plan ? 'Save Changes' : 'Create Plan'}
        </button>
      </div>
    </FormDrawer>
  )
}

export function PlansSettingsPage() {
  const canManage = usePermission('platform.settings.manage')
  const { data, isLoading, isError } = useQuery({
    queryKey: ['platform', 'licensing', 'plans'],
    queryFn: () => api.get<PlatformLicensePlan[]>('/api/platform/licensing/plans'),
    staleTime: 30_000,
  })
  const [drawerOpen, setDrawerOpen] = useState(false)
  const [editingPlan, setEditingPlan] = useState<PlatformLicensePlan | null>(null)

  const columns: DataTableColumn<PlatformLicensePlan>[] = [
    { key: 'name', header: 'Plan', cell: (row) => <span className="font-medium text-slate-900">{row.name}</span> },
    { key: 'code', header: 'Code', cell: (row) => <span className="font-mono text-xs">{row.code}</span> },
    { key: 'monthlyPrice', header: 'Monthly', cell: (row) => row.monthlyPrice.toLocaleString() },
    { key: 'annualPrice', header: 'Annual', cell: (row) => row.annualPrice.toLocaleString() },
    { key: 'maxUsers', header: 'Max Users', cell: (row) => row.maxUsers ?? 'Unlimited' },
    { key: 'modulesIncluded', header: 'Modules', cell: (row) => row.modulesIncluded.join(', ') || '—' },
    { key: 'isActive', header: 'Status', cell: (row) => <StatusBadge status={row.isActive ? 'Active' : 'Inactive'} /> },
    { key: 'actions', header: '', cell: (row) => (canManage ? <button type="button" onClick={() => { setEditingPlan(row); setDrawerOpen(true) }} className="rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-xs font-medium text-slate-700 hover:bg-slate-50">Edit</button> : null) },
  ]

  return (
    <div className="space-y-6">
      <PageHeader
        title="License Plans"
        subtitle="Pricing tiers and entitlement limits offered to tenant workspaces."
        primaryAction={canManage ? <button type="button" onClick={() => { setEditingPlan(null); setDrawerOpen(true) }} className="inline-flex h-11 items-center gap-2 rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white hover:bg-[var(--app-secondary)]"><Plus className="h-4 w-4" />New Plan</button> : undefined}
      />
      <DataTable
        columns={columns}
        data={data ?? []}
        loading={isLoading}
        emptyTitle={isError ? 'Plans unavailable' : 'No license plans configured'}
        emptyDescription={isError ? 'License plans could not be loaded.' : 'Create a plan to start onboarding tenants.'}
      />
      <PlanDrawer open={drawerOpen} plan={editingPlan} onClose={() => setDrawerOpen(false)} />
    </div>
  )
}
