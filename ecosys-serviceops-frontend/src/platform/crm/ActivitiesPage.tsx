import { useState } from 'react'
import toast from 'react-hot-toast'
import { Plus } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { FormDrawer } from '../../shared/components/FormDrawer'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { formatDateTime } from '../../shared/utils/dates'
import { useCrmActivities, useLogCrmActivity, type CrmActivity, type CrmActivityType } from './crmStubApi'
import { CrmSubNav } from './CrmSubNav'

const backendGapNote = 'No Activities resource exists yet in the current backend. This is the full intended activity-log UI wired against typed stub data until a backend Activities endpoint is connected.'

export function ActivitiesPage() {
  const { data, isLoading } = useCrmActivities()
  const logActivity = useLogCrmActivity()
  const [showNewActivity, setShowNewActivity] = useState(false)
  const [contactId, setContactId] = useState('')
  const [type, setType] = useState<CrmActivityType>('Call')
  const [summary, setSummary] = useState('')

  const submit = async () => {
    try {
      await logActivity.mutateAsync({ contactId, type, summary })
      toast.success('Activity logged')
      setShowNewActivity(false)
    } catch {
      toast.error('CRM activities are not yet backed by a live API — this is a UI stub.')
    }
  }

  const columns: DataTableColumn<CrmActivity>[] = [
    { key: 'contactName', header: 'Contact', cell: (row) => <span className="font-medium text-slate-900">{row.contactName}</span> },
    { key: 'type', header: 'Type', cell: (row) => <StatusBadge status={row.type} /> },
    { key: 'summary', header: 'Summary', cell: (row) => row.summary },
    { key: 'occurredAt', header: 'When', cell: (row) => formatDateTime(row.occurredAt) },
    { key: 'loggedByName', header: 'Logged By', cell: (row) => row.loggedByName },
  ]

  return (
    <div className="space-y-6">
      <PageHeader
        title="Activities"
        subtitle="Calls, emails, meetings, and notes logged against CRM contacts."
        primaryAction={
          <button type="button" onClick={() => setShowNewActivity(true)} className="inline-flex h-11 items-center gap-2 rounded-lg bg-slate-900 px-4 text-sm font-medium text-white transition hover:bg-slate-800">
            <Plus className="h-4 w-4" />Log Activity
          </button>
        }
      />
      <CrmSubNav />
      <div className="rounded-2xl border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-900">{backendGapNote}</div>
      <DataTable
        columns={columns}
        data={data ?? []}
        loading={isLoading}
        emptyTitle="No activities logged yet"
        emptyDescription="Activity history for contacts will appear here once a backend Activities endpoint is connected."
      />

      <FormDrawer open={showNewActivity} title="Log Activity" onClose={() => setShowNewActivity(false)}>
        <div className="space-y-4">
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Contact ID</label>
            <input value={contactId} onChange={(event) => setContactId(event.target.value)} placeholder="Contact GUID" className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900" />
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Type</label>
            <select value={type} onChange={(event) => setType(event.target.value as CrmActivityType)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900">
              <option>Call</option>
              <option>Email</option>
              <option>Meeting</option>
              <option>Note</option>
            </select>
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Summary</label>
            <textarea value={summary} onChange={(event) => setSummary(event.target.value)} rows={4} className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-slate-900" />
          </div>
          <button
            type="button"
            disabled={!contactId.trim() || !summary.trim() || logActivity.isPending}
            onClick={() => void submit()}
            className="inline-flex h-11 w-full items-center justify-center rounded-lg bg-slate-900 px-4 text-sm font-medium text-white hover:bg-slate-800 disabled:opacity-60"
          >
            {logActivity.isPending ? 'Logging…' : 'Log Activity'}
          </button>
        </div>
      </FormDrawer>
    </div>
  )
}
