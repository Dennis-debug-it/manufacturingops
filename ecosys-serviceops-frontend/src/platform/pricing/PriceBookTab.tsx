import { useEffect, useState, type ReactNode } from 'react'
import toast from 'react-hot-toast'
import { Plus, Save, Trash2 } from 'lucide-react'
import { Spinner } from '../../shared/components/Spinner'
import { formatDateTime } from '../../shared/utils/dates'
import { cn } from '../../shared/utils/cn'
import { Field, inputClass, primaryButton, secondaryButton, textareaClass } from '../../features/factory/components/ui'
import { EDITION_LABEL, money, usePriceBook, useSavePriceBook, type PriceBook } from './api'

type Draft = Omit<PriceBook, 'updatedAt'>

/** The whole price list in one form; every quote and per-factory licence is worked out from it. */
export function PriceBookTab() {
  const book = usePriceBook()
  const save = useSavePriceBook()
  const [draft, setDraft] = useState<Draft | null>(null)
  useEffect(() => { if (book.data && !draft) setDraft(book.data) }, [book.data, draft])
  if (book.isLoading || !draft) return <Spinner label="Loading the price book" />
  const set = (patch: Partial<Draft>) => setDraft({ ...draft, ...patch })
  const num = (value: string) => (value === '' ? 0 : Number(value))
  const c = draft.currency

  const submit = async () => {
    try {
      const saved = await save.mutateAsync(draft)
      setDraft(saved)
      toast.success('Price book saved')
    } catch { /* shown by the API client */ }
  }

  // A worked example so the effect of a change is visible before saving.
  const example = (factories: number) => {
    const perFactory = (draft.editions.find((x) => x.code === 'professional')?.factoryAnnualPrice ?? 0) + draft.securitySuiteFactoryAnnualPrice
    const discount = draft.discountTiers.filter((x) => factories >= x.minFactories).reduce((max, x) => Math.max(max, x.percent), 0)
    return { yearly: perFactory * factories * (1 - discount / 100), discount }
  }

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <p className="text-sm text-slate-600">Prices exclude VAT. Saved quotes keep the prices they were made with until they are saved again.{book.data ? ` Last saved ${formatDateTime(book.data.updatedAt)}.` : ''}</p>
        <button type="button" className={primaryButton} disabled={save.isPending} onClick={() => void submit()}><Save className="h-4 w-4" />Save price book</button>
      </div>

      <div className="grid gap-4 xl:grid-cols-2">
        <Card title="Subscription per factory per year" hint="The edition, plus Visitors and Security as add-ons. Taken together they are priced as a bundle.">
          <div className="grid gap-3 sm:grid-cols-3">
            {draft.editions.map((edition, i) => (
              <Field key={edition.code} label={`Maintenance ${EDITION_LABEL[edition.code] ?? edition.name}`}>
                <Money currency={c} value={edition.factoryAnnualPrice} onChange={(v) => set({ editions: draft.editions.map((x, j) => (j === i ? { ...x, factoryAnnualPrice: v } : x)) })} />
              </Field>
            ))}
            <Field label="Visitor Management"><Money currency={c} value={draft.visitorsFactoryAnnualPrice} onChange={(v) => set({ visitorsFactoryAnnualPrice: v })} /></Field>
            <Field label="Security Patrol"><Money currency={c} value={draft.securityFactoryAnnualPrice} onChange={(v) => set({ securityFactoryAnnualPrice: v })} /></Field>
            <Field label="Both (bundle)" hint={draft.securitySuiteFactoryAnnualPrice >= draft.visitorsFactoryAnnualPrice + draft.securityFactoryAnnualPrice ? 'Not cheaper than the two apart.' : undefined}>
              <Money currency={c} value={draft.securitySuiteFactoryAnnualPrice} onChange={(v) => set({ securitySuiteFactoryAnnualPrice: v })} />
            </Field>
          </div>
        </Card>

        <Card title="Users included" hint="A ceiling, not a meter: most factories never pay for extra users.">
          <div className="grid gap-3 sm:grid-cols-3">
            <Field label="Office users per factory"><input className={inputClass} type="number" min={1} value={draft.includedOfficeUsersPerFactory} onChange={(e) => set({ includedOfficeUsersPerFactory: num(e.target.value) })} /></Field>
            <Field label="Field users per factory" hint="Technicians and guards."><input className={inputClass} type="number" min={0} value={draft.includedFieldUsersPerFactory} onChange={(e) => set({ includedFieldUsersPerFactory: num(e.target.value) })} /></Field>
            <Field label="Extra user, per month"><Money currency={c} value={draft.extraUserMonthlyPrice} onChange={(v) => set({ extraUserMonthlyPrice: v })} /></Field>
          </div>
        </Card>

        <Card title="Group discount" hint="By number of factories on one contract; the highest tier reached applies.">
          <div className="space-y-2">
            {draft.discountTiers.map((tier, i) => (
              <div key={i} className="flex items-center gap-2 text-sm">
                <span className="text-slate-600">From</span>
                <input className={cn(inputClass, 'w-20')} type="number" min={2} aria-label={`Tier ${i + 1} factories`} value={tier.minFactories}
                  onChange={(e) => set({ discountTiers: draft.discountTiers.map((x, j) => (j === i ? { ...x, minFactories: num(e.target.value) } : x)) })} />
                <span className="text-slate-600">factories:</span>
                <input className={cn(inputClass, 'w-20')} type="number" min={1} max={90} aria-label={`Tier ${i + 1} percent`} value={tier.percent}
                  onChange={(e) => set({ discountTiers: draft.discountTiers.map((x, j) => (j === i ? { ...x, percent: num(e.target.value) } : x)) })} />
                <span className="text-slate-600">% off</span>
                <button type="button" className="ml-auto text-slate-400 hover:text-rose-600" aria-label={`Remove tier ${i + 1}`} onClick={() => set({ discountTiers: draft.discountTiers.filter((_, j) => j !== i) })}><Trash2 className="h-4 w-4" /></button>
              </div>
            ))}
            <button type="button" className={secondaryButton} onClick={() => set({ discountTiers: [...draft.discountTiers, { minFactories: (draft.discountTiers.at(-1)?.minFactories ?? 1) * 2 || 5, percent: (draft.discountTiers.at(-1)?.percent ?? 0) + 5 }] })}>
              <Plus className="h-4 w-4" />Add a tier
            </button>
            <div className="rounded-lg bg-slate-50 p-3 text-xs text-slate-600">
              Professional with Visitors and Security:{' '}
              {[1, 5, 20, 65].map((n) => { const e = example(n); return `${n} ${n === 1 ? 'factory' : 'factories'} ${money(e.yearly, c)}${e.discount ? ` (−${e.discount}%)` : ''}` }).join(' · ')} a year.
            </div>
          </div>
        </Card>

        <Card title="One-off fees">
          <div className="grid gap-3 sm:grid-cols-3">
            <Field label="Implementation, lowest"><Money currency={c} value={draft.implementationFeeMin} onChange={(v) => set({ implementationFeeMin: v })} /></Field>
            <Field label="Usual"><Money currency={c} value={draft.implementationFeeDefault} onChange={(v) => set({ implementationFeeDefault: v })} /></Field>
            <Field label="Highest"><Money currency={c} value={draft.implementationFeeMax} onChange={(v) => set({ implementationFeeMax: v })} /></Field>
          </div>
          <p className="mt-1 text-xs text-slate-500">Per factory: asset survey and data loading, tagging, checkpoints, on-site training.</p>
          <div className="mt-3 grid gap-3 sm:grid-cols-2">
            <Field label="On-premises: subscription ×" hint="For groups that run it on their own servers."><input className={inputClass} type="number" min={1} max={5} step={0.1} value={draft.onPremisesMultiplier} onChange={(e) => set({ onPremisesMultiplier: num(e.target.value) })} /></Field>
            <Field label="On-premises set-up"><Money currency={c} value={draft.onPremisesSetupFee} onChange={(v) => set({ onPremisesSetupFee: v })} /></Field>
          </div>
        </Card>

        <Card title="Pilot" hint="A paid pilot gets taken seriously; agree the success measures and decision date before it starts.">
          <div className="grid gap-3 sm:grid-cols-2">
            <Field label="Months"><input className={inputClass} type="number" min={1} max={12} value={draft.pilotMonths} onChange={(e) => set({ pilotMonths: num(e.target.value) })} /></Field>
            <Field label="Fee (one factory)"><Money currency={c} value={draft.pilotFee} onChange={(v) => set({ pilotFee: v })} /></Field>
          </div>
          <label className="mt-3 flex items-center gap-2 text-sm"><input type="checkbox" checked={draft.pilotCreditedToYearOne} onChange={(e) => set({ pilotCreditedToYearOne: e.target.checked })} />Take the pilot fee off year one when they continue</label>
        </Card>

        <Card title="Quotes and tax">
          <div className="grid gap-3 sm:grid-cols-3">
            <Field label="Currency"><input className={cn(inputClass, 'uppercase')} maxLength={3} value={draft.currency} onChange={(e) => set({ currency: e.target.value.toUpperCase() })} /></Field>
            <Field label="VAT %"><input className={inputClass} type="number" min={0} max={50} value={draft.vatPercent} onChange={(e) => set({ vatPercent: num(e.target.value) })} /></Field>
            <Field label="Quote valid (days)"><input className={inputClass} type="number" min={1} max={365} value={draft.quoteValidDays} onChange={(e) => set({ quoteValidDays: num(e.target.value) })} /></Field>
          </div>
          <Field label="Terms printed on every quote" className="mt-3"><textarea className={textareaClass} value={draft.quoteTerms ?? ''} onChange={(e) => set({ quoteTerms: e.target.value || null })} /></Field>
        </Card>
      </div>

      <Card title="Hardware" hint="Sold at cost plus a margin; the quote shows the price to the customer.">
        <div className="overflow-x-auto">
          <table className="w-full min-w-[720px] text-sm">
            <thead className="text-left text-xs uppercase tracking-wide text-slate-500">
              <tr><th className="py-2 pr-2">Item</th><th className="py-2 pr-2">Code</th><th className="py-2 pr-2">Unit</th><th className="py-2 pr-2">Cost</th><th className="py-2 pr-2">Margin %</th><th className="py-2 pr-2 text-right">Price</th><th /></tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {draft.hardware.map((item, i) => {
                const update = (patch: Partial<typeof item>) => set({ hardware: draft.hardware.map((x, j) => (j === i ? { ...x, ...patch } : x)) })
                return (
                  <tr key={i}>
                    <td className="py-1.5 pr-2"><input className={inputClass} aria-label="Item name" value={item.name} onChange={(e) => update({ name: e.target.value })} /></td>
                    <td className="py-1.5 pr-2"><input className={cn(inputClass, 'w-36 font-mono text-xs')} aria-label="Item code" value={item.code} onChange={(e) => update({ code: e.target.value })} /></td>
                    <td className="py-1.5 pr-2"><input className={cn(inputClass, 'w-20')} aria-label="Unit" value={item.unit} onChange={(e) => update({ unit: e.target.value })} /></td>
                    <td className="py-1.5 pr-2"><input className={cn(inputClass, 'w-28')} type="number" min={0} aria-label="Cost" value={item.unitCost} onChange={(e) => update({ unitCost: num(e.target.value) })} /></td>
                    <td className="py-1.5 pr-2"><input className={cn(inputClass, 'w-20')} type="number" min={0} aria-label="Margin" value={item.marginPercent} onChange={(e) => update({ marginPercent: num(e.target.value) })} /></td>
                    <td className="py-1.5 pr-2 text-right tabular-nums">{money(Math.round(item.unitCost * (1 + item.marginPercent / 100)), c)}</td>
                    <td className="py-1.5 text-right"><button type="button" className="text-slate-400 hover:text-rose-600" aria-label={`Remove ${item.name}`} onClick={() => set({ hardware: draft.hardware.filter((_, j) => j !== i) })}><Trash2 className="h-4 w-4" /></button></td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </div>
        <button type="button" className={cn(secondaryButton, 'mt-2')} onClick={() => set({ hardware: [...draft.hardware, { code: `item-${draft.hardware.length + 1}`, name: '', unit: 'each', unitCost: 0, marginPercent: 30 }] })}>
          <Plus className="h-4 w-4" />Add an item
        </button>
      </Card>
    </div>
  )
}

function Card({ title, hint, children }: { title: string; hint?: string; children: ReactNode }) {
  return (
    <section className="rounded-2xl border border-slate-200 bg-white p-4">
      <h2 className="text-sm font-semibold text-slate-900">{title}</h2>
      {hint ? <p className="mb-3 mt-0.5 text-xs text-slate-500">{hint}</p> : <div className="mb-3" />}
      {children}
    </section>
  )
}

function Money({ value, onChange, currency }: { value: number; onChange: (value: number) => void; currency: string }) {
  return (
    <div className="flex items-center rounded-lg border border-slate-200 bg-white focus-within:border-[var(--app-primary)]">
      <span className="pl-3 text-xs text-slate-500">{currency}</span>
      <input className="h-10 w-full rounded-lg bg-transparent px-2 text-sm text-slate-800 outline-none" type="number" min={0} step={1000} value={value}
        onChange={(e) => onChange(e.target.value === '' ? 0 : Number(e.target.value))} />
    </div>
  )
}
