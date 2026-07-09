import { useEffect, useState } from 'react'
import toast from 'react-hot-toast'
import { FormDrawer } from '../../../shared/components/FormDrawer'
import { useClients } from '../../clients/api'
import { useUpdateInvoice } from '../api'
import type { InvoiceDetail } from '../types'

const fieldClass = 'h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none transition focus:border-slate-900'
const labelClass = 'mb-1.5 block text-sm font-medium text-slate-700'

interface Line { description: string; quantity: string; unitRate: string; taxRate: string; unit: string; itemType: string }

function toLines(invoice: InvoiceDetail): Line[] {
  if (invoice.lines.length === 0) {
    return [{ description: '', quantity: '1', unitRate: '0', taxRate: '16', unit: 'unit', itemType: 'Service' }]
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

  const [clientId, setClientId] = useState(invoice.clientId)
  const [invoiceDate, setInvoiceDate] = useState(invoice.invoiceDate.slice(0, 10))
  const [dueDate, setDueDate] = useState(invoice.dueDate.slice(0, 10))
  const [notes, setNotes] = useState(invoice.notes ?? '')
  const [lines, setLines] = useState<Line[]>(() => toLines(invoice))

  useEffect(() => {
    if (open) {
      setClientId(invoice.clientId)
      setInvoiceDate(invoice.invoiceDate.slice(0, 10))
      setDueDate(invoice.dueDate.slice(0, 10))
      setNotes(invoice.notes ?? '')
      setLines(toLines(invoice))
    }
  }, [open, invoice])

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
          <button type="button" onClick={() => void submit()} disabled={updateInvoice.isPending} className="inline-flex h-11 items-center justify-center rounded-lg bg-slate-900 px-4 text-sm font-medium text-white transition hover:bg-slate-800 disabled:opacity-60">
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
          <textarea id="edit-invoice-notes" value={notes} onChange={(event) => setNotes(event.target.value)} rows={3} className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-slate-900" />
        </div>
        <div>
          <label className={labelClass}>Line Items</label>
          <div className="space-y-2">
            {lines.map((line, index) => (
              <div key={index} className="grid grid-cols-[1fr_4.5rem_5.5rem_4rem] gap-2">
                <input placeholder="Description" value={line.description} onChange={(event) => updateLine(index, 'description', event.target.value)} className={fieldClass} />
                <input type="number" placeholder="Qty" value={line.quantity} onChange={(event) => updateLine(index, 'quantity', event.target.value)} className={fieldClass} />
                <input type="number" placeholder="Rate" value={line.unitRate} onChange={(event) => updateLine(index, 'unitRate', event.target.value)} className={fieldClass} />
                <input type="number" placeholder="Tax %" value={line.taxRate} onChange={(event) => updateLine(index, 'taxRate', event.target.value)} className={fieldClass} />
              </div>
            ))}
          </div>
          <button
            type="button"
            onClick={() => setLines((current) => [...current, { description: '', quantity: '1', unitRate: '0', taxRate: '16', unit: 'unit', itemType: 'Service' }])}
            className="mt-2 text-xs font-medium text-slate-600 underline decoration-slate-300 underline-offset-4 hover:text-slate-900"
          >
            + Add line
          </button>
        </div>
      </div>
    </FormDrawer>
  )
}
