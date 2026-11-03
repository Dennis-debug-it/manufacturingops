import { useMemo, useState } from 'react'
import toast from 'react-hot-toast'
import { Mail, MessageCircle } from 'lucide-react'
import { FormDrawer } from '../../../shared/components/FormDrawer'
import { SummarySection } from '../../../shared/pages/ModulePage'
import { formatDateTime } from '../../../shared/utils/dates'
import { cn } from '../../../shared/utils/cn'
import { useAddActivity, useDealWhatsApp, useSendDealEmail } from '../api'
import { whatsAppLink, type SalesOpportunityDetail } from '../types'
import { EMAIL_INTAKE_ENABLED, WHATSAPP_ENABLED } from '../../../shared/config/features'

const fieldClass = 'h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none transition focus:border-[var(--app-primary)]'
const labelClass = 'mb-1.5 block text-sm font-medium text-slate-700'
const outlineButton = 'inline-flex h-11 items-center gap-2 rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50 disabled:opacity-60'

interface Person {
  name: string | null
  email: string | null
  phone: string | null
}

/** The deal's own contact details first, then the people linked to it, without repeats. */
function peopleOf(opportunity: SalesOpportunityDetail): Person[] {
  const people: Person[] = []
  if (opportunity.contactEmail || opportunity.contactPhone)
    people.push({ name: opportunity.contactName, email: opportunity.contactEmail, phone: opportunity.contactPhone })
  for (const contact of opportunity.contacts ?? []) {
    if (people.some((p) => (contact.email && p.email === contact.email) || (contact.phone && p.phone === contact.phone))) continue
    people.push({ name: contact.fullName, email: contact.email, phone: contact.phone })
  }
  return people
}

const label = (person: Person, detail: string | null) => (person.name ? `${person.name} — ${detail}` : (detail ?? ''))

/** Email and WhatsApp buttons for the deal page header, with their drawers. */
export function DealMessageButtons({ opportunity }: { opportunity: SalesOpportunityDetail }) {
  const [mode, setMode] = useState<'email' | 'whatsapp' | null>(null)
  const people = useMemo(() => peopleOf(opportunity), [opportunity])
  return (
    <>
      <button type="button" onClick={() => setMode('email')} className={outlineButton}><Mail className="h-4 w-4" />Email</button>
      {WHATSAPP_ENABLED ? <button type="button" onClick={() => setMode('whatsapp')} className={outlineButton}><MessageCircle className="h-4 w-4" />WhatsApp</button> : null}
      {mode === 'email' ? <EmailDrawer opportunity={opportunity} people={people} onClose={() => setMode(null)} /> : null}
      {mode === 'whatsapp' ? <WhatsAppDrawer opportunity={opportunity} people={people} onClose={() => setMode(null)} /> : null}
    </>
  )
}

function EmailDrawer({ opportunity, people, onClose }: { opportunity: SalesOpportunityDetail; people: Person[]; onClose: () => void }) {
  const withEmail = people.filter((p) => p.email)
  const [to, setTo] = useState(withEmail[0]?.email ?? '')
  const [cc, setCc] = useState('')
  const [subject, setSubject] = useState(opportunity.title)
  const [body, setBody] = useState(withEmail[0]?.name ? `Dear ${withEmail[0].name.split(' ')[0]},\n\n` : '')
  const send = useSendDealEmail(opportunity.id)

  const submit = async () => {
    if (!to.trim()) return void toast.error('Who is it to?')
    if (!subject.trim() || !body.trim()) return void toast.error('Write a subject and a message')
    try {
      await send.mutateAsync({
        to: to.trim(),
        toName: people.find((p) => p.email === to.trim())?.name ?? null,
        cc: cc.trim() ? cc.split(/[,;\s]+/).filter(Boolean) : null,
        subject: subject.trim(),
        body,
      })
      toast.success('Email sent and logged on the deal')
      onClose()
    } catch {
      // interceptor surfaces the error toast
    }
  }

  return (
    <FormDrawer
      open
      title="Email the client"
      onClose={onClose}
      footer={
        <div className="flex items-center justify-end gap-2">
          <button type="button" onClick={onClose} className={outlineButton}>Cancel</button>
          <button type="button" disabled={send.isPending} onClick={() => void submit()} className="inline-flex h-11 items-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:opacity-60">
            {send.isPending ? 'Sending…' : 'Send'}
          </button>
        </div>
      }
    >
      <div className="space-y-4">
        <div>
          <label className={labelClass} htmlFor="deal-email-to">To</label>
          <input id="deal-email-to" list="deal-email-people" type="email" value={to} onChange={(event) => setTo(event.target.value)} className={fieldClass} placeholder="name@company.com" />
          <datalist id="deal-email-people">
            {withEmail.map((p) => <option key={p.email} value={p.email ?? ''}>{label(p, p.email)}</option>)}
          </datalist>
        </div>
        <div>
          <label className={labelClass} htmlFor="deal-email-cc">Copy to (optional)</label>
          <input id="deal-email-cc" value={cc} onChange={(event) => setCc(event.target.value)} className={fieldClass} placeholder="Separate addresses with commas" />
        </div>
        <div>
          <label className={labelClass} htmlFor="deal-email-subject">Subject</label>
          <input id="deal-email-subject" value={subject} onChange={(event) => setSubject(event.target.value)} className={fieldClass} maxLength={200} />
        </div>
        <div>
          <label className={labelClass} htmlFor="deal-email-body">Message</label>
          <textarea id="deal-email-body" value={body} onChange={(event) => setBody(event.target.value)} rows={10} className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[var(--app-primary)]" />
        </div>
        <p className="text-xs text-slate-500">
          {EMAIL_INTAKE_ENABLED
            ? "Sent from your workspace's address with your name, and logged on this deal. The subject carries the deal's reference, so when the workspace's intake mailbox is set up, the client's reply comes back onto this deal."
            : "Sent from your workspace's address with your name, and logged on this deal. The client's reply comes to your own email."}
        </p>
      </div>
    </FormDrawer>
  )
}

function WhatsAppDrawer({ opportunity, people, onClose }: { opportunity: SalesOpportunityDetail; people: Person[]; onClose: () => void }) {
  const withPhone = people.filter((p) => p.phone)
  const [phone, setPhone] = useState(withPhone[0]?.phone ?? '')
  const [text, setText] = useState(withPhone[0]?.name ? `Hello ${withPhone[0].name.split(' ')[0]}, ` : 'Hello, ')
  const log = useAddActivity(opportunity.id)

  const open = async () => {
    if (phone.replace(/\D/g, '').length < 9) return void toast.error('Enter the phone number')
    if (!text.trim()) return void toast.error('Write the message')
    window.open(whatsAppLink(phone, text.trim()), '_blank', 'noopener')
    const person = withPhone.find((p) => p.phone === phone)
    try {
      await log.mutateAsync({ type: 'WhatsApp', summary: `To ${person?.name ?? phone}: ${text.trim()}` })
    } catch {
      // the message still opened; the interceptor reports the logging error
    }
    onClose()
  }

  return (
    <FormDrawer
      open
      title="WhatsApp the client"
      onClose={onClose}
      footer={
        <div className="flex items-center justify-end gap-2">
          <button type="button" onClick={onClose} className={outlineButton}>Cancel</button>
          <button type="button" onClick={() => void open()} className="inline-flex h-11 items-center gap-2 rounded-lg bg-emerald-600 px-4 text-sm font-medium text-white transition hover:bg-emerald-700">
            <MessageCircle className="h-4 w-4" />Open in WhatsApp
          </button>
        </div>
      }
    >
      <div className="space-y-4">
        <div>
          <label className={labelClass} htmlFor="deal-wa-phone">Phone</label>
          <input id="deal-wa-phone" list="deal-wa-people" value={phone} onChange={(event) => setPhone(event.target.value)} className={fieldClass} placeholder="0712 345 678" />
          <datalist id="deal-wa-people">
            {withPhone.map((p) => <option key={p.phone} value={p.phone ?? ''}>{label(p, p.phone)}</option>)}
          </datalist>
        </div>
        <div>
          <label className={labelClass} htmlFor="deal-wa-text">Message</label>
          <textarea id="deal-wa-text" value={text} onChange={(event) => setText(event.target.value)} rows={6} className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[var(--app-primary)]" />
        </div>
        <p className="text-xs text-slate-500">Opens WhatsApp on this device with the message ready to send from your own number, and logs it on the deal.</p>
      </div>
    </FormDrawer>
  )
}

/** Messages in the workspace's WhatsApp inbox with the deal's people; hidden when there are none. */
export function DealWhatsAppHistory({ opportunityId }: { opportunityId: string }) {
  return WHATSAPP_ENABLED ? <WhatsAppHistory opportunityId={opportunityId} /> : null
}

function WhatsAppHistory({ opportunityId }: { opportunityId: string }) {
  const { data } = useDealWhatsApp(opportunityId)
  if (!data || data.length === 0) return null
  return (
    <SummarySection title="WhatsApp" description="From the workspace's WhatsApp inbox.">
      <ol className="max-h-80 space-y-2 overflow-y-auto">
        {data.map((message) => (
          <li key={message.id} className={cn('max-w-[90%] rounded-xl px-3 py-2 text-sm', message.direction === 'Inbound' ? 'bg-slate-100 text-slate-900' : 'ml-auto bg-emerald-50 text-emerald-950')}>
            <div className="whitespace-pre-wrap">{message.body}</div>
            <div className="mt-1 text-[11px] text-slate-500">
              {message.direction === 'Inbound' ? message.contactName : 'Sent'} · {formatDateTime(message.sentAt)}
            </div>
          </li>
        ))}
      </ol>
    </SummarySection>
  )
}
