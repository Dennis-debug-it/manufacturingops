import { useState } from 'react'
import toast from 'react-hot-toast'
import { Plus } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { FormDrawer } from '../../shared/components/FormDrawer'
import { formatDate } from '../../shared/utils/dates'
import { useCreateCrmContact, useCrmContacts, type CrmContact } from './crmStubApi'
import { CrmSubNav } from './CrmSubNav'

const backendGapNote = 'Only PlatformLeadsController exists for CRM today (leads/pipeline). No Contacts resource exists yet. This is the full intended UI wired against typed stub data until a backend Contacts endpoint is connected.'

export function ContactsPage() {
  const { data, isLoading } = useCrmContacts()
  const createContact = useCreateCrmContact()
  const [showNewContact, setShowNewContact] = useState(false)
  const [fullName, setFullName] = useState('')
  const [companyName, setCompanyName] = useState('')
  const [email, setEmail] = useState('')
  const [phone, setPhone] = useState('')
  const [role, setRole] = useState('')

  const submit = async () => {
    try {
      await createContact.mutateAsync({ fullName, companyName, email, phone: phone || undefined, role: role || undefined })
      toast.success('Contact created')
      setShowNewContact(false)
    } catch {
      toast.error('CRM contacts are not yet backed by a live API — this is a UI stub.')
    }
  }

  const columns: DataTableColumn<CrmContact>[] = [
    { key: 'fullName', header: 'Name', cell: (row) => <span className="font-medium text-slate-900">{row.fullName}</span> },
    { key: 'companyName', header: 'Company', cell: (row) => row.companyName },
    { key: 'email', header: 'Email', cell: (row) => row.email },
    { key: 'phone', header: 'Phone', cell: (row) => row.phone ?? '—' },
    { key: 'role', header: 'Role', cell: (row) => row.role ?? '—' },
    { key: 'createdAt', header: 'Created', cell: (row) => formatDate(row.createdAt) },
  ]

  return (
    <div className="space-y-6">
      <PageHeader
        title="Contacts"
        subtitle="People associated with prospective and existing tenant accounts."
        primaryAction={
          <button type="button" onClick={() => setShowNewContact(true)} className="inline-flex h-11 items-center gap-2 rounded-lg bg-slate-900 px-4 text-sm font-medium text-white transition hover:bg-slate-800">
            <Plus className="h-4 w-4" />New Contact
          </button>
        }
      />
      <CrmSubNav />
      <div className="rounded-2xl border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-900">{backendGapNote}</div>
      <DataTable
        columns={columns}
        data={data ?? []}
        loading={isLoading}
        emptyTitle="No contacts yet"
        emptyDescription="Contacts tied to leads and tenants will appear here once a backend Contacts endpoint is connected."
      />

      <FormDrawer open={showNewContact} title="New Contact" onClose={() => setShowNewContact(false)}>
        <div className="space-y-4">
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Full Name</label>
            <input value={fullName} onChange={(event) => setFullName(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900" />
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Company</label>
            <input value={companyName} onChange={(event) => setCompanyName(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900" />
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Email</label>
            <input type="email" value={email} onChange={(event) => setEmail(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900" />
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Phone (optional)</label>
            <input value={phone} onChange={(event) => setPhone(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900" />
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Role (optional)</label>
            <input value={role} onChange={(event) => setRole(event.target.value)} placeholder="e.g. Operations Manager" className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900" />
          </div>
          <button
            type="button"
            disabled={!fullName.trim() || !companyName.trim() || !email.trim() || createContact.isPending}
            onClick={() => void submit()}
            className="inline-flex h-11 w-full items-center justify-center rounded-lg bg-slate-900 px-4 text-sm font-medium text-white hover:bg-slate-800 disabled:opacity-60"
          >
            {createContact.isPending ? 'Creating…' : 'Create Contact'}
          </button>
        </div>
      </FormDrawer>
    </div>
  )
}
