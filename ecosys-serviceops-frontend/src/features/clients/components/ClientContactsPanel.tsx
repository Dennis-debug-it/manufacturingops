import { useState } from 'react'
import toast from 'react-hot-toast'
import { Mail, Phone, Plus, Star } from 'lucide-react'
import { SummarySection } from '../../../shared/pages/ModulePage'
import { usePermission } from '../../../shared/hooks/usePermission'
import { useClientContacts, useDeleteClientContact, useSaveClientContact } from '../api'
import { CONTACT_ROLES, contactRoleLabels, type ClientContact, type UpsertClientContactPayload } from '../types'

const fieldClass = 'h-10 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]'
const empty: UpsertClientContactPayload = { fullName: '', jobTitle: null, email: null, phone: null, role: 'Other', isPrimary: false, notes: null }

/** The people at a client: who to call, and what part they play in buying. */
export function ClientContactsPanel({ clientId }: { clientId: string }) {
  const canEdit = usePermission('clients.edit')
  const { data: contacts, isLoading } = useClientContacts(clientId)
  const save = useSaveClientContact(clientId)
  const remove = useDeleteClientContact(clientId)
  const [editing, setEditing] = useState<(UpsertClientContactPayload & { id?: string }) | null>(null)

  const submit = async () => {
    if (!editing?.fullName.trim()) {
      toast.error('Enter the contact’s name')
      return
    }
    try {
      await save.mutateAsync(editing)
      toast.success('Contact saved')
      setEditing(null)
    } catch {
      // interceptor surfaces the error toast
    }
  }

  const edit = (contact: ClientContact) => setEditing({ ...contact })

  return (
    <SummarySection title="Contacts" description="People at this client and the part they play.">
      {canEdit && !editing ? (
        <div className="mb-3 flex justify-end">
          <button type="button" onClick={() => setEditing({ ...empty })} className="inline-flex h-10 items-center gap-2 rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)]">
            <Plus className="h-4 w-4" />Add contact
          </button>
        </div>
      ) : null}

      {editing ? (
        <div className="mb-4 grid gap-3 rounded-xl border border-slate-200 p-3 sm:grid-cols-2">
          <input aria-label="Name" placeholder="Full name" value={editing.fullName} onChange={(event) => setEditing({ ...editing, fullName: event.target.value })} className={fieldClass} />
          <input aria-label="Job title" placeholder="Job title" value={editing.jobTitle ?? ''} onChange={(event) => setEditing({ ...editing, jobTitle: event.target.value || null })} className={fieldClass} />
          <input aria-label="Email" type="email" placeholder="Email" value={editing.email ?? ''} onChange={(event) => setEditing({ ...editing, email: event.target.value || null })} className={fieldClass} />
          <input aria-label="Phone" placeholder="Phone" value={editing.phone ?? ''} onChange={(event) => setEditing({ ...editing, phone: event.target.value || null })} className={fieldClass} />
          <select aria-label="Role" value={editing.role} onChange={(event) => setEditing({ ...editing, role: event.target.value as UpsertClientContactPayload['role'] })} className={fieldClass}>
            {CONTACT_ROLES.map((role) => <option key={role} value={role}>{contactRoleLabels[role]}</option>)}
          </select>
          <label className="flex items-center gap-2 text-sm text-slate-700">
            <input type="checkbox" checked={editing.isPrimary} onChange={(event) => setEditing({ ...editing, isPrimary: event.target.checked })} />
            Main contact
          </label>
          <div className="flex gap-2 sm:col-span-2">
            <button type="button" disabled={save.isPending} onClick={() => void submit()} className="inline-flex h-10 items-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white disabled:opacity-60">Save</button>
            <button type="button" onClick={() => setEditing(null)} className="inline-flex h-10 items-center rounded-lg border border-slate-200 px-4 text-sm font-medium text-slate-700">Cancel</button>
          </div>
        </div>
      ) : null}

      {isLoading ? <p className="text-sm text-slate-400">Loading…</p> : null}
      {contacts && contacts.length === 0 && !editing ? <p className="text-sm text-slate-500">No contacts yet.</p> : null}
      <ul className="divide-y divide-slate-100">
        {(contacts ?? []).map((contact) => (
          <li key={contact.id} className="flex flex-col gap-2 py-3 sm:flex-row sm:items-start sm:justify-between">
            <div className="min-w-0">
              <div className="flex items-center gap-2 text-sm font-medium text-slate-900">
                {contact.fullName}
                {contact.isPrimary ? <Star className="h-3.5 w-3.5 fill-amber-400 text-amber-400" aria-label="Main contact" /> : null}
                <span className="rounded-full bg-slate-100 px-2 py-0.5 text-xs font-normal text-slate-600">{contactRoleLabels[contact.role] ?? contact.role}</span>
              </div>
              {contact.jobTitle ? <div className="text-xs text-slate-500">{contact.jobTitle}</div> : null}
              <div className="mt-1 flex flex-wrap gap-x-4 gap-y-1 text-xs text-slate-600">
                {contact.email ? <a href={`mailto:${contact.email}`} className="inline-flex items-center gap-1 hover:underline"><Mail className="h-3 w-3" />{contact.email}</a> : null}
                {contact.phone ? <a href={`tel:${contact.phone}`} className="inline-flex items-center gap-1 hover:underline"><Phone className="h-3 w-3" />{contact.phone}</a> : null}
              </div>
            </div>
            {canEdit ? (
              <div className="flex shrink-0 gap-2">
                <button type="button" onClick={() => edit(contact)} className="text-xs font-medium text-slate-600 hover:underline">Edit</button>
                <button type="button" disabled={remove.isPending} onClick={() => remove.mutate(contact.id)} className="text-xs font-medium text-rose-600 hover:underline">Remove</button>
              </div>
            ) : null}
          </li>
        ))}
      </ul>
    </SummarySection>
  )
}
