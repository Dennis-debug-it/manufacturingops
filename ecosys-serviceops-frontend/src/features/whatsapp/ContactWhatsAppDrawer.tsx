import { useEffect, useState } from 'react'
import toast from 'react-hot-toast'
import { FormDrawer } from '../../shared/components/FormDrawer'
import { formatDateTime } from '../../shared/utils/dates'
import { CONSENT_METHODS, consentMethodLabel, formatWhatsAppNumber, normalizeWhatsAppNumber } from './reminderLogic'
import { useContactWhatsApp, useRecordContactWhatsApp } from './remindersApi'

const inputClass = 'h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]'

/**
 * Office staff record that a client contact agreed to WhatsApp updates (Release 3 design 4.2): the
 * number, and how they agreed. Client messages go only to contacts marked here or who opted in from
 * the portal.
 */
export function ContactWhatsAppDrawer({ open, clientId, contact, onClose }: {
  open: boolean
  clientId: string
  contact: { id: string; fullName: string; phone: string | null }
  onClose: () => void
}) {
  const { data, isLoading } = useContactWhatsApp(clientId, contact.id, open)
  const record = useRecordContactWhatsApp(clientId, contact.id)
  const [number, setNumber] = useState('')
  const [method, setMethod] = useState('')
  const [agreed, setAgreed] = useState(false)

  useEffect(() => {
    if (!open) return
    setNumber(data?.number ?? contact.phone ?? '')
    setMethod('')
    setAgreed(false)
  }, [open, data?.number, contact.phone])

  const normalized = normalizeWhatsAppNumber(number)
  const keepsPortalConsent = data?.consentMethod === 'Portal' && data.number === normalized

  const save = async (receivesWhatsApp: boolean) => {
    try {
      await record.mutateAsync({ phoneNumber: receivesWhatsApp ? normalized : null, receivesWhatsApp, consentMethod: receivesWhatsApp && !keepsPortalConsent ? method : null })
      toast.success(receivesWhatsApp ? `WhatsApp updates on for ${contact.fullName}` : `WhatsApp updates off for ${contact.fullName}`)
      onClose()
    } catch {
      // interceptor surfaces the error toast
    }
  }

  const canTurnOn = Boolean(normalized) && (keepsPortalConsent || (Boolean(method) && agreed))

  return (
    <FormDrawer open={open} title={`WhatsApp updates: ${contact.fullName}`} onClose={onClose}>
      {isLoading ? <p className="text-sm text-slate-500">Loading…</p> : (
        <div className="space-y-4">
          {data?.receivesWhatsApp ? (
            <div className="rounded-lg bg-emerald-50 px-3 py-2 text-sm text-emerald-800">
              On for {formatWhatsAppNumber(data.number)}. {consentMethodLabel(data.consentMethod)}
              {data.consentRecordedBy ? `, recorded by ${data.consentRecordedBy}` : ''}{data.consentAt ? ` on ${formatDateTime(data.consentAt)}` : ''}.
            </div>
          ) : null}
          {data?.blockedByStop ? <p className="rounded-lg bg-amber-50 px-3 py-2 text-sm text-amber-800">This number replied STOP, so nothing is sent to it until they reply START.</p> : null}

          <p className="text-sm text-slate-600">
            Only turn this on if the contact agreed to get job, invoice and valuation updates on WhatsApp. Messages come from the shared Ecosys number and name your company. They can reply STOP at any time.
          </p>
          <div>
            <label htmlFor="contact-whatsapp-number" className="mb-1.5 block text-sm font-medium text-slate-700">WhatsApp number</label>
            <input id="contact-whatsapp-number" type="tel" value={number} onChange={(event) => setNumber(event.target.value)} placeholder="0712 345 678" className={inputClass} />
            {number.trim() && !normalized ? <p className="mt-1 text-xs text-rose-600">That doesn’t look like a mobile number.</p> : null}
          </div>
          {!keepsPortalConsent ? (
            <>
              <div>
                <label htmlFor="contact-whatsapp-method" className="mb-1.5 block text-sm font-medium text-slate-700">How did they agree?</label>
                <select id="contact-whatsapp-method" value={method} onChange={(event) => setMethod(event.target.value)} className={inputClass}>
                  <option value="">Choose…</option>
                  {CONSENT_METHODS.map((m) => <option key={m.value} value={m.value}>{m.label}</option>)}
                </select>
              </div>
              <label className="flex items-start gap-2 text-sm text-slate-700">
                <input type="checkbox" checked={agreed} onChange={(event) => setAgreed(event.target.checked)} className="mt-0.5" />
                The contact agreed to receive WhatsApp updates from us. Your name and today’s date are recorded with this.
              </label>
            </>
          ) : null}
          <div className="flex gap-2">
            {data?.receivesWhatsApp ? (
              <button type="button" disabled={record.isPending} onClick={() => void save(false)} className="inline-flex h-11 flex-1 items-center justify-center rounded-lg border border-slate-300 bg-white px-4 text-sm font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-60">
                Turn off
              </button>
            ) : null}
            <button type="button" disabled={!canTurnOn || record.isPending} onClick={() => void save(true)} className="inline-flex h-11 flex-1 items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white hover:bg-[var(--app-secondary)] disabled:opacity-60">
              {data?.receivesWhatsApp ? 'Save' : 'Turn on WhatsApp updates'}
            </button>
          </div>
        </div>
      )}
    </FormDrawer>
  )
}
