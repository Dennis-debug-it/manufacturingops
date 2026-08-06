import { useEffect, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import toast from 'react-hot-toast'
import { PageHeader } from '../../shared/components/PageHeader'
import { Spinner } from '../../shared/components/Spinner'
import { EmptyState } from '../../shared/components/EmptyState'
import { usePermission } from '../../shared/hooks/usePermission'
import { api, apiClient } from '../../shared/api/apiClient'
import { useTenants } from '../api'
import { Field, SectionTitle } from '../common'

interface SupportSettings {
  supportEmail: string
  supportPhone: string
  escalationEmail: string
  slaResponseHours: number
  businessHours: string
}

export function SupportSettingsPage() {
  const canManage = usePermission('platform.settings.manage')
  const queryClient = useQueryClient()
  const { data, isLoading, isError, refetch } = useQuery({
    queryKey: ['platform', 'settings', 'support'],
    queryFn: () => api.get<SupportSettings>('/api/platform/settings/support'),
    staleTime: 30_000,
  })
  const [values, setValues] = useState<SupportSettings | null>(null)
  useEffect(() => { if (data) setValues(data) }, [data])

  const save = useMutation({
    mutationFn: (payload: SupportSettings) => api.put<SupportSettings>('/api/platform/settings/support', payload),
    onSuccess: () => {
      toast.success('Support settings saved')
      void queryClient.invalidateQueries({ queryKey: ['platform', 'settings', 'support'] })
    },
  })

  if (isError) return <EmptyState title="Couldn’t load support settings" description="Something went wrong fetching these settings." actionLabel="Retry" onAction={() => { void refetch() }} />
  if (isLoading || !values) return <Spinner label="Loading support settings" />

  const set = <K extends keyof SupportSettings>(key: K, value: SupportSettings[K]) => setValues((current) => (current ? { ...current, [key]: value } : current))

  return (
    <div className="space-y-6">
      <PageHeader
        title="Support"
        subtitle="Support contact details, escalation routing, and admin tools."
        primaryAction={canManage ? <button type="button" onClick={() => save.mutate(values)} disabled={save.isPending} className="inline-flex h-11 items-center justify-center rounded-lg bg-slate-900 px-4 text-sm font-medium text-white hover:bg-slate-800 disabled:opacity-60">{save.isPending ? 'Saving…' : 'Save Changes'}</button> : undefined}
      />
      <div className="space-y-5 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
        <SectionTitle title="Contact & Escalation" description="Where tenant support requests and escalations are routed." />
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <Field label="Support Email">
            <input value={values.supportEmail} disabled={!canManage} onChange={(event) => set('supportEmail', event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900 disabled:bg-slate-50" />
          </Field>
          <Field label="Support Phone">
            <input value={values.supportPhone} disabled={!canManage} onChange={(event) => set('supportPhone', event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900 disabled:bg-slate-50" />
          </Field>
          <Field label="Escalation Email">
            <input value={values.escalationEmail} disabled={!canManage} onChange={(event) => set('escalationEmail', event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900 disabled:bg-slate-50" />
          </Field>
          <Field label="SLA First Response (hours)">
            <input type="number" min={0} value={values.slaResponseHours} disabled={!canManage} onChange={(event) => set('slaResponseHours', Number(event.target.value))} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900 disabled:bg-slate-50" />
          </Field>
          <Field label="Business Hours">
            <input value={values.businessHours} disabled={!canManage} onChange={(event) => set('businessHours', event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900 disabled:bg-slate-50" />
          </Field>
        </div>
      </div>

      {canManage ? <SupportTools /> : null}
    </div>
  )
}

function SupportTools() {
  const { data: tenants } = useTenants()
  const [tenantId, setTenantId] = useState('')
  const [documentType, setDocumentType] = useState('WorkOrder')
  const [busy, setBusy] = useState<string | null>(null)

  const requireTenant = () => {
    if (!tenantId) { toast.error('Select a tenant first'); return false }
    return true
  }

  const run = async (label: string, fn: () => Promise<string>) => {
    if (!requireTenant()) return
    setBusy(label)
    try {
      toast.success(await fn())
    } catch {
      // interceptor surfaces the error toast
    } finally {
      setBusy(null)
    }
  }

  const resendWelcome = () => run('welcome', async () => {
    const res = await api.post<{ message: string }>(`/api/platform/support/tenants/${tenantId}/resend-welcome`, {})
    return res.message
  })

  const resetNumbering = () => run('numbering', async () => {
    const res = await api.post<{ message: string }>(`/api/platform/support/tenants/${tenantId}/reset-numbering`, { documentType })
    return res.message
  })

  const reindex = () => run('reindex', async () => {
    const res = await api.post<{ counts: { workOrders: number; clients: number; invoices: number } }>(`/api/platform/support/tenants/${tenantId}/reindex`, {})
    return `Reindexed — ${res.counts.workOrders} work orders, ${res.counts.clients} clients, ${res.counts.invoices} invoices.`
  })

  const exportData = async () => {
    if (!requireTenant()) return
    setBusy('export')
    try {
      const response = await apiClient.get(`/api/platform/support/tenants/${tenantId}/export`, { responseType: 'blob' })
      const url = URL.createObjectURL(response.data as Blob)
      const link = document.createElement('a')
      link.href = url
      link.download = `tenant-export-${tenantId}.json`
      link.click()
      URL.revokeObjectURL(url)
    } catch {
      toast.error('Could not export tenant data')
    } finally {
      setBusy(null)
    }
  }

  const buttonClass = 'inline-flex h-10 items-center justify-center rounded-lg border border-slate-200 bg-white px-3 text-sm font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-60'

  return (
    <div className="space-y-5 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
      <SectionTitle title="Support Tools" description="Run maintenance actions against a specific tenant workspace." />
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
        <Field label="Tenant">
          <select value={tenantId} onChange={(event) => setTenantId(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900">
            <option value="">Select a tenant…</option>
            {(tenants ?? []).map((t) => <option key={t.tenantId} value={t.tenantId}>{t.name}</option>)}
          </select>
        </Field>
        <Field label="Numbering Document Type">
          <select value={documentType} onChange={(event) => setDocumentType(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900">
            {['WorkOrder', 'Invoice', 'Quotation', 'Receipt', 'PurchaseOrder', 'Expense'].map((d) => <option key={d} value={d}>{d}</option>)}
          </select>
        </Field>
      </div>
      <div className="flex flex-wrap gap-2">
        <button type="button" disabled={busy !== null} onClick={() => void resendWelcome()} className={buttonClass}>{busy === 'welcome' ? 'Sending…' : 'Resend Welcome Email'}</button>
        <button type="button" disabled={busy !== null} onClick={() => void resetNumbering()} className={buttonClass}>{busy === 'numbering' ? 'Resetting…' : 'Reset Numbering'}</button>
        <button type="button" disabled={busy !== null} onClick={() => void reindex()} className={buttonClass}>{busy === 'reindex' ? 'Reindexing…' : 'Re-index Counts'}</button>
        <button type="button" disabled={busy !== null} onClick={() => void exportData()} className={buttonClass}>{busy === 'export' ? 'Exporting…' : 'Export Data (JSON)'}</button>
      </div>
    </div>
  )
}
