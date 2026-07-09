import { useState } from 'react'
import toast from 'react-hot-toast'
import { PageHeader } from '../../shared/components/PageHeader'
import { usePermission } from '../../shared/hooks/usePermission'
import { Field, SectionTitle } from '../common'

// TODO(backend): No Platform Support settings endpoint exists yet — this form is fully
// built out but changes are not persisted until a backend endpoint is available.
export function SupportSettingsPage() {
  const canManage = usePermission('platform.settings.manage')
  const [supportEmail, setSupportEmail] = useState('support@ecosys.io')
  const [supportPhone, setSupportPhone] = useState('+254700000000')
  const [escalationEmail, setEscalationEmail] = useState('escalations@ecosys.io')
  const [slaResponseHours, setSlaResponseHours] = useState(4)
  const [businessHours, setBusinessHours] = useState('Mon–Fri, 08:00–18:00 EAT')

  const save = () => toast.error('Support settings cannot be saved yet — no backend endpoint exists.')

  return (
    <div className="space-y-6">
      <PageHeader
        title="Support"
        subtitle="Support contact details and escalation routing shown to tenants."
        primaryAction={canManage ? <button type="button" onClick={save} className="inline-flex h-11 items-center justify-center rounded-lg bg-slate-900 px-4 text-sm font-medium text-white hover:bg-slate-800">Save Changes</button> : undefined}
      />
      <div className="rounded-2xl border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-900">
        No Platform Support settings endpoint exists yet — this form is fully built out but changes are not persisted.
      </div>
      <div className="space-y-5 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
        <SectionTitle title="Contact & Escalation" description="Where tenant support requests and escalations are routed." />
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <Field label="Support Email">
            <input value={supportEmail} disabled={!canManage} onChange={(event) => setSupportEmail(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900 disabled:bg-slate-50" />
          </Field>
          <Field label="Support Phone">
            <input value={supportPhone} disabled={!canManage} onChange={(event) => setSupportPhone(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900 disabled:bg-slate-50" />
          </Field>
          <Field label="Escalation Email">
            <input value={escalationEmail} disabled={!canManage} onChange={(event) => setEscalationEmail(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900 disabled:bg-slate-50" />
          </Field>
          <Field label="SLA First Response (hours)">
            <input type="number" min={0} value={slaResponseHours} disabled={!canManage} onChange={(event) => setSlaResponseHours(Number(event.target.value))} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900 disabled:bg-slate-50" />
          </Field>
          <Field label="Business Hours">
            <input value={businessHours} disabled={!canManage} onChange={(event) => setBusinessHours(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900 disabled:bg-slate-50" />
          </Field>
        </div>
      </div>
    </div>
  )
}
