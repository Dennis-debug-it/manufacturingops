import { useSearchParams } from 'react-router-dom'
import { Plus } from 'lucide-react'
import { useState } from 'react'
import { PageHeader } from '../../shared/components/PageHeader'
import { Spinner } from '../../shared/components/Spinner'
import { EmptyState } from '../../shared/components/EmptyState'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { formatDate } from '../../shared/utils/dates'
import { cn } from '../../shared/utils/cn'
import { inputClass, primaryButton } from '../../features/factory/components/ui'
import { QUOTE_STATUSES, money, scopeLabel, useQuotes } from './api'
import { PriceBookTab } from './PriceBookTab'
import { QuoteEditor } from './QuoteEditor'

const TABS = [{ id: 'quotes', label: 'Quotes' }, { id: 'price-book', label: 'Price book' }] as const

/**
 * Factory pricing: the price book (per factory per year, discounts, fees, pilot, hardware) and the
 * quotes worked out from it. A quote opens in place (?quote=new or ?quote=<id>).
 */
export function PricingPage() {
  const [params, setParams] = useSearchParams()
  const tab = params.get('tab') === 'price-book' ? 'price-book' : 'quotes'
  const quote = params.get('quote')
  const open = (id: string | null) => setParams(id ? { quote: id } : { tab: 'quotes' })

  if (quote) return <QuoteEditor id={quote === 'new' ? null : quote} onOpen={open} onClose={() => open(null)} />

  return (
    <div className="space-y-5">
      <PageHeader
        title="Pricing"
        subtitle="Prices per factory per year, group discounts, one-off fees, and quotes to send."
        primaryAction={tab === 'quotes' ? <button type="button" className={primaryButton} onClick={() => open('new')}><Plus className="h-4 w-4" />New quote</button> : undefined}
      />
      <div className="inline-flex rounded-xl border border-slate-200 bg-white p-1" role="tablist">
        {TABS.map((x) => (
          <button key={x.id} type="button" role="tab" aria-selected={tab === x.id} onClick={() => setParams({ tab: x.id })}
            className={cn('rounded-lg px-4 py-2 text-sm font-medium', tab === x.id ? 'bg-[var(--app-primary)] text-white' : 'text-slate-600 hover:bg-slate-50')}>{x.label}</button>
        ))}
      </div>
      {tab === 'quotes' ? <QuotesTab onOpen={open} /> : <PriceBookTab />}
    </div>
  )
}

function QuotesTab({ onOpen }: { onOpen: (id: string) => void }) {
  const [status, setStatus] = useState('')
  const quotes = useQuotes(status)
  const rows = quotes.data ?? []
  const pipeline = rows.filter((x) => x.status === 'Sent' && !x.expired).reduce((sum, x) => sum + x.annualSubscription, 0)
  const won = rows.filter((x) => x.status === 'Accepted').reduce((sum, x) => sum + x.annualSubscription, 0)
  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center gap-3">
        <select className={cn(inputClass, 'w-44')} aria-label="Status" value={status} onChange={(e) => setStatus(e.target.value)}>
          <option value="">All quotes</option>
          {QUOTE_STATUSES.map((x) => <option key={x}>{x}</option>)}
        </select>
        {rows.length > 0 && status === '' ? (
          <span className="text-sm text-slate-600">Sent and open: <span className="font-medium">{money(pipeline)}</span> a year · Accepted: <span className="font-medium">{money(won)}</span> a year</span>
        ) : null}
      </div>
      {quotes.isLoading ? <Spinner /> : rows.length === 0 ? (
        <EmptyState title="No quotes yet" description="Build a quote for a factory or a group: the totals follow the price book, and the PDF is ready to send." />
      ) : (
        <div className="overflow-x-auto rounded-2xl border border-slate-200 bg-white">
          <table className="w-full min-w-[860px] text-sm">
            <thead className="bg-slate-50 text-left text-xs uppercase tracking-wide text-slate-500">
              <tr><th className="px-3 py-2">Quote</th><th className="px-3 py-2">Customer</th><th className="px-3 py-2">Scope</th><th className="px-3 py-2 text-right">Per year</th><th className="px-3 py-2 text-right">Year one</th><th className="px-3 py-2">Status</th><th className="px-3 py-2">Valid until</th></tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {rows.map((x) => (
                <tr key={x.id} className="cursor-pointer hover:bg-slate-50" onClick={() => onOpen(x.id)}>
                  <td className="px-3 py-2 font-mono text-xs"><button type="button" className="text-[var(--app-primary)] hover:underline" onClick={() => onOpen(x.id)}>{x.quoteNumber}</button></td>
                  <td className="px-3 py-2"><div className="font-medium text-slate-900">{x.customerName}</div><div className="text-xs text-slate-500">{x.tenantName ? `Workspace: ${x.tenantName}` : x.contactName ?? ''}</div></td>
                  <td className="px-3 py-2 text-xs text-slate-600">{scopeLabel(x)}<div>{x.factories} {x.factories === 1 ? 'factory' : 'factories'}{x.includePilot ? ' · pilot' : ''}</div></td>
                  <td className="px-3 py-2 text-right tabular-nums">{money(x.annualSubscription)}</td>
                  <td className="px-3 py-2 text-right tabular-nums">{money(x.yearOneTotal)}</td>
                  <td className="px-3 py-2"><StatusBadge status={x.status} /></td>
                  <td className={cn('px-3 py-2 text-xs', x.expired ? 'font-medium text-rose-700' : 'text-slate-600')}>{formatDate(x.validUntil)}{x.expired ? ' · expired' : ''}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}
