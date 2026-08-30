import { useEffect, useState } from 'react'
import toast from 'react-hot-toast'
import { Trash2 } from 'lucide-react'
import { FormDrawer } from '../../../shared/components/FormDrawer'
import { formatCurrency } from '../../../shared/utils/format'
import { useClients } from '../../clients/api'
import { useDefaultTaxRate, useUpdateInvoice } from '../api'
import { RateCardAutocomplete } from './RateCardAutocomplete'
import type { InvoiceDetail } from '../types'

const fieldClass = 'h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none transition focus:border-[var(--app-primary)]'
const labelClass = 'mb-1.5 block text-sm font-medium text-slate-700'

interface Line { description: string; quantity: string; unitRate: string; taxRate: string; unit: string; itemType: string }

/** A brand-new line starts at the tenant's configured rate, or blank when it has configured none. */
const emptyLine = (tenantTaxRate: number | null): Line => ({
  description: '', quantity: '1', unitRate: '0',
  taxRate: tenantTaxRate != null ? String(tenantTaxRate) : '',
  unit: 'unit', itemType: 'Service',
})

function toLines(invoice: InvoiceDetail, tenantTaxRate: number | null): Line[] {
  if (invoice.lines.length === 0) {
    return [emptyLine(tenantTaxRate)]
  }
  return invoice.lines.map((line) => ({
    description: line.description,
    quantity: String(line.quantity),
    unitRate: String(line.unitRate),
    taxRate: String(line.taxRate),
    unit: line.unit,
    itemType: line.itemType,
  }))
}

export function EditInvoiceDrawer({ open, invoice, onClose }: { open: boolean; invoice: InvoiceDetail; onClose: () => void }) {
  const { data: clients } = useClients()
  const updateInvoice = useUpdateInvoice(invoice.id)
  const { taxRate: tenantTaxRate } = useDefaultTaxRate()

  const [clientId, setClientId] = useState(invoice.clientId)
  const [invoiceDate, setInvoiceDate] = useState(invoice.invoiceDate.slice(0, 10))
  const [dueDate, setDueDate] = useState(invoice.dueDate.slice(0, 10))
  const [notes, setNotes] = useState(invoice.notes ?? '')
  const [lines, setLines] = useState<Line[]>(() => toLines(invoice, tenantTaxRate))

  useEffect(() => {
    if (open) {
      setClientId(invoice.clientId)
      setInvoiceDate(invoice.invoiceDate.slice(0, 10))
      setDueDate(invoice.dueDate.slice(0, 10))
      setNotes(invoice.notes ?? '')
      setLines(toLines(invoice, tenantTaxRate))
    }
  }, [open, invoice, tenantTaxRate])

  const updateLine = (index: number, field: keyof Line, value: string) => {
    setLines((current) => current.map((line, i) => (i === index ? { ...line, [field]: value } : line)))
  }

  const submit = async () => {
    if (!clientId || !dueDate) {
      toast.error('Client and due date are required')
      return
    }
    try {
      await updateInvoice.mutateAsync({
        clientId,
        invoiceDate: new Date(invoiceDate).toISOString(),
        dueDate: new Date(dueDate).toISOString(),
        notes: notes.trim() || null,
        currency: invoice.currency,
        lines: lines.filter((line) => line.description.trim()).map((line) => ({
          description: line.description.trim(),
          itemType: line.itemType || 'Service',
          quantity: Number(line.quantity) || 1,
          unit: line.unit || 'unit',
          unitRate: Number(line.unitRate) || 0,
          taxRate: Number(line.taxRate) || 0,
        })),
      })
      toast.success('Invoice updated')
      onClose()
    } catch {
      // interceptor surfaces the error toast
    }
  }

  return (
    <FormDrawer
      open={open}
      title={`Edit ${invoice.invoiceNumber}`}
      onClose={onClose}
      footer={
        <div className="flex items-center justify-end gap-2">
          <button type="button" onClick={onClose} className="inline-flex h-11 items-center justify-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">Cancel</button>
          <button type="button" onClick={() => void submit()} disabled={updateInvoice.isPending} className="inline-flex h-11 items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:opacity-60">
            {updateInvoice.isPending ? 'Saving…' : 'Save Changes'}
          </button>
        </div>
      }
    >
      <div className="space-y-4">
        <div className="rounded-xl border border-slate-200 bg-slate-50 px-4 py-3 text-sm text-slate-600">
          Only draft documents can be edited. Saving replaces all line items with the list below.
        </div>
        <div>
          <label className={labelClass} htmlFor="edit-invoice-client">Client</label>
          <select id="edit-invoice-client" value={clientId} onChange={(event) => setClientId(event.target.value)} className={fieldClass}>
            {(clients ?? []).map((client) => (<option key={client.id} value={client.id}>{client.clientName}</option>))}
          </select>
        </div>
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className={labelClass} htmlFor="edit-invoice-date">Invoice Date</label>
            <input id="edit-invoice-date" type="date" value={invoiceDate} onChange={(event) => setInvoiceDate(event.target.value)} className={fieldClass} />
          </div>
          <div>
            <label className={labelClass} htmlFor="edit-invoice-due">Due Date</label>
            <input id="edit-invoice-due" type="date" value={dueDate} onChange={(event) => setDueDate(event.target.value)} className={fieldClass} />
          </div>
        </div>
        <div>
          <label className={labelClass} htmlFor="edit-invoice-notes">Notes</label>
          <textarea id="edit-invoice-notes" value={notes} onChange={(event) => setNotes(event.target.value)} rows={3} className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[var(--app-primary)]" />
        </div>
        <div>
          <label className={labelClass}>Line Items</label>
          <div className="space-y-3">
            {lines.map((line, index) => {
              const qty = Number(line.quantity) || 0
              const rate = Number(line.unitRate) || 0
              const tax = Number(line.taxRate) || 0
              const lineTotal = qty * rate * (1 + tax / 100)
              return (
                <div key={index} className="rounded-xl border border-slate-200 bg-slate-50/60 p-3">
                  <RateCardAutocomplete
                    value={line.description}
                    onChange={(next) => updateLine(index, 'description', next)}
                    onSelectRateCard={(card) => {
                      updateLine(index, 'description', card.description?.trim() || card.name)
                      if (!line.unitRate || Number(line.unitRate) === 0) updateLine(index, 'unitRate', String(card.unitRate))
                      if (card.taxRate != null) updateLine(index, 'taxRate', String(card.taxRate))
                    }}
                    aria-label="Description"
                    placeholder="Search or type a description…"
                  />
                  <div className="mt-2 flex items-end gap-2.5">
                    <label className="flex flex-col gap-1">
                      <span className="text-xs font-medium text-slate-500">Qty</span>
                      <input type="number" min="0" placeholder="0" value={line.quantity} onChange={(event) => updateLine(index, 'quantity', event.target.value)} aria-label="Quantity" className="h-10 w-16 rounded-lg border border-slate-300 px-2 text-sm outline-none focus:border-[var(--app-primary)]" />
                    </label>
                    <label className="flex flex-col gap-1">
                      <span className="text-xs font-medium text-slate-500">Rate</span>
                      <input type="number" min="0" placeholder="0.00" value={line.unitRate} onChange={(event) => updateLine(index, 'unitRate', event.target.value)} aria-label="Unit rate" className="h-10 w-24 rounded-lg border border-slate-300 px-2 text-sm outline-none focus:border-[var(--app-primary)]" />
                    </label>
                    <label className="flex flex-col gap-1">
                      <span className="text-xs font-medium text-slate-500">Tax %</span>
                      <input type="number" min="0" placeholder="0" value={line.taxRate} onChange={(event) => updateLine(index, 'taxRate', event.target.value)} aria-label="Tax percent" className="h-10 w-16 rounded-lg border border-slate-300 px-2 text-sm outline-none focus:border-[var(--app-primary)]" />
                    </label>
                    <div className="flex flex-1 flex-col gap-1 text-right">
                      <span className="text-xs font-medium text-slate-500">Total</span>
                      <span className="flex h-10 items-center justify-end px-1 text-sm font-semibold tabular-nums text-slate-900">{formatCurrency(lineTotal)}</span>
                    </div>
                    <button type="button" disabled={lines.length === 1} onClick={() => setLines((current) => current.filter((_, i) => i !== index))} className="inline-flex h-10 w-10 shrink-0 items-center justify-center rounded-lg border border-slate-200 bg-white text-slate-400 hover:bg-slate-50 disabled:opacity-30" aria-label="Remove line">
                      <Trash2 className="h-4 w-4" />
                    </button>
                  </div>
                </div>
              )
            })}
          </div>
          <button
            type="button"
            onClick={() => setLines((current) => [...current, emptyLine(tenantTaxRate)])}
            className="mt-3 text-xs font-medium text-slate-600 underline decoration-slate-300 underline-offset-4 hover:text-slate-900"
          >
            + Add line
          </button>
        </div>
      </div>
    </FormDrawer>
  )
}
