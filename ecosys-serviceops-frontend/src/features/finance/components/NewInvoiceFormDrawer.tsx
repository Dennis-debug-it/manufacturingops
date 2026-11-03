import { useState } from 'react'
import toast from 'react-hot-toast'
import { useNavigate } from 'react-router-dom'
import { Trash2 } from 'lucide-react'
import { FormDrawer } from '../../../shared/components/FormDrawer'
import { ProjectPicker } from '../../../shared/components/ProjectPicker'
import { formatCurrency } from '../../../shared/utils/format'
import { getActiveCurrency } from '../../../shared/utils/currency'
import { useClients } from '../../clients/api'
import { useCreateInvoice, useDefaultTaxRate } from '../api'
import { RateCardAutocomplete } from './RateCardAutocomplete'

const fieldClass = 'h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none transition focus:border-[var(--app-primary)]'
const labelClass = 'mb-1.5 block text-sm font-medium text-slate-700'

/** `taxRate: null` means "the user hasn't set one" — the tenant's configured rate is shown instead. */
interface Line { description: string; quantity: string; unitRate: string; taxRate: string | null }

const emptyLine = (): Line => ({ description: '', quantity: '1', unitRate: '0', taxRate: null })

export function NewInvoiceFormDrawer({ open, onClose }: { open: boolean; onClose: () => void }) {
  const navigate = useNavigate()
  const { data: clients } = useClients()
  const createInvoice = useCreateInvoice()
  // The rate the tenant configured on its default rate card. Null when it has configured none —
  // in which case the control below starts blank rather than assuming a jurisdiction's rate.
  const { taxRate: tenantTaxRate } = useDefaultTaxRate()

  const [clientId, setClientId] = useState('')
  const [projectId, setProjectId] = useState('')
  const [invoiceDate, setInvoiceDate] = useState(new Date().toISOString().slice(0, 10))
  const [dueDate, setDueDate] = useState('')
  const [currency, setCurrency] = useState(getActiveCurrency)
  const [lines, setLines] = useState<Line[]>([emptyLine()])

  /** What the Tax % input shows: the user's own entry, else the tenant's configured rate. */
  const displayedTaxRate = (line: Line) => line.taxRate ?? (tenantTaxRate != null ? String(tenantTaxRate) : '')

  const close = () => {
    setClientId('')
    setProjectId('')
    setCurrency(getActiveCurrency())
    setLines([emptyLine()])
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
        currency: currency.trim().toUpperCase() || getActiveCurrency(),
        projectId: projectId || null,
        lines: lines.filter((line) => line.description.trim()).map((line) => ({
          description: line.description.trim(),
          itemType: 'Service',
          quantity: Number(line.quantity) || 1,
          unit: 'unit',
          unitRate: Number(line.unitRate) || 0,
          taxRate: Number(displayedTaxRate(line)) || 0,
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
          <button type="button" onClick={submit} disabled={createInvoice.isPending} className="inline-flex h-11 items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:opacity-60">
            {createInvoice.isPending ? 'Creating…' : 'Create Invoice'}
          </button>
        </div>
      }
    >
      <div className="space-y-4">
        <div>
          <label className={labelClass} htmlFor="invoice-client">Client</label>
          <select
            id="invoice-client"
            value={clientId}
            onChange={(event) => {
              setClientId(event.target.value)
              // Projects belong to one client, so a new client clears the project.
              setProjectId('')
            }}
            className={fieldClass}
          >
            <option value="">Select a client</option>
            {(clients ?? []).map((client) => (<option key={client.id} value={client.id}>{client.clientName}</option>))}
          </select>
        </div>
        <ProjectPicker id="invoice-project" value={projectId} onChange={setProjectId} clientId={clientId || null} className={fieldClass} labelClassName={labelClass} />
        <div className="grid grid-cols-3 gap-3">
          <div>
            <label className={labelClass} htmlFor="invoice-date">Invoice Date</label>
            <input id="invoice-date" type="date" value={invoiceDate} onChange={(event) => setInvoiceDate(event.target.value)} className={fieldClass} />
          </div>
          <div>
            <label className={labelClass} htmlFor="invoice-due">Due Date</label>
            <input id="invoice-due" type="date" value={dueDate} onChange={(event) => setDueDate(event.target.value)} className={fieldClass} />
          </div>
          <div>
            <label className={labelClass} htmlFor="invoice-currency">Currency</label>
            <input id="invoice-currency" value={currency} maxLength={3} onChange={(event) => setCurrency(event.target.value.toUpperCase())} className={fieldClass} />
          </div>
        </div>
        <div>
          <label className={labelClass}>Line Items</label>
          <div className="space-y-3">
            {lines.map((line, index) => {
              const net = (Number(line.quantity) || 0) * (Number(line.unitRate) || 0)
              const lineTotal = net * (1 + (Number(displayedTaxRate(line)) || 0) / 100)
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
                  <div className="mt-2 flex items-end gap-3">
                    <label className="flex flex-col gap-1">
                      <span className="text-xs font-medium text-slate-500">Qty</span>
                      <input type="number" min="0" placeholder="0" value={line.quantity} onChange={(event) => updateLine(index, 'quantity', event.target.value)} aria-label="Quantity" className="h-10 w-20 rounded-lg border border-slate-300 px-2 text-sm outline-none focus:border-[var(--app-primary)]" />
                    </label>
                    <label className="flex flex-col gap-1">
                      <span className="text-xs font-medium text-slate-500">Rate</span>
                      <input type="number" min="0" placeholder="0.00" value={line.unitRate} onChange={(event) => updateLine(index, 'unitRate', event.target.value)} aria-label="Unit rate" className="h-10 w-24 rounded-lg border border-slate-300 px-2 text-sm outline-none focus:border-[var(--app-primary)]" />
                    </label>
                    <label className="flex flex-col gap-1">
                      <span className="text-xs font-medium text-slate-500">Tax %</span>
                      <input type="number" min="0" placeholder="0" value={displayedTaxRate(line)} onChange={(event) => updateLine(index, 'taxRate', event.target.value)} aria-label="Tax percent" className="h-10 w-16 rounded-lg border border-slate-300 px-2 text-sm outline-none focus:border-[var(--app-primary)]" />
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
          <button type="button" onClick={() => setLines((current) => [...current, emptyLine()])} className="mt-3 text-xs font-medium text-slate-600 underline decoration-slate-300 underline-offset-4 hover:text-slate-900">
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
