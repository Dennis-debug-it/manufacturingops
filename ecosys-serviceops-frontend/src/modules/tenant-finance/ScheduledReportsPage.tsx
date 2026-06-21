import { useEffect, useState } from 'react'
import { ErrorState } from '../../components/ui/ErrorState'
import { LoadingState } from '../../components/ui/LoadingState'
import { SectionCard } from '../../components/ui/Workspace'
import { useAsyncData } from '../../hooks/useAsyncData'
import { clientService } from '../../services/clientService'
import { scheduledReportService } from '../../services/scheduledReportService'
import type { ClientRecord, CreateScheduledReportInput, ScheduledReport } from '../../types/api'

// ── Constants ─────────────────────────────────────────────────────────────────

const REPORT_TYPES = [
  { value: 'Revenue', label: 'Revenue Summary' },
  { value: 'Aging', label: 'Aging Analysis' },
  { value: 'ClientStatement', label: 'Client Statement' },
  { value: 'WorkOrderSummary', label: 'Work Order Summary' },
  { value: 'Invoices', label: 'Invoice List' },
]

const FREQUENCIES = [
  { value: 'Daily', label: 'Daily' },
  { value: 'Weekly', label: 'Weekly' },
  { value: 'Monthly', label: 'Monthly' },
  { value: 'Quarterly', label: 'Quarterly' },
]

const DATE_RANGE_PRESETS = [
  { value: 'LastWeek', label: 'Last Week' },
  { value: 'LastMonth', label: 'Last Month' },
  { value: 'LastQuarter', label: 'Last Quarter' },
  { value: 'LastYear', label: 'Last Year' },
  { value: 'YTD', label: 'Year to Date' },
]

const DAYS_OF_WEEK = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday']

// ── Helpers ───────────────────────────────────────────────────────────────────

function formatNextRun(dt: string | null): string {
  if (!dt) return '—'
  try {
    return new Date(dt).toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' })
  } catch {
    return dt
  }
}

function computePreviewNextRun(
  frequency: string,
  dayOfMonth: string,
  dayOfWeek: string,
  timeOfDay: string,
): string {
  const now = new Date()
  const [h, m] = timeOfDay.split(':').map(Number)
  try {
    if (frequency === 'Daily') {
      const d = new Date(now)
      d.setHours(h, m, 0, 0)
      if (d <= now) d.setDate(d.getDate() + 1)
      return d.toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' })
    }
    if (frequency === 'Weekly') {
      const targetDow = DAYS_OF_WEEK.indexOf(dayOfWeek)
      const diff = (targetDow - now.getDay() + 7) % 7 || 7
      const d = new Date(now)
      d.setDate(d.getDate() + diff)
      d.setHours(h, m, 0, 0)
      return d.toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' })
    }
    if (frequency === 'Monthly' || frequency === 'Quarterly') {
      const dom = Math.min(parseInt(dayOfMonth) || 1, 28)
      const d = new Date(now.getFullYear(), now.getMonth(), dom, h, m, 0)
      if (d <= now) d.setMonth(d.getMonth() + (frequency === 'Quarterly' ? 3 : 1))
      return d.toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' })
    }
  } catch {
    // ignore
  }
  return '—'
}

// ── Form drawer ───────────────────────────────────────────────────────────────

interface DrawerProps {
  open: boolean
  editing: ScheduledReport | null
  clients: ClientRecord[]
  currentUserEmail: string
  onClose: () => void
  onSaved: (report: ScheduledReport) => void
}

function ScheduleDrawer({ open, editing, clients, currentUserEmail, onClose, onSaved }: DrawerProps) {
  const [name, setName] = useState('')
  const [reportType, setReportType] = useState('Revenue')
  const [frequency, setFrequency] = useState('Monthly')
  const [dayOfMonth, setDayOfMonth] = useState('1')
  const [dayOfWeek, setDayOfWeek] = useState('Monday')
  const [timeOfDay, setTimeOfDay] = useState('08:00')
  const [dateRangePreset, setDateRangePreset] = useState('LastMonth')
  const [groupBy, setGroupBy] = useState('month')
  const [clientId, setClientId] = useState('')
  const [recipients, setRecipients] = useState(currentUserEmail)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)

  // Populate form when editing changes
  useEffect(() => {
    if (editing) {
      setName(editing.name)
      setReportType(editing.reportType)
      setFrequency(editing.frequency)
      setDayOfMonth(String(editing.dayOfMonth ?? 1))
      setDayOfWeek(editing.dayOfWeek ?? 'Monday')
      setTimeOfDay(editing.timeOfDay)
      setDateRangePreset(editing.dateRangePreset)
      setGroupBy(editing.groupBy ?? 'month')
      setClientId(editing.clientId ?? '')
      setRecipients(editing.recipients)
    } else {
      setName('')
      setReportType('Revenue')
      setFrequency('Monthly')
      setDayOfMonth('1')
      setDayOfWeek('Monday')
      setTimeOfDay('08:00')
      setDateRangePreset('LastMonth')
      setGroupBy('month')
      setClientId('')
      setRecipients(currentUserEmail)
    }
    setError(null)
  }, [editing, open, currentUserEmail])

  const previewDate = computePreviewNextRun(frequency, dayOfMonth, dayOfWeek, timeOfDay)
  const recipientList = recipients.split(',').map((e) => e.trim()).filter(Boolean)

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    if (!name.trim()) { setError('Report name is required.'); return }
    if (!recipients.trim()) { setError('At least one recipient email is required.'); return }

    setSaving(true)
    setError(null)
    try {
      const input: CreateScheduledReportInput = {
        name: name.trim(),
        reportType,
        frequency,
        dayOfMonth: (frequency === 'Monthly' || frequency === 'Quarterly') ? parseInt(dayOfMonth) : null,
        dayOfWeek: frequency === 'Weekly' ? dayOfWeek : null,
        timeOfDay,
        dateRangePreset,
        groupBy: reportType === 'Revenue' ? groupBy : null,
        clientId: reportType === 'ClientStatement' && clientId ? clientId : null,
        recipients: recipients.trim(),
      }
      const saved = editing
        ? await scheduledReportService.update(editing.id, input)
        : await scheduledReportService.create(input)
      onSaved(saved)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to save schedule.')
    } finally {
      setSaving(false)
    }
  }

  if (!open) return null

  return (
    <div className="fixed inset-0 z-50 flex">
      {/* Backdrop */}
      <div className="flex-1 bg-black/40" onClick={onClose} />

      {/* Panel */}
      <aside className="flex h-full w-full max-w-md flex-col overflow-y-auto bg-surface shadow-2xl">
        <header className="flex items-center justify-between border-b border-border px-5 py-4">
          <h2 className="text-base font-semibold text-app">
            {editing ? 'Edit Schedule' : 'New Report Schedule'}
          </h2>
          <button type="button" onClick={onClose} className="text-muted hover:text-app">✕</button>
        </header>

        <form onSubmit={(e) => void handleSubmit(e)} className="flex flex-1 flex-col gap-4 p-5">
          {error ? (
            <p className="rounded-lg bg-danger/10 px-3 py-2 text-sm text-danger">{error}</p>
          ) : null}

          {/* Report Name */}
          <label className="block space-y-1">
            <span className="text-xs font-medium text-muted">Report Name *</span>
            <input
              type="text"
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="e.g. Monthly Revenue Report"
              className="field-input w-full"
              required
            />
          </label>

          {/* Report Type */}
          <label className="block space-y-1">
            <span className="text-xs font-medium text-muted">Report Type *</span>
            <select value={reportType} onChange={(e) => setReportType(e.target.value)} className="field-input w-full">
              {REPORT_TYPES.map((t) => (
                <option key={t.value} value={t.value}>{t.label}</option>
              ))}
            </select>
          </label>

          {/* Date Range */}
          <label className="block space-y-1">
            <span className="text-xs font-medium text-muted">Date Range</span>
            <select value={dateRangePreset} onChange={(e) => setDateRangePreset(e.target.value)} className="field-input w-full">
              {DATE_RANGE_PRESETS.map((p) => (
                <option key={p.value} value={p.value}>{p.label}</option>
              ))}
            </select>
          </label>

          {/* Group By (Revenue only) */}
          {reportType === 'Revenue' ? (
            <label className="block space-y-1">
              <span className="text-xs font-medium text-muted">Group By</span>
              <select value={groupBy} onChange={(e) => setGroupBy(e.target.value)} className="field-input w-full">
                <option value="month">Month</option>
                <option value="client">Client</option>
              </select>
            </label>
          ) : null}

          {/* Client (ClientStatement only) */}
          {reportType === 'ClientStatement' ? (
            <label className="block space-y-1">
              <span className="text-xs font-medium text-muted">Client</span>
              <select value={clientId} onChange={(e) => setClientId(e.target.value)} className="field-input w-full">
                <option value="">All Clients</option>
                {clients.map((c) => (
                  <option key={c.id} value={c.id}>{c.clientName}</option>
                ))}
              </select>
            </label>
          ) : null}

          {/* Frequency */}
          <label className="block space-y-1">
            <span className="text-xs font-medium text-muted">Frequency *</span>
            <select value={frequency} onChange={(e) => setFrequency(e.target.value)} className="field-input w-full">
              {FREQUENCIES.map((f) => (
                <option key={f.value} value={f.value}>{f.label}</option>
              ))}
            </select>
          </label>

          {/* Day of Week (Weekly) */}
          {frequency === 'Weekly' ? (
            <label className="block space-y-1">
              <span className="text-xs font-medium text-muted">Day of Week</span>
              <select value={dayOfWeek} onChange={(e) => setDayOfWeek(e.target.value)} className="field-input w-full">
                {DAYS_OF_WEEK.map((d) => (
                  <option key={d} value={d}>{d}</option>
                ))}
              </select>
            </label>
          ) : null}

          {/* Day of Month (Monthly/Quarterly) */}
          {(frequency === 'Monthly' || frequency === 'Quarterly') ? (
            <label className="block space-y-1">
              <span className="text-xs font-medium text-muted">Day of Month (1–28)</span>
              <input
                type="number"
                min={1}
                max={28}
                value={dayOfMonth}
                onChange={(e) => setDayOfMonth(e.target.value)}
                className="field-input w-full"
              />
            </label>
          ) : null}

          {/* Time */}
          <label className="block space-y-1">
            <span className="text-xs font-medium text-muted">Time (UTC)</span>
            <input
              type="time"
              value={timeOfDay}
              onChange={(e) => setTimeOfDay(e.target.value)}
              className="field-input w-full"
            />
          </label>

          {/* Recipients */}
          <label className="block space-y-1">
            <span className="text-xs font-medium text-muted">Recipients * (comma-separated)</span>
            <input
              type="text"
              value={recipients}
              onChange={(e) => setRecipients(e.target.value)}
              placeholder="alice@example.com, bob@example.com"
              className="field-input w-full"
              required
            />
          </label>

          {/* Preview */}
          <div className="rounded-xl border border-border bg-surface-muted p-3 text-xs text-muted">
            <p>
              <span className="font-medium text-app">Next run:</span>{' '}
              {previewDate !== '—' ? previewDate : 'Configure frequency above'}
            </p>
            <p className="mt-1">
              <span className="font-medium text-app">Recipients:</span>{' '}
              {recipientList.length > 0 ? recipientList.join(', ') : '—'}
            </p>
          </div>

          <div className="flex justify-end gap-2 border-t border-border pt-4">
            <button type="button" onClick={onClose} className="button-ghost">Cancel</button>
            <button type="submit" disabled={saving} className="button-primary">
              {saving ? 'Saving…' : editing ? 'Update Schedule' : 'Create Schedule'}
            </button>
          </div>
        </form>
      </aside>
    </div>
  )
}

// ── Main page ─────────────────────────────────────────────────────────────────

export function ScheduledReportsPage() {
  const { data: reports, loading, error, reload } = useAsyncData<ScheduledReport[]>(
    (signal) => scheduledReportService.list(signal),
    [],
    [],
  )

  const { data: clients } = useAsyncData<ClientRecord[]>(
    (signal) => clientService.list({ signal, status: 'all' }),
    [],
    [],
  )

  const [drawerOpen, setDrawerOpen] = useState(false)
  const [editingReport, setEditingReport] = useState<ScheduledReport | null>(null)
  const [actionError, setActionError] = useState<string | null>(null)
  const [runningId, setRunningId] = useState<string | null>(null)

  // Seed current-user email from local storage once on mount
  const [currentUserEmail, setCurrentUserEmail] = useState('')
  useEffect(() => {
    try {
      const raw = localStorage.getItem('ecosys.serviceops.auth')
      if (raw) {
        const parsed = JSON.parse(raw) as { email?: string }
        setCurrentUserEmail(parsed.email ?? '')
      }
    } catch {
      // ignore
    }
  }, [])

  function openNew() {
    setEditingReport(null)
    setDrawerOpen(true)
    setActionError(null)
  }

  function openEdit(r: ScheduledReport) {
    setEditingReport(r)
    setDrawerOpen(true)
    setActionError(null)
  }

  function handleSaved(_saved: ScheduledReport) {
    setDrawerOpen(false)
    reload()
  }

  async function handleDelete(id: string) {
    if (!confirm('Delete this scheduled report?')) return
    setActionError(null)
    try {
      await scheduledReportService.remove(id)
      reload()
    } catch (err) {
      setActionError(err instanceof Error ? err.message : 'Failed to delete.')
    }
  }

  async function handleToggle(id: string) {
    setActionError(null)
    try {
      await scheduledReportService.toggle(id)
      reload()
    } catch (err) {
      setActionError(err instanceof Error ? err.message : 'Failed to toggle.')
    }
  }

  async function handleRunNow(id: string) {
    setRunningId(id)
    setActionError(null)
    try {
      await scheduledReportService.runNow(id)
      reload()
    } catch (err) {
      setActionError(err instanceof Error ? err.message : 'Failed to trigger report.')
    } finally {
      setRunningId(null)
    }
  }

  const statusBadge = (r: ScheduledReport) => {
    if (!r.isActive) return <span className="rounded-full bg-surface-muted px-2 py-0.5 text-xs text-muted">Off</span>
    return <span className="rounded-full bg-success/10 px-2 py-0.5 text-xs font-medium text-success">On</span>
  }

  const lastStatusBadge = (status: string | null) => {
    if (!status) return null
    const cls = status === 'Success'
      ? 'bg-success/10 text-success'
      : status === 'Failed'
        ? 'bg-danger/10 text-danger'
        : 'bg-warning/10 text-warning'
    return <span className={`rounded-full px-2 py-0.5 text-xs ${cls}`}>{status}</span>
  }

  return (
    <>
      <ScheduleDrawer
        open={drawerOpen}
        editing={editingReport}
        clients={clients}
        currentUserEmail={currentUserEmail}
        onClose={() => setDrawerOpen(false)}
        onSaved={handleSaved}
      />

      <SectionCard
        title="Scheduled Reports"
        description="Automatically generate and email reports on a recurring schedule."
      >
        <div className="mb-4 flex items-center justify-between">
          <div />
          <button type="button" onClick={openNew} className="button-primary text-sm">
            + New Schedule
          </button>
        </div>

        {actionError ? (
          <p className="mb-3 rounded-lg bg-danger/10 px-3 py-2 text-sm text-danger">{actionError}</p>
        ) : null}

        {loading ? <LoadingState label="Loading scheduled reports" /> : null}
        {!loading && error ? <ErrorState title="Unable to load scheduled reports" description={error} /> : null}

        {!loading && !error ? (
          reports.length === 0 ? (
            <div className="py-10 text-center text-sm text-muted">
              <p className="font-medium text-app">No scheduled reports yet</p>
              <p className="mt-1">Click "+ New Schedule" to automate your first report.</p>
            </div>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b border-border text-left text-xs text-muted">
                    <th className="py-2 pr-4">Name</th>
                    <th className="py-2 pr-4">Type</th>
                    <th className="py-2 pr-4">Frequency</th>
                    <th className="py-2 pr-4">Next Run</th>
                    <th className="py-2 pr-4">Last Status</th>
                    <th className="py-2 pr-4">Active</th>
                    <th className="py-2">Actions</th>
                  </tr>
                </thead>
                <tbody>
                  {reports.map((r) => (
                    <tr key={r.id} className="border-b border-border/50 hover:bg-surface-muted/40">
                      <td className="py-2 pr-4 font-medium text-app">{r.name}</td>
                      <td className="py-2 pr-4 text-muted">
                        {REPORT_TYPES.find((t) => t.value === r.reportType)?.label ?? r.reportType}
                      </td>
                      <td className="py-2 pr-4 text-muted">
                        {r.frequency}
                        {r.frequency === 'Weekly' && r.dayOfWeek ? ` · ${r.dayOfWeek}` : ''}
                        {(r.frequency === 'Monthly' || r.frequency === 'Quarterly') && r.dayOfMonth
                          ? ` · Day ${r.dayOfMonth}`
                          : ''}
                      </td>
                      <td className="py-2 pr-4 text-muted">{formatNextRun(r.nextRunAt)}</td>
                      <td className="py-2 pr-4">{lastStatusBadge(r.lastRunStatus)}</td>
                      <td className="py-2 pr-4">{statusBadge(r)}</td>
                      <td className="py-2">
                        <div className="flex items-center gap-2">
                          <button
                            type="button"
                            onClick={() => openEdit(r)}
                            className="text-xs text-accent hover:underline"
                          >
                            Edit
                          </button>
                          <button
                            type="button"
                            onClick={() => void handleToggle(r.id)}
                            className="text-xs text-muted hover:text-app"
                          >
                            {r.isActive ? 'Disable' : 'Enable'}
                          </button>
                          <button
                            type="button"
                            onClick={() => void handleRunNow(r.id)}
                            disabled={runningId === r.id}
                            className="text-xs text-info hover:underline disabled:opacity-50"
                          >
                            {runningId === r.id ? 'Running…' : 'Run Now'}
                          </button>
                          <button
                            type="button"
                            onClick={() => void handleDelete(r.id)}
                            className="text-xs text-danger hover:underline"
                          >
                            Delete
                          </button>
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )
        ) : null}
      </SectionCard>
    </>
  )
}
