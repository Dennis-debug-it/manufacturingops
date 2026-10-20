import { useEffect, useState } from 'react'
import toast from 'react-hot-toast'
import { useNavigate } from 'react-router-dom'
import { FormDrawer } from '../../../shared/components/FormDrawer'
import { useAuth } from '../../../shared/hooks/useAuth'
import { getActiveCurrency } from '../../../shared/utils/currency'
import { useClients } from '../../clients/api'
import { useCreateTender, useUpdateTender } from '../api'
import { TENDER_TYPES, tenderTypeLabels, type SalesTenderDetail, type TenderType } from '../types'
import { OwnerSelect } from './OwnerSelect'

const fieldClass = 'h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none transition focus:border-[var(--app-primary)]'
const labelClass = 'mb-1.5 block text-sm font-medium text-slate-700'

interface FormState {
  type: TenderType
  reference: string
  title: string
  partyMode: 'prospect' | 'client'
  clientId: string
  issuerName: string
  contactName: string
  contactEmail: string
  contactPhone: string
  receivedAt: string
  clarificationDeadline: string
  submissionDeadline: string
  submissionTime: string
  submissionMethod: string
  bidBondRequired: boolean
  bidBondAmount: string
  description: string
  estimatedValue: string
  currency: string
  ownerUserId: string
}

const today = () => new Date().toISOString().slice(0, 10)

/** Local date + time from a stored UTC instant, for the date and time inputs. */
function splitLocal(value: string | null | undefined) {
  if (!value) return { date: '', time: '' }
  const d = new Date(value)
  const pad = (n: number) => String(n).padStart(2, '0')
  return { date: `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`, time: `${pad(d.getHours())}:${pad(d.getMinutes())}` }
}

function toForm(tender: SalesTenderDetail | null | undefined, currentUserId: string): FormState {
  const deadline = splitLocal(tender?.submissionDeadline)
  return {
    type: tender?.type ?? 'RFQ',
    reference: tender?.reference ?? '',
    title: tender?.title ?? '',
    partyMode: tender?.clientId ? 'client' : 'prospect',
    clientId: tender?.clientId ?? '',
    issuerName: tender?.issuerName ?? '',
    contactName: tender?.contactName ?? '',
    contactEmail: tender?.contactEmail ?? '',
    contactPhone: tender?.contactPhone ?? '',
    receivedAt: tender ? splitLocal(tender.receivedAt).date : today(),
    clarificationDeadline: splitLocal(tender?.clarificationDeadline).date,
    submissionDeadline: deadline.date,
    submissionTime: deadline.time || '12:00',
    submissionMethod: tender?.submissionMethod ?? '',
    bidBondRequired: tender?.bidBondRequired ?? false,
    bidBondAmount: tender?.bidBondAmount != null ? String(tender.bidBondAmount) : '',
    description: tender?.description ?? '',
    estimatedValue: tender ? String(tender.estimatedValue) : '',
    currency: tender?.currency ?? getActiveCurrency(),
    ownerUserId: tender ? (tender.ownerUserId ?? '') : currentUserId,
  }
}

const orNull = (value: string) => (value.trim() ? value.trim() : null)

export function TenderFormDrawer({ open, onClose, tender }: { open: boolean; onClose: () => void; tender?: SalesTenderDetail | null }) {
  const navigate = useNavigate()
  const isEdit = Boolean(tender)
  const { user } = useAuth()
  const currentUserId = user?.userId ?? ''
  const [form, setForm] = useState<FormState>(() => toForm(tender, currentUserId))
  const { data: clients } = useClients()
  const create = useCreateTender()
  const update = useUpdateTender(tender?.id ?? '')
  const saving = create.isPending || update.isPending

  useEffect(() => {
    if (open) setForm(toForm(tender, currentUserId))
  }, [open, tender, currentUserId])

  const set = (patch: Partial<FormState>) => setForm((current) => ({ ...current, ...patch }))

  const submit = async () => {
    if (!form.title.trim()) return void toast.error('Give the request a title')
    if (form.partyMode === 'client' && !form.clientId) return void toast.error('Pick the client')
    if (form.partyMode === 'prospect' && !form.issuerName.trim()) return void toast.error('Enter who issued the request')
    if (!form.submissionDeadline) return void toast.error('The submission deadline is required')

    const payload = {
      type: form.type,
      reference: orNull(form.reference),
      title: form.title.trim(),
      clientId: form.partyMode === 'client' ? form.clientId : null,
      issuerName: form.partyMode === 'prospect' ? orNull(form.issuerName) : null,
      contactName: orNull(form.contactName),
      contactEmail: orNull(form.contactEmail),
      contactPhone: orNull(form.contactPhone),
      receivedAt: form.receivedAt ? new Date(`${form.receivedAt}T00:00`).toISOString() : null,
      clarificationDeadline: form.clarificationDeadline ? new Date(`${form.clarificationDeadline}T23:59`).toISOString() : null,
      submissionDeadline: new Date(`${form.submissionDeadline}T${form.submissionTime || '23:59'}`).toISOString(),
      submissionMethod: orNull(form.submissionMethod),
      bidBondRequired: form.bidBondRequired,
      bidBondAmount: form.bidBondRequired && form.bidBondAmount ? Number(form.bidBondAmount) : null,
      description: orNull(form.description),
      estimatedValue: Number(form.estimatedValue) || 0,
      currency: form.currency.trim().toUpperCase() || getActiveCurrency(),
      ownerUserId: form.ownerUserId || null,
    }
    try {
      if (isEdit) {
        await update.mutateAsync(payload)
        toast.success('Request updated')
        onClose()
      } else {
        const created = await create.mutateAsync(payload)
        toast.success(`${created.type} logged`)
        onClose()
        navigate(`/crm/tenders/${created.id}`)
      }
    } catch {
      // interceptor surfaces the error toast
    }
  }

  return (
    <FormDrawer
      open={open}
      title={isEdit ? `Edit ${tender?.type}` : 'Log RFP / RFQ'}
      onClose={onClose}
      footer={
        <div className="flex items-center justify-end gap-2">
          <button type="button" onClick={onClose} className="inline-flex h-11 items-center justify-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">Cancel</button>
          <button type="button" onClick={() => void submit()} disabled={saving} className="inline-flex h-11 items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:opacity-60">
            {saving ? 'Saving…' : isEdit ? 'Save Changes' : 'Log Request'}
          </button>
        </div>
      }
    >
      <div className="space-y-4">
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-[1fr_1fr]">
          <div>
            <label className={labelClass} htmlFor="tender-type">Type</label>
            <select id="tender-type" value={form.type} onChange={(event) => set({ type: event.target.value as TenderType })} className={fieldClass}>
              {TENDER_TYPES.map((type) => (
                <option key={type} value={type}>{tenderTypeLabels[type]}</option>
              ))}
            </select>
          </div>
          <div>
            <label className={labelClass} htmlFor="tender-ref">Their reference</label>
            <input id="tender-ref" value={form.reference} onChange={(event) => set({ reference: event.target.value })} className={fieldClass} placeholder="e.g. KPLC/RFQ/2026/114" />
          </div>
        </div>

        <div>
          <label className={labelClass} htmlFor="tender-title">Title</label>
          <input id="tender-title" value={form.title} onChange={(event) => set({ title: event.target.value })} className={fieldClass} placeholder="e.g. Supply and installation of 2 × 100 kVA generators" />
        </div>

        <div>
          <span className={labelClass}>Issued by</span>
          <div className="inline-flex rounded-lg border border-slate-200 p-1">
            {(['prospect', 'client'] as const).map((mode) => (
              <button key={mode} type="button" onClick={() => set({ partyMode: mode })} className={`h-9 rounded-md px-3 text-sm font-medium transition ${form.partyMode === mode ? 'bg-[var(--app-primary)] text-white' : 'text-slate-600 hover:bg-slate-50'}`}>
                {mode === 'prospect' ? 'New organisation' : 'Existing client'}
              </button>
            ))}
          </div>
        </div>

        {form.partyMode === 'client' ? (
          <div>
            <label className={labelClass} htmlFor="tender-client">Client</label>
            <select id="tender-client" value={form.clientId} onChange={(event) => set({ clientId: event.target.value })} className={fieldClass}>
              <option value="">Select client</option>
              {(clients ?? []).map((client) => (
                <option key={client.id} value={client.id}>{client.clientName}</option>
              ))}
            </select>
          </div>
        ) : (
          <div>
            <label className={labelClass} htmlFor="tender-issuer">Organisation</label>
            <input id="tender-issuer" value={form.issuerName} onChange={(event) => set({ issuerName: event.target.value })} className={fieldClass} />
          </div>
        )}

        <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
          <div>
            <label className={labelClass} htmlFor="tender-contact">Contact person</label>
            <input id="tender-contact" value={form.contactName} onChange={(event) => set({ contactName: event.target.value })} className={fieldClass} />
          </div>
          <div>
            <label className={labelClass} htmlFor="tender-email">Email</label>
            <input id="tender-email" type="email" value={form.contactEmail} onChange={(event) => set({ contactEmail: event.target.value })} className={fieldClass} />
          </div>
          <div>
            <label className={labelClass} htmlFor="tender-phone">Phone</label>
            <input id="tender-phone" value={form.contactPhone} onChange={(event) => set({ contactPhone: event.target.value })} className={fieldClass} />
          </div>
        </div>

        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          <div>
            <label className={labelClass} htmlFor="tender-received">Received on</label>
            <input id="tender-received" type="date" value={form.receivedAt} onChange={(event) => set({ receivedAt: event.target.value })} className={fieldClass} />
          </div>
          <div>
            <label className={labelClass} htmlFor="tender-clarify">Questions / clarifications by</label>
            <input id="tender-clarify" type="date" value={form.clarificationDeadline} onChange={(event) => set({ clarificationDeadline: event.target.value })} className={fieldClass} />
          </div>
          <div>
            <label className={labelClass} htmlFor="tender-deadline">Submission deadline</label>
            <div className="flex gap-2">
              <input id="tender-deadline" type="date" value={form.submissionDeadline} onChange={(event) => set({ submissionDeadline: event.target.value })} className={fieldClass} />
              <input aria-label="Deadline time" type="time" value={form.submissionTime} onChange={(event) => set({ submissionTime: event.target.value })} className={`${fieldClass} w-32`} />
            </div>
          </div>
          <div>
            <label className={labelClass} htmlFor="tender-method">How to submit</label>
            <input id="tender-method" value={form.submissionMethod} onChange={(event) => set({ submissionMethod: event.target.value })} className={fieldClass} placeholder="e.g. Supplier portal, sealed envelope" />
          </div>
        </div>

        <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
          <div>
            <label className={labelClass} htmlFor="tender-value">Estimated value</label>
            <input id="tender-value" type="number" min="0" value={form.estimatedValue} onChange={(event) => set({ estimatedValue: event.target.value })} className={fieldClass} />
          </div>
          <div>
            <label className={labelClass} htmlFor="tender-currency">Currency</label>
            <input id="tender-currency" value={form.currency} onChange={(event) => set({ currency: event.target.value })} className={fieldClass} maxLength={3} />
          </div>
          <div>
            <label className={labelClass} htmlFor="tender-owner">Owner</label>
            <OwnerSelect id="tender-owner" value={form.ownerUserId} onChange={(ownerUserId) => set({ ownerUserId })} className={fieldClass} emptyLabel="Unassigned" />
          </div>
        </div>

        <div className="flex flex-wrap items-end gap-3">
          <label className="flex h-11 items-center gap-2 text-sm text-slate-700">
            <input type="checkbox" checked={form.bidBondRequired} onChange={(event) => set({ bidBondRequired: event.target.checked })} className="h-4 w-4" />
            Bid bond / security required
          </label>
          {form.bidBondRequired ? (
            <div className="w-48">
              <label className={labelClass} htmlFor="tender-bond">Bond amount</label>
              <input id="tender-bond" type="number" min="0" value={form.bidBondAmount} onChange={(event) => set({ bidBondAmount: event.target.value })} className={fieldClass} />
            </div>
          ) : null}
        </div>

        <div>
          <label className={labelClass} htmlFor="tender-description">Scope / requirements</label>
          <textarea id="tender-description" value={form.description} onChange={(event) => set({ description: event.target.value })} rows={4} className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[var(--app-primary)]" placeholder="What is being asked for, evaluation criteria, mandatory documents…" />
        </div>
      </div>
    </FormDrawer>
  )
}
