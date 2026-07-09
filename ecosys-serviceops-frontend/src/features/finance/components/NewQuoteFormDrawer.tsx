import { useState } from 'react'
import toast from 'react-hot-toast'
import { useNavigate } from 'react-router-dom'
import { Trash2 } from 'lucide-react'
import { FormDrawer } from '../../../shared/components/FormDrawer'
import { formatCurrency } from '../../../shared/utils/format'
import { useClients } from '../../clients/api'
import { useCreateQuotation, useFinanceRateCards } from '../api'

const fieldClass = 'h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none transition focus:border-slate-900'
const labelClass = 'mb-1.5 block text-sm font-medium text-slate-700'

interface Line {
  description: string
  quantity: string
  unitRate: string
  unit: string
  taxRate: string
  itemType: string
  rateCardId: string
}

const emptyLine = (): Line => ({ description: '', quantity: '1', unitRate: '0', unit: 'unit', taxRate: '16', itemType: 'Service', rateCardId: '' })

export function NewQuoteFormDrawer({ open, onClose }: { open: boolean; onClose: () => void }) {
  const navigate = useNavigate()
  const { data: clients } = useClients()
  const { data: rateCards } = useFinanceRateCards()
  const createQuotation = useCreateQuotation()

  const [clientId, setClientId] = useState('')
  const [subject, setSubject] = useState('')
  const [validUntil, setValidUntil] = useState('')
  const [lines, setLines] = useState<Line[]>([emptyLine()])

  const activeRateCards = (rateCards ?? []).filter((card) => card.isActive)

  const close = () => {
    setClientId('')
    setSubject('')
    setValidUntil('')
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
      taxRate: card.taxRate != null ? String(card.taxRate) : '16',
      itemType: card.category || 'Service',
    })
  }

  const removeLine = (index: number) => {
    setLines((current) => (current.length > 1 ? current.filter((_, i) => i !== index) : current))
  }

  const total = lines.reduce((sum, line) => {
    const quantity = Number(line.quantity) || 0
    const rate = Number(line.unitRate) || 0
    const tax = Number(line.taxRate) || 0
    const lineTotal = quantity * rate
    return sum + lineTotal + lineTotal * (tax / 100)
  }, 0)

  const submit = async () => {
    if (!clientId || !validUntil) {
      toast.error('Client and valid-until date are required')
      return
    }
    try {
      const created = await createQuotation.mutateAsync({
        clientId,
        quoteDate: new Date().toISOString(),
        validUntil: new Date(validUntil).toISOString(),
        subject: subject.trim() || null,
        lines: lines.filter((line) => line.description.trim()).map((line) => ({
          description: line.description.trim(),
          itemType: line.itemType || 'Service',
          quantity: Number(line.quantity) || 1,
          unit: line.unit || 'unit',
          unitRate: Number(line.unitRate) || 0,
          taxRate: Number(line.taxRate) || 0,
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
            <button type="button" onClick={() => void submit()} disabled={createQuotation.isPending} className="inline-flex h-11 items-center justify-center rounded-lg bg-slate-900 px-4 text-sm font-medium text-white transition hover:bg-slate-800 disabled:opacity-60">
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
        <div>
          <label className={labelClass} htmlFor="quote-valid-until">Valid Until</label>
          <input id="quote-valid-until" type="date" value={validUntil} onChange={(event) => setValidUntil(event.target.value)} className={fieldClass} />
        </div>
        <div>
          <label className={labelClass}>Line Items</label>
          <div className="space-y-3">
            {lines.map((line, index) => (
              <div key={index} className="space-y-2 rounded-xl border border-slate-200 p-3">
                <div className="flex items-center gap-2">
                  <select
                    value={line.rateCardId}
                    onChange={(event) => applyRateCard(index, event.target.value)}
                    className={fieldClass}
                    aria-label="Fill from rate card"
                  >
                    <option value="">Fill from rate card…</option>
                    {activeRateCards.map((card) => (
                      <option key={card.id} value={card.id}>
                        {card.name} — {formatCurrency(card.unitRate)} / {card.unit}
                      </option>
                    ))}
                  </select>
                  <button
                    type="button"
                    onClick={() => removeLine(index)}
                    disabled={lines.length === 1}
                    aria-label="Remove line"
                    className="inline-flex h-11 w-11 shrink-0 items-center justify-center rounded-lg border border-slate-200 bg-white text-slate-500 transition hover:bg-rose-50 hover:text-rose-600 disabled:opacity-40"
                  >
                    <Trash2 className="h-4 w-4" />
                  </button>
                </div>
                <input placeholder="Description" value={line.description} onChange={(event) => updateLine(index, { description: event.target.value, rateCardId: '' })} className={fieldClass} />
                <div className="grid grid-cols-3 gap-2">
                  <input type="number" placeholder="Qty" value={line.quantity} onChange={(event) => updateLine(index, { quantity: event.target.value })} className={fieldClass} />
                  <input type="number" placeholder="Rate" value={line.unitRate} onChange={(event) => updateLine(index, { unitRate: event.target.value })} className={fieldClass} />
                  <input type="number" placeholder="Tax %" value={line.taxRate} onChange={(event) => updateLine(index, { taxRate: event.target.value })} className={fieldClass} />
                </div>
              </div>
            ))}
          </div>
          <button type="button" onClick={() => setLines((current) => [...current, emptyLine()])} className="mt-2 text-xs font-medium text-slate-600 underline decoration-slate-300 underline-offset-4 hover:text-slate-900">
            + Add line
          </button>
        </div>
      </div>
    </FormDrawer>
  )
}
