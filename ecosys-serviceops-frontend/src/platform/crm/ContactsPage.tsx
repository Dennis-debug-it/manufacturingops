import { useState } from 'react'
import toast from 'react-hot-toast'
import { Plus } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { FormDrawer } from '../../shared/components/FormDrawer'
import { formatDate } from '../../shared/utils/dates'
import { useCreateCrmContact, useCrmContacts, type CrmContact } from './crmApi'
import { CrmSubNav } from './CrmSubNav'


export function ContactsPage() {
  const { data, isLoading, isError, refetch } = useCrmContacts()
  const createContact = useCreateCrmContact()
  const [showNewContact, setShowNewContact] = useState(false)
  const [fullName, setFullName] = useState('')
  const [companyName, setCompanyName] = useState('')
  const [email, setEmail] = useState('')
  const [phone, setPhone] = useState('')
  const [role, setRole] = useState('')

  const submit = async () => {
    await createContact.mutateAsync({ fullName, companyName, email, phone: phone || undefined, role: role || undefined })
    toast.success('Contact created')
    setShowNewContact(false)
    setFullName('')
    setCompanyName('')
    setEmail('')
    setPhone('')
    setRole('')
  }

  const columns: DataTableColumn<CrmContact>[] = [
    { key: 'fullName', header: 'Name', cell: (row) => <span className="font-medium text-slate-900">{row.fullName}</span>, sortValue: (row) => row.fullName },
    { key: 'companyName', header: 'Company', cell: (row) => row.companyName, sortValue: (row) => row.companyName },
    { key: 'email', header: 'Email', cell: (row) => row.email, sortValue: (row) => row.email },
    { key: 'phone', header: 'Phone', cell: (row) => row.phone ?? '—', sortValue: (row) => row.phone },
    { key: 'role', header: 'Role', cell: (row) => row.role ?? '—', sortValue: (row) => row.role },
    { key: 'createdAt', header: 'Created', cell: (row) => formatDate(row.createdAt), sortValue: (row) => row.createdAt },
  ]

  return (
    <div className="space-y-4">
      <PageHeader
        title="Contacts"
        subtitle="People associated with prospective and existing tenant accounts."
        primaryAction={
          <button type="button" onClick={() => setShowNewContact(true)} className="inline-flex h-11 items-center gap-2 rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)]">
            <Plus className="h-4 w-4" />New Contact
          </button>
        }
      />
      <CrmSubNav />
      <DataTable
        columns={columns}
        data={data ?? []}
        loading={isLoading}
        emptyTitle={isError ? 'Contacts unavailable' : 'No contacts yet'}
        emptyDescription={
          isError
            ? 'The contact list could not be loaded — this is not a confirmation that none exist.'
            : 'Add a contact to start tracking the people behind prospective and existing tenant accounts.'
        }
        emptyActionLabel={isError ? 'Retry' : undefined}
        onEmptyAction={isError ? () => void refetch() : undefined}
      />

      <FormDrawer open={showNewContact} title="New Contact" onClose={() => setShowNewContact(false)}>
        <div className="space-y-4">
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Full Name</label>
            <input value={fullName} onChange={(event) => setFullName(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]" />
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Company</label>
            <input value={companyName} onChange={(event) => setCompanyName(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]" />
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Email</label>
            <input type="email" value={email} onChange={(event) => setEmail(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]" />
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Phone (optional)</label>
            <input value={phone} onChange={(event) => setPhone(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]" />
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Role (optional)</label>
            <input value={role} onChange={(event) => setRole(event.target.value)} placeholder="e.g. Operations Manager" className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]" />
          </div>
          <button
            type="button"
            disabled={!fullName.trim() || !companyName.trim() || !email.trim() || createContact.isPending}
            onClick={() => void submit()}
            className="inline-flex h-11 w-full items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white hover:bg-[var(--app-secondary)] disabled:opacity-60"
          >
            {createContact.isPending ? 'Creating…' : 'Create Contact'}
          </button>
        </div>
      </FormDrawer>
    </div>
  )
}
