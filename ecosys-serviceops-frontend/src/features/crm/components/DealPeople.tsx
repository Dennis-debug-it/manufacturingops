import { useState } from 'react'
import toast from 'react-hot-toast'
import { Mail, Phone, X } from 'lucide-react'
import { SummarySection } from '../../../shared/pages/ModulePage'
import { useClientContacts } from '../../clients/api'
import { CONTACT_ROLES, contactRoleLabels } from '../../clients/types'
import { useAddDealContact, useRemoveDealContact } from '../api'
import type { SalesOpportunityDetail } from '../types'

const fieldClass = 'h-10 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]'

/** Who at the client is involved in this deal; adding someone new also adds them to the client's contacts. */
export function DealPeople({ opportunity, canManage }: { opportunity: SalesOpportunityDetail; canManage: boolean }) {
  const people = opportunity.contacts ?? []
  const { data: clientContacts } = useClientContacts(opportunity.clientId ?? undefined)
  const add = useAddDealContact(opportunity.id)
  const remove = useRemoveDealContact(opportunity.id)
  const [adding, setAdding] = useState(false)
  const [pick, setPick] = useState('')
  const [form, setForm] = useState({ fullName: '', email: '', phone: '', role: 'Other' })
  const others = (clientContacts ?? []).filter((contact) => !people.some((person) => person.id === contact.id))

  const submit = async () => {
    const payload = pick
      ? { contactId: pick }
      : { fullName: form.fullName.trim(), email: form.email.trim() || null, phone: form.phone.trim() || null, role: form.role }
    if (!pick && !payload.fullName) {
      toast.error('Pick a contact or enter a name')
      return
    }
    try {
      await add.mutateAsync(payload)
      setAdding(false)
      setPick('')
      setForm({ fullName: '', email: '', phone: '', role: 'Other' })
    } catch {
      // interceptor surfaces the error toast
    }
  }

  return (
    <SummarySection title="People" description="Who at the client is involved.">
      {people.length === 0 && !adding ? <p className="text-sm text-slate-500">No one added yet.</p> : null}
      <ul className="divide-y divide-slate-100">
        {people.map((person) => (
          <li key={person.id} className="flex items-start justify-between gap-2 py-2">
            <div className="min-w-0 text-sm">
              <div className="font-medium text-slate-900">{person.fullName} <span className="text-xs font-normal text-slate-500">· {contactRoleLabels[person.role] ?? person.role}</span></div>
              <div className="mt-0.5 flex flex-wrap gap-x-3 text-xs text-slate-600">
                {person.email ? <a href={`mailto:${person.email}`} className="inline-flex items-center gap-1 hover:underline"><Mail className="h-3 w-3" />{person.email}</a> : null}
                {person.phone ? <a href={`tel:${person.phone}`} className="inline-flex items-center gap-1 hover:underline"><Phone className="h-3 w-3" />{person.phone}</a> : null}
              </div>
            </div>
            {canManage ? (
              <button type="button" aria-label={`Remove ${person.fullName} from the deal`} onClick={() => remove.mutate(person.id)} className="rounded p-1 text-slate-400 hover:bg-slate-100 hover:text-slate-600">
                <X className="h-4 w-4" />
              </button>
            ) : null}
          </li>
        ))}
      </ul>
      {canManage && opportunity.clientId ? (
        adding ? (
          <div className="mt-3 space-y-2">
            {others.length > 0 ? (
              <select aria-label="Existing contact" value={pick} onChange={(event) => setPick(event.target.value)} className={fieldClass}>
                <option value="">Someone new…</option>
                {others.map((contact) => <option key={contact.id} value={contact.id}>{contact.fullName}</option>)}
              </select>
            ) : null}
            {!pick ? (
              <>
                <input aria-label="Name" placeholder="Full name" value={form.fullName} onChange={(event) => setForm({ ...form, fullName: event.target.value })} className={fieldClass} />
                <input aria-label="Email" type="email" placeholder="Email" value={form.email} onChange={(event) => setForm({ ...form, email: event.target.value })} className={fieldClass} />
                <input aria-label="Phone" placeholder="Phone" value={form.phone} onChange={(event) => setForm({ ...form, phone: event.target.value })} className={fieldClass} />
                <select aria-label="Role" value={form.role} onChange={(event) => setForm({ ...form, role: event.target.value })} className={fieldClass}>
                  {CONTACT_ROLES.map((role) => <option key={role} value={role}>{contactRoleLabels[role]}</option>)}
                </select>
              </>
            ) : null}
            <div className="flex gap-2">
              <button type="button" disabled={add.isPending} onClick={() => void submit()} className="inline-flex h-10 items-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white disabled:opacity-60">Add</button>
              <button type="button" onClick={() => setAdding(false)} className="inline-flex h-10 items-center rounded-lg border border-slate-200 px-4 text-sm font-medium text-slate-700">Cancel</button>
            </div>
          </div>
        ) : (
          <button type="button" onClick={() => setAdding(true)} className="mt-2 text-sm font-medium text-[var(--app-primary)] hover:underline">+ Add a person</button>
        )
      ) : null}
    </SummarySection>
  )
}
