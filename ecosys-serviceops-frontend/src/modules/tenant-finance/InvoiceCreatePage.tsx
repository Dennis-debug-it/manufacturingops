import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { AlertTriangle, Plus, Trash2 } from 'lucide-react'
import { useToast } from '../../components/ui/ToastProvider'
import { PageScaffold, SectionCard, StickyActionFooter } from '../../components/ui/Workspace'
import { useAsyncData } from '../../hooks/useAsyncData'
import { clientService } from '../../services/clientService'
import { creditTermService } from '../../services/creditTermService'
import { invoiceService } from '../../services/invoiceService'
import { tenantFinanceService } from '../../services/tenantFinanceService'
import type { ClientRecord, CreateInvoiceInput, CreateInvoiceLineInput, CreditTermRecord, RateCardRecord } from '../../types/api'

// ── Constants ─────────────────────────────────────────────────────────────────

const ITEM_TYPES = ['Labour', 'Callout', 'Service', 'Transport', 'Material', 'Other'] as const
const UNITS = ['hour', 'visit', 'km', 'unit', 'day', 'job'] as const
const CURRENCIES = ['KES', 'USD', 'GBP', 'EUR', 'ZAR', 'UGX', 'TZS'] as const

const emptyLine = (): CreateInvoiceLineInput => ({
  description: '',
  itemType: 'Service',
  quantity: 1,
  unit: 'unit',
  unitRate: 0,
  taxRate: 16,
  billableItemId: null,
  workOrderId: null,
})

const today = () => new Date().toISOString().slice(0, 10)

const emptyForm = (): CreateInvoiceInput => ({
  clientId: '',
  invoiceDate: today(),
  dueDate: new Date(Date.now() + 30 * 24 * 60 * 60 * 1000).toISOString().slice(0, 10),
  creditTermId: null,
  notes: '',
  termsAndConditions: '',
  currency: 'KES',
  lines: [emptyLine()],
})

// ── Page ──────────────────────────────────────────────────────────────────────

export function InvoiceCreatePage() {
  const navigate = useNavigate()
  const { pushToast } = useToast()

  const [form, setForm] = useState<CreateInvoiceInput>(emptyForm)
  const [saving, setSaving] = useState(false)

  const { data: clients } = useAsyncData<ClientRecord[]>(
    (signal) => clientService.list({ signal, status: 'active' }),
    [],
    [],
  )

  const { data: creditTerms } = useAsyncData<CreditTermRecord[]>(
    (signal) => creditTermService.list(signal),
    [],
    [],
  )

  const { data: rateCards } = useAsyncData<RateCardRecord[]>(
    (signal) => tenantFinanceService.getRateCards(signal),
    [],
    [],
  )

  // ── Derived ──────────────────────────────────────────────────────────────

  const selectedCreditTerm = creditTerms.find((t) => t.id === form.creditTermId) ?? null

  const subtotal = form.lines.reduce((s, l) => s + l.quantity * l.unitRate, 0)
  const taxTotal = form.lines.reduce((s, l) => s + l.quantity * l.unitRate * (l.taxRate / 100), 0)
  const grandTotal = subtotal + taxTotal
  const depositAmount = selectedCreditTerm?.isDepositRequired
    ? Math.round(grandTotal * (selectedCreditTerm.depositPercent / 100) * 100) / 100
    : null

  // ── Handlers ─────────────────────────────────────────────────────────────

  function setField<K extends keyof CreateInvoiceInput>(key: K, value: CreateInvoiceInput[K]) {
    setForm((prev) => ({ ...prev, [key]: value }))
  }

  function setLine<K extends keyof CreateInvoiceLineInput>(idx: number, key: K, value: CreateInvoiceLineInput[K]) {
    setForm((prev) => ({
      ...prev,
      lines: prev.lines.map((l, i) => (i === idx ? { ...l, [key]: value } : l)),
    }))
  }

  function applyRateCard(idx: number, card: RateCardRecord) {
    setForm((prev) => ({
      ...prev,
      lines: prev.lines.map((l, i) =>
        i === idx
          ? {
              ...l,
              description: card.name,
              itemType: card.category,
              unit: card.unit,
              unitRate: card.unitRate,
              taxRate: card.taxRate ?? l.taxRate,
              billableItemId: null,
            }
          : l,
      ),
    }))
  }

  function addLine() {
    setForm((prev) => ({ ...prev, lines: [...prev.lines, emptyLine()] }))
  }

  function removeLine(idx: number) {
    setForm((prev) => ({ ...prev, lines: prev.lines.filter((_, i) => i !== idx) }))
  }

  function handleCreditTermChange(termId: string) {
    const term = creditTerms.find((t) => t.id === termId)
    setForm((prev) => {
      const invoiceDate = new Date(`${prev.invoiceDate}T00:00:00.000Z`)
      const dueDate = term
        ? new Date(invoiceDate.getTime() + term.netDays * 86400000).toISOString().slice(0, 10)
        : prev.dueDate
      return { ...prev, creditTermId: termId || null, dueDate }
    })
  }

  async function handleSave() {
    if (!form.clientId) {
      pushToast({ title: 'Client required', description: 'Select a client before saving.', tone: 'danger' })
      return
    }
    if (form.lines.some((l) => !l.description.trim())) {
      pushToast({ title: 'Line description required', description: 'Each line must have a description.', tone: 'danger' })
      return
    }

    setSaving(true)
    try {
      const payload: CreateInvoiceInput = {
        ...form,
        invoiceDate: new Date(`${form.invoiceDate}T00:00:00.000Z`).toISOString(),
        dueDate: new Date(`${form.dueDate}T00:00:00.000Z`).toISOString(),
        notes: form.notes?.trim() || null,
        termsAndConditions: form.termsAndConditions?.trim() || null,
        creditTermId: form.creditTermId || null,
      }
      const invoice = await invoiceService.create(payload)
      pushToast({ title: 'Invoice created', description: `${invoice.invoiceNumber} created as draft.`, tone: 'success' })
      navigate(`/finance/invoices/${invoice.id}`)
    } catch (err) {
      pushToast({ title: 'Save failed', description: err instanceof Error ? err.message : 'Unable to create invoice.', tone: 'danger' })
    } finally {
      setSaving(false)
    }
  }

  // ── Render ────────────────────────────────────────────────────────────────

  return (
    <PageScaffold title="New Invoice" description="Create a new invoice for a client.">
      <div className="space-y-6 pb-28">
        {/* Header */}
        <div>
          <p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted">Finance</p>
          <h1 className="text-2xl font-bold text-app mt-1">New Invoice</h1>
        </div>

        {/* Client + Dates */}
        <SectionCard title="Invoice details" description="Client, dates, currency and credit terms.">
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {/* Client */}
            <div className="space-y-1 sm:col-span-2 lg:col-span-1">
              <label className="field-label">Client *</label>
              <select
                className="field-input"
                value={form.clientId}
                onChange={(e) => setField('clientId', e.target.value)}
              >
                <option value="">Select client…</option>
                {clients.map((c) => (
                  <option key={c.id} value={c.id}>{c.clientName}</option>
                ))}
              </select>
            </div>

            {/* Invoice Date */}
            <div className="space-y-1">
              <label className="field-label">Invoice date</label>
              <input
                type="date"
                className="field-input"
                value={form.invoiceDate}
                onChange={(e) => setField('invoiceDate', e.target.value)}
              />
            </div>

            {/* Currency */}
            <div className="space-y-1">
              <label className="field-label">Currency</label>
              <select
                className="field-input"
                value={form.currency ?? 'KES'}
                onChange={(e) => setField('currency', e.target.value)}
              >
                {CURRENCIES.map((c) => <option key={c} value={c}>{c}</option>)}
              </select>
            </div>

            {/* Credit term */}
            <div className="space-y-1">
              <label className="field-label">Credit terms</label>
              <select
                className="field-input"
                value={form.creditTermId ?? ''}
                onChange={(e) => handleCreditTermChange(e.target.value)}
              >
                <option value="">Manual due date…</option>
                {creditTerms.map((t) => (
                  <option key={t.id} value={t.id}>
                    {t.name}{t.isDefault ? ' (default)' : ''}
                  </option>
                ))}
              </select>
              {selectedCreditTerm && (
                <p className="text-xs text-muted mt-1">{selectedCreditTerm.description}</p>
              )}
            </div>

            {/* Due Date */}
            <div className="space-y-1">
              <label className="field-label">Due date</label>
              <input
                type="date"
                className="field-input"
                value={form.dueDate}
                onChange={(e) => setField('dueDate', e.target.value)}
                readOnly={!!selectedCreditTerm}
              />
              {selectedCreditTerm && (
                <p className="text-xs text-muted mt-1">
                  Auto-set: {selectedCreditTerm.netDays} days from invoice date
                </p>
              )}
            </div>
          </div>

          {/* Deposit info banner */}
          {selectedCreditTerm?.isDepositRequired && depositAmount !== null && (
            <div className="mt-4 flex items-start gap-3 rounded-2xl border border-warning/30 bg-warning/10 p-4">
              <AlertTriangle className="mt-0.5 h-4 w-4 flex-shrink-0 text-warning" />
              <div>
                <p className="text-sm font-semibold text-app">Deposit required</p>
                <p className="text-xs text-muted mt-0.5">
                  {selectedCreditTerm.depositPercent}% deposit ({form.currency} {depositAmount.toLocaleString(undefined, { minimumFractionDigits: 2 })}) is due upon invoice date per <strong>{selectedCreditTerm.name}</strong> terms.
                </p>
              </div>
            </div>
          )}
        </SectionCard>

        {/* Lines */}
        <SectionCard title="Invoice lines" description="Add billable items for this invoice.">
          <div className="space-y-3">
            {form.lines.map((line, idx) => (
              <div key={idx} className="rounded-2xl border border-app bg-subtle p-4 space-y-3">
                <div className="flex items-center justify-between gap-2">
                  <p className="text-sm font-semibold text-app">Line {idx + 1}</p>
                  <div className="flex items-center gap-2">
                    {/* Rate card quick-fill */}
                    <select
                      className="field-input text-xs py-1"
                      defaultValue=""
                      onChange={(e) => {
                        const card = rateCards.find((r) => r.id === e.target.value)
                        if (card) applyRateCard(idx, card)
                        e.target.value = ''
                      }}
                    >
                      <option value="">Fill from rate card…</option>
                      {rateCards.map((r) => (
                        <option key={r.id} value={r.id}>{r.name}</option>
                      ))}
                    </select>
                    {form.lines.length > 1 && (
                      <button
                        type="button"
                        className="button-ghost p-1"
                        onClick={() => removeLine(idx)}
                        title="Remove line"
                      >
                        <Trash2 className="h-4 w-4 text-danger" />
                      </button>
                    )}
                  </div>
                </div>

                <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
                  <div className="space-y-1 sm:col-span-2 lg:col-span-2">
                    <label className="field-label">Description *</label>
                    <input
                      type="text"
                      className="field-input"
                      value={line.description}
                      placeholder="Describe this line item…"
                      onChange={(e) => setLine(idx, 'description', e.target.value)}
                    />
                  </div>

                  <div className="space-y-1">
                    <label className="field-label">Item type</label>
                    <select
                      className="field-input"
                      value={line.itemType}
                      onChange={(e) => setLine(idx, 'itemType', e.target.value)}
                    >
                      {ITEM_TYPES.map((t) => <option key={t} value={t}>{t}</option>)}
                    </select>
                  </div>

                  <div className="space-y-1">
                    <label className="field-label">Qty</label>
                    <input
                      type="number"
                      min="0"
                      step="0.01"
                      className="field-input"
                      value={line.quantity}
                      onChange={(e) => setLine(idx, 'quantity', parseFloat(e.target.value) || 0)}
                    />
                  </div>

                  <div className="space-y-1">
                    <label className="field-label">Unit</label>
                    <select
                      className="field-input"
                      value={line.unit}
                      onChange={(e) => setLine(idx, 'unit', e.target.value)}
                    >
                      {UNITS.map((u) => <option key={u} value={u}>{u}</option>)}
                    </select>
                  </div>

                  <div className="space-y-1">
                    <label className="field-label">Unit rate</label>
                    <input
                      type="number"
                      min="0"
                      step="0.01"
                      className="field-input"
                      value={line.unitRate}
                      onChange={(e) => setLine(idx, 'unitRate', parseFloat(e.target.value) || 0)}
                    />
                  </div>

                  <div className="space-y-1">
                    <label className="field-label">Tax %</label>
                    <input
                      type="number"
                      min="0"
                      max="100"
                      step="0.01"
                      className="field-input"
                      value={line.taxRate}
                      onChange={(e) => setLine(idx, 'taxRate', parseFloat(e.target.value) || 0)}
                    />
                  </div>

                  <div className="space-y-1">
                    <label className="field-label">Line total</label>
                    <p className="field-input bg-muted/10 text-muted cursor-default">
                      {(line.quantity * line.unitRate * (1 + line.taxRate / 100)).toLocaleString(undefined, { minimumFractionDigits: 2 })}
                    </p>
                  </div>
                </div>
              </div>
            ))}

            <button type="button" className="button-ghost text-sm" onClick={addLine}>
              <Plus className="h-4 w-4 mr-1" /> Add line
            </button>
          </div>

          {/* Totals */}
          <div className="ml-auto mt-4 max-w-xs space-y-2 rounded-2xl border border-border bg-surface p-4">
            <div className="flex justify-between text-sm">
              <span className="text-muted">Subtotal</span>
              <span>{form.currency} {subtotal.toLocaleString(undefined, { minimumFractionDigits: 2 })}</span>
            </div>
            <div className="flex justify-between text-sm">
              <span className="text-muted">Tax</span>
              <span>{form.currency} {taxTotal.toLocaleString(undefined, { minimumFractionDigits: 2 })}</span>
            </div>
            {depositAmount !== null && (
              <div className="flex justify-between text-sm text-warning">
                <span>Deposit due</span>
                <span>{form.currency} {depositAmount.toLocaleString(undefined, { minimumFractionDigits: 2 })}</span>
              </div>
            )}
            <div className="flex justify-between font-semibold border-t border-border pt-2">
              <span>Total</span>
              <span>{form.currency} {grandTotal.toLocaleString(undefined, { minimumFractionDigits: 2 })}</span>
            </div>
          </div>
        </SectionCard>

        {/* Notes */}
        <SectionCard title="Notes & terms" description="Optional internal notes and T&Cs shown on the invoice.">
          <div className="grid gap-4 sm:grid-cols-2">
            <div className="space-y-1">
              <label className="field-label">Notes</label>
              <textarea
                className="field-input"
                rows={3}
                value={form.notes ?? ''}
                onChange={(e) => setField('notes', e.target.value)}
                placeholder="Internal notes…"
              />
            </div>
            <div className="space-y-1">
              <label className="field-label">Terms & conditions</label>
              <textarea
                className="field-input"
                rows={3}
                value={form.termsAndConditions ?? ''}
                onChange={(e) => setField('termsAndConditions', e.target.value)}
                placeholder="Payment terms shown on invoice…"
              />
            </div>
          </div>
        </SectionCard>
      </div>

      <StickyActionFooter>
        <button type="button" className="button-secondary" onClick={() => navigate('/finance/invoices')}>
          Cancel
        </button>
        <button type="button" className="button-primary" onClick={handleSave} disabled={saving}>
          {saving ? 'Creating…' : 'Create Invoice'}
        </button>
      </StickyActionFooter>
    </PageScaffold>
  )
}
