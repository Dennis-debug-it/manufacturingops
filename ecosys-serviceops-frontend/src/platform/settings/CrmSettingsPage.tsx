import { useState } from 'react'
import toast from 'react-hot-toast'
import { PageHeader } from '../../shared/components/PageHeader'
import { usePermission } from '../../shared/hooks/usePermission'
import { Field, SectionTitle } from '../common'

// TODO(backend): No tenant-lead pipeline/routing configuration endpoint exists yet — this form
// is fully built out but changes are not persisted until a backend Platform CRM settings endpoint exists.
export function CrmSettingsPage() {
  const canManage = usePermission('platform.settings.manage')
  const [autoAssignLeads, setAutoAssignLeads] = useState(true)
  const [defaultOwnerEmail, setDefaultOwnerEmail] = useState('sales@ecosys.io')
  const [followUpReminderDays, setFollowUpReminderDays] = useState(3)
  const [staleLeadDays, setStaleLeadDays] = useState(14)

  const save = () => toast.error('CRM settings cannot be saved yet — no backend Platform CRM settings endpoint exists.')

  return (
    <div className="space-y-6">
      <PageHeader
        title="CRM"
        subtitle="Pipeline and lead routing configuration."
        primaryAction={canManage ? <button type="button" onClick={save} className="inline-flex h-11 items-center justify-center rounded-lg bg-slate-900 px-4 text-sm font-medium text-white hover:bg-slate-800">Save Changes</button> : undefined}
      />
      <div className="rounded-2xl border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-900">
        No Platform CRM settings endpoint exists yet — this form is fully built out but changes are not persisted.
      </div>
      <div className="space-y-5 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
        <SectionTitle title="Lead Routing" description="Control how new inbound leads are assigned to sales owners." />
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <label className="flex items-center gap-2 text-sm text-slate-700 sm:col-span-2">
            <input type="checkbox" checked={autoAssignLeads} disabled={!canManage} onChange={(event) => setAutoAssignLeads(event.target.checked)} className="h-4 w-4 rounded border-slate-300" />
            Auto-assign new leads to a default owner
          </label>
          <Field label="Default Owner Email">
            <input value={defaultOwnerEmail} disabled={!canManage} onChange={(event) => setDefaultOwnerEmail(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900 disabled:bg-slate-50" />
          </Field>
          <Field label="Follow-up Reminder (days)">
            <input type="number" min={0} value={followUpReminderDays} disabled={!canManage} onChange={(event) => setFollowUpReminderDays(Number(event.target.value))} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900 disabled:bg-slate-50" />
          </Field>
          <Field label="Stale Lead Threshold (days)">
            <input type="number" min={0} value={staleLeadDays} disabled={!canManage} onChange={(event) => setStaleLeadDays(Number(event.target.value))} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900 disabled:bg-slate-50" />
          </Field>
        </div>
      </div>
    </div>
  )
}
