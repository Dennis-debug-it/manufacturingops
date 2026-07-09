import { useState } from 'react'
import toast from 'react-hot-toast'
import { useNavigate } from 'react-router-dom'
import { FormDrawer } from '../../../shared/components/FormDrawer'
import { useClients } from '../../clients/api'
import { useCreateInvoice } from '../api'

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
          <div className="space-y-2">
            {lines.map((line, index) => (
              <div key={index} className="grid grid-cols-[1fr_5rem_6rem] gap-2">
                <input placeholder="Description" value={line.description} onChange={(event) => updateLine(index, 'description', event.target.value)} className={fieldClass} />
                <input type="number" placeholder="Qty" value={line.quantity} onChange={(event) => updateLine(index, 'quantity', event.target.value)} className={fieldClass} />
                <input type="number" placeholder="Rate" value={line.unitRate} onChange={(event) => updateLine(index, 'unitRate', event.target.value)} className={fieldClass} />
              </div>
            ))}
          </div>
          <button type="button" onClick={() => setLines((current) => [...current, { description: '', quantity: '1', unitRate: '0' }])} className="mt-2 text-xs font-medium text-slate-600 underline decoration-slate-300 underline-offset-4 hover:text-slate-900">
            + Add line
          </button>
        </div>
      </div>
    </FormDrawer>
  )
}
