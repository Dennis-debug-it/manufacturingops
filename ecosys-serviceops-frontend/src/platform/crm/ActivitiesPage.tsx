import { useState } from 'react'
import toast from 'react-hot-toast'
import { Plus } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { FormDrawer } from '../../shared/components/FormDrawer'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { formatDateTime } from '../../shared/utils/dates'
import { useCrmActivities, useCrmContacts, useLogCrmActivity, type CrmActivity, type CrmActivityType } from './crmApi'
import { CrmSubNav } from './CrmSubNav'

export function ActivitiesPage() {
  const { data, isLoading, isError, refetch } = useCrmActivities()
  const { data: contacts } = useCrmContacts()
  const logActivity = useLogCrmActivity()
  const [showNewActivity, setShowNewActivity] = useState(false)
  const [contactId, setContactId] = useState('')
  const [type, setType] = useState<CrmActivityType>('Call')
  const [summary, setSummary] = useState('')

  const submit = async () => {
    if (!contactId) return toast.error('Select a contact')
    await logActivity.mutateAsync({ contactId, type, summary })
    toast.success('Activity logged')
    setShowNewActivity(false)
    setSummary('')
  }

  const columns: DataTableColumn<CrmActivity>[] = [
    { key: 'contactName', header: 'Contact', cell: (row) => <span className="font-medium text-slate-900">{row.contactName}</span>, sortValue: (row) => row.contactName },
    { key: 'type', header: 'Type', cell: (row) => <StatusBadge status={row.type} />, sortValue: (row) => row.type },
    { key: 'summary', header: 'Summary', cell: (row) => row.summary },
    { key: 'occurredAt', header: 'When', cell: (row) => formatDateTime(row.occurredAt), sortValue: (row) => row.occurredAt },
    { key: 'loggedByName', header: 'Logged By', cell: (row) => row.loggedByName, sortValue: (row) => row.loggedByName },
  ]

  return (
    <div className="space-y-4">
      <PageHeader
        title="Activities"
        subtitle="Calls, emails, meetings, and notes logged against CRM contacts."
        primaryAction={
          <button type="button" onClick={() => setShowNewActivity(true)} className="inline-flex h-11 items-center gap-2 rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)]">
            <Plus className="h-4 w-4" />Log Activity
          </button>
        }
      />
      <CrmSubNav />
      <DataTable
        columns={columns}
        data={data ?? []}
        loading={isLoading}
        emptyTitle={isError ? 'Activities unavailable' : 'No activities logged yet'}
        emptyDescription={
          isError
            ? 'The activity history could not be loaded — this is not a confirmation that none exist.'
            : 'Log a call, email, meeting, or note against a contact and it will appear here.'
        }
        emptyActionLabel={isError ? 'Retry' : undefined}
        onEmptyAction={isError ? () => void refetch() : undefined}
      />

      <FormDrawer open={showNewActivity} title="Log Activity" onClose={() => setShowNewActivity(false)}>
        <div className="space-y-4">
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Contact</label>
            <select value={contactId} onChange={(event) => setContactId(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]">
              <option value="">Select a contact…</option>
              {(contacts ?? []).map((c) => <option key={c.id} value={c.id}>{c.fullName} — {c.companyName}</option>)}
            </select>
            {contacts && contacts.length === 0 ? <p className="mt-1 text-xs text-amber-600">No contacts yet — add one on the Contacts page first.</p> : null}
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Type</label>
            <select value={type} onChange={(event) => setType(event.target.value as CrmActivityType)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]">
              <option>Call</option>
              <option>Email</option>
              <option>Meeting</option>
              <option>Note</option>
            </select>
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Summary</label>
            <textarea value={summary} onChange={(event) => setSummary(event.target.value)} rows={4} className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[var(--app-primary)]" />
          </div>
          <button
            type="button"
            disabled={!contactId.trim() || !summary.trim() || logActivity.isPending}
            onClick={() => void submit()}
            className="inline-flex h-11 w-full items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white hover:bg-[var(--app-secondary)] disabled:opacity-60"
          >
            {logActivity.isPending ? 'Logging…' : 'Log Activity'}
          </button>
        </div>
      </FormDrawer>
    </div>
  )
}
