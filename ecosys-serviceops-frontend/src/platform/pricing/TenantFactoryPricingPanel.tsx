import { useState } from 'react'
import { Link } from 'react-router-dom'
import toast from 'react-hot-toast'
import { FormDrawer } from '../../shared/components/FormDrawer'
import { cn } from '../../shared/utils/cn'
import { Field, inputClass, primaryButton, secondaryButton } from '../../features/factory/components/ui'
import { money, useTenantPricing, useSaveTenantPricing, type TenantFactoryPricing, type TenantFactoryTerms } from './api'

/**
 * How the tenant is billed: per seat (the original model) or per factory from the price book. Per factory,
 * the user limits follow the factories licensed, and an agreed price per factory can replace the list.
 */
export function TenantFactoryPricingPanel({ tenantId, className }: { tenantId: string; className: string }) {
  const terms = useTenantPricing(tenantId)
  const [editing, setEditing] = useState(false)
  const data = terms.data
  if (!data) return null
  const perFactory = data.pricingBasis === 'PerFactory'
  const factories = data.licensedFactories ?? data.factoriesInWorkspace

  return (
    <section className={className} data-testid="tenant-factory-pricing">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <p className="text-lg font-semibold text-slate-900">Billing basis</p>
          <p className="mt-1 text-sm text-slate-600">
            {perFactory
              ? 'Billed per factory per year from the price book. The seat prices above are not used.'
              : 'Billed per seat (above). Switch to per factory to bill from the price book.'}{' '}
            <Link className="text-[var(--app-primary)] hover:underline" to="/platform/pricing?tab=price-book">Price book</Link>
          </p>
        </div>
        <button type="button" className={secondaryButton} onClick={() => setEditing(true)}>{perFactory ? 'Change terms' : 'Bill per factory'}</button>
      </div>
      <div className="mt-4 grid grid-cols-1 gap-3 md:grid-cols-2 xl:grid-cols-4">
        <Stat label="Basis" value={perFactory ? 'Per factory' : 'Per seat'} />
        <Stat label="Factories" value={perFactory ? `${data.licensedFactories} licensed` : `${data.factoriesInWorkspace} in the workspace`}
          sub={perFactory && data.factoriesInWorkspace > (data.licensedFactories ?? 0) ? `${data.factoriesInWorkspace} set up: more than licensed` : undefined} warn={perFactory && data.factoriesInWorkspace > (data.licensedFactories ?? 0)} />
        <Stat label="Users (office / field)" value={`${data.officeSeatsUsed} / ${data.fieldSeatsUsed} of ${data.officeSeats} / ${data.fieldSeats}`} />
        <Stat label="Monthly value" value={money(data.monthlyValue)} sub={data.billingCycle === 'Annual' ? `${money(data.monthlyValue * 12)} a year` : `${data.billingCycle} billing`} />
      </div>
      {!perFactory && data.perFactoryPrice ? (
        <p className="mt-3 text-sm text-slate-600">
          Per factory, {data.editionName ?? 'this edition'}{data.visitors ? ' + Visitors' : ''}{data.security ? ' + Security' : ''} for {factories} {factories === 1 ? 'factory' : 'factories'} would be{' '}
          <span className="font-medium">{money(data.perFactoryPrice.annualSubscription)}</span> a year ({money(data.perFactoryPrice.monthlyEquivalent)} a month).
        </p>
      ) : null}
      {perFactory && data.factoryAnnualPriceOverride != null ? <p className="mt-3 text-sm text-slate-600">Agreed price: {money(data.factoryAnnualPriceOverride)} per factory per year.</p> : null}
      {data.problem ? <p className="mt-3 text-sm text-amber-700">{data.problem}</p> : null}
      {editing ? <TermsDrawer data={data} onClose={() => setEditing(false)} /> : null}
    </section>
  )
}

function TermsDrawer({ data, onClose }: { data: TenantFactoryPricing; onClose: () => void }) {
  const save = useSaveTenantPricing(data.tenantId)
  const [form, setForm] = useState({
    pricingBasis: data.pricingBasis === 'PerSeat' ? 'PerFactory' : data.pricingBasis,
    licensedFactories: String(data.licensedFactories ?? Math.max(1, data.factoriesInWorkspace)),
    agreed: data.factoryAnnualPriceOverride != null ? String(data.factoryAnnualPriceOverride) : '',
    extraOfficeUsers: String(data.extraOfficeUsers),
    extraFieldUsers: String(data.extraFieldUsers),
  })
  const submit = async (terms: TenantFactoryTerms) => {
    try {
      await save.mutateAsync(terms)
      toast.success(terms.pricingBasis === 'PerFactory' ? 'Now billed per factory' : 'Back to per-seat billing')
      onClose()
    } catch { /* shown by the API client */ }
  }
  const factories = Number(form.licensedFactories) || 0
  return (
    <FormDrawer open title="Per-factory billing" onClose={onClose}
      footer={
        <div className="flex flex-wrap justify-between gap-2">
          {data.pricingBasis === 'PerFactory'
            ? <button type="button" className={secondaryButton} disabled={save.isPending} onClick={() => void submit({ pricingBasis: 'PerSeat', licensedFactories: null, factoryAnnualPriceOverride: null, extraOfficeUsers: 0, extraFieldUsers: 0 })}>Back to per seat</button>
            : <span />}
          <div className="flex gap-2">
            <button type="button" className={secondaryButton} onClick={onClose}>Cancel</button>
            <button type="button" className={primaryButton} disabled={save.isPending || factories < 1} onClick={() => void submit({
              pricingBasis: 'PerFactory', licensedFactories: factories, factoryAnnualPriceOverride: form.agreed === '' ? null : Number(form.agreed),
              extraOfficeUsers: Number(form.extraOfficeUsers) || 0, extraFieldUsers: Number(form.extraFieldUsers) || 0,
            })}>Save</button>
          </div>
        </div>
      }>
      <div className="space-y-4">
        <p className="text-sm text-slate-600">
          {data.editionName ?? 'No edition'}{data.visitors ? ' + Visitor Management' : ''}{data.security ? ' + Security Patrol' : ''}. The workspace has {data.factoriesInWorkspace} {data.factoriesInWorkspace === 1 ? 'factory' : 'factories'} set up.
        </p>
        <div className="grid gap-3 sm:grid-cols-2">
          <Field label="Factories licensed" required><input className={inputClass} type="number" min={1} value={form.licensedFactories} onChange={(e) => setForm({ ...form, licensedFactories: e.target.value })} /></Field>
          <Field label="Agreed price per factory per year" hint="Leave empty for the price book with its group discount.">
            <input className={inputClass} type="number" min={0} step={1000} value={form.agreed} onChange={(e) => setForm({ ...form, agreed: e.target.value })} placeholder="Price book" />
          </Field>
          <Field label="Extra office users"><input className={inputClass} type="number" min={0} value={form.extraOfficeUsers} onChange={(e) => setForm({ ...form, extraOfficeUsers: e.target.value })} /></Field>
          <Field label="Extra field users"><input className={inputClass} type="number" min={0} value={form.extraFieldUsers} onChange={(e) => setForm({ ...form, extraFieldUsers: e.target.value })} /></Field>
        </div>
        <p className="rounded-lg bg-slate-50 p-3 text-sm text-slate-600">
          The user limits become the users included for {factories || '—'} {factories === 1 ? 'factory' : 'factories'} plus the extras, replacing the seat counts. An accepted quote can set all of this in one step from Pricing → Quotes.
        </p>
      </div>
    </FormDrawer>
  )
}

function Stat({ label, value, sub, warn }: { label: string; value: string; sub?: string | undefined; warn?: boolean }) {
  return (
    <div className="rounded-xl border border-slate-200 bg-white/70 p-3">
      <div className="text-xs font-semibold uppercase tracking-[0.16em] text-slate-500">{label}</div>
      <div className="mt-1 text-sm font-semibold text-slate-900">{value}</div>
      {sub ? <div className={cn('text-xs', warn ? 'font-medium text-amber-700' : 'text-slate-500')}>{sub}</div> : null}
    </div>
  )
}
