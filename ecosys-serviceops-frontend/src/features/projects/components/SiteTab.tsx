import { useEffect, useMemo, useState } from 'react'
import toast from 'react-hot-toast'
import { ChevronLeft, ChevronRight, Lock, Plus, UserPlus } from 'lucide-react'
import { FormDrawer } from '../../../shared/components/FormDrawer'
import { formatDate } from '../../../shared/utils/dates'
import { cn } from '../../../shared/utils/cn'
import { useMusterSheet, useProjectWorkers, useSaveMuster, useSaveWorker } from '../siteApi'
import type { MusterStatus, ProjectDetail, ProjectWorker, WorkerEngagement } from '../types'
import { fieldClass, labelClass, primaryButtonClass, secondaryButtonClass, shiftDateInput, smallButtonClass, textOrNull, todayInput } from './formStyles'

const statusOptions: Array<{ value: MusterStatus; label: string; tone: string }> = [
  { value: 'Present', label: 'Present', tone: 'border-emerald-500 bg-emerald-50 text-emerald-800' },
  { value: 'HalfDay', label: 'Half day', tone: 'border-amber-500 bg-amber-50 text-amber-800' },
  { value: 'Absent', label: 'Absent', tone: 'border-slate-400 bg-slate-100 text-slate-700' },
]

interface WorkerDraft {
  id?: string
  fullName: string
  phone: string
  trade: string
  engagementType: WorkerEngagement
  isActive: boolean
}

const emptyWorker: WorkerDraft = { fullName: '', phone: '', trade: '', engagementType: 'Casual', isActive: true }

/**
 * The muster roll and worker roster for a project. Marks presence only: this tab never shows or
 * takes pay rates, which the office sets on the Financials tab.
 */
export function SiteTab({ project }: { project: ProjectDetail }) {
  const projectId = project.summary.id
  const isFinal = project.summary.stageCategory === 'Closed' || project.summary.stageCategory === 'Cancelled'
  const canMark = project.abilities.canRunSite && !isFinal
  const today = todayInput()
  const [date, setDate] = useState(today)
  const { data: sheet, isLoading } = useMusterSheet(projectId, date)
  const { data: workers } = useProjectWorkers(projectId, true)
  const saveMuster = useSaveMuster(projectId)
  const saveWorker = useSaveWorker(projectId)
  const [marks, setMarks] = useState<Record<string, MusterStatus | null>>({})
  const [workerDraft, setWorkerDraft] = useState<WorkerDraft | null>(null)

  // Start each day from the saved marks and the suggestions (yesterday's mark, or a clock-in).
  useEffect(() => {
    setMarks(Object.fromEntries((sheet?.rows ?? []).map((row) => [row.workerId, row.status])))
  }, [sheet])

  const rows = useMemo(() => sheet?.rows ?? [], [sheet])
  const changed = useMemo(() => rows.some((row) => marks[row.workerId] !== row.status || row.isSuggestion), [marks, rows])
  const markedCount = rows.filter((row) => marks[row.workerId]).length
  const locked = sheet?.isLocked ?? false
  const editable = canMark && (!locked || project.abilities.canManage)

  const save = async () => {
    const toSave = rows.filter((row) => marks[row.workerId]).map((row) => ({ workerId: row.workerId, status: marks[row.workerId] as MusterStatus, note: row.note }))
    if (toSave.length === 0) return
    try {
      const saved = await saveMuster.mutateAsync({ date, marks: toSave })
      toast.success(`Muster roll saved: ${saved.manDays} man-day${saved.manDays === 1 ? '' : 's'}`)
    } catch {
      // toast from the API client
    }
  }

  const saveWorkerDraft = async () => {
    if (!workerDraft) return
    try {
      await saveWorker.mutateAsync({
        ...(workerDraft.id ? { workerId: workerDraft.id } : {}),
        fullName: workerDraft.fullName.trim(),
        phone: textOrNull(workerDraft.phone),
        trade: textOrNull(workerDraft.trade),
        engagementType: workerDraft.engagementType,
        technicianId: null,
        isActive: workerDraft.isActive,
      })
      toast.success(workerDraft.id ? 'Worker updated' : `${workerDraft.fullName.trim()} added to the roster`)
      setWorkerDraft(null)
    } catch {
      // toast from the API client
    }
  }

  const editWorker = (worker: ProjectWorker) =>
    setWorkerDraft({ id: worker.id, fullName: worker.fullName, phone: worker.phone ?? '', trade: worker.trade ?? '', engagementType: worker.engagementType, isActive: worker.isActive })

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-slate-200 bg-white p-3">
        <div className="flex items-center gap-2">
          <button type="button" aria-label="Previous day" className={smallButtonClass} onClick={() => setDate(shiftDateInput(date, -1))}><ChevronLeft className="h-4 w-4" /></button>
          <input type="date" aria-label="Muster date" value={date} max={today} onChange={(event) => event.target.value && setDate(event.target.value)} className={cn(fieldClass, 'w-44')} />
          <button type="button" aria-label="Next day" className={smallButtonClass} disabled={date >= today} onClick={() => setDate(shiftDateInput(date, 1))}><ChevronRight className="h-4 w-4" /></button>
        </div>
        <div className="flex flex-wrap items-center gap-3 text-sm text-slate-600">
          {locked ? <span className="inline-flex items-center gap-1 text-amber-700"><Lock className="h-4 w-4" />Locked{project.abilities.canManage ? ': corrections are audited' : ''}</span> : null}
          <span>{markedCount} of {rows.length} marked · {sheet?.manDays ?? 0} man-days saved</span>
        </div>
      </div>

      {isLoading ? <p className="text-sm text-slate-500">Loading the day sheet…</p> : rows.length === 0 ? (
        <div className="rounded-xl border border-dashed border-slate-300 p-6 text-center text-sm text-slate-600">
          No workers on the roster yet. Add the people working on this site, then mark them each day.
        </div>
      ) : (
        <div className="divide-y divide-slate-100 rounded-xl border border-slate-200 bg-white">
          {editable ? (
            <div className="flex justify-end p-3">
              <button type="button" className={smallButtonClass} onClick={() => setMarks(Object.fromEntries(rows.map((row) => [row.workerId, 'Present' as MusterStatus])))}>Mark all present</button>
            </div>
          ) : null}
          {rows.map((row) => {
            const current = marks[row.workerId] ?? null
            return (
              <div key={row.workerId} className="flex flex-col gap-2 p-3 sm:flex-row sm:items-center sm:justify-between">
                <div className="min-w-0">
                  <div className="font-medium text-slate-900">{row.fullName}{row.isActive ? '' : ' (inactive)'}</div>
                  <div className="text-xs text-slate-500">{[row.trade, row.engagementType].filter(Boolean).join(' · ')}{row.isSuggestion && current === row.status ? ' · suggested, not saved' : ''}</div>
                </div>
                <div className="grid grid-cols-3 gap-2 sm:w-80">
                  {statusOptions.map((option) => (
                    <button
                      key={option.value}
                      type="button"
                      disabled={!editable}
                      aria-pressed={current === option.value}
                      onClick={() => setMarks({ ...marks, [row.workerId]: option.value })}
                      className={cn('h-11 rounded-lg border text-sm font-medium transition disabled:opacity-60', current === option.value ? option.tone : 'border-slate-200 bg-white text-slate-600 hover:bg-slate-50')}
                    >
                      {option.label}
                    </button>
                  ))}
                </div>
              </div>
            )
          })}
        </div>
      )}

      <div className="flex flex-wrap justify-end gap-2">
        {canMark ? <button type="button" className={secondaryButtonClass} onClick={() => setWorkerDraft(emptyWorker)}><UserPlus className="h-4 w-4" />Add worker</button> : null}
        {editable && rows.length > 0 ? (
          <button type="button" className={primaryButtonClass} disabled={!changed || markedCount === 0 || saveMuster.isPending} onClick={() => void save()}>
            Save marks for {formatDate(date)}
          </button>
        ) : null}
      </div>

      <section className="space-y-2">
        <h3 className="text-sm font-semibold text-slate-900">Roster</h3>
        <div className="grid grid-cols-1 gap-2 sm:grid-cols-2 xl:grid-cols-3">
          {(workers ?? []).map((worker) => (
            <div key={worker.id} className={cn('flex items-start justify-between gap-2 rounded-xl border border-slate-200 bg-white p-3', !worker.isActive && 'opacity-60')}>
              <div className="min-w-0">
                <div className="truncate font-medium text-slate-900">{worker.fullName}</div>
                <div className="text-xs text-slate-500">{[worker.trade, worker.engagementType, worker.phone].filter(Boolean).join(' · ')}</div>
              </div>
              {canMark ? <button type="button" className={smallButtonClass} onClick={() => editWorker(worker)}>Edit</button> : null}
            </div>
          ))}
          {canMark && (workers ?? []).length === 0 ? (
            <button type="button" className={cn(secondaryButtonClass, 'border-dashed')} onClick={() => setWorkerDraft(emptyWorker)}><Plus className="h-4 w-4" />Add the first worker</button>
          ) : null}
        </div>
      </section>

      <FormDrawer
        open={workerDraft !== null}
        title={workerDraft?.id ? 'Edit worker' : 'Add worker'}
        onClose={() => setWorkerDraft(null)}
        footer={
          <div className="flex justify-end gap-2">
            <button type="button" className={secondaryButtonClass} onClick={() => setWorkerDraft(null)}>Cancel</button>
            <button type="button" className={primaryButtonClass} disabled={!workerDraft?.fullName.trim() || saveWorker.isPending} onClick={() => void saveWorkerDraft()}>Save</button>
          </div>
        }
      >
        {workerDraft ? (
          <div className="space-y-4">
            <div>
              <label className={labelClass} htmlFor="worker-name">Full name</label>
              <input id="worker-name" value={workerDraft.fullName} onChange={(event) => setWorkerDraft({ ...workerDraft, fullName: event.target.value })} className={fieldClass} />
            </div>
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
              <div>
                <label className={labelClass} htmlFor="worker-phone">Phone (M-Pesa)</label>
                <input id="worker-phone" type="tel" value={workerDraft.phone} onChange={(event) => setWorkerDraft({ ...workerDraft, phone: event.target.value })} className={fieldClass} />
              </div>
              <div>
                <label className={labelClass} htmlFor="worker-trade">Trade</label>
                <input id="worker-trade" value={workerDraft.trade} onChange={(event) => setWorkerDraft({ ...workerDraft, trade: event.target.value })} placeholder="e.g. Mason" className={fieldClass} />
              </div>
            </div>
            <div>
              <label className={labelClass} htmlFor="worker-engagement">Engagement</label>
              <select id="worker-engagement" value={workerDraft.engagementType} onChange={(event) => setWorkerDraft({ ...workerDraft, engagementType: event.target.value as WorkerEngagement })} className={fieldClass}>
                <option value="Casual">Casual</option>
                <option value="Permanent">Permanent</option>
                <option value="Subcontractor">Subcontractor</option>
              </select>
            </div>
            {workerDraft.id ? (
              <label className="flex items-center gap-2 text-sm text-slate-700">
                <input type="checkbox" checked={workerDraft.isActive} onChange={(event) => setWorkerDraft({ ...workerDraft, isActive: event.target.checked })} />
                Still working on this site
              </label>
            ) : null}
            <p className="text-xs text-slate-500">The office sets daily rates separately. Supervisors never see them.</p>
          </div>
        ) : null}
      </FormDrawer>
    </div>
  )
}
