import { useState } from 'react'
import toast from 'react-hot-toast'
import { useNavigate } from 'react-router-dom'
import { Trash2 } from 'lucide-react'
import { FormDrawer } from '../../../shared/components/FormDrawer'
import { formatCurrency } from '../../../shared/utils/format'
import { getActiveCurrency } from '../../../shared/utils/currency'
import { useClients } from '../../clients/api'
import { useCreateQuotation, useDefaultTaxRate, useFinanceRateCards } from '../api'
import { RateCardAutocomplete } from './RateCardAutocomplete'

const fieldClass = 'h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none transition focus:border-[var(--app-primary)]'
const labelClass = 'mb-1.5 block text-sm font-medium text-slate-700'

interface Line {
  description: string
  quantity: string
  unitRate: string
  unit: string
  /** `null` means "the user hasn't set one" — the tenant's configured rate is shown instead. */
  taxRate: string | null
  itemType: string
  rateCardId: string
}

const emptyLine = (): Line => ({ description: '', quantity: '1', unitRate: '0', unit: 'unit', taxRate: null, itemType: 'Service', rateCardId: '' })

const VALIDITY_OPTIONS = [30, 60, 90, 120] as const
const DEFAULT_VALIDITY_DAYS = 30

/** End of the day `days` from today — quotes are valid through the whole of their last day. */
function validUntilFor(days: number) {
  const date = new Date()
  date.setDate(date.getDate() + days)
  date.setHours(23, 59, 59, 0)
  return date
}

export function NewQuoteFormDrawer({ open, onClose }: { open: boolean; onClose: () => void }) {
  const navigate = useNavigate()
  const { data: clients } = useClients()
  const { data: rateCards } = useFinanceRateCards()
  const { taxRate: tenantTaxRate } = useDefaultTaxRate()
  const createQuotation = useCreateQuotation()

  const [clientId, setClientId] = useState('')
  const [subject, setSubject] = useState('')
  const [validityDays, setValidityDays] = useState(DEFAULT_VALIDITY_DAYS)
  const [currency, setCurrency] = useState(getActiveCurrency)
  const [lines, setLines] = useState<Line[]>([emptyLine()])

  const activeRateCards = (rateCards ?? []).filter((card) => card.isActive)

  /** What the Tax % input shows: the user's own entry, else the tenant's configured rate. */
  const displayedTaxRate = (line: Line) => line.taxRate ?? (tenantTaxRate != null ? String(tenantTaxRate) : '')

  const close = () => {
    setClientId('')
    setSubject('')
    setValidityDays(DEFAULT_VALIDITY_DAYS)
    setCurrency(getActiveCurrency())
    setLines([emptyLine()])
    onClose()
  }

  const updateLine = (index: number, patch: Partial<Line>) => {
    setLines((current) => current.map((line, i) => (i === index ? { ...line, ...patch } : line)))
  }

  const applyRateCard = (index: number, rateCardId: string) => {
    if (!rateCardId) {
      updateLine(index, { rateCardId: '' })
      return
    }
    const card = activeRateCards.find((item) => item.id === rateCardId)
    if (!card) return
    updateLine(index, {
      rateCardId,
      description: card.description?.trim() || card.name,
      unitRate: String(card.unitRate),
      unit: card.unit || 'unit',
      ...(card.taxRate != null ? { taxRate: String(card.taxRate) } : {}),
      itemType: card.category || 'Service',
    })
  }

  const removeLine = (index: number) => {
    setLines((current) => (current.length > 1 ? current.filter((_, i) => i !== index) : current))
  }

  const total = lines.reduce((sum, line) => {
    const quantity = Number(line.quantity) || 0
    const rate = Number(line.unitRate) || 0
    const tax = Number(displayedTaxRate(line)) || 0
    const lineTotal = quantity * rate
    return sum + lineTotal + lineTotal * (tax / 100)
  }, 0)

  const submit = async () => {
    if (!clientId) {
      toast.error('Client is required')
      return
    }
    try {
      const created = await createQuotation.mutateAsync({
        clientId,
        quoteDate: new Date().toISOString(),
        validUntil: validUntilFor(validityDays).toISOString(),
        subject: subject.trim() || null,
        currency: currency.trim().toUpperCase() || getActiveCurrency(),
        lines: lines.filter((line) => line.description.trim()).map((line) => ({
          description: line.description.trim(),
          itemType: line.itemType || 'Service',
          quantity: Number(line.quantity) || 1,
          unit: line.unit || 'unit',
          unitRate: Number(line.unitRate) || 0,
          taxRate: Number(displayedTaxRate(line)) || 0,
        })),
      })
      toast.success(`Quote ${created.quoteNumber} created`)
      close()
      navigate(`/finance/quotes/${created.id}`)
    } catch {
      // interceptor surfaces the error toast
    }
  }

  return (
    <FormDrawer
      open={open}
      title="New Quote"
      onClose={close}
      footer={
        <div className="flex items-center justify-between gap-2">
          <div className="text-sm text-slate-600">
            Estimated total: <span className="font-semibold text-slate-900">{formatCurrency(total)}</span>
          </div>
          <div className="flex items-center gap-2">
            <button type="button" onClick={close} className="inline-flex h-11 items-center justify-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">Cancel</button>
            <button type="button" onClick={() => void submit()} disabled={createQuotation.isPending} className="inline-flex h-11 items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:opacity-60">
              {createQuotation.isPending ? 'Creating…' : 'Create Quote'}
            </button>
          </div>
        </div>
      }
    >
      <div className="space-y-4">
        <div>
          <label className={labelClass} htmlFor="quote-client">Client</label>
          <select id="quote-client" value={clientId} onChange={(event) => setClientId(event.target.value)} className={fieldClass}>
            <option value="">Select a client</option>
            {(clients ?? []).map((client) => (<option key={client.id} value={client.id}>{client.clientName}</option>))}
          </select>
        </div>
        <div>
          <label className={labelClass} htmlFor="quote-subject">Subject</label>
          <input id="quote-subject" value={subject} onChange={(event) => setSubject(event.target.value)} className={fieldClass} />
        </div>
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className={labelClass} htmlFor="quote-validity">Validity</label>
            <select id="quote-validity" value={validityDays} onChange={(event) => setValidityDays(Number(event.target.value))} className={fieldClass}>
              {VALIDITY_OPTIONS.map((days) => (<option key={days} value={days}>{days} days</option>))}
            </select>
            <p className="mt-1 text-xs text-slate-500">Valid until {validUntilFor(validityDays).toLocaleDateString()}</p>
          </div>
          <div>
            <label className={labelClass} htmlFor="quote-currency">Currency</label>
            <input id="quote-currency" value={currency} maxLength={3} onChange={(event) => setCurrency(event.target.value.toUpperCase())} className={fieldClass} />
          </div>
        </div>
        <div>
          <label className={labelClass}>Line Items</label>
          <div className="space-y-3">
            {lines.map((line, index) => {
              const qty = Number(line.quantity) || 0
              const rate = Number(line.unitRate) || 0
              const tax = Number(displayedTaxRate(line)) || 0
              const lineTotal = qty * rate * (1 + tax / 100)
              return (
                <div key={index} className="rounded-xl border border-slate-200 p-3">
                  <RateCardAutocomplete
                    value={line.description}
                    onChange={(next) => updateLine(index, { description: next, rateCardId: '' })}
                    onSelectRateCard={(card) => applyRateCard(index, card.id)}
                    aria-label="Description"
                    placeholder="Search rate cards or type a description…"
                  />
                  <div className="mt-2 flex items-end gap-2.5">
                    <label className="flex flex-col gap-1">
                      <span className="text-xs font-medium text-slate-500">Qty</span>
                      <input type="number" min="0" placeholder="0" value={line.quantity} onChange={(event) => updateLine(index, { quantity: event.target.value })} aria-label="Quantity" className="h-10 w-16 rounded-lg border border-slate-300 px-2 text-sm outline-none focus:border-[var(--app-primary)]" />
                    </label>
                    <label className="flex flex-col gap-1">
                      <span className="text-xs font-medium text-slate-500">Rate</span>
                      <input type="number" min="0" placeholder="0.00" value={line.unitRate} onChange={(event) => updateLine(index, { unitRate: event.target.value })} aria-label="Unit rate" className="h-10 w-24 rounded-lg border border-slate-300 px-2 text-sm outline-none focus:border-[var(--app-primary)]" />
                    </label>
                    <label className="flex flex-col gap-1">
                      <span className="text-xs font-medium text-slate-500">Tax %</span>
                      <input type="number" min="0" placeholder="0" value={displayedTaxRate(line)} onChange={(event) => updateLine(index, { taxRate: event.target.value })} aria-label="Tax percent" className="h-10 w-16 rounded-lg border border-slate-300 px-2 text-sm outline-none focus:border-[var(--app-primary)]" />
                    </label>
                    <div className="flex flex-1 flex-col gap-1 text-right">
                      <span className="text-xs font-medium text-slate-500">Total</span>
                      <span className="flex h-10 items-center justify-end px-1 text-sm font-semibold tabular-nums text-slate-900">{formatCurrency(lineTotal)}</span>
                    </div>
                    <button
                      type="button"
                      onClick={() => removeLine(index)}
                      disabled={lines.length === 1}
                      aria-label="Remove line"
                      className="inline-flex h-10 w-10 shrink-0 items-center justify-center rounded-lg border border-slate-200 bg-white text-slate-400 transition hover:bg-rose-50 hover:text-rose-600 disabled:opacity-30"
                    >
                      <Trash2 className="h-4 w-4" />
                    </button>
                  </div>
                </div>
              )
            })}
          </div>
          <button type="button" onClick={() => setLines((current) => [...current, emptyLine()])} className="mt-2 text-xs font-medium text-slate-600 underline decoration-slate-300 underline-offset-4 hover:text-slate-900">
            + Add line
          </button>
          <p className="mt-2 text-xs text-slate-500">
            {tenantTaxRate != null
              ? `Tax % defaults to your default rate card's ${tenantTaxRate}% and can be changed per line.`
              : 'No default rate card with a tax rate is configured, so Tax % starts blank — set it per line, or add one under Finance → Rate Cards.'}
          </p>
        </div>
      </div>
    </FormDrawer>
  )
}
