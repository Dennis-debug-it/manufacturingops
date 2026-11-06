import { useState } from 'react'
import toast from 'react-hot-toast'
import { MessageCircle, Plus } from 'lucide-react'
import { DataTable, type DataTableColumn } from '../../../shared/components/DataTable'
import { StatusBadge } from '../../../shared/components/StatusBadge'
import { FormDrawer } from '../../../shared/components/FormDrawer'
import { usePermission } from '../../../shared/hooks/usePermission'
import { ContactWhatsAppDrawer } from '../../whatsapp/ContactWhatsAppDrawer'
import { formatWhatsAppNumber } from '../../whatsapp/reminderLogic'
import { staffWhatsAppApi, useWhatsAppMyStatus } from '../../whatsapp/remindersApi'
import { WHATSAPP_ENABLED } from '../../../shared/config/features'
import {
  useClientPortalContacts,
  useCreateClientPortalContact,
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
      const created = await createContact.mutateAsync({ fullName, email, phone: phone || undefined })
      toast.success(`${created.fullName} added`)
      onClose()
      setFullName('')
      setEmail('')
      setPhone('')
    } catch {
      // interceptor surfaces the error toast
    }
  }

  return (
    <FormDrawer open={open} title="Add Contact" onClose={onClose}>
      <div className="space-y-4">
        <div>
          <label className="mb-1.5 block text-sm font-medium text-slate-700">Full Name</label>
          <input value={fullName} onChange={(event) => setFullName(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]" />
        </div>
        <div>
          <label className="mb-1.5 block text-sm font-medium text-slate-700">Email</label>
          <input type="email" value={email} onChange={(event) => setEmail(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]" />
        </div>
        <div>
          <label className="mb-1.5 block text-sm font-medium text-slate-700">Phone (optional)</label>
          <input value={phone} onChange={(event) => setPhone(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]" />
        </div>
        <button
          type="button"
          disabled={!fullName.trim() || !email.trim() || createContact.isPending}
          onClick={() => void submit()}
          className="inline-flex h-11 w-full items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white hover:bg-[var(--app-secondary)] disabled:opacity-60"
        >
          {createContact.isPending ? 'Saving…' : 'Add Contact'}
        </button>
      </div>
    </FormDrawer>
  )
}

function ContactRowActions({ clientId, contact, onWhatsApp }: { clientId: string; contact: ClientPortalContact; onWhatsApp: (() => void) | null }) {
  const canManage = usePermission('clients.edit')
  const updateStatus = useUpdateClientPortalContactStatus(clientId)
  if (!canManage) return null

  return (
    <div className="flex justify-end gap-2">
      {onWhatsApp ? (
        <button
          type="button"
          onClick={onWhatsApp}
          title="Record that this contact agreed to WhatsApp updates"
          className="inline-flex items-center gap-1 rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-xs font-medium text-slate-700 hover:bg-slate-50"
        >
          <MessageCircle className="h-3.5 w-3.5" />WhatsApp
        </button>
      ) : null}
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
  // WhatsApp updates only when the workspace has the feature (Release 3 design 4.2).
  const whatsApp = (useWhatsAppMyStatus(staffWhatsAppApi).data?.featureEnabled ?? false) && WHATSAPP_ENABLED
  // Rendered outside the table: inside a row the drawer was clipped by the table's scroll container.
  const [whatsAppContact, setWhatsAppContact] = useState<ClientPortalContact | null>(null)

  const columns: DataTableColumn<ClientPortalContact>[] = [
    { key: 'fullName', header: 'Name', cell: (row) => <span className="font-medium text-slate-900">{row.fullName}</span> },
    { key: 'email', header: 'Email', cell: (row) => row.email },
    { key: 'phone', header: 'Phone', cell: (row) => row.phone ?? '—' },
    { key: 'isActive', header: 'Status', cell: (row) => <StatusBadge status={row.isActive ? 'Active' : 'Inactive'} /> },
    ...(whatsApp ? [{ key: 'whatsApp', header: 'WhatsApp', cell: (row: ClientPortalContact) => (row.receivesWhatsApp ? formatWhatsAppNumber(row.whatsAppNumber) : 'Off') }] : []),
    { key: 'actions', header: '', cell: (row) => <ContactRowActions clientId={clientId} contact={row} onWhatsApp={whatsApp ? () => setWhatsAppContact(row) : null} /> },
  ]

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <p className="text-sm text-slate-600">People at this client who get job updates by email or WhatsApp and can be invited to meetings.</p>
        {canManage ? (
          <button type="button" onClick={() => setShowInvite(true)} className="inline-flex h-11 shrink-0 items-center gap-2 rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white hover:bg-[var(--app-secondary)]">
            <Plus className="h-4 w-4" />Add Contact
          </button>
        ) : null}
      </div>
      <DataTable
        columns={columns}
        data={data ?? []}
        loading={isLoading}
        emptyTitle={isError ? 'Contacts unavailable' : 'No contacts yet'}
        emptyDescription={isError ? 'Contacts could not be loaded.' : 'Add the people at this client who should hear about their jobs.'}
      />
      <NewContactDrawer open={showInvite} clientId={clientId} onClose={() => setShowInvite(false)} />
      {whatsAppContact ? <ContactWhatsAppDrawer open clientId={clientId} contact={whatsAppContact} onClose={() => setWhatsAppContact(null)} /> : null}
    </div>
  )
}
