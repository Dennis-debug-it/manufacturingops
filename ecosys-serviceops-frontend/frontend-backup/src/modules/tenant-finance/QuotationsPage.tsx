import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '../../auth/AuthContext'
import { Badge } from '../../components/ui/Badge'
import { DataTable } from '../../components/ui/DataTable'
import { Drawer } from '../../components/ui/Drawer'
import { EmptyState } from '../../components/ui/EmptyState'
import { ErrorState } from '../../components/ui/ErrorState'
import { LoadingState } from '../../components/ui/LoadingState'
import { useToast } from '../../components/ui/ToastProvider'
import { SectionCard, StickyActionFooter } from '../../components/ui/Workspace'
import { useAsyncData } from '../../hooks/useAsyncData'
import { clientService } from '../../services/clientService'
import { tenantFinanceService } from '../../services/tenantFinanceService'
import { quotationService } from '../../services/quotationService'
import type {
  ClientRecord,
  RateCardRecord,
  TenantQuotationRecord,
  UpsertQuotationInput,
  UpsertQuotationLineInput,
} from '../../types/api'
import { formatDateOnly } from '../../utils/date'
import type { BadgeTone } from '../../types/app'

const STATUS_TABS = ['All', 'Draft', 'Sent', 'Accepted', 'Rejected', 'Expired', 'Converted'] as const
type StatusTab = (typeof STATUS_TABS)[number]

function statusTone(status: string): BadgeTone {
  switch (status) {
    case 'Accepted': return 'success'
    case 'Sent': return 'info'
    case 'Rejected': return 'danger'
    case 'Converted': return 'neutral'
    case 'Expired': return 'warning'
    default: return 'default'
  }
}

const emptyLine: UpsertQuotationLineInput = {
  rateCardId: null,
  description: '',
  itemType: 'Service',
  quantity: 1,
  unit: 'unit',
  unitRate: 0,
  taxRate: 16,
}

const emptyForm: UpsertQuotationInput = {
  clientId: '',
  siteId: null,
  quoteDate: new Date().toISOString().slice(0, 10),
  validUntil: new Date(Date.now() + 30 * 24 * 60 * 60 * 1000).toISOString().slice(0, 10),
  subject: '',
  notes: '',
  currency: 'KES',
  lines: [{ ...emptyLine }],
}

export function QuotationsPage() {
  const navigate = useNavigate()
  const { permissions } = useAuth()
  const { pushToast } = useToast()
  const canManage = Boolean(permissions?.canManageFinance || permissions?.canManageBillableWork)

  const [activeTab, setActiveTab] = useState<StatusTab>('All')
  const [search, setSearch] = useState('')
  const [drawerOpen, setDrawerOpen] = useState(false)
  const [editingId, setEditingId] = useState<string | null>(null)
  const [form, setForm] = useState<UpsertQuotationInput>(emptyForm)
  const [saving, setSaving] = useState(false)

  const { data, loading, error, reload } = useAsyncData<TenantQuotationRecord[]>(
    (signal) => quotationService.list({ status: activeTab === 'All' ? undefined : activeTab, signal }),
    [],
    [activeTab],
  )

  const { data: clients } = useAsyncData<ClientRecord[]>(
    (signal) => clientService.list({ signal, status: 'all' }),
    [],
    [],
  )

  const { data: rateCards } = useAsyncData<RateCardRecord[]>(
    (signal) => tenantFinanceService.getRateCards(signal),
    [],
    [],
  )

  const filtered = search.trim()
    ? data.filter(
        (q) =>
          q.quoteNumber.toLowerCase().includes(search.toLowerCase()) ||
          (q.clientName ?? '').toLowerCase().includes(search.toLowerCase()) ||
          (q.subject ?? '').toLowerCase().includes(search.toLowerCase()),
      )
    : data

  function openCreate() {
    setEditingId(null)
    setForm({ ...emptyForm, lines: [{ ...emptyLine }] })
    setDrawerOpen(true)
  }

  function addLine() {
    setForm((f) => ({ ...f, lines: [...f.lines, { ...emptyLine }] }))
  }

  function removeLine(idx: number) {
    setForm((f) => ({ ...f, lines: f.lines.filter((_, i) => i !== idx) }))
  }

  function updateLine(idx: number, patch: Partial<UpsertQuotationLineInput>) {
    setForm((f) => ({
      ...f,
      lines: f.lines.map((l, i) => (i === idx ? { ...l, ...patch } : l)),
    }))
  }

  function applyRateCard(idx: number, cardId: string) {
    const card = rateCards.find((c) => c.id === cardId)
    if (!card) return
    updateLine(idx, {
      rateCardId: card.id,
      description: card.name,
      itemType: card.category,
      unit: card.unit,
      unitRate: card.unitRate,
      taxRate: card.taxRate ?? 16,
    })
  }

  const lineSubtotal = form.lines.reduce((sum, l) => sum + l.quantity * l.unitRate, 0)
  const lineTax = form.lines.reduce((sum, l) => sum + l.quantity * l.unitRate * (l.taxRate / 100), 0)
  const lineTotal = lineSubtotal + lineTax

  async function saveQuotation() {
    setSaving(true)
    try {
      const payload: UpsertQuotationInput = {
        ...form,
        quoteDate: new Date(`${form.quoteDate}T00:00:00.000Z`).toISOString(),
        validUntil: new Date(`${form.validUntil}T00:00:00.000Z`).toISOString(),
      }
      if (editingId) {
        await quotationService.update(editingId, payload)
        pushToast({ title: 'Quotation updated', tone: 'success' })
      } else {
        await quotationService.create(payload)
        pushToast({ title: 'Quotation created', tone: 'success' })
      }
      setDrawerOpen(false)
      await reload()
    } catch (err) {
      pushToast({ title: 'Save failed', description: err instanceof Error ? err.message : 'Unable to save.', tone: 'danger' })
    } finally {
      setSaving(false)
    }
  }

  async function handleSend(q: TenantQuotationRecord) {
    try {
      await quotationService.send(q.id)
      pushToast({ title: 'Sent', description: `${q.quoteNumber} marked as Sent.`, tone: 'success' })
      await reload()
    } catch (err) {
      pushToast({ title: 'Failed', description: err instanceof Error ? err.message : 'Error', tone: 'danger' })
    }
  }

  async function handleConvertToInvoice(q: TenantQuotationRecord) {
    try {
      const result = await quotationService.convertToInvoice(q.id) as { id: string }
      pushToast({ title: 'Invoice created', description: `Invoice created from ${q.quoteNumber}.`, tone: 'success' })
      await reload()
      if (result?.id) navigate(`/finance/invoices/${result.id}`)
    } catch (err) {
      pushToast({ title: 'Convert failed', description: err instanceof Error ? err.message : 'Error', tone: 'danger' })
    }
  }

  async function handleConvertToWO(q: TenantQuotationRecord) {
    try {
      const result = await quotationService.convertToWorkOrder(q.id)
      pushToast({ title: 'Work order created', description: `WO ${result.workOrderNumber} created.`, tone: 'success' })
      await reload()
      navigate(`/work-orders/${result.workOrderId}`)
    } catch (err) {
      pushToast({ title: 'Convert failed', description: err instanceof Error ? err.message : 'Error', tone: 'danger' })
    }
  }

  async function handleDownloadPdf(q: TenantQuotationRecord) {
    try {
      const result = await quotationService.downloadPdf(q.id)
      const url = URL.createObjectURL(result.blob)
      const a = document.createElement('a')
      a.href = url
      a.download = result.fileName
      a.click()
      URL.revokeObjectURL(url)
    } catch {
      pushToast({ title: 'PDF failed', tone: 'danger' })
    }
  }

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex gap-1 overflow-x-auto">
          {STATUS_TABS.map((tab) => (
            <button
              key={tab}
              type="button"
              onClick={() => setActiveTab(tab)}
              className={`rounded-full px-4 py-1.5 text-sm font-medium transition-colors ${
                activeTab === tab ? 'bg-accent text-white' : 'bg-surface-muted text-muted hover:text-app'
              }`}
            >
              {tab}
            </button>
          ))}
        </div>
        <div className="flex items-center gap-2">
          <input
            type="text"
            placeholder="Search quote # or client…"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            className="field-input w-60 py-1.5 text-sm"
          />
          {canManage ? (
            <button type="button" className="button-primary" onClick={openCreate}>
              + New Quote
            </button>
          ) : null}
        </div>
      </div>

      <SectionCard title="Quotations" description="Client quotations and estimates.">
        {loading ? <LoadingState label="Loading quotations" /> : null}
        {!loading && error ? <ErrorState title="Unable to load quotations" description={error} /> : null}
        {!loading && !error && filtered.length === 0 ? (
          <EmptyState title="No quotations found" description="Create the first quotation for a client." />
        ) : null}
        {!loading && !error && filtered.length > 0 ? (
          <DataTable
            rows={filtered}
            rowKey={(r) => r.id}
            minTableWidth="min-w-[1080px] w-full"
            columns={[
              { key: 'number', header: 'Quote #', cell: (r) => <span className="font-semibold">{r.quoteNumber}</span> },
              { key: 'client', header: 'Client', cell: (r) => r.clientName ?? '—' },
              { key: 'subject', header: 'Subject', cell: (r) => r.subject ?? '—' },
              { key: 'date', header: 'Date', cell: (r) => formatDateOnly(r.quoteDate) },
              { key: 'validUntil', header: 'Valid Until', cell: (r) => formatDateOnly(r.validUntil) },
              { key: 'amount', header: 'Amount', cell: (r) => `${r.currency} ${r.totalAmount.toLocaleString(undefined, { minimumFractionDigits: 2 })}` },
              { key: 'status', header: 'Status', cell: (r) => <Badge tone={statusTone(r.status)}>{r.status}</Badge> },
              {
                key: 'actions',
                header: 'Actions',
                className: 'min-w-[280px]',
                cell: (r) => (
                  <div className="flex flex-wrap gap-1">
                    {r.status === 'Draft' && canManage ? (
                      <button type="button" className="button-ghost py-1 text-xs" onClick={() => void handleSend(r)}>Send</button>
                    ) : null}
                    {(r.status === 'Accepted' || r.status === 'Sent') && canManage ? (
                      <button type="button" className="button-ghost py-1 text-xs" onClick={() => void handleConvertToInvoice(r)}>→ Invoice</button>
                    ) : null}
                    {(r.status === 'Accepted' || r.status === 'Sent') && canManage ? (
                      <button type="button" className="button-ghost py-1 text-xs" onClick={() => void handleConvertToWO(r)}>→ Work Order</button>
                    ) : null}
                    <button type="button" className="button-ghost py-1 text-xs" onClick={() => void handleDownloadPdf(r)}>PDF</button>
                  </div>
                ),
              },
            ]}
          />
        ) : null}
      </SectionCard>

      {/* Editor Drawer */}
      <Drawer
        open={drawerOpen}
        title={editingId ? 'Edit quotation' : 'New quotation'}
        description="Build a client quotation with line items from rate cards or manual entry."
        onClose={() => setDrawerOpen(false)}
      >
        <div className="space-y-4 p-4">
          <div className="grid gap-4 md:grid-cols-2">
            <label className="block space-y-1">
              <span className="text-sm font-medium text-app">Client</span>
              <select value={form.clientId} onChange={(e) => setForm((f) => ({ ...f, clientId: e.target.value }))} className="field-input">
                <option value="">Select client</option>
                {clients.map((c) => <option key={c.id} value={c.id}>{c.clientName}</option>)}
              </select>
            </label>
            <label className="block space-y-1">
              <span className="text-sm font-medium text-app">Subject</span>
              <input value={form.subject ?? ''} onChange={(e) => setForm((f) => ({ ...f, subject: e.target.value }))} className="field-input" />
            </label>
            <label className="block space-y-1">
              <span className="text-sm font-medium text-app">Quote date</span>
              <input type="date" value={form.quoteDate} onChange={(e) => setForm((f) => ({ ...f, quoteDate: e.target.value }))} className="field-input" />
            </label>
            <label className="block space-y-1">
              <span className="text-sm font-medium text-app">Valid until</span>
              <input type="date" value={form.validUntil} onChange={(e) => setForm((f) => ({ ...f, validUntil: e.target.value }))} className="field-input" />
            </label>
            <label className="block space-y-1">
              <span className="text-sm font-medium text-app">Currency</span>
              <input value={form.currency ?? 'KES'} onChange={(e) => setForm((f) => ({ ...f, currency: e.target.value.toUpperCase() }))} className="field-input" />
            </label>
          </div>

          <div className="space-y-2">
            <div className="flex items-center justify-between">
              <span className="text-sm font-medium text-app">Line items</span>
              <button type="button" className="button-ghost py-1 text-xs" onClick={addLine}>+ Add line</button>
            </div>
            {form.lines.map((line, idx) => (
              <div key={idx} className="rounded-xl border border-border bg-surface-muted p-3 space-y-2">
                <div className="grid gap-2 md:grid-cols-3">
                  <label className="block space-y-1 md:col-span-3">
                    <span className="text-xs text-muted">From rate card</span>
                    <select
                      className="field-input text-sm"
                      value={line.rateCardId ?? ''}
                      onChange={(e) => applyRateCard(idx, e.target.value)}
                    >
                      <option value="">Manual entry</option>
                      {rateCards.map((c) => <option key={c.id} value={c.id}>{c.name} ({c.category})</option>)}
                    </select>
                  </label>
                  <label className="block space-y-1 md:col-span-2">
                    <span className="text-xs text-muted">Description</span>
                    <input value={line.description} onChange={(e) => updateLine(idx, { description: e.target.value })} className="field-input text-sm" />
                  </label>
                  <label className="block space-y-1">
                    <span className="text-xs text-muted">Item type</span>
                    <input value={line.itemType} onChange={(e) => updateLine(idx, { itemType: e.target.value })} className="field-input text-sm" />
                  </label>
                  <label className="block space-y-1">
                    <span className="text-xs text-muted">Qty</span>
                    <input type="number" min={0} step={0.01} value={line.quantity} onChange={(e) => updateLine(idx, { quantity: Number(e.target.value) })} className="field-input text-sm" />
                  </label>
                  <label className="block space-y-1">
                    <span className="text-xs text-muted">Unit</span>
                    <input value={line.unit} onChange={(e) => updateLine(idx, { unit: e.target.value })} className="field-input text-sm" />
                  </label>
                  <label className="block space-y-1">
                    <span className="text-xs text-muted">Rate</span>
                    <input type="number" min={0} step={0.01} value={line.unitRate} onChange={(e) => updateLine(idx, { unitRate: Number(e.target.value) })} className="field-input text-sm" />
                  </label>
                  <label className="block space-y-1">
                    <span className="text-xs text-muted">Tax %</span>
                    <input type="number" min={0} step={0.01} value={line.taxRate} onChange={(e) => updateLine(idx, { taxRate: Number(e.target.value) })} className="field-input text-sm" />
                  </label>
                </div>
                <div className="flex items-center justify-between">
                  <span className="text-xs text-muted">
                    Total: {form.currency} {(line.quantity * line.unitRate * (1 + line.taxRate / 100)).toLocaleString(undefined, { minimumFractionDigits: 2 })}
                  </span>
                  {form.lines.length > 1 ? (
                    <button type="button" className="button-ghost py-0.5 text-xs text-danger" onClick={() => removeLine(idx)}>Remove</button>
                  ) : null}
                </div>
              </div>
            ))}
          </div>

          <div className="rounded-xl border border-border bg-surface p-3 space-y-1 text-sm">
            <div className="flex justify-between">
              <span className="text-muted">Subtotal</span>
              <span>{form.currency} {lineSubtotal.toLocaleString(undefined, { minimumFractionDigits: 2 })}</span>
            </div>
            <div className="flex justify-between">
              <span className="text-muted">Tax</span>
              <span>{form.currency} {lineTax.toLocaleString(undefined, { minimumFractionDigits: 2 })}</span>
            </div>
            <div className="flex justify-between font-semibold">
              <span>Total</span>
              <span>{form.currency} {lineTotal.toLocaleString(undefined, { minimumFractionDigits: 2 })}</span>
            </div>
          </div>

          <label className="block space-y-1">
            <span className="text-sm font-medium text-app">Notes</span>
            <textarea value={form.notes ?? ''} onChange={(e) => setForm((f) => ({ ...f, notes: e.target.value }))} className="field-input" rows={2} />
          </label>
        </div>
        <StickyActionFooter>
          <button type="button" className="button-secondary" onClick={() => setDrawerOpen(false)}>Cancel</button>
          <button type="button" className="button-primary" onClick={() => void saveQuotation()} disabled={saving}>
            {saving ? 'Saving…' : editingId ? 'Update quotation' : 'Create quotation'}
          </button>
        </StickyActionFooter>
      </Drawer>
    </div>
  )
}
