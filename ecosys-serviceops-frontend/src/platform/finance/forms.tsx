import { useMemo, useState, type ReactNode } from 'react'
import { Plus, Trash2 } from 'lucide-react'
import toast from 'react-hot-toast'
import { useQuery } from '@tanstack/react-query'
import { api } from '../../shared/api/apiClient'
import { endpoints } from '../../shared/api/endpoints'
import { formatCurrency } from '../../shared/utils/format'
import { cn } from '../../shared/utils/cn'
import { inputClass, textareaClass } from '../../features/factory/components/ui'
import { useTenants } from '../api'
import type { PlatformExpense, PlatformInvoice, PlatformQuotation } from '../types'

export type CreateKind = 'quotation' | 'invoice' | 'payment' | 'expense'

export const KIND_LABEL: Record<CreateKind, string> = {
  quotation: 'New quotation',
  invoice: 'New invoice',
  payment: 'Record payment',
  expense: 'Record expense',
}

export const PAYMENT_METHODS = ['M-Pesa', 'Bank Transfer', 'Cheque', 'Card', 'Cash', 'Other'] as const
export const EXPENSE_CATEGORIES = ['Hosting & infrastructure', 'Software & licences', 'Salaries', 'Marketing', 'Office & utilities', 'Travel', 'Professional fees', 'Other'] as const
const CURRENCIES = ['KES', 'USD', 'UGX', 'TZS', 'RWF', 'EUR', 'GBP']

export interface PlatformTax { id: string; name: string; rate: number; mode: string; isDefault: boolean }

export const useFinanceTaxes = () =>
  useQuery({ queryKey: ['platform', 'finance', 'taxes'], queryFn: () => api.get<PlatformTax[]>(endpoints.platformFinance.taxes), staleTime: 60_000 })

const today = () => new Date().toISOString().slice(0, 10)
const inDays = (days: number) => new Date(Date.now() + days * 86_400_000).toISOString().slice(0, 10)
const dateOnly = (value: string | null | undefined) => (value ? value.slice(0, 10) : '')

interface Line { description: string; quantity: number; unitPrice: number; taxable: boolean }

/** Quotations and invoices: who for, the lines, discount and tax, with the totals worked out as you type. */
export function DocumentForm({ kind, formId, existing, onSaved }: {
  kind: 'quotation' | 'invoice'
  formId: string
  existing?: PlatformQuotation | PlatformInvoice | null
  onSaved: () => void
}) {
  const tenants = useTenants()
  const taxes = useFinanceTaxes()
  const defaultTax = taxes.data?.find((x) => x.isDefault)?.rate ?? 16
  const [tenantId, setTenantId] = useState(existing?.tenantId ?? '')
  const [customerName, setCustomerName] = useState(existing?.customerName ?? '')
  const [customerEmail, setCustomerEmail] = useState(existing?.customerEmail ?? '')
  const [currency, setCurrency] = useState(existing?.currency ?? 'KES')
  const [issueDate, setIssueDate] = useState(existing && 'issueDate' in existing ? dateOnly(existing.issueDate) : today())
  const [dueDate, setDueDate] = useState(
    existing ? dateOnly('dueDate' in existing ? existing.dueDate : existing.validUntil) : inDays(kind === 'invoice' ? 14 : 30),
  )
  const [discountRate, setDiscountRate] = useState(existing?.discountRate ?? 0)
  const [taxRate, setTaxRate] = useState<number | null>(existing ? existing.taxRate : null)
  const [notes, setNotes] = useState(existing?.notes ?? '')
  const [lines, setLines] = useState<Line[]>(
    existing?.lines.length ? existing.lines.map((x) => ({ description: x.description, quantity: x.quantity, unitPrice: x.unitPrice, taxable: x.taxable })) : [{ description: '', quantity: 1, unitPrice: 0, taxable: true }],
  )
  const [saving, setSaving] = useState(false)
  const rate = taxRate ?? defaultTax

  const totals = useMemo(() => {
    const subtotal = lines.reduce((sum, x) => sum + x.quantity * x.unitPrice, 0)
    const discount = Math.round(subtotal * discountRate) / 100
    const taxable = lines.filter((x) => x.taxable).reduce((sum, x) => sum + x.quantity * x.unitPrice, 0) * (1 - Math.min(discountRate, 100) / 100)
    const tax = Math.round(taxable * rate) / 100
    return { subtotal, discount, tax, total: subtotal - discount + tax }
  }, [lines, discountRate, rate])

  const pickTenant = (id: string) => {
    setTenantId(id)
    const tenant = tenants.data?.find((x) => x.tenantId === id)
    if (tenant) {
      setCustomerName(tenant.name)
      setCustomerEmail(tenant.primaryAdminEmail ?? tenant.contactEmail ?? '')
    }
  }

  const setLine = (index: number, patch: Partial<Line>) => setLines((current) => current.map((x, i) => (i === index ? { ...x, ...patch } : x)))

  const save = async () => {
    if (!customerName.trim()) return void toast.error('Who is it for? Pick a workspace or type the customer name.')
    const filled = lines.filter((x) => x.description.trim())
    if (filled.length === 0) return void toast.error('Add at least one line with a description.')
    if (filled.some((x) => x.quantity <= 0)) return void toast.error('Each line needs a quantity above zero.')
    const body = {
      tenantId: tenantId || null,
      customerName: customerName.trim(),
      customerEmail: customerEmail.trim() || null,
      currency,
      taxRate: rate,
      discountRate,
      status: existing?.status ?? 'Draft',
      notes: notes.trim() || null,
      lines: filled.map((x) => ({ ...x, description: x.description.trim() })),
    }
    setSaving(true)
    try {
      if (kind === 'quotation') {
        const payload = { ...body, validUntil: dueDate || null }
        if (existing) await api.put(endpoints.platformFinance.quotation(existing.id), payload)
        else await api.post(endpoints.platformFinance.quotations, payload)
      } else {
        const payload = { ...body, quotationId: existing && 'quotationId' in existing ? existing.quotationId : null, issueDate: new Date(issueDate).toISOString(), dueDate: dueDate ? new Date(dueDate).toISOString() : null }
        if (existing) await api.put(endpoints.platformFinance.invoice(existing.id), payload)
        else await api.post(endpoints.platformFinance.invoices, payload)
      }
      toast.success(existing ? 'Saved' : kind === 'quotation' ? 'Quotation saved as a draft' : 'Invoice saved as a draft')
      onSaved()
    } catch {
      // the interceptor shows the error
    } finally {
      setSaving(false)
    }
  }

  return (
    <form id={formId} aria-busy={saving} className="space-y-5" onSubmit={(e) => { e.preventDefault(); void save() }}>
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
        <Labelled label="Workspace" hint="optional">
          <select className={inputClass} value={tenantId} onChange={(e) => pickTenant(e.target.value)}>
            <option value="">Not a workspace yet (a prospect)</option>
            {(tenants.data ?? []).map((x) => <option key={x.tenantId} value={x.tenantId}>{x.name}</option>)}
          </select>
        </Labelled>
        <Labelled label="Customer name">
          <input className={inputClass} value={customerName} onChange={(e) => setCustomerName(e.target.value)} required />
        </Labelled>
        <Labelled label="Customer email" hint="where it is sent">
          <input type="email" className={inputClass} value={customerEmail} onChange={(e) => setCustomerEmail(e.target.value)} />
        </Labelled>
        <Labelled label="Currency">
          <select className={inputClass} value={currency} onChange={(e) => setCurrency(e.target.value)}>
            {CURRENCIES.map((x) => <option key={x}>{x}</option>)}
          </select>
        </Labelled>
        {kind === 'invoice' ? (
          <Labelled label="Invoice date">
            <input type="date" className={inputClass} value={issueDate} onChange={(e) => setIssueDate(e.target.value)} required />
          </Labelled>
        ) : null}
        <Labelled label={kind === 'invoice' ? 'Due date' : 'Valid until'}>
          <input type="date" className={inputClass} value={dueDate} onChange={(e) => setDueDate(e.target.value)} />
        </Labelled>
      </div>

      <div>
        <p className="mb-2 text-xs font-semibold text-slate-600">Lines</p>
        <div className="space-y-2">
          <div className="hidden grid-cols-[minmax(0,1fr)_70px_120px_56px_32px] gap-2 px-1 text-[11px] font-semibold uppercase tracking-wide text-slate-500 sm:grid">
            <span>Description</span><span className="text-right">Qty</span><span className="text-right">Unit price</span><span className="text-center">VAT</span><span />
          </div>
          {lines.map((line, index) => (
            <div key={index} className="grid grid-cols-[minmax(0,1fr)_70px_120px_56px_32px] items-center gap-2">
              <input className={inputClass} aria-label={`Line ${index + 1} description`} placeholder="e.g. Standard edition, 1 factory, 12 months" value={line.description} onChange={(e) => setLine(index, { description: e.target.value })} />
              <input type="number" min={0} step="any" className={cn(inputClass, 'text-right')} aria-label={`Line ${index + 1} quantity`} value={line.quantity} onChange={(e) => setLine(index, { quantity: Number(e.target.value) || 0 })} />
              <input type="number" min={0} step="any" className={cn(inputClass, 'text-right')} aria-label={`Line ${index + 1} unit price`} value={line.unitPrice} onChange={(e) => setLine(index, { unitPrice: Number(e.target.value) || 0 })} />
              <label className="flex justify-center"><input type="checkbox" aria-label={`Line ${index + 1} taxable`} checked={line.taxable} onChange={(e) => setLine(index, { taxable: e.target.checked })} /></label>
              <button type="button" aria-label={`Remove line ${index + 1}`} disabled={lines.length === 1} onClick={() => setLines((current) => current.filter((_, i) => i !== index))}
                className="flex h-8 w-8 items-center justify-center rounded-lg text-slate-400 hover:bg-rose-50 hover:text-rose-600 disabled:opacity-30"><Trash2 className="h-4 w-4" /></button>
            </div>
          ))}
        </div>
        <button type="button" className="mt-2 inline-flex items-center gap-1 text-sm font-medium text-[var(--app-primary)] hover:underline" onClick={() => setLines((current) => [...current, { description: '', quantity: 1, unitPrice: 0, taxable: true }])}>
          <Plus className="h-4 w-4" />Add line
        </button>
      </div>

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-[minmax(0,1fr)_260px]">
        <div className="grid grid-cols-2 gap-3 self-start">
          <Labelled label="Discount %">
            <input type="number" min={0} max={100} step="any" className={inputClass} value={discountRate} onChange={(e) => setDiscountRate(Number(e.target.value) || 0)} />
          </Labelled>
          <Labelled label="VAT %">
            <select className={inputClass} value={String(rate)} onChange={(e) => setTaxRate(Number(e.target.value))}>
              {[...new Set([rate, ...(taxes.data ?? []).map((x) => x.rate), 16, 0])].map((x) => (
                <option key={x} value={x}>{taxes.data?.find((t) => t.rate === x)?.name ?? (x === 0 ? 'No VAT' : 'VAT')} · {x}%</option>
              ))}
            </select>
          </Labelled>
          <div className="col-span-2">
            <Labelled label="Notes" hint="printed on the document">
              <textarea className={textareaClass} rows={2} value={notes} onChange={(e) => setNotes(e.target.value)} />
            </Labelled>
          </div>
        </div>
        <dl className="space-y-1 rounded-xl bg-slate-50 p-3 text-sm">
          <Total label="Subtotal" value={formatCurrency(totals.subtotal, currency)} />
          {totals.discount > 0 ? <Total label={`Discount ${discountRate}%`} value={`-${formatCurrency(totals.discount, currency)}`} /> : null}
          <Total label={`VAT ${rate}%`} value={formatCurrency(totals.tax, currency)} />
          <div className="border-t border-slate-200 pt-1"><Total label="Total" value={formatCurrency(totals.total, currency)} strong /></div>
        </dl>
      </div>
    </form>
  )
}

/** A payment received, against an invoice (which it pays down) or on its own. */
export function PaymentForm({ formId, invoices, presetInvoiceId, onSaved }: { formId: string; invoices: PlatformInvoice[]; presetInvoiceId?: string | null; onSaved: () => void }) {
  const open = invoices.filter((x) => x.status !== 'Paid' && x.status !== 'Void' && x.balance > 0)
  const preset = open.find((x) => x.id === presetInvoiceId)
  const [invoiceId, setInvoiceId] = useState(preset?.id ?? '')
  const [amount, setAmount] = useState(preset?.balance ?? 0)
  const [currency, setCurrency] = useState(preset?.currency ?? 'KES')
  const [method, setMethod] = useState<string>('M-Pesa')
  const [reference, setReference] = useState('')
  const [paidAt, setPaidAt] = useState(today())
  const [notes, setNotes] = useState('')
  const invoice = open.find((x) => x.id === invoiceId)

  const pick = (id: string) => {
    setInvoiceId(id)
    const chosen = open.find((x) => x.id === id)
    if (chosen) { setAmount(chosen.balance); setCurrency(chosen.currency) }
  }

  const save = async () => {
    if (amount <= 0) return void toast.error('Enter the amount received.')
    if (invoice && amount > invoice.balance) return void toast.error(`That is more than the ${formatCurrency(invoice.balance, invoice.currency)} still owing on ${invoice.invoiceNumber}.`)
    try {
      await api.post(endpoints.platformFinance.payments, {
        invoiceId: invoiceId || null, tenantId: invoice?.tenantId ?? null, amount, currency, method, status: 'Paid',
        reference: reference.trim() || null, paidAt: new Date(paidAt).toISOString(), notes: notes.trim() || null,
      })
      toast.success(invoice ? `Payment recorded against ${invoice.invoiceNumber}` : 'Payment recorded')
      onSaved()
    } catch {
      // the interceptor shows the error
    }
  }

  return (
    <form id={formId} className="grid grid-cols-1 gap-3 sm:grid-cols-2" onSubmit={(e) => { e.preventDefault(); void save() }}>
      <div className="sm:col-span-2">
        <Labelled label="For invoice" hint="the invoice's balance goes down by the amount">
          <select className={inputClass} value={invoiceId} onChange={(e) => pick(e.target.value)}>
            <option value="">Not against an invoice</option>
            {open.map((x) => <option key={x.id} value={x.id}>{x.invoiceNumber} · {x.customerName} · {formatCurrency(x.balance, x.currency)} owing</option>)}
          </select>
        </Labelled>
      </div>
      <Labelled label="Amount received">
        <input type="number" min={0} step="any" className={inputClass} value={amount} onChange={(e) => setAmount(Number(e.target.value) || 0)} required />
      </Labelled>
      <Labelled label="Currency">
        <select className={inputClass} value={currency} onChange={(e) => setCurrency(e.target.value)} disabled={Boolean(invoice)}>
          {CURRENCIES.map((x) => <option key={x}>{x}</option>)}
        </select>
      </Labelled>
      <Labelled label="Method">
        <select className={inputClass} value={method} onChange={(e) => setMethod(e.target.value)}>
          {PAYMENT_METHODS.map((x) => <option key={x}>{x}</option>)}
        </select>
      </Labelled>
      <Labelled label="Reference" hint="M-Pesa code, cheque or transfer number">
        <input className={inputClass} value={reference} onChange={(e) => setReference(e.target.value)} />
      </Labelled>
      <Labelled label="Date received">
        <input type="date" className={inputClass} value={paidAt} onChange={(e) => setPaidAt(e.target.value)} required />
      </Labelled>
      <div className="sm:col-span-2">
        <Labelled label="Notes">
          <textarea className={textareaClass} rows={2} value={notes} onChange={(e) => setNotes(e.target.value)} />
        </Labelled>
      </div>
      {invoice ? <p className="text-sm text-slate-600 sm:col-span-2">A receipt is emailed, and if this clears the invoice the workspace's subscription is extended by the period it covers.</p> : null}
    </form>
  )
}

/** Money the platform spent. */
export function ExpenseForm({ formId, existing, onSaved }: { formId: string; existing?: PlatformExpense | null; onSaved: () => void }) {
  const [expenseDate, setExpenseDate] = useState(dateOnly(existing?.expenseDate) || today())
  const [category, setCategory] = useState(existing?.category ?? EXPENSE_CATEGORIES[0])
  const [vendor, setVendor] = useState(existing?.vendor ?? '')
  const [description, setDescription] = useState(existing?.description ?? '')
  const [amount, setAmount] = useState(existing?.amount ?? 0)
  const [taxAmount, setTaxAmount] = useState(existing?.taxAmount ?? 0)
  const [currency, setCurrency] = useState(existing?.currency ?? 'KES')
  const [paymentMethod, setPaymentMethod] = useState(existing?.paymentMethod ?? 'Bank Transfer')
  const categories = EXPENSE_CATEGORIES.includes(category as (typeof EXPENSE_CATEGORIES)[number]) ? EXPENSE_CATEGORIES : [category, ...EXPENSE_CATEGORIES]

  const save = async () => {
    if (!description.trim()) return void toast.error('Say what the expense was for.')
    if (amount <= 0) return void toast.error('Enter the amount.')
    const payload = {
      expenseDate: new Date(expenseDate).toISOString(), category, vendor: vendor.trim() || null, description: description.trim(),
      amount, taxAmount, currency, paymentMethod, attachmentUrl: existing?.attachmentUrl ?? null, status: existing?.status ?? 'Submitted', tenantId: null,
    }
    try {
      if (existing) await api.put(endpoints.platformFinance.expense(existing.id), payload)
      else await api.post(endpoints.platformFinance.expenses, payload)
      toast.success(existing ? 'Expense saved' : 'Expense recorded, waiting for approval')
      onSaved()
    } catch {
      // the interceptor shows the error
    }
  }

  return (
    <form id={formId} className="grid grid-cols-1 gap-3 sm:grid-cols-2" onSubmit={(e) => { e.preventDefault(); void save() }}>
      <Labelled label="Date">
        <input type="date" className={inputClass} value={expenseDate} onChange={(e) => setExpenseDate(e.target.value)} required />
      </Labelled>
      <Labelled label="Category">
        <select className={inputClass} value={category} onChange={(e) => setCategory(e.target.value)}>
          {categories.map((x) => <option key={x}>{x}</option>)}
        </select>
      </Labelled>
      <Labelled label="Paid to">
        <input className={inputClass} value={vendor} onChange={(e) => setVendor(e.target.value)} placeholder="e.g. Safaricom, AWS" />
      </Labelled>
      <Labelled label="Paid by">
        <select className={inputClass} value={paymentMethod} onChange={(e) => setPaymentMethod(e.target.value)}>
          {PAYMENT_METHODS.map((x) => <option key={x}>{x}</option>)}
        </select>
      </Labelled>
      <div className="sm:col-span-2">
        <Labelled label="What for">
          <input className={inputClass} value={description} onChange={(e) => setDescription(e.target.value)} required />
        </Labelled>
      </div>
      <Labelled label="Amount before tax">
        <input type="number" min={0} step="any" className={inputClass} value={amount} onChange={(e) => setAmount(Number(e.target.value) || 0)} required />
      </Labelled>
      <Labelled label="Tax">
        <input type="number" min={0} step="any" className={inputClass} value={taxAmount} onChange={(e) => setTaxAmount(Number(e.target.value) || 0)} />
      </Labelled>
      <Labelled label="Currency">
        <select className={inputClass} value={currency} onChange={(e) => setCurrency(e.target.value)}>
          {CURRENCIES.map((x) => <option key={x}>{x}</option>)}
        </select>
      </Labelled>
      <div className="self-end rounded-xl bg-slate-50 p-3 text-sm"><Total label="Total" value={formatCurrency(amount + taxAmount, currency)} strong /></div>
    </form>
  )
}

/** Add or change a tax rate. */
export function TaxForm({ formId, existing, onSaved }: { formId: string; existing?: PlatformTax | null; onSaved: () => void }) {
  const [name, setName] = useState(existing?.name ?? 'VAT')
  const [rate, setRate] = useState(existing?.rate ?? 16)
  const [mode, setMode] = useState(existing?.mode ?? 'Exclusive')
  const [isDefault, setIsDefault] = useState(existing?.isDefault ?? false)

  const save = async () => {
    if (!name.trim()) return void toast.error('Name the tax.')
    const payload = { name: name.trim(), rate, mode, isDefault }
    try {
      if (existing) await api.put(`${endpoints.platformFinance.taxes}/${existing.id}`, payload)
      else await api.post(endpoints.platformFinance.taxes, payload)
      toast.success('Tax saved')
      onSaved()
    } catch {
      // the interceptor shows the error
    }
  }

  return (
    <form id={formId} className="grid grid-cols-1 gap-3 sm:grid-cols-2" onSubmit={(e) => { e.preventDefault(); void save() }}>
      <Labelled label="Name"><input className={inputClass} value={name} onChange={(e) => setName(e.target.value)} required /></Labelled>
      <Labelled label="Rate %"><input type="number" min={0} max={100} step="any" className={inputClass} value={rate} onChange={(e) => setRate(Number(e.target.value) || 0)} /></Labelled>
      <Labelled label="Prices are">
        <select className={inputClass} value={mode} onChange={(e) => setMode(e.target.value)}>
          <option value="Exclusive">Before tax (tax added on top)</option>
          <option value="Inclusive">Including tax</option>
        </select>
      </Labelled>
      <label className="flex items-center gap-2 self-end pb-2 text-sm text-slate-700">
        <input type="checkbox" checked={isDefault} onChange={(e) => setIsDefault(e.target.checked)} />Use by default on new quotations and invoices
      </label>
    </form>
  )
}

function Labelled({ label, hint, children }: { label: string; hint?: string; children: ReactNode }) {
  return (
    <label className="block space-y-1.5">
      <span className="block text-xs font-semibold text-slate-600">{label}{hint ? <span className="ml-1 font-normal text-slate-400">({hint})</span> : null}</span>
      {children}
    </label>
  )
}

function Total({ label, value, strong = false }: { label: string; value: string; strong?: boolean }) {
  return (
    <div className="flex items-center justify-between gap-3">
      <dt className={strong ? 'font-semibold text-slate-900' : 'text-slate-600'}>{label}</dt>
      <dd className={cn('tabular-nums', strong ? 'font-semibold text-slate-900' : 'text-slate-800')}>{value}</dd>
    </div>
  )
}
