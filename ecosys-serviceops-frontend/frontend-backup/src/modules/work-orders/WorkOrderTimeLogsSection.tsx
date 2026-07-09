import { Clock, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { Badge } from '../../components/ui/Badge'
import { LoadingState } from '../../components/ui/LoadingState'
import { useToast } from '../../components/ui/ToastProvider'
import { useAsyncData } from '../../hooks/useAsyncData'
import { workOrderBillingService } from '../../services/workOrderBillingService'
import type { TechnicianRecord, UpsertTimeLogInput, WorkOrderTimeLogRecord } from '../../types/api'
import { formatDateTime } from '../../utils/date'

interface Props {
  workOrderId: string
  canEdit: boolean
  technicians: TechnicianRecord[]
}

const emptyForm: UpsertTimeLogInput = {
  technicianId: null,
  technicianName: null,
  startedAt: new Date().toISOString().slice(0, 16),
  endedAt: null,
  durationMinutes: 0,
  notes: null,
}

function calcDuration(start: string, end: string): number {
  try {
    const s = new Date(start).getTime()
    const e = new Date(end).getTime()
    const mins = Math.round((e - s) / 60000)
    return mins > 0 ? mins : 0
  } catch {
    return 0
  }
}

function formatDuration(minutes: number): string {
  if (minutes < 60) return `${minutes}m`
  const h = Math.floor(minutes / 60)
  const m = minutes % 60
  return m > 0 ? `${h}h ${m}m` : `${h}h`
}

export function WorkOrderTimeLogsSection({ workOrderId, canEdit, technicians }: Props) {
  const { pushToast } = useToast()
  const [showForm, setShowForm] = useState(false)
  const [form, setForm] = useState<UpsertTimeLogInput>(emptyForm)
  const [saving, setSaving] = useState(false)

  const { data: logs, loading, reload } = useAsyncData<WorkOrderTimeLogRecord[]>(
    (signal) => workOrderBillingService.getTimeLogs(workOrderId, signal),
    [],
    [workOrderId],
  )

  function field<K extends keyof UpsertTimeLogInput>(key: K, value: UpsertTimeLogInput[K]) {
    setForm((prev) => {
      const next = { ...prev, [key]: value }
      // Auto-calc duration when both dates are set
      if ((key === 'startedAt' || key === 'endedAt') && next.startedAt && next.endedAt) {
        next.durationMinutes = calcDuration(next.startedAt as string, next.endedAt as string)
      }
      return next
    })
  }

  async function handleSave() {
    if (!form.startedAt) {
      pushToast({ title: 'Start time required', description: 'Please set a start time.', tone: 'danger' })
      return
    }
    setSaving(true)
    try {
      await workOrderBillingService.addTimeLog(workOrderId, {
        ...form,
        startedAt: new Date(form.startedAt as string).toISOString(),
        endedAt: form.endedAt ? new Date(form.endedAt as string).toISOString() : null,
      })
      pushToast({ title: 'Time logged', tone: 'success' })
      setForm(emptyForm)
      setShowForm(false)
      await reload()
    } catch (err) {
      pushToast({ title: 'Failed to save', description: err instanceof Error ? err.message : 'Unable to save time log.', tone: 'danger' })
    } finally {
      setSaving(false)
    }
  }

  async function handleDelete(log: WorkOrderTimeLogRecord) {
    try {
      await workOrderBillingService.deleteTimeLog(workOrderId, log.id)
      pushToast({ title: 'Time log removed', tone: 'success' })
      await reload()
    } catch (err) {
      pushToast({ title: 'Delete failed', description: err instanceof Error ? err.message : 'Unable to delete time log.', tone: 'danger' })
    }
  }

  return (
    <div className="rounded-[18px] border border-app bg-[var(--app-surface)] p-4">
      <div className="mb-3 flex items-center justify-between gap-2">
        <div className="flex items-center gap-2">
          <Clock className="h-4 w-4 text-muted" />
          <span className="text-sm font-semibold text-app">Time Logs</span>
          {logs.length > 0 && (
            <span className="text-xs text-muted">
              {formatDuration(logs.reduce((sum, l) => sum + l.durationMinutes, 0))} total
            </span>
          )}
        </div>
        {canEdit && !showForm && (
          <button type="button" className="button-secondary py-1 text-xs" onClick={() => setShowForm(true)}>
            + Log time
          </button>
        )}
      </div>

      {/* Add form */}
      {showForm && (
        <div className="mb-4 rounded-[14px] border border-app bg-[var(--app-surface-strong)] p-3 space-y-3">
          <div className="grid grid-cols-2 gap-3">
            <div className="flex flex-col gap-1">
              <label className="text-xs font-medium text-muted">Technician</label>
              <select
                className="input-field text-sm"
                value={form.technicianId ?? ''}
                onChange={(e) => field('technicianId', e.target.value || null)}
              >
                <option value="">Select technician</option>
                {technicians.map((t) => (
                  <option key={t.id} value={t.id}>{t.fullName}</option>
                ))}
              </select>
            </div>
            <div className="flex flex-col gap-1">
              <label className="text-xs font-medium text-muted">Duration (auto-calc)</label>
              <input
                type="number"
                min="0"
                className="input-field text-sm"
                value={form.durationMinutes}
                onChange={(e) => field('durationMinutes', parseInt(e.target.value) || 0)}
                placeholder="minutes"
              />
            </div>
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div className="flex flex-col gap-1">
              <label className="text-xs font-medium text-muted">Start</label>
              <input
                type="datetime-local"
                className="input-field text-sm"
                value={form.startedAt as string}
                onChange={(e) => field('startedAt', e.target.value)}
              />
            </div>
            <div className="flex flex-col gap-1">
              <label className="text-xs font-medium text-muted">End</label>
              <input
                type="datetime-local"
                className="input-field text-sm"
                value={form.endedAt as string ?? ''}
                onChange={(e) => field('endedAt', e.target.value || null)}
              />
            </div>
          </div>
          <div className="flex flex-col gap-1">
            <label className="text-xs font-medium text-muted">Notes</label>
            <input
              type="text"
              className="input-field text-sm"
              placeholder="Optional notes…"
              value={form.notes ?? ''}
              onChange={(e) => field('notes', e.target.value || null)}
            />
          </div>
          <div className="flex gap-2">
            <button type="button" className="button-primary py-1.5 text-xs" onClick={handleSave} disabled={saving}>
              {saving ? 'Saving…' : 'Save'}
            </button>
            <button type="button" className="button-secondary py-1.5 text-xs" onClick={() => { setShowForm(false); setForm(emptyForm) }}>
              Cancel
            </button>
          </div>
        </div>
      )}

      {loading ? <LoadingState label="Loading time logs" /> : null}

      {!loading && logs.length === 0 && !showForm ? (
        <div className="py-4 text-center text-sm text-muted">No time logged yet. Log technician time to include labour in billable items.</div>
      ) : null}

      {!loading && logs.length > 0 ? (
        <div className="space-y-1">
          {logs.map((log) => (
            <div key={log.id} className="flex items-start justify-between gap-3 rounded-[10px] bg-[var(--app-surface-strong)] px-3 py-2">
              <div className="min-w-0 flex-1">
                <div className="flex items-center gap-2 flex-wrap">
                  <span className="text-sm font-medium text-app">{log.technicianName ?? 'Unassigned'}</span>
                  <Badge tone="neutral">{formatDuration(log.durationMinutes)}</Badge>
                  {log.isBilled ? <Badge tone="success">Billed</Badge> : null}
                </div>
                <p className="mt-0.5 text-xs text-muted">
                  {formatDateTime(log.startedAt)}{log.endedAt ? ` → ${formatDateTime(log.endedAt)}` : ''}
                </p>
                {log.notes ? <p className="mt-0.5 text-xs text-muted">{log.notes}</p> : null}
              </div>
              {canEdit && !log.isBilled ? (
                <button
                  type="button"
                  className="icon-button h-7 w-7 shrink-0 text-muted hover:text-danger"
                  onClick={() => handleDelete(log)}
                  aria-label="Delete time log"
                >
                  <Trash2 className="h-3.5 w-3.5" />
                </button>
              ) : null}
            </div>
          ))}
        </div>
      ) : null}
    </div>
  )
}
