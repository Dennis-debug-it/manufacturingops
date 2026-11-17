import { useEffect, useState } from 'react'
import toast from 'react-hot-toast'
import { Link } from 'react-router-dom'
import { AlertTriangle, ArrowLeft, Building2, Copy, Download, FileText, Save, Trash2 } from 'lucide-react'
import { Spinner } from '../../shared/components/Spinner'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { formatDate } from '../../shared/utils/dates'
import { cn } from '../../shared/utils/cn'
import { Field, dangerButton, inputClass, primaryButton, secondaryButton, textareaClass } from '../../features/factory/components/ui'
import { useTenants } from '../api'
import {
  EDITION_LABEL, downloadQuotePdf, money, newTerms, useApplyQuote, useCopyQuote, useDeleteQuote, useInvoiceQuote, usePreview, usePriceBook, useQuote, useQuoteStatus, useSaveQuote,
  type PricingResult, type QuoteStatus, type QuoteTerms, type SaveQuote,
} from './api'

/** Builds or edits a quote, with the totals worked out by the server as the terms change. */
export function QuoteEditor({ id, onOpen, onClose }: { id: string | null; onOpen: (id: string) => void; onClose: () => void }) {
  const book = usePriceBook()
  const existing = useQuote(id)
  if (book.isLoading || (id && existing.isLoading)) return <Spinner label="Loading quote" />
  if (id && !existing.data) return <p className="text-sm text-slate-500">This quote was not found.</p>
  return <QuoteForm key={id ?? 'new'} id={id} onOpen={onOpen} onClose={onClose}
    initial={existing.data ? { ...existing.data.request, terms: { ...existing.data.request.terms, hardware: existing.data.request.terms.hardware ?? [] } }
      : { customerName: '', contactName: null, contactEmail: null, tenantId: null, notes: null, validUntil: null, terms: newTerms(book.data) }} />
}

function QuoteForm({ id, initial, onOpen, onClose }: { id: string | null; initial: SaveQuote; onOpen: (id: string) => void; onClose: () => void }) {
  const book = usePriceBook()
  const detail = useQuote(id)
  const tenants = useTenants()
  const save = useSaveQuote()
  const setStatus = useQuoteStatus()
  const copy = useCopyQuote()
  const remove = useDeleteQuote()
  const apply = useApplyQuote()
  const invoice = useInvoiceQuote()
  const invoices = detail.data?.invoices ?? []
  const raise = (purpose: 'YearOne' | 'Pilot') => {
    if (!id) return
    void invoice.mutateAsync({ id, purpose }).then((d) => {
      const raised = d.invoices.find((x) => x.purpose === purpose)
      toast.success(raised ? `Draft invoice ${raised.invoiceNumber} raised; send it from Finance` : 'Invoice raised')
    }).catch(() => undefined)
  }
  const [form, setForm] = useState<SaveQuote>(initial)
  const [dirty, setDirty] = useState(false)
  const terms = form.terms
  const setTerms = (patch: Partial<QuoteTerms>) => { setForm((f) => ({ ...f, terms: { ...f.terms, ...patch } })); setDirty(true) }
  const set = (patch: Partial<SaveQuote>) => { setForm((f) => ({ ...f, ...patch })); setDirty(true) }

  // The totals are asked for once typing pauses.
  const [debounced, setDebounced] = useState<QuoteTerms>(terms)
  useEffect(() => { const t = window.setTimeout(() => setDebounced(terms), 300); return () => window.clearTimeout(t) }, [terms])
  const valid = debounced.factories >= 1 && (debounced.edition !== null || debounced.visitors || debounced.security)
  const preview = usePreview(valid ? debounced : null)
  const quote = detail.data?.quote
  const locked = quote?.status === 'Accepted' || quote?.status === 'Declined'
  const shown: PricingResult | undefined = locked ? detail.data?.result ?? undefined : preview.data
  const hardware = book.data?.hardware ?? []
  const quantity = (code: string) => terms.hardware.find((x) => x.code === code)?.quantity ?? 0
  const setQuantity = (code: string, value: number) =>
    setTerms({ hardware: [...terms.hardware.filter((x) => x.code !== code), ...(value > 0 ? [{ code, quantity: value }] : [])] })

  const submit = async () => {
    try {
      const saved = await save.mutateAsync({ id: id ?? undefined, quote: form })
      setDirty(false)
      toast.success(id ? 'Quote saved' : `Quote ${saved.quote.quoteNumber} created`)
      if (!id) onOpen(saved.quote.id)
    } catch { /* shown by the API client */ }
  }
  const mark = async (status: QuoteStatus) => {
    if (!id) return
    try { await setStatus.mutateAsync({ id, status }); toast.success(`Marked ${status.toLowerCase()}`) } catch { /* shown */ }
  }

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex items-center gap-3">
          <button type="button" className={secondaryButton} onClick={onClose}><ArrowLeft className="h-4 w-4" />Quotes</button>
          <h1 className="text-xl font-semibold text-slate-900">{quote ? quote.quoteNumber : 'New quote'}</h1>
          {quote ? <StatusBadge status={quote.status} /> : null}
          {quote?.expired ? <span className="text-sm font-medium text-rose-700">Expired {formatDate(quote.validUntil)}</span> : null}
        </div>
        <div className="flex flex-wrap gap-2">
          {quote ? <button type="button" className={secondaryButton} disabled={dirty} title={dirty ? 'Save first' : undefined} onClick={() => void downloadQuotePdf(quote.id, quote.quoteNumber)}><Download className="h-4 w-4" />PDF</button> : null}
          {quote ? <button type="button" className={secondaryButton} onClick={() => void copy.mutateAsync(quote.id).then((c) => { toast.success(`Copied to ${c.quote.quoteNumber}`); onOpen(c.quote.id) }).catch(() => undefined)}><Copy className="h-4 w-4" />Copy</button> : null}
          {quote?.status === 'Draft' ? <button type="button" className={dangerButton} onClick={() => { if (window.confirm(`Delete ${quote.quoteNumber}?`)) void remove.mutateAsync(quote.id).then(onClose).catch(() => undefined) }}><Trash2 className="h-4 w-4" />Delete</button> : null}
          {!locked ? <button type="button" className={primaryButton} disabled={save.isPending || !form.customerName.trim() || !valid} onClick={() => void submit()}><Save className="h-4 w-4" />{id ? 'Save' : 'Create quote'}</button> : null}
        </div>
      </div>

      {quote ? (
        <div className="flex flex-wrap items-center gap-2 rounded-xl border border-slate-200 bg-white p-3 text-sm">
          <span className="text-slate-600">Status:</span>
          {quote.status === 'Draft' ? <button type="button" className={secondaryButton} disabled={dirty} onClick={() => void mark('Sent')}>Mark sent</button> : null}
          {quote.status === 'Draft' || quote.status === 'Sent' ? (
            <>
              <button type="button" className={secondaryButton} disabled={dirty} onClick={() => void mark('Accepted')}>Accepted</button>
              <button type="button" className={secondaryButton} disabled={dirty} onClick={() => void mark('Declined')}>Declined</button>
            </>
          ) : null}
          {locked ? <span className="text-slate-500">Kept as it was when {quote.status.toLowerCase()}. Copy it to change the terms.</span> : null}
          {quote.status === 'Accepted' ? (
            quote.tenantId ? (
              <button type="button" className={primaryButton} disabled={apply.isPending}
                onClick={() => { if (window.confirm(`Bill ${quote.tenantName} per factory from this quote? Its edition, Visitors/Security and user limits are set to match.`)) void apply.mutateAsync(quote.id).then(() => toast.success('Applied to the workspace')).catch(() => undefined) }}>
                <Building2 className="h-4 w-4" />Apply to {quote.tenantName}
              </button>
            ) : <span className="text-slate-500">Link it to a workspace (copy, then choose one) to bill from it.</span>
          ) : null}
          {quote.status === 'Accepted' ? (
            <>
              {!invoices.some((x) => x.purpose === 'YearOne') ? <button type="button" className={secondaryButton} disabled={invoice.isPending} onClick={() => raise('YearOne')}><FileText className="h-4 w-4" />Invoice year one</button> : null}
              {detail.data?.result?.pilot && !invoices.some((x) => x.purpose === 'Pilot') ? <button type="button" className={secondaryButton} disabled={invoice.isPending} onClick={() => raise('Pilot')}><FileText className="h-4 w-4" />Invoice the pilot</button> : null}
            </>
          ) : null}
          {invoices.map((x) => (
            <Link key={x.id} to="/platform/finance" className="inline-flex items-center gap-1 rounded-lg bg-slate-50 px-3 py-2 text-xs text-slate-700 hover:bg-slate-100">
              <FileText className="h-3.5 w-3.5" />{x.purpose === 'Pilot' ? 'Pilot' : 'Year one'}: {x.invoiceNumber} · {x.status} · {money(x.total)}
            </Link>
          ))}
        </div>
      ) : null}

      <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_minmax(0,1.1fr)]">
        <fieldset disabled={locked} className="space-y-4 rounded-2xl border border-slate-200 bg-white p-4">
          <h2 className="text-sm font-semibold text-slate-900">Customer</h2>
          <Field label="Customer" required><input className={inputClass} value={form.customerName} onChange={(e) => set({ customerName: e.target.value })} placeholder="e.g. Chelal Tea Factory" /></Field>
          <div className="grid gap-3 sm:grid-cols-2">
            <Field label="Contact"><input className={inputClass} value={form.contactName ?? ''} onChange={(e) => set({ contactName: e.target.value || null })} /></Field>
            <Field label="Contact email"><input className={inputClass} type="email" value={form.contactEmail ?? ''} onChange={(e) => set({ contactEmail: e.target.value || null })} /></Field>
            <Field label="Workspace" hint="When they already have one; needed to bill from the quote.">
              <select className={inputClass} value={form.tenantId ?? ''} onChange={(e) => set({ tenantId: e.target.value || null })}>
                <option value="">Not yet a customer</option>
                {(tenants.data ?? []).map((x) => <option key={x.tenantId} value={x.tenantId}>{x.name}</option>)}
              </select>
            </Field>
            <Field label="Valid until" hint={`Default ${book.data?.quoteValidDays ?? 30} days.`}>
              <input className={inputClass} type="date" value={(form.validUntil ?? quote?.validUntil ?? '').slice(0, 10)} onChange={(e) => set({ validUntil: e.target.value || null })} />
            </Field>
          </div>

          <h2 className="pt-2 text-sm font-semibold text-slate-900">What is included</h2>
          <div className="grid gap-3 sm:grid-cols-2">
            <Field label="Maintenance edition">
              <select className={inputClass} value={terms.edition ?? ''} onChange={(e) => setTerms({ edition: e.target.value || null })}>
                <option value="">None (Visitors / Security only)</option>
                {(book.data?.editions ?? []).map((x) => <option key={x.code} value={x.code}>{EDITION_LABEL[x.code] ?? x.name} · {money(x.factoryAnnualPrice)}</option>)}
              </select>
            </Field>
            <Field label="Factories" required>
              <input className={inputClass} type="number" min={1} value={terms.factories} onChange={(e) => setTerms({ factories: Math.max(0, Number(e.target.value) || 0) })} />
            </Field>
          </div>
          <div className="flex flex-wrap gap-4 text-sm">
            <label className="flex items-center gap-2"><input type="checkbox" checked={terms.visitors} onChange={(e) => setTerms({ visitors: e.target.checked })} />Visitor Management</label>
            <label className="flex items-center gap-2"><input type="checkbox" checked={terms.security} onChange={(e) => setTerms({ security: e.target.checked })} />Security Patrol</label>
          </div>
          {!valid ? <p className="text-sm text-amber-700">Choose an edition or at least one of Visitors and Security, for one or more factories.</p> : null}
          <div className="grid gap-3 sm:grid-cols-2">
            <Field label="Extra office users" hint={`${book.data?.includedOfficeUsersPerFactory ?? 15} per factory are included.`}>
              <input className={inputClass} type="number" min={0} value={terms.extraOfficeUsers} onChange={(e) => setTerms({ extraOfficeUsers: Math.max(0, Number(e.target.value) || 0) })} />
            </Field>
            <Field label="Extra field users" hint={`${book.data?.includedFieldUsersPerFactory ?? 30} per factory are included.`}>
              <input className={inputClass} type="number" min={0} value={terms.extraFieldUsers} onChange={(e) => setTerms({ extraFieldUsers: Math.max(0, Number(e.target.value) || 0) })} />
            </Field>
            <Field label="Implementation per factory" hint={book.data ? `Usually ${money(book.data.implementationFeeMin)}–${money(book.data.implementationFeeMax)}.` : undefined}>
              <input className={inputClass} type="number" min={0} step={5000} value={terms.implementationFeePerFactory ?? ''} onChange={(e) => setTerms({ implementationFeePerFactory: e.target.value === '' ? null : Number(e.target.value) })} />
            </Field>
            <Field label="Agreed discount (%)" hint="Leave empty to use the group discount for the number of factories.">
              <input className={inputClass} type="number" min={0} max={100} value={terms.discountPercentOverride ?? ''} onChange={(e) => setTerms({ discountPercentOverride: e.target.value === '' ? null : Number(e.target.value) })} />
            </Field>
          </div>
          <div className="space-y-2 text-sm">
            <label className="flex items-start gap-2"><input type="checkbox" className="mt-0.5" checked={terms.onPremises} onChange={(e) => setTerms({ onPremises: e.target.checked })} />
              <span>Installed on their own servers<span className="block text-xs text-slate-500">Subscription × {book.data?.onPremisesMultiplier ?? 1.5}, plus {money(book.data?.onPremisesSetupFee ?? 0)} set-up.</span></span></label>
            <label className="flex items-start gap-2"><input type="checkbox" className="mt-0.5" checked={terms.includePilot} onChange={(e) => setTerms({ includePilot: e.target.checked })} />
              <span>Offer a paid pilot<span className="block text-xs text-slate-500">One factory for {book.data?.pilotMonths ?? 3} months at {money(book.data?.pilotFee ?? 0)}{book.data?.pilotCreditedToYearOne ? ', taken off year one if they continue' : ''}.</span></span></label>
          </div>

          {hardware.length > 0 ? (
            <>
              <h2 className="pt-2 text-sm font-semibold text-slate-900">Hardware</h2>
              <div className="divide-y divide-slate-100 rounded-lg border border-slate-200">
                {hardware.map((item) => (
                  <div key={item.code} className="flex items-center justify-between gap-3 px-3 py-2 text-sm">
                    <div className="min-w-0"><div className="text-slate-800">{item.name}</div><div className="text-xs text-slate-500">{money(item.unitPrice ?? item.unitCost)} {item.unit}</div></div>
                    <input className={cn(inputClass, 'w-24 text-right')} type="number" min={0} aria-label={`${item.name} quantity`} value={quantity(item.code) || ''} placeholder="0"
                      onChange={(e) => setQuantity(item.code, Math.max(0, Number(e.target.value) || 0))} />
                  </div>
                ))}
              </div>
            </>
          ) : null}

          <Field label="Notes on the quote"><textarea className={textareaClass} value={form.notes ?? ''} onChange={(e) => set({ notes: e.target.value || null })} /></Field>
        </fieldset>

        <Totals result={shown} loading={valid && preview.isFetching && !shown} error={valid && preview.isError ? 'The totals could not be worked out; check the terms.' : null} dirty={dirty && Boolean(quote)} />
      </div>
    </div>
  )
}

function Totals({ result, loading, error, dirty }: { result: PricingResult | undefined; loading: boolean; error: string | null; dirty: boolean }) {
  if (loading) return <div className="rounded-2xl border border-slate-200 bg-white p-4"><Spinner label="Working out the totals" /></div>
  if (!result) return <div className="rounded-2xl border border-dashed border-slate-300 p-6 text-sm text-slate-500">{error ?? 'The totals appear here as you fill in the terms.'}</div>
  const c = result.currency
  const recurring = result.lines.filter((x) => x.kind === 'Subscription' || x.kind === 'Discount')
  const oneOff = result.lines.filter((x) => x.kind === 'OneOff' || x.kind === 'Hardware')
  return (
    <section className="space-y-4 self-start rounded-2xl border border-slate-200 bg-white p-4 lg:sticky lg:top-4" aria-label="Totals">
      <div className="grid grid-cols-2 gap-3">
        <Big label="Per year" value={money(result.annualSubscription, c)} sub={`${money(result.monthlyEquivalent, c)} a month`} />
        <Big label="Per factory per month" value={money(result.perFactoryPerMonth, c)} sub={result.discountPercent > 0 ? `after ${result.discountPercent}% discount` : 'list price'} />
      </div>
      <Lines title="Yearly subscription" lines={recurring} total={result.annualSubscription} currency={c} />
      {oneOff.length > 0 ? <Lines title="One-off" lines={oneOff} total={result.oneOffTotal} currency={c} /> : null}
      <div className="space-y-1 rounded-xl bg-slate-50 p-3 text-sm">
        <Row label="Year one, excluding VAT" value={money(result.yearOneTotal, c)} />
        <Row label={`VAT ${result.vatPercent}%`} value={money(result.yearOneVat, c)} />
        <Row label="Year one, including VAT" value={money(result.yearOneTotalWithVat, c)} strong />
        <p className="pt-1 text-xs text-slate-500">From year two: {money(result.annualSubscription, c)} a year plus VAT. Includes {result.includedOfficeUsers} office and {result.includedFieldUsers} field users.</p>
      </div>
      {result.pilot ? (
        <div className="rounded-xl border border-[var(--app-primary)] p-3 text-sm">
          <div className="font-medium text-slate-900">Pilot: {money(result.pilot.fee, c)} plus VAT</div>
          <div className="text-xs text-slate-600">One factory, {result.pilot.months} months{result.pilot.creditedToYearOne ? '; taken off year one if they continue' : ''}.</div>
        </div>
      ) : null}
      {result.warnings.map((w) => <p key={w} className="flex gap-2 text-sm text-amber-700"><AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />{w}</p>)}
      {dirty ? <p className="text-xs text-slate-500">Unsaved changes: save to update the quote and its PDF.</p> : null}
    </section>
  )
}

function Big({ label, value, sub }: { label: string; value: string; sub: string }) {
  return (
    <div className="rounded-xl border border-slate-200 p-3">
      <div className="text-xs font-medium uppercase tracking-wide text-slate-500">{label}</div>
      <div className="mt-1 text-xl font-semibold tabular-nums text-slate-900">{value}</div>
      <div className="text-xs text-slate-500">{sub}</div>
    </div>
  )
}

function Lines({ title, lines, total, currency }: { title: string; lines: PricingResult['lines']; total: number; currency: string }) {
  return (
    <div>
      <h3 className="mb-1 text-sm font-semibold text-slate-900">{title}</h3>
      <table className="w-full text-sm">
        <tbody className="divide-y divide-slate-100">
          {lines.map((x, i) => (
            <tr key={i}>
              <td className={cn('py-1.5 pr-2', x.kind === 'Discount' && 'text-emerald-700')}>{x.description}{x.kind !== 'Discount' && x.quantity !== 1 ? <span className="text-xs text-slate-500"> · {x.quantity} × {money(x.unitPrice, currency)}</span> : null}</td>
              <td className={cn('whitespace-nowrap py-1.5 pl-2 text-right align-top tabular-nums', x.kind === 'Discount' && 'text-emerald-700')}>{money(x.amount, currency)}</td>
            </tr>
          ))}
          <tr><td className="py-1.5 font-medium">Total</td><td className="whitespace-nowrap py-1.5 pl-2 text-right font-medium tabular-nums">{money(total, currency)}</td></tr>
        </tbody>
      </table>
    </div>
  )
}

function Row({ label, value, strong }: { label: string; value: string; strong?: boolean }) {
  return <div className={cn('flex justify-between', strong && 'font-semibold text-slate-900')}><span>{label}</span><span className="tabular-nums">{value}</span></div>
}
