import { useState } from 'react'
import toast from 'react-hot-toast'
import { Copy, Link2, MessageCircle, Plus, RefreshCw } from 'lucide-react'
import { DataTable, type DataTableColumn } from '../../../shared/components/DataTable'
import { StatusBadge } from '../../../shared/components/StatusBadge'
import { ConfirmDialog } from '../../../shared/components/ConfirmDialog'
import { FormDrawer } from '../../../shared/components/FormDrawer'
import { usePermission } from '../../../shared/hooks/usePermission'
import { useAuth } from '../../../shared/hooks/useAuth'
import { formatDateTime } from '../../../shared/utils/dates'
import { ContactWhatsAppDrawer } from '../../whatsapp/ContactWhatsAppDrawer'
import { formatWhatsAppNumber } from '../../whatsapp/reminderLogic'
import { staffWhatsAppApi, useWhatsAppMyStatus } from '../../whatsapp/remindersApi'
import { WHATSAPP_ENABLED } from '../../../shared/config/features'
import {
  portalLoginUrl,
  useClientPortalContacts,
  usePortalAccessLink,
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
      const created = await createContact.mutateAsync({ fullName, email, phone: phone || undefined })
      toast.success(created.isPendingApproval ? `${created.email} added — awaiting admin approval before an invite is sent` : `Portal invitation emailed to ${created.email}`)
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
        <p className="text-xs text-slate-500">An invitation with their personal portal link (it doesn't expire) is emailed to this contact — or, if your portal requires admin approval for new contacts, once an admin approves them. No password is needed: clients sign in with their email.</p>
        <button
          type="button"
          disabled={!fullName.trim() || !email.trim() || createContact.isPending}
          onClick={() => void submit()}
          className="inline-flex h-11 w-full items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white hover:bg-[var(--app-secondary)] disabled:opacity-60"
        >
          {createContact.isPending ? 'Sending Invitation…' : 'Send Portal Invitation'}
        </button>
      </div>
    </FormDrawer>
  )
}

function ContactRowActions({ clientId, contact, onWhatsApp }: { clientId: string; contact: ClientPortalContact; onWhatsApp: (() => void) | null }) {
  const canManage = usePermission('clients.edit')
  const { user } = useAuth()
  const isAdmin = user?.role === 'admin' || user?.role === 'super_admin'
  const updateStatus = useUpdateClientPortalContactStatus(clientId)
  const resendLink = useResendClientPortalLink(clientId)
  const accessLink = usePortalAccessLink(clientId)
  const [sharedLink, setSharedLink] = useState<string | null>(null)
  const [confirmReset, setConfirmReset] = useState(false)

  const copyLink = async (reset: boolean) => {
    try {
      const { link } = await accessLink.mutateAsync({ id: contact.id, reset })
      try {
        await navigator.clipboard.writeText(link)
        toast.success(reset ? 'New portal link copied — the old link no longer works' : 'Portal link copied — it does not expire')
      } catch {
        setSharedLink(link)
      }
    } catch {
      // interceptor surfaces the error toast
    } finally {
      setConfirmReset(false)
    }
  }

  if (!canManage) return null

  if (contact.isPendingApproval) {
    return (
      <div className="flex justify-end gap-2">
        {isAdmin ? (
          <button
            type="button"
            onClick={async () => {
              try {
                await updateStatus.mutateAsync({ id: contact.id, isActive: true })
                toast.success(`${contact.email} approved — sign-in link sent`)
              } catch {
                // interceptor surfaces the error toast
              }
            }}
            disabled={updateStatus.isPending}
            className="rounded-lg bg-[var(--app-primary)] px-3 py-1.5 text-xs font-medium text-white hover:bg-[var(--app-secondary)] disabled:opacity-60"
          >
            Approve
          </button>
        ) : (
          <span className="text-xs text-slate-500">Awaiting admin approval</span>
        )}
      </div>
    )
  }

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
            await resendLink.mutateAsync(contact.id)
            toast.success(`Invitation resent to ${contact.email}`)
          } catch {
            // interceptor surfaces the error toast
          }
        }}
        disabled={resendLink.isPending}
        className="rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-xs font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-60"
      >
        Resend Invite
      </button>
      {isAdmin && contact.isActive ? (
        <>
          <button
            type="button"
            onClick={() => void copyLink(false)}
            disabled={accessLink.isPending}
            title="Copy this contact's permanent portal link to share by WhatsApp or SMS"
            className="inline-flex items-center gap-1 rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-xs font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-60"
          >
            <Link2 className="h-3.5 w-3.5" />Copy portal link
          </button>
          <button
            type="button"
            onClick={() => setConfirmReset(true)}
            disabled={accessLink.isPending}
            title="Replace the portal link if it was shared with the wrong person"
            className="inline-flex items-center gap-1 rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-xs font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-60"
          >
            <RefreshCw className="h-3.5 w-3.5" />Reset link
          </button>
          <ConfirmDialog
            open={confirmReset}
            title="Reset portal link?"
            message={`${contact.fullName}'s current portal link will stop working immediately, including any copy already shared. A new link is copied for you to send.`}
            confirmLabel="Reset link"
            danger
            confirmDisabled={accessLink.isPending}
            onCancel={() => setConfirmReset(false)}
            onConfirm={() => void copyLink(true)}
          />
        </>
      ) : null}
      {sharedLink ? (
        <input readOnly autoFocus value={sharedLink} onFocus={(event) => event.currentTarget.select()} onBlur={() => setSharedLink(null)} className="h-8 w-56 rounded-lg border border-slate-300 px-2 text-xs" aria-label="Portal link — copy it manually" />
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

/** The address clients use; they type their email and receive a one-time sign-in link — no password. */
function PortalAddress() {
  const url = portalLoginUrl()
  return (
    <div className="flex flex-wrap items-center justify-between gap-2 rounded-xl border border-slate-200 bg-slate-50 px-4 py-3">
      <div className="text-sm">
        <span className="text-slate-500">Client portal address: </span>
        <span className="font-medium text-slate-900">{url}</span>
        <span className="block text-xs text-slate-500">Clients sign in with the email you invite — there's no password to share.</span>
      </div>
      <button
        type="button"
        onClick={async () => {
          try {
            await navigator.clipboard.writeText(url)
            toast.success('Portal address copied')
          } catch {
            toast.error('Could not copy — select the address and copy it manually')
          }
        }}
        className="inline-flex items-center gap-1 rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-xs font-medium text-slate-700 hover:bg-slate-50"
      >
        <Copy className="h-3.5 w-3.5" />Copy address
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
    { key: 'isActive', header: 'Status', cell: (row) => <StatusBadge status={row.isPendingApproval ? 'Pending Approval' : row.isActive ? 'Active' : 'Inactive'} /> },
    { key: 'lastLoginAt', header: 'Last Sign-In', cell: (row) => (row.lastLoginAt ? formatDateTime(row.lastLoginAt) : 'Never') },
    ...(whatsApp ? [{ key: 'whatsApp', header: 'WhatsApp', cell: (row: ClientPortalContact) => (row.receivesWhatsApp ? formatWhatsAppNumber(row.whatsAppNumber) : 'Off') }] : []),
    { key: 'actions', header: '', cell: (row) => <ContactRowActions clientId={clientId} contact={row} onWhatsApp={whatsApp ? () => setWhatsAppContact(row) : null} /> },
  ]

  return (
    <div className="space-y-4">
      <PortalAddress />
      <div className="flex items-center justify-between">
        <p className="text-sm text-slate-600">Contacts invited here can sign in to the client portal to raise requests and view their work orders, invoices, and quotes.</p>
        {canManage ? (
          <button type="button" onClick={() => setShowInvite(true)} className="inline-flex h-11 shrink-0 items-center gap-2 rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white hover:bg-[var(--app-secondary)]">
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
      {whatsAppContact ? <ContactWhatsAppDrawer open clientId={clientId} contact={whatsAppContact} onClose={() => setWhatsAppContact(null)} /> : null}
    </div>
  )
}
