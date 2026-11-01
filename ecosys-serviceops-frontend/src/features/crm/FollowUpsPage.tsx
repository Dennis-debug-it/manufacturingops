import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { CheckCircle2 } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { StatCard } from '../../shared/components/StatCard'
import { usePermission } from '../../shared/hooks/usePermission'
import { useAuth } from '../../shared/hooks/useAuth'
import { formatDate, formatDateTime } from '../../shared/utils/dates'
import { cn } from '../../shared/utils/cn'
import { formatCurrency } from '../../shared/utils/format'
import { useCompleteActivity, useFollowUps, usePipelines, useQuotesAwaitingAnswer } from './api'
import { CrmNav } from './components/CrmNav'
import { OwnerSelect } from './components/OwnerSelect'
import { activityTypeOptions, daysUntilDeadline, stageName, type SalesFollowUpItem, type SalesQuoteAwaitingAnswer } from './types'

type Bucket = 'overdue' | 'today' | 'week' | 'later'

const buckets: Array<{ key: Bucket; title: string; empty: string }> = [
  { key: 'overdue', title: 'Overdue', empty: 'Nothing overdue.' },
  { key: 'today', title: 'Due today', empty: 'Nothing else due today.' },
  { key: 'week', title: 'Next 7 days', empty: 'Nothing due this week.' },
  { key: 'later', title: 'Later', empty: 'Nothing scheduled further out.' },
]

/** Overdue means its due time has passed, even earlier today; "today" is what is still ahead today. */
export function followUpBucket(dueAt: string, now = new Date()): Bucket {
  if (new Date(dueAt).getTime() < now.getTime()) return 'overdue'
  const days = daysUntilDeadline(dueAt, now)
  if (days <= 0) return 'today'
  return days <= 7 ? 'week' : 'later'
}

/** Every open follow-up across the pipeline, so the day's sales calls live in one list instead of inside each deal. */
export function FollowUpsPage() {
  const canManage = usePermission('crm.manage')
  const { user } = useAuth()
  const [ownerFilter, setOwnerFilter] = useState('')
  const { data, isLoading, isError } = useFollowUps({ ownerUserId: ownerFilter || undefined })
  const completeActivity = useCompleteActivity()
  const { data: pipelines } = usePipelines()
  const { data: quotes } = useQuotesAwaitingAnswer({ ownerUserId: ownerFilter || undefined })

  const grouped = useMemo(() => {
    const groups: Record<Bucket, SalesFollowUpItem[]> = { overdue: [], today: [], week: [], later: [] }
    const now = new Date()
    ;(data ?? []).forEach((item) => groups[followUpBucket(item.dueAt, now)].push(item))
    return groups
  }, [data])

  return (
    <div className="space-y-4">
      <PageHeader title="Follow-ups" subtitle="Calls, meetings and next steps due across every open deal." />

      <CrmNav />

      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-4">
        <StatCard label="Overdue" value={data ? grouped.overdue.length : '—'} tone={grouped.overdue.length > 0 ? 'warning' : 'default'} />
        <StatCard label="Due today" value={data ? grouped.today.length : '—'} />
        <StatCard label="Next 7 days" value={data ? grouped.week.length : '—'} />
        <StatCard label="Quotes awaiting an answer" value={quotes ? quotes.length : '—'} />
      </div>

      <div className="flex flex-col gap-3 rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] p-4 shadow-sm backdrop-blur-[var(--glass-blur-md)] sm:flex-row sm:items-center">
        <OwnerSelect
          aria-label="Filter by deal owner"
          value={ownerFilter}
          onChange={setOwnerFilter}
          emptyLabel="All owners"
          className="h-10 rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]"
        />
        {user && ownerFilter !== user.userId ? (
          <button type="button" onClick={() => setOwnerFilter(user.userId)} className="inline-flex h-10 items-center self-start rounded-lg border border-slate-200 px-3 text-sm font-medium text-slate-600 transition hover:bg-slate-50 sm:self-auto">
            Mine
          </button>
        ) : null}
      </div>

      {quotes && quotes.length > 0 ? <QuotesAwaitingAnswer quotes={quotes} /> : null}

      {isLoading ? <div className="py-10 text-center text-sm text-slate-400">Loading…</div> : null}
      {isError ? <div className="rounded-xl border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-900">Follow-ups could not be loaded.</div> : null}
      {data && data.length === 0 ? (
        <div className="rounded-[var(--radius-card)] border border-slate-200 bg-white px-4 py-10 text-center text-sm text-slate-500">
          No open follow-ups. Add one from a deal's activity log with a due date.
        </div>
      ) : null}

      {data && data.length > 0
        ? buckets.map((bucket) => {
            const items = grouped[bucket.key]
            // Empty upcoming groups add nothing; an empty Overdue is worth saying.
            if (items.length === 0 && bucket.key !== 'overdue') return null
            return (
              <section key={bucket.key} className="rounded-[var(--radius-card)] border border-slate-200 bg-white">
                <header className="flex items-center justify-between border-b border-slate-200 px-4 py-2.5">
                  <h2 className={cn('text-sm font-semibold', bucket.key === 'overdue' && items.length > 0 ? 'text-amber-700' : 'text-slate-900')}>
                    {bucket.title} <span className="font-normal text-slate-500">({items.length})</span>
                  </h2>
                </header>
                {items.length === 0 ? <div className="px-4 py-4 text-sm text-slate-500">{bucket.empty}</div> : null}
                <ul className="divide-y divide-slate-100">
                  {items.map((item) => (
                    <li key={item.id} className="flex flex-col gap-2 px-4 py-3 sm:flex-row sm:items-start sm:justify-between">
                      <div className="min-w-0">
                        <div className="text-sm text-slate-800">
                          <span className="mr-2 text-xs font-semibold uppercase tracking-wide text-[var(--app-primary)]">
                            {activityTypeOptions.find((option) => option.value === item.type)?.label ?? item.type}
                          </span>
                          {item.summary}
                        </div>
                        <div className="mt-1 text-xs text-slate-500">
                          <Link to={`/crm/opportunities/${item.opportunityId}`} className="font-medium text-slate-700 underline decoration-slate-300 underline-offset-4">
                            {item.opportunityTitle}
                          </Link>
                          {item.partyName ? ` · ${item.partyName}` : ''} · {stageName(item.opportunityStage, pipelines)}
                          {item.ownerName ? ` · Owner: ${item.ownerName}` : ''}
                        </div>
                      </div>
                      <div className="flex shrink-0 items-center gap-3">
                        <span className={cn('whitespace-nowrap text-xs', bucket.key === 'overdue' ? 'font-medium text-amber-700' : 'text-slate-500')}>
                          {bucket.key === 'later' ? formatDate(item.dueAt) : formatDateTime(item.dueAt)}
                        </span>
                        {canManage ? (
                          <button
                            type="button"
                            disabled={completeActivity.isPending && completeActivity.variables === item.id}
                            onClick={() => completeActivity.mutate(item.id)}
                            className="inline-flex h-8 items-center gap-1 rounded-md border border-slate-200 px-2.5 text-xs font-medium text-slate-600 transition hover:bg-slate-50 disabled:opacity-60"
                          >
                            <CheckCircle2 className="h-3.5 w-3.5" />Done
                          </button>
                        ) : null}
                      </div>
                    </li>
                  ))}
                </ul>
              </section>
            )
          })
        : null}
    </div>
  )
}

/** Days between two instants, rounded down; negative once `to` is in the past. */
function wholeDaysBetween(from: Date, to: Date) {
  return Math.floor((to.getTime() - from.getTime()) / 86_400_000)
}

/**
 * Quotes sent and not yet answered: chase them before they expire. The quote itself is a finance
 * document, so its number only links through for people who can open finance.
 */
function QuotesAwaitingAnswer({ quotes }: { quotes: SalesQuoteAwaitingAnswer[] }) {
  const canOpenQuotes = usePermission('finance.view')
  const now = new Date()
  return (
    <section className="rounded-[var(--radius-card)] border border-slate-200 bg-white">
      <header className="border-b border-slate-200 px-4 py-2.5">
        <h2 className="text-sm font-semibold text-slate-900">
          Quotes awaiting an answer <span className="font-normal text-slate-500">({quotes.length})</span>
        </h2>
      </header>
      <ul className="divide-y divide-slate-100">
        {quotes.map((quote) => {
          const waiting = Math.max(0, wholeDaysBetween(new Date(quote.sentAt), now))
          const expiresIn = daysUntilDeadline(quote.validUntil, now)
          return (
            <li key={quote.id} className="flex flex-col gap-2 px-4 py-3 sm:flex-row sm:items-start sm:justify-between">
              <div className="min-w-0">
                <div className="text-sm text-slate-800">
                  {canOpenQuotes ? (
                    <Link to={`/finance/quotes/${quote.id}`} className="mr-2 font-mono text-slate-900 underline decoration-slate-300 underline-offset-4">{quote.quoteNumber}</Link>
                  ) : (
                    <span className="mr-2 font-mono text-slate-900">{quote.quoteNumber}</span>
                  )}
                  {quote.subject ?? quote.clientName ?? ''}
                </div>
                <div className="mt-1 text-xs text-slate-500">
                  {quote.opportunityId ? (
                    <Link to={`/crm/opportunities/${quote.opportunityId}`} className="font-medium text-slate-700 underline decoration-slate-300 underline-offset-4">{quote.opportunityTitle}</Link>
                  ) : (
                    <span>No deal</span>
                  )}
                  {quote.clientName ? ` · ${quote.clientName}` : ''}
                  {quote.ownerName ? ` · ${quote.opportunityId ? 'Owner' : 'Sent by'}: ${quote.ownerName}` : ''}
                </div>
              </div>
              <div className="flex shrink-0 flex-col items-start gap-0.5 text-xs sm:items-end">
                <span className="font-semibold text-slate-900">{formatCurrency(quote.totalAmount, quote.currency)}</span>
                <span className={cn(waiting >= 3 ? 'font-medium text-amber-700' : 'text-slate-500')}>
                  Sent {waiting === 0 ? 'today' : `${waiting} day${waiting === 1 ? '' : 's'} ago`}
                </span>
                <span className={cn(expiresIn <= 2 ? 'font-medium text-rose-700' : 'text-slate-500')}>
                  {expiresIn < 0 ? `Expired ${formatDate(quote.validUntil)}` : expiresIn === 0 ? 'Expires today' : `Expires in ${expiresIn} day${expiresIn === 1 ? '' : 's'}`}
                </span>
              </div>
            </li>
          )
        })}
      </ul>
    </section>
  )
}
