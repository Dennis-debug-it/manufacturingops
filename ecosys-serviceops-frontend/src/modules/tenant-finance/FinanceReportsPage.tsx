import { useState } from 'react'
import { ScheduledReportsPage } from './ScheduledReportsPage'
import { EmailStatementDrawer } from './EmailStatementDrawer'
import {
  Bar,
  BarChart,
  CartesianGrid,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts'
import { ErrorState } from '../../components/ui/ErrorState'
import { LoadingState } from '../../components/ui/LoadingState'
import { SectionCard } from '../../components/ui/Workspace'
import { useAsyncData } from '../../hooks/useAsyncData'
import { clientService } from '../../services/clientService'
import { financeReportsService } from '../../services/financeReportsService'
import type { AgingRow, ClientRecord, ClientStatementResponse, RevenueReportRow } from '../../types/api'
import { formatDateOnly } from '../../utils/date'

type ReportTab = 'revenue' | 'aging' | 'statement' | 'scheduled'

// ── Revenue Report ────────────────────────────────────────────────────────────

function RevenueReport() {
  const [from, setFrom] = useState(() => `${new Date().getFullYear()}-01-01`)
  const [to, setTo] = useState(() => new Date().toISOString().slice(0, 10))
  const [groupBy, setGroupBy] = useState<'client' | 'month'>('month')

  const { data, loading, error } = useAsyncData<RevenueReportRow[]>(
    (signal) => financeReportsService.getRevenueReport({ from, to, groupBy, signal }),
    [],
    [from, to, groupBy],
  )

  const currency = 'KES'

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap gap-3">
        <label className="block space-y-1">
          <span className="text-xs text-muted">From</span>
          <input type="date" value={from} onChange={(e) => setFrom(e.target.value)} className="field-input py-1 text-sm" />
        </label>
        <label className="block space-y-1">
          <span className="text-xs text-muted">To</span>
          <input type="date" value={to} onChange={(e) => setTo(e.target.value)} className="field-input py-1 text-sm" />
        </label>
        <label className="block space-y-1">
          <span className="text-xs text-muted">Group by</span>
          <select value={groupBy} onChange={(e) => setGroupBy(e.target.value as 'client' | 'month')} className="field-input py-1 text-sm">
            <option value="month">Month</option>
            <option value="client">Client</option>
          </select>
        </label>
      </div>

      {loading ? <LoadingState label="Loading revenue report" /> : null}
      {!loading && error ? <ErrorState title="Unable to load revenue report" description={error} /> : null}
      {!loading && !error ? (
        <>
          <div className="h-64">
            <ResponsiveContainer width="100%" height="100%">
              <BarChart data={data} margin={{ top: 4, right: 16, left: 0, bottom: 4 }}>
                <CartesianGrid strokeDasharray="3 3" />
                <XAxis dataKey="group" tick={{ fontSize: 11 }} />
                <YAxis tick={{ fontSize: 11 }} />
                <Tooltip formatter={(v) => `${currency} ${(v as number).toLocaleString()}`} />
                <Bar dataKey="invoiced" name="Invoiced" fill="#6366f1" radius={[4, 4, 0, 0]} />
                <Bar dataKey="collected" name="Collected" fill="#22c55e" radius={[4, 4, 0, 0]} />
              </BarChart>
            </ResponsiveContainer>
          </div>
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-border text-left text-xs text-muted">
                  <th className="py-2 pr-4">Group</th>
                  <th className="py-2 pr-4 text-right">Invoiced</th>
                  <th className="py-2 pr-4 text-right">Collected</th>
                  <th className="py-2 pr-4 text-right">Outstanding</th>
                  <th className="py-2 text-right">Count</th>
                </tr>
              </thead>
              <tbody>
                {data.map((row) => (
                  <tr key={row.group} className="border-b border-border/50">
                    <td className="py-2 pr-4 font-medium">{row.group}</td>
                    <td className="py-2 pr-4 text-right">{currency} {row.invoiced.toLocaleString(undefined, { minimumFractionDigits: 2 })}</td>
                    <td className="py-2 pr-4 text-right text-success">{currency} {row.collected.toLocaleString(undefined, { minimumFractionDigits: 2 })}</td>
                    <td className="py-2 pr-4 text-right text-warning">{currency} {row.outstanding.toLocaleString(undefined, { minimumFractionDigits: 2 })}</td>
                    <td className="py-2 text-right">{row.count}</td>
                  </tr>
                ))}
                {data.length === 0 ? (
                  <tr><td colSpan={5} className="py-6 text-center text-sm text-muted">No data for selected range.</td></tr>
                ) : null}
              </tbody>
            </table>
          </div>
        </>
      ) : null}
    </div>
  )
}

// ── Aging Analysis ────────────────────────────────────────────────────────────

function agingCellClass(amount: number, severity: 'low' | 'medium' | 'high' | 'critical'): string {
  if (amount === 0) return 'text-muted'
  const base = 'font-semibold'
  switch (severity) {
    case 'low': return `${base} text-success`
    case 'medium': return `${base} text-warning`
    case 'high': return `${base} text-orange-500`
    case 'critical': return `${base} text-danger`
  }
}

function AgingAnalysis() {
  const { data, loading, error } = useAsyncData<AgingRow[]>(
    (signal) => financeReportsService.getAgingReport(signal),
    [],
    [],
  )

  const [reminderTarget, setReminderTarget] = useState<AgingRow | null>(null)

  const totals = data.reduce(
    (acc, r) => ({
      current: acc.current + r.current,
      days1To30: acc.days1To30 + r.days1To30,
      days31To60: acc.days31To60 + r.days31To60,
      days61To90: acc.days61To90 + r.days61To90,
      days90Plus: acc.days90Plus + r.days90Plus,
      total: acc.total + r.total,
    }),
    { current: 0, days1To30: 0, days31To60: 0, days61To90: 0, days90Plus: 0, total: 0 },
  )

  const currency = 'KES'
  const fmt = (v: number) => (v === 0 ? '—' : `${currency} ${v.toLocaleString(undefined, { minimumFractionDigits: 2 })}`)

  if (loading) return <LoadingState label="Loading aging report" />
  if (error) return <ErrorState title="Unable to load aging report" description={error} />

  return (
    <>
      <div className="overflow-x-auto">
        <table className="w-full text-sm">
          <thead>
            <tr className="border-b border-border text-left text-xs text-muted">
              <th className="py-2 pr-4">Client</th>
              <th className="py-2 pr-4 text-right">Current</th>
              <th className="py-2 pr-4 text-right">1–30 days</th>
              <th className="py-2 pr-4 text-right">31–60 days</th>
              <th className="py-2 pr-4 text-right">61–90 days</th>
              <th className="py-2 pr-4 text-right">90+ days</th>
              <th className="py-2 pr-4 text-right">Total</th>
              <th className="py-2" />
            </tr>
          </thead>
          <tbody>
            {data.map((row) => (
              <tr key={row.clientId} className="border-b border-border/50">
                <td className="py-2 pr-4 font-medium">{row.clientName}</td>
                <td className={`py-2 pr-4 text-right ${agingCellClass(row.current, 'low')}`}>{fmt(row.current)}</td>
                <td className={`py-2 pr-4 text-right ${agingCellClass(row.days1To30, 'medium')}`}>{fmt(row.days1To30)}</td>
                <td className={`py-2 pr-4 text-right ${agingCellClass(row.days31To60, 'high')}`}>{fmt(row.days31To60)}</td>
                <td className={`py-2 pr-4 text-right ${agingCellClass(row.days61To90, 'critical')}`}>{fmt(row.days61To90)}</td>
                <td className={`py-2 pr-4 text-right ${agingCellClass(row.days90Plus, 'critical')}`}>{fmt(row.days90Plus)}</td>
                <td className="py-2 pr-4 text-right font-bold">{fmt(row.total)}</td>
                <td className="py-2">
                  <button
                    type="button"
                    className="button-secondary py-1 px-2 text-xs"
                    onClick={() => setReminderTarget(row)}
                  >
                    Send Reminder
                  </button>
                </td>
              </tr>
            ))}
            {data.length === 0 ? (
              <tr><td colSpan={8} className="py-6 text-center text-sm text-muted">No outstanding balances.</td></tr>
            ) : null}
          </tbody>
          {data.length > 0 ? (
            <tfoot>
              <tr className="border-t-2 border-border font-bold text-sm">
                <td className="py-2 pr-4">TOTAL</td>
                <td className="py-2 pr-4 text-right">{fmt(totals.current)}</td>
                <td className="py-2 pr-4 text-right">{fmt(totals.days1To30)}</td>
                <td className="py-2 pr-4 text-right">{fmt(totals.days31To60)}</td>
                <td className="py-2 pr-4 text-right">{fmt(totals.days61To90)}</td>
                <td className="py-2 pr-4 text-right">{fmt(totals.days90Plus)}</td>
                <td className="py-2 pr-4 text-right">{fmt(totals.total)}</td>
                <td />
              </tr>
            </tfoot>
          ) : null}
        </table>
      </div>

      {reminderTarget ? (
        <EmailStatementDrawer
          open={!!reminderTarget}
          onClose={() => setReminderTarget(null)}
          clientId={reminderTarget.clientId}
          clientName={reminderTarget.clientName}
          defaultOverdueOnly
        />
      ) : null}
    </>
  )
}

// ── Client Statement ──────────────────────────────────────────────────────────

function ClientStatement() {
  const [clientId, setClientId] = useState('')
  const [from, setFrom] = useState(() => `${new Date().getFullYear()}-01-01`)
  const [to, setTo] = useState(() => new Date().toISOString().slice(0, 10))
  const [fetching, setFetching] = useState(false)
  const [statement, setStatement] = useState<ClientStatementResponse | null>(null)
  const [fetchError, setFetchError] = useState<string | null>(null)

  const { data: clients } = useAsyncData<ClientRecord[]>(
    (signal) => clientService.list({ signal, status: 'all' }),
    [],
    [],
  )

  async function fetchStatement() {
    if (!clientId) return
    setFetching(true)
    setFetchError(null)
    try {
      const result = await financeReportsService.getClientStatement({ clientId, from, to })
      setStatement(result)
    } catch (err) {
      setFetchError(err instanceof Error ? err.message : 'Failed to load statement.')
    } finally {
      setFetching(false)
    }
  }

  const currency = 'KES'
  const fmt = (v: number) => (v === 0 ? '—' : `${currency} ${v.toLocaleString(undefined, { minimumFractionDigits: 2 })}`)

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-end gap-3">
        <label className="block space-y-1">
          <span className="text-xs text-muted">Client</span>
          <select value={clientId} onChange={(e) => setClientId(e.target.value)} className="field-input w-52 py-1 text-sm">
            <option value="">Select client</option>
            {clients.map((c) => <option key={c.id} value={c.id}>{c.clientName}</option>)}
          </select>
        </label>
        <label className="block space-y-1">
          <span className="text-xs text-muted">From</span>
          <input type="date" value={from} onChange={(e) => setFrom(e.target.value)} className="field-input py-1 text-sm" />
        </label>
        <label className="block space-y-1">
          <span className="text-xs text-muted">To</span>
          <input type="date" value={to} onChange={(e) => setTo(e.target.value)} className="field-input py-1 text-sm" />
        </label>
        <button type="button" className="button-primary" onClick={() => void fetchStatement()} disabled={!clientId || fetching}>
          {fetching ? 'Loading…' : 'Generate Statement'}
        </button>
      </div>

      {fetchError ? <ErrorState title="Failed to load statement" description={fetchError} /> : null}

      {statement ? (
        <div className="space-y-4">
          <div className="rounded-xl border border-border bg-surface p-4">
            <h2 className="text-sm font-semibold text-app">{statement.clientName}</h2>
            <p className="text-xs text-muted">{formatDateOnly(statement.from ?? undefined)} – {formatDateOnly(statement.to ?? undefined)}</p>
            <div className="mt-3 flex flex-wrap gap-6 text-sm">
              <div><span className="text-muted">Total Invoiced: </span><span className="font-semibold">{fmt(statement.totalInvoiced)}</span></div>
              <div><span className="text-muted">Total Paid: </span><span className="font-semibold text-success">{fmt(statement.totalPaid)}</span></div>
              <div><span className="text-muted">Balance: </span><span className="font-bold text-danger">{fmt(statement.balance)}</span></div>
            </div>
          </div>
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-border text-left text-xs text-muted">
                  <th className="py-2 pr-4">Date</th>
                  <th className="py-2 pr-4">Reference</th>
                  <th className="py-2 pr-4">Type</th>
                  <th className="py-2 pr-4 text-right">Debit (Invoice)</th>
                  <th className="py-2 text-right">Credit (Payment)</th>
                </tr>
              </thead>
              <tbody>
                {statement.entries.map((entry: { date: string; reference: string; entryType: string; debit: number; credit: number }, idx: number) => (
                  <tr key={idx} className="border-b border-border/50">
                    <td className="py-2 pr-4">{formatDateOnly(entry.date)}</td>
                    <td className="py-2 pr-4 font-medium">{entry.reference}</td>
                    <td className="py-2 pr-4">
                      <span className={`rounded-full px-2 py-0.5 text-xs ${entry.entryType === 'invoice' ? 'bg-info/10 text-info' : 'bg-success/10 text-success'}`}>
                        {entry.entryType}
                      </span>
                    </td>
                    <td className="py-2 pr-4 text-right">{entry.debit > 0 ? fmt(entry.debit) : '—'}</td>
                    <td className="py-2 text-right text-success">{entry.credit > 0 ? fmt(entry.credit) : '—'}</td>
                  </tr>
                ))}
                {statement.entries.length === 0 ? (
                  <tr><td colSpan={5} className="py-6 text-center text-muted text-sm">No transactions in this period.</td></tr>
                ) : null}
              </tbody>
            </table>
          </div>
        </div>
      ) : null}
    </div>
  )
}

// ── Main page ─────────────────────────────────────────────────────────────────

export function FinanceReportsPage() {
  const [tab, setTab] = useState<ReportTab>('revenue')

  const tabs: { id: ReportTab; label: string }[] = [
    { id: 'revenue', label: 'Revenue' },
    { id: 'aging', label: 'Aging Analysis' },
    { id: 'statement', label: 'Client Statement' },
    { id: 'scheduled', label: 'Scheduled' },
  ]

  return (
    <div className="space-y-4">
      <div className="flex gap-1">
        {tabs.map((t) => (
          <button
            key={t.id}
            type="button"
            onClick={() => setTab(t.id)}
            className={`rounded-full px-4 py-1.5 text-sm font-medium transition-colors ${
              tab === t.id ? 'bg-accent text-white' : 'bg-surface-muted text-muted hover:text-app'
            }`}
          >
            {t.label}
          </button>
        ))}
      </div>

      {tab === 'revenue' ? (
        <SectionCard title="Revenue report" description="Invoiced, collected, and outstanding amounts over a date range.">
          <RevenueReport />
        </SectionCard>
      ) : null}

      {tab === 'aging' ? (
        <SectionCard title="Aging analysis" description="Outstanding balances by age bucket per client. Colour: green=current, yellow=1–30d, orange=31–60d, red=61+d.">
          <AgingAnalysis />
        </SectionCard>
      ) : null}

      {tab === 'statement' ? (
        <SectionCard title="Client statement" description="Full transaction statement for a client over a date range.">
          <ClientStatement />
        </SectionCard>
      ) : null}

      {tab === 'scheduled' ? <ScheduledReportsPage /> : null}
    </div>
  )
}
