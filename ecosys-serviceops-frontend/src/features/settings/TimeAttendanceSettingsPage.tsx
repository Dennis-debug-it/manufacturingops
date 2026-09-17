import { useEffect, useState } from 'react'
import toast from 'react-hot-toast'
import { Plus, Trash2 } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { Spinner } from '../../shared/components/Spinner'
import { ErrorState } from '../../shared/components/ErrorState'
import { usePermission } from '../../shared/hooks/usePermission'
import { useSaveTimeAttendanceSettings, useTimeAttendanceSettings, type TimeAttendanceSettings } from '../team/timeAttendance/api'

export function TimeAttendanceSettingsPage() {
  const canManage = usePermission('settings.manage')
  const { data, isLoading, isError, refetch } = useTimeAttendanceSettings()
  const save = useSaveTimeAttendanceSettings()
  const [values, setValues] = useState<TimeAttendanceSettings | null>(null)

  useEffect(() => {
    if (data) setValues(data)
  }, [data])

  if (isLoading) {
    return <Spinner label="Loading settings" />
  }

  // Without this branch an exhausted retry leaves `values` null forever and the spinner never releases.
  if (isError || !values) {
    return (
      <ErrorState
        title="Time & Attendance settings unavailable"
        description="We couldn’t load the work-hour, rounding, and leave-type settings. Nothing has been changed — try again."
        onRetry={() => void refetch()}
      />
    )
  }

  const setField = <K extends keyof TimeAttendanceSettings>(key: K, value: TimeAttendanceSettings[K]) =>
    setValues((current) => (current ? { ...current, [key]: value } : current))

  const addLeaveType = () =>
    setValues((current) =>
      current
        ? { ...current, leaveTypes: [...current.leaveTypes, { code: '', name: '', isPaid: true, annualAllowanceDays: 0 }] }
        : current,
    )

  const removeLeaveType = (index: number) =>
    setValues((current) => (current ? { ...current, leaveTypes: current.leaveTypes.filter((_, i) => i !== index) } : current))

  const updateLeaveType = (index: number, patch: Partial<TimeAttendanceSettings['leaveTypes'][number]>) =>
    setValues((current) =>
      current
        ? { ...current, leaveTypes: current.leaveTypes.map((lt, i) => (i === index ? { ...lt, ...patch } : lt)) }
        : current,
    )

  const onSave = async () => {
    if (!values) return
    try {
      await save.mutateAsync(values)
      toast.success('Settings saved')
    } catch {
      toast.error('Failed to save Time & Attendance settings.')
    }
  }

  return (
    <div className="space-y-4">
      <PageHeader
        title="Time & Attendance Settings"
        subtitle="Work hours, rounding rules, overtime thresholds, and leave types."
        primaryAction={
          canManage ? (
            <button type="button" onClick={() => void onSave()} disabled={save.isPending} className="inline-flex h-11 items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:opacity-60">
              {save.isPending ? 'Saving…' : 'Save Changes'}
            </button>
          ) : undefined
        }
      />
      <div className="grid grid-cols-1 gap-4 rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 shadow-sm sm:grid-cols-2">
        <div>
          <label className="mb-1.5 block text-sm font-medium text-slate-700">Clock Rounding (minutes)</label>
          <input
            type="number"
            min={0}
            value={values.roundingMinutes}
            disabled={!canManage}
            onChange={(event) => setField('roundingMinutes', Number(event.target.value))}
            className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)] disabled:bg-slate-50"
          />
          <p className="mt-1 text-xs text-slate-500">Clock in/out times round to the nearest interval.</p>
        </div>
        <div>
          <label className="mb-1.5 block text-sm font-medium text-slate-700">Overtime Threshold (hours/day)</label>
          <input
            type="number"
            min={0}
            step={0.5}
            value={values.overtimeThresholdHoursPerDay}
            disabled={!canManage}
            onChange={(event) => setField('overtimeThresholdHoursPerDay', Number(event.target.value))}
            className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)] disabled:bg-slate-50"
          />
        </div>
        <div>
          <label className="mb-1.5 block text-sm font-medium text-slate-700">Grace Period (minutes)</label>
          <input
            type="number"
            min={0}
            value={values.graceMinutes}
            disabled={!canManage}
            onChange={(event) => setField('graceMinutes', Number(event.target.value))}
            className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)] disabled:bg-slate-50"
          />
          <p className="mt-1 text-xs text-slate-500">Minutes after shift start before a clock-in is flagged late.</p>
        </div>
        <div>
          <label className="mb-1.5 block text-sm font-medium text-slate-700">Auto Clock-Out After (hours)</label>
          <input
            type="number"
            min={1}
            value={values.autoClockOutAfterHours}
            disabled={!canManage}
            onChange={(event) => setField('autoClockOutAfterHours', Number(event.target.value))}
            className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)] disabled:bg-slate-50"
          />
        </div>
      </div>

      <div className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 shadow-sm">
        <div className="flex items-center justify-between">
          <h2 className="text-sm font-semibold uppercase tracking-[0.18em] text-slate-500">Leave Types</h2>
          {canManage ? (
            <button type="button" onClick={addLeaveType} className="inline-flex h-9 items-center gap-1.5 rounded-lg border border-slate-200 bg-white px-3 text-xs font-medium text-slate-700 hover:bg-slate-50">
              <Plus className="h-3.5 w-3.5" />Add Leave Type
            </button>
          ) : null}
        </div>
        <div className="mt-4 space-y-3">
          {values.leaveTypes.map((leaveType, index) => (
            <div key={index} className="grid grid-cols-1 gap-3 rounded-xl border border-slate-200 p-3 sm:grid-cols-[1fr_2fr_auto_auto_auto]">
              <input
                value={leaveType.code}
                disabled={!canManage}
                onChange={(event) => updateLeaveType(index, { code: event.target.value.toUpperCase() })}
                placeholder="CODE"
                className="h-10 rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)] disabled:bg-slate-50"
              />
              <input
                value={leaveType.name}
                disabled={!canManage}
                onChange={(event) => updateLeaveType(index, { name: event.target.value })}
                placeholder="Name"
                className="h-10 rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)] disabled:bg-slate-50"
              />
              <label className="flex items-center gap-2 text-sm text-slate-700">
                <input
                  type="checkbox"
                  checked={leaveType.isPaid}
                  disabled={!canManage}
                  onChange={(event) => updateLeaveType(index, { isPaid: event.target.checked })}
                  className="h-4 w-4 rounded border-slate-300"
                />
                Paid
              </label>
              <input
                type="number"
                min={0}
                value={leaveType.annualAllowanceDays}
                disabled={!canManage}
                onChange={(event) => updateLeaveType(index, { annualAllowanceDays: Number(event.target.value) })}
                className="h-10 w-24 rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)] disabled:bg-slate-50"
              />
              {canManage ? (
                <button type="button" onClick={() => removeLeaveType(index)} className="inline-flex h-10 items-center justify-center rounded-lg border border-rose-200 bg-white px-3 text-rose-600 hover:bg-rose-50">
                  <Trash2 className="h-4 w-4" />
                </button>
              ) : null}
            </div>
          ))}
        </div>
      </div>
    </div>
  )
}
