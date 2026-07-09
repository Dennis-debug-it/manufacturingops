import { useState } from 'react'
import toast from 'react-hot-toast'
import { Plus } from 'lucide-react'
import { DataTable, type DataTableColumn } from '../../../shared/components/DataTable'
import { StatusBadge } from '../../../shared/components/StatusBadge'
import { FormDrawer } from '../../../shared/components/FormDrawer'
import { usePermission } from '../../../shared/hooks/usePermission'
import { formatDateTime } from '../../../shared/utils/dates'
import {
  useClientPortalContacts,
  useCreateClientPortalContact,
  useResendClientPortalLink,
  useUpdateClientPortalContactStatus,
  type ClientPortalContact,
} from '../api'

function NewContactDrawer({ open, clientId, onClose }: { open: boolean; clientId: string; onClose: () => void }) {
  const createContact = useCreateClientPortalContact(clientId)
  const [fullName, setFullName] = useState('')
  const [email, setEmail] = useState('')
  const [phone, setPhone] = useState('')

  const submit = async () => {
    try {
      await createContact.mutateAsync({ fullName, email, phone: phone || undefined })
      toast.success(`Portal invite sent to ${email}`)
      onClose()
      setFullName('')
      setEmail('')
      setPhone('')
    } catch {
      // interceptor surfaces the error toast
    }
  }

  return (
    <FormDrawer open={open} title="Invite Portal Contact" onClose={onClose}>
      <div className="space-y-4">
        <div>
          <label className="mb-1.5 block text-sm font-medium text-slate-700">Full Name</label>
          <input value={fullName} onChange={(event) => setFullName(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900" />
        </div>
        <div>
          <label className="mb-1.5 block text-sm font-medium text-slate-700">Email</label>
          <input type="email" value={email} onChange={(event) => setEmail(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900" />
        </div>
        <div>
          <label className="mb-1.5 block text-sm font-medium text-slate-700">Phone (optional)</label>
          <input value={phone} onChange={(event) => setPhone(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900" />
        </div>
        <p className="text-xs text-slate-500">A one-time sign-in link will be emailed to this contact immediately.</p>
        <button
          type="button"
          disabled={!fullName.trim() || !email.trim() || createContact.isPending}
          onClick={() => void submit()}
          className="inline-flex h-11 w-full items-center justify-center rounded-lg bg-slate-900 px-4 text-sm font-medium text-white hover:bg-slate-800 disabled:opacity-60"
        >
          {createContact.isPending ? 'Sending Invite…' : 'Send Portal Invite'}
        </button>
      </div>
    </FormDrawer>
  )
}

function ContactRowActions({ clientId, contact }: { clientId: string; contact: ClientPortalContact }) {
  const canManage = usePermission('clients.edit')
  const updateStatus = useUpdateClientPortalContactStatus(clientId)
  const resendLink = useResendClientPortalLink(clientId)

  if (!canManage) return null

  return (
    <div className="flex justify-end gap-2">
      <button
        type="button"
        onClick={async () => {
          try {
            await resendLink.mutateAsync(contact.id)
            toast.success(`Sign-in link resent to ${contact.email}`)
          } catch {
            // interceptor surfaces the error toast
          }
        }}
        disabled={resendLink.isPending}
        className="rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-xs font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-60"
      >
        Resend Link
      </button>
      <button
        type="button"
        onClick={async () => {
          try {
            await updateStatus.mutateAsync({ id: contact.id, isActive: !contact.isActive })
            toast.success(contact.isActive ? 'Contact deactivated' : 'Contact activated')
          } catch {
            // interceptor surfaces the error toast
          }
        }}
        disabled={updateStatus.isPending}
        className="rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-xs font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-60"
      >
        {contact.isActive ? 'Deactivate' : 'Activate'}
      </button>
    </div>
  )
}

export function ClientPortalContactsTab({ clientId }: { clientId: string }) {
  const canManage = usePermission('clients.edit')
  const { data, isLoading, isError } = useClientPortalContacts(clientId)
  const [showInvite, setShowInvite] = useState(false)

  const columns: DataTableColumn<ClientPortalContact>[] = [
    { key: 'fullName', header: 'Name', cell: (row) => <span className="font-medium text-slate-900">{row.fullName}</span> },
    { key: 'email', header: 'Email', cell: (row) => row.email },
    { key: 'phone', header: 'Phone', cell: (row) => row.phone ?? '—' },
    { key: 'isActive', header: 'Status', cell: (row) => <StatusBadge status={row.isActive ? 'Active' : 'Inactive'} /> },
    { key: 'lastLoginAt', header: 'Last Sign-In', cell: (row) => (row.lastLoginAt ? formatDateTime(row.lastLoginAt) : 'Never') },
    { key: 'actions', header: '', cell: (row) => <ContactRowActions clientId={clientId} contact={row} /> },
  ]

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <p className="text-sm text-slate-600">Contacts invited here can sign in to the client portal to view their requests, invoices, and quotes.</p>
        {canManage ? (
          <button type="button" onClick={() => setShowInvite(true)} className="inline-flex h-11 shrink-0 items-center gap-2 rounded-lg bg-slate-900 px-4 text-sm font-medium text-white hover:bg-slate-800">
            <Plus className="h-4 w-4" />Invite Contact
          </button>
        ) : null}
      </div>
      <DataTable
        columns={columns}
        data={data ?? []}
        loading={isLoading}
        emptyTitle={isError ? 'Portal contacts unavailable' : 'No portal contacts yet'}
        emptyDescription={isError ? 'Portal contacts could not be loaded.' : 'Invite a contact to give this client access to the self-service portal.'}
      />
      <NewContactDrawer open={showInvite} clientId={clientId} onClose={() => setShowInvite(false)} />
    </div>
  )
}
