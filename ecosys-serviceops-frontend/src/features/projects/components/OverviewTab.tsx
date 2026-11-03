import { useState } from 'react'
import toast from 'react-hot-toast'
import { SummarySection } from '../../../shared/pages/ModulePage'
import { StatCard } from '../../../shared/components/StatCard'
import { ConfirmDialog } from '../../../shared/components/ConfirmDialog'
import { formatCurrency } from '../../../shared/utils/format'
import { formatDate, formatDateTime } from '../../../shared/utils/dates'
import { useOverrideProgress } from '../api'
import type { ProjectDetail } from '../types'
import { ProgressBar } from './ProjectBadges'
import { fieldClass, labelClass, smallButtonClass, textAreaClass, textOrNull } from './formStyles'

function Fact({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-xl border border-slate-200 px-4 py-3">
      <div className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">{label}</div>
      <div className="mt-2 text-sm text-slate-900">{value}</div>
    </div>
  )
}

export function OverviewTab({ project }: { project: ProjectDetail }) {
  const { summary } = project
  const overrideProgress = useOverrideProgress(summary.id)
  const [overrideOpen, setOverrideOpen] = useState(false)
  const [percent, setPercent] = useState('')
  const [reason, setReason] = useState('')
  const money = (value: number) => formatCurrency(value, summary.currency)
  const isFinal = summary.stageCategory === 'Closed' || summary.stageCategory === 'Cancelled'
  const canOverride = project.abilities.canManage && project.features.allowManualProgressOverride && !isFinal

  const saveOverride = async (clear: boolean) => {
    try {
      await overrideProgress.mutateAsync(clear ? { percent: null, reason: null } : { percent: Number(percent), reason: textOrNull(reason) })
      toast.success(clear ? 'Progress override removed' : 'Progress overridden')
      setOverrideOpen(false)
    } catch {
      // toast from the API client
    }
  }

  const consultants = project.consultants.map((c) => c.firmName).join(', ')

  return (
    <div className="space-y-4">
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-4">
        <StatCard label="Contract value" value={money(summary.originalValue)} />
        <StatCard label="Revised value" value={money(summary.revisedValue)} tone={summary.revisedValue !== summary.originalValue ? 'success' : 'default'} />
        <StatCard label="Pending variations" value={project.pendingVariationsCount > 0 ? `${project.pendingVariationsCount} · ${money(project.pendingVariationsValue)}` : 'None'} tone={project.pendingVariationsCount > 0 ? 'warning' : 'default'} />
        <StatCard
          label="Days remaining"
          value={summary.daysRemaining == null ? '—' : summary.daysRemaining < 0 ? `${Math.abs(summary.daysRemaining)} late` : summary.daysRemaining}
          tone={summary.daysRemaining != null && summary.daysRemaining < 0 ? 'danger' : 'default'}
        />
      </div>

      <SummarySection title="Progress" description="Weighted by milestone. The tick shows where the plan says the project should be today.">
        <ProgressBar actual={summary.progressPercent} planned={project.plannedProgressPercent} behind={summary.isBehindSchedule} />
        <div className="mt-3 flex flex-wrap items-center justify-between gap-2 text-sm text-slate-600">
          <span>
            {project.progressOverridePercent != null
              ? <>Overridden to <strong>{project.progressOverridePercent}%</strong> (calculated {project.calculatedProgressPercent}%): {project.progressOverrideReason}</>
              : <>Calculated from milestones: <strong>{project.calculatedProgressPercent}%</strong>{summary.isBehindSchedule ? ' — behind plan' : ''}</>}
          </span>
          {canOverride ? (
            <div className="flex gap-2">
              {project.progressOverridePercent != null ? (
                <button type="button" className={smallButtonClass} onClick={() => void saveOverride(true)}>Use calculated</button>
              ) : null}
              <button type="button" className={smallButtonClass} onClick={() => { setPercent(String(summary.progressPercent)); setReason(''); setOverrideOpen(true) }}>Override</button>
            </div>
          ) : null}
        </div>
      </SummarySection>

      <SummarySection title="Details" description="Commercial and schedule summary.">
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-3">
          <Fact label="Client" value={summary.clientName} />
          <Fact label="Site" value={summary.siteName ?? '—'} />
          <Fact label="Type" value={summary.projectTypeName} />
          <Fact label="Project manager" value={summary.projectManagerName ?? 'Not assigned'} />
          <Fact label="Consultants" value={consultants || '—'} />
          <Fact label="Branch" value={project.branchName ?? '—'} />
          <Fact label="Approved additions" value={money(project.approvedAdditions)} />
          <Fact label="Approved omissions" value={project.approvedOmissions ? `− ${money(project.approvedOmissions)}` : money(0)} />
          <Fact label="Client portal" value={project.visibleInClientPortal ? 'Visible to the client' : 'Hidden from the client'} />
          <Fact label="Planned" value={`${summary.plannedStartDate ? formatDate(summary.plannedStartDate) : '—'} → ${summary.plannedEndDate ? formatDate(summary.plannedEndDate) : '—'}`} />
          <Fact label="Actual" value={`${project.actualStartDate ? formatDate(project.actualStartDate) : 'Not started'} → ${project.actualEndDate ? formatDate(project.actualEndDate) : '—'}`} />
          <Fact label="Handed over" value={project.handedOverAt ? formatDateTime(project.handedOverAt) : '—'} />
        </div>
        {project.description ? <p className="mt-4 whitespace-pre-line text-sm leading-6 text-slate-700">{project.description}</p> : null}
        {project.closedAt ? (
          <p className="mt-4 rounded-xl border border-slate-200 bg-slate-50 px-4 py-3 text-sm text-slate-700">
            Closed {formatDateTime(project.closedAt)}{project.closureNotes ? `: ${project.closureNotes}` : ''}
          </p>
        ) : null}
      </SummarySection>

      <ConfirmDialog
        open={overrideOpen}
        title="Override progress"
        message="Use this when milestones don't reflect the real position. The override and its reason are recorded."
        confirmLabel="Save override"
        confirmDisabled={!reason.trim() || percent === '' || Number(percent) < 0 || Number(percent) > 100 || overrideProgress.isPending}
        onCancel={() => setOverrideOpen(false)}
        onConfirm={() => void saveOverride(false)}
      >
        <div className="space-y-3 text-left">
          <div>
            <label className={labelClass} htmlFor="override-percent">Progress %</label>
            <input id="override-percent" type="number" min="0" max="100" value={percent} onChange={(event) => setPercent(event.target.value)} className={fieldClass} />
          </div>
          <div>
            <label className={labelClass} htmlFor="override-reason">Reason</label>
            <textarea id="override-reason" rows={2} value={reason} onChange={(event) => setReason(event.target.value)} className={textAreaClass} />
          </div>
        </div>
      </ConfirmDialog>
    </div>
  )
}
