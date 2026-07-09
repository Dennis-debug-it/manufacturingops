import { useState } from 'react'
import toast from 'react-hot-toast'
import { useNavigate, useParams } from 'react-router-dom'
import { PageHeader } from '../../shared/components/PageHeader'
import { EmptyState } from '../../shared/components/EmptyState'
import { Spinner } from '../../shared/components/Spinner'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { ConfirmDialog } from '../../shared/components/ConfirmDialog'
import { formatDateTime } from '../../shared/utils/dates'
import { api } from '../../shared/api/apiClient'
import { endpoints } from '../../shared/api/endpoints'
import { useLead, useUpdateLeadNotes, useUpdateLeadStatus } from './api'
import type { LeadStatus } from './types'

export function LeadDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const { data: lead, isLoading, isError } = useLead(id)
  const updateStatus = useUpdateLeadStatus(id ?? '')
  const updateNotes = useUpdateLeadNotes(id ?? '')

  const [notes, setNotes] = useState('')
  const [pendingStatus, setPendingStatus] = useState<LeadStatus | null>(null)
  const [convertStep, setConvertStep] = useState<'idle' | 'confirm' | 'creating-tenant' | 'failed'>('idle')

  const runStatusChange = async () => {
    if (!pendingStatus) return
    try {
      await updateStatus.mutateAsync(pendingStatus)
      toast.success(`Marked ${pendingStatus}`)
    } catch {
      // interceptor surfaces the error toast
    } finally {
      setPendingStatus(null)
    }
  }

  const saveNotes = async () => {
    try {
      await updateNotes.mutateAsync(notes)
      toast.success('Notes saved')
    } catch {
      // interceptor surfaces the error toast
    }
  }

  const convertToTenant = async () => {
    if (!lead || !id) return
    setConvertStep('creating-tenant')
    try {
      await updateStatus.mutateAsync('Converted to Workspace')
    } catch {
      setConvertStep('failed')
      toast.error('Step 1 failed: could not mark the lead as converted.')
      return
    }
    try {
      const tenant = await api.post<{ tenantId: string }>(endpoints.platform.tenants, {
        companyName: lead.companyName,
        contactName: lead.contactPersonName,
        contactEmail: lead.email,
        contactPhone: lead.phone,
        country: lead.country ?? undefined,
      })
      toast.success('Tenant workspace created')
      navigate(`/platform/tenants/${tenant.tenantId}`)
    } catch {
      setConvertStep('failed')
      toast.error('Step 1 succeeded (lead marked Converted) but Step 2 failed: tenant workspace was not created. Retry from Tenant Management.')
    }
  }

  if (isLoading) {
    return <Spinner label="Loading lead" />
  }

  if (isError || !lead || !id) {
    return <EmptyState title="Lead unavailable" description="This lead could not be loaded." />
  }

  return (
    <div className="space-y-6 text-white">
      <PageHeader
        title={lead.companyName}
        subtitle={lead.contactPersonName}
        breadcrumbs={[{ label: 'Leads', to: '/platform/crm/leads' }, { label: lead.companyName }]}
        secondaryActions={<StatusBadge status={lead.status} />}
        primaryAction={
          <div className="flex flex-wrap gap-2">
            {lead.status !== 'Qualified' ? <button type="button" onClick={() => setPendingStatus('Qualified')} className="inline-flex h-11 items-center rounded-lg border border-white/20 bg-white/5 px-4 text-sm font-medium text-white hover:bg-white/10">Mark Qualified</button> : null}
            {lead.status !== 'Not a Fit' ? <button type="button" onClick={() => setPendingStatus('Not a Fit')} className="inline-flex h-11 items-center rounded-lg border border-rose-400/40 bg-rose-500/10 px-4 text-sm font-medium text-rose-200 hover:bg-rose-500/20">Mark Not a Fit</button> : null}
            {lead.status !== 'Converted to Workspace' && !lead.convertedTenantId ? (
              <button type="button" onClick={() => setConvertStep('confirm')} className="inline-flex h-11 items-center rounded-lg bg-white px-4 text-sm font-medium text-slate-900 hover:bg-slate-100">Convert to Tenant</button>
            ) : null}
          </div>
        }
      />

      <div className="grid grid-cols-1 gap-6 xl:grid-cols-2">
        <section className="space-y-3 rounded-2xl border border-white/10 bg-white/5 p-5">
          <h3 className="text-sm font-semibold text-white">Contact Details</h3>
          <dl className="space-y-2 text-sm">
            {[
              ['Email', lead.email],
              ['Phone', lead.phone],
              ['Country', lead.country ?? '—'],
              ['Industry', lead.industry ?? '—'],
              ['Company Size', lead.companySize ?? '—'],
              ['Preferred Contact', lead.preferredContactMethod ?? '—'],
              ['Created', formatDateTime(lead.createdAt)],
            ].map(([label, value]) => (
              <div key={label} className="flex justify-between border-b border-white/5 pb-2">
                <dt className="text-slate-400">{label}</dt>
                <dd className="text-white">{value}</dd>
              </div>
            ))}
          </dl>
          {lead.message ? (
            <div className="mt-3 rounded-lg bg-white/5 p-3 text-sm text-slate-200">{lead.message}</div>
          ) : null}
        </section>

        <section className="space-y-3 rounded-2xl border border-white/10 bg-white/5 p-5">
          <h3 className="text-sm font-semibold text-white">Notes</h3>
          <textarea
            defaultValue={lead.notes ?? ''}
            onChange={(event) => setNotes(event.target.value)}
            rows={8}
            className="w-full rounded-lg border border-white/10 bg-white/5 px-3 py-2 text-sm text-white outline-none focus:border-white/30"
          />
          <button type="button" onClick={saveNotes} disabled={updateNotes.isPending} className="inline-flex h-10 items-center rounded-lg bg-white px-4 text-sm font-medium text-slate-900 hover:bg-slate-100 disabled:opacity-60">
            {updateNotes.isPending ? 'Saving…' : 'Save Notes'}
          </button>
        </section>
      </div>

      <ConfirmDialog
        open={pendingStatus !== null}
        title={`Mark ${pendingStatus}`}
        message={`This will move ${lead.companyName} to "${pendingStatus}".`}
        onCancel={() => setPendingStatus(null)}
        onConfirm={runStatusChange}
      />

      <ConfirmDialog
        open={convertStep === 'confirm'}
        title="Convert to Tenant"
        message="Step 1: mark this lead as Converted to Workspace. Step 2: create the tenant workspace. If step 2 fails, you'll see exactly which step failed and can retry."
        confirmLabel="Convert"
        onCancel={() => setConvertStep('idle')}
        onConfirm={() => {
          setConvertStep('idle')
          void convertToTenant()
        }}
      />
    </div>
  )
}
