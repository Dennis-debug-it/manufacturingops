import { useState } from 'react'
import toast from 'react-hot-toast'
import { useNavigate } from 'react-router-dom'
import { Trash2 } from 'lucide-react'
import { FormDrawer } from '../../../shared/components/FormDrawer'
import { formatCurrency } from '../../../shared/utils/format'
import { useClients } from '../../clients/api'
import { useCreateInvoice } from '../api'
import { RateCardAutocomplete } from './RateCardAutocomplete'

const fieldClass = 'h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none transition focus:border-slate-900'
const labelClass = 'mb-1.5 block text-sm font-medium text-slate-700'

interface Line { description: string; quantity: string; unitRate: string }

export function NewInvoiceFormDrawer({ open, onClose }: { open: boolean; onClose: () => void }) {
  const navigate = useNavigate()
  const { data: clients } = useClients()
  const createInvoice = useCreateInvoice()

  const [clientId, setClientId] = useState('')
  const [invoiceDate, setInvoiceDate] = useState(new Date().toISOString().slice(0, 10))
  const [dueDate, setDueDate] = useState('')
  const [lines, setLines] = useState<Line[]>([{ description: '', quantity: '1', unitRate: '0' }])

  const close = () => {
    setClientId('')
    setLines([{ description: '', quantity: '1', unitRate: '0' }])
    onClose()
  }

  const updateLine = (index: number, field: keyof Line, value: string) => {
    setLines((current) => current.map((line, i) => (i === index ? { ...line, [field]: value } : line)))
  }

  const submit = async () => {
    if (!clientId || !dueDate) {
      toast.error('Client and due date are required')
      return
    }
    try {
      const created = await createInvoice.mutateAsync({
        clientId,
        invoiceDate: new Date(invoiceDate).toISOString(),
        dueDate: new Date(dueDate).toISOString(),
        lines: lines.filter((line) => line.description.trim()).map((line) => ({
          description: line.description.trim(),
          itemType: 'Service',
          quantity: Number(line.quantity) || 1,
          unit: 'unit',
          unitRate: Number(line.unitRate) || 0,
          taxRate: 16,
        })),
      })
      toast.success(`Invoice ${created.invoiceNumber} created`)
      close()
      navigate(`/finance/invoices/${created.id}`)
    } catch {
      // interceptor surfaces the error toast
    }
  }

  return (
    <FormDrawer
      open={open}
      title="New Invoice"
      onClose={close}
      footer={
        <div className="flex items-center justify-end gap-2">
          <button type="button" onClick={close} className="inline-flex h-11 items-center justify-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">Cancel</button>
          <button type="button" onClick={submit} disabled={createInvoice.isPending} className="inline-flex h-11 items-center justify-center rounded-lg bg-slate-900 px-4 text-sm font-medium text-white transition hover:bg-slate-800 disabled:opacity-60">
            {createInvoice.isPending ? 'Creating…' : 'Create Invoice'}
          </button>
        </div>
      }
    >
      <div className="space-y-4">
        <div>
          <label className={labelClass} htmlFor="invoice-client">Client</label>
          <select id="invoice-client" value={clientId} onChange={(event) => setClientId(event.target.value)} className={fieldClass}>
            <option value="">Select a client</option>
            {(clients ?? []).map((client) => (<option key={client.id} value={client.id}>{client.clientName}</option>))}
          </select>
        </div>
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className={labelClass} htmlFor="invoice-date">Invoice Date</label>
            <input id="invoice-date" type="date" value={invoiceDate} onChange={(event) => setInvoiceDate(event.target.value)} className={fieldClass} />
          </div>
          <div>
            <label className={labelClass} htmlFor="invoice-due">Due Date</label>
            <input id="invoice-due" type="date" value={dueDate} onChange={(event) => setDueDate(event.target.value)} className={fieldClass} />
          </div>
        </div>
        <div>
          <label className={labelClass}>Line Items</label>
          <div className="space-y-3">
            {lines.map((line, index) => {
              const subtotal = (Number(line.quantity) || 0) * (Number(line.unitRate) || 0)
              return (
                <div key={index} className="rounded-xl border border-slate-200 bg-slate-50/60 p-3">
                  <RateCardAutocomplete
                    value={line.description}
                    onChange={(next) => updateLine(index, 'description', next)}
                    onSelectRateCard={(card) => {
                      updateLine(index, 'description', card.description?.trim() || card.name)
                      if (!line.unitRate || Number(line.unitRate) === 0) updateLine(index, 'unitRate', String(card.unitRate))
                    }}
                    aria-label="Description"
                    placeholder="Search or type a description…"
                  />
                  <div className="mt-2 flex items-end gap-3">
                    <label className="flex flex-col gap-1">
                      <span className="text-xs font-medium text-slate-500">Qty</span>
                      <input type="number" min="0" placeholder="0" value={line.quantity} onChange={(event) => updateLine(index, 'quantity', event.target.value)} aria-label="Quantity" className="h-10 w-20 rounded-lg border border-slate-300 px-2 text-sm outline-none focus:border-slate-900" />
                    </label>
                    <label className="flex flex-col gap-1">
                      <span className="text-xs font-medium text-slate-500">Rate</span>
                      <input type="number" min="0" placeholder="0.00" value={line.unitRate} onChange={(event) => updateLine(index, 'unitRate', event.target.value)} aria-label="Unit rate" className="h-10 w-28 rounded-lg border border-slate-300 px-2 text-sm outline-none focus:border-slate-900" />
                    </label>
                    <div className="flex flex-1 flex-col gap-1 text-right">
                      <span className="text-xs font-medium text-slate-500">Subtotal</span>
                      <span className="flex h-10 items-center justify-end px-1 text-sm font-semibold tabular-nums text-slate-900">{formatCurrency(subtotal)}</span>
                    </div>
                    <button type="button" disabled={lines.length === 1} onClick={() => setLines((current) => current.filter((_, i) => i !== index))} className="inline-flex h-10 w-10 shrink-0 items-center justify-center rounded-lg border border-slate-200 bg-white text-slate-400 hover:bg-slate-50 disabled:opacity-30" aria-label="Remove line">
                      <Trash2 className="h-4 w-4" />
                    </button>
                  </div>
                </div>
              )
            })}
          </div>
          <button type="button" onClick={() => setLines((current) => [...current, { description: '', quantity: '1', unitRate: '0' }])} className="mt-3 text-xs font-medium text-slate-600 underline decoration-slate-300 underline-offset-4 hover:text-slate-900">
            + Add line
          </button>
        </div>
      </div>
    </FormDrawer>
  )
}
