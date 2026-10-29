import { useEffect, useState } from 'react'
import toast from 'react-hot-toast'
import { PageHeader } from '../../shared/components/PageHeader'
import { Spinner } from '../../shared/components/Spinner'
import { cn } from '../../shared/utils/cn'
import { useSalesSettings, useUpdateSalesSettings } from '../crm/api'
import { OwnerSelect } from '../crm/components/OwnerSelect'
import { AssignmentRulesSection } from './components/AssignmentRulesSection'
import type { DealVisibility, SalesSettings } from '../crm/types'

const visibilityOptions: Array<{ value: DealVisibility; title: string; description: string }> = [
  {
    value: 'All',
    title: 'Everyone sees all deals',
    description: 'Anyone with access to Sales sees the whole pipeline. Suits small teams that share accounts.',
  },
  {
    value: 'Own',
    title: 'People see their own deals',
    description: 'Each person sees the deals, RFPs/RFQs and follow-ups they own. Admins, and anyone given "see all deals" (managers by default), still see everything.',
  },
]

type FormState = Pick<SalesSettings, 'defaultOwnerUserId' | 'dealVisibility' | 'createRenewalDeals' | 'stalledAfterDays'>

export function SalesSettingsPage() {
  const { data, isLoading } = useSalesSettings()
  const update = useUpdateSalesSettings()
  const [form, setForm] = useState<FormState | null>(null)

  useEffect(() => {
    if (data) setForm({ defaultOwnerUserId: data.defaultOwnerUserId, dealVisibility: data.dealVisibility, createRenewalDeals: data.createRenewalDeals, stalledAfterDays: data.stalledAfterDays })
  }, [data])

  if (isLoading || !form) return <Spinner label="Loading sales settings" />

  const save = async () => {
    try {
      await update.mutateAsync(form)
      toast.success('Sales settings saved')
    } catch {
      // The API client shows the error.
    }
  }

  return (
    <div className="space-y-4">
      <PageHeader title="Sales Settings" subtitle="Who sees which deals, and who owns the deals the system opens for you." />

      <section className="space-y-3 rounded-[var(--radius-card)] border border-slate-200 bg-white p-4">
        <h2 className="text-sm font-semibold text-slate-900">Deal visibility</h2>
        <div className="grid gap-3 md:grid-cols-2">
          {visibilityOptions.map((option) => (
            <label
              key={option.value}
              className={cn(
                'flex cursor-pointer gap-3 rounded-xl border p-3 text-sm transition',
                form.dealVisibility === option.value ? 'border-[var(--app-primary)] bg-slate-50' : 'border-slate-200 hover:bg-slate-50',
              )}
            >
              <input
                type="radio"
                name="deal-visibility"
                className="mt-1"
                checked={form.dealVisibility === option.value}
                onChange={() => setForm({ ...form, dealVisibility: option.value })}
              />
              <span>
                <span className="block font-medium text-slate-900">{option.title}</span>
                <span className="mt-1 block text-slate-500">{option.description}</span>
              </span>
            </label>
          ))}
        </div>
        <p className="text-xs text-slate-500">Grant or remove "see all deals" (crm.view_all) for a person or permission group under Users &amp; Access.</p>
      </section>

      <section className="space-y-3 rounded-[var(--radius-card)] border border-slate-200 bg-white p-4">
        <h2 className="text-sm font-semibold text-slate-900">Deals the system opens</h2>
        <p className="text-sm text-slate-500">
          Renewal deals and leads flagged by technicians go to the best owner available: the contract's renewal owner or account manager, then
          whoever owns the client's latest deal, then the first matching lead assignment rule (below), then the default owner, then an admin.
        </p>
        <label className="block max-w-sm text-sm">
          <span className="font-medium text-slate-700">Default owner</span>
          <OwnerSelect
            value={form.defaultOwnerUserId ?? ''}
            onChange={(value) => setForm({ ...form, defaultOwnerUserId: value || null })}
            emptyLabel="Pick automatically (an admin)"
            className="mt-1 h-10 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]"
          />
        </label>
        <label className="flex items-start gap-3 text-sm">
          <input type="checkbox" className="mt-1" checked={form.createRenewalDeals} onChange={(event) => setForm({ ...form, createRenewalDeals: event.target.checked })} />
          <span>
            <span className="block font-medium text-slate-900">Open a renewal deal when a contract nears its end</span>
            <span className="mt-0.5 block text-slate-500">Created when the contract enters its renewal notice period. Renewing the contract wins the deal; letting it lapse marks it lost.</span>
          </span>
        </label>
      </section>

      <section className="space-y-3 rounded-[var(--radius-card)] border border-slate-200 bg-white p-4">
        <h2 className="text-sm font-semibold text-slate-900">Stalled deals</h2>
        <label className="flex flex-wrap items-center gap-2 text-sm text-slate-700">
          Flag an open deal when nothing has been logged on it for
          <input
            type="number"
            min={0}
            max={365}
            value={form.stalledAfterDays}
            onChange={(event) => setForm({ ...form, stalledAfterDays: Math.max(0, Math.min(365, Number(event.target.value) || 0)) })}
            className="h-9 w-20 rounded-lg border border-slate-300 px-2 text-sm outline-none focus:border-[var(--app-primary)]"
            aria-label="Stalled after days"
          />
          days.
        </label>
        <p className="text-xs text-slate-500">The deal is marked on the pipeline and its owner is told once; logging anything on it starts the clock again. 0 turns this off.</p>
      </section>

      <div className="flex justify-end">
        <button type="button" disabled={update.isPending} onClick={() => void save()} className="inline-flex h-11 items-center rounded-lg bg-[var(--app-primary)] px-5 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:opacity-60">
          Save
        </button>
      </div>

      <AssignmentRulesSection />
    </div>
  )
}
