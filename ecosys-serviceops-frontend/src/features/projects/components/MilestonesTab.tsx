import { useState } from 'react'
import toast from 'react-hot-toast'
import { Plus, Pencil, Trash2 } from 'lucide-react'
import { DataTable, type DataTableColumn } from '../../../shared/components/DataTable'
import { FormDrawer } from '../../../shared/components/FormDrawer'
import { ConfirmDialog } from '../../../shared/components/ConfirmDialog'
import { StatusBadge } from '../../../shared/components/StatusBadge'
import { formatDate } from '../../../shared/utils/dates'
import { useCreateMilestone, useDeleteMilestone, useProjectMilestones, useSetMilestoneStatus, useUpdateMilestone } from '../api'
import type { MilestoneStatus, ProjectMilestone } from '../types'
import { dateInputValue, dateOrNull, fieldClass, labelClass, primaryButtonClass, secondaryButtonClass, smallButtonClass } from './formStyles'

const statusLabels: Record<MilestoneStatus, string> = {
  NotStarted: 'Not started',
  InProgress: 'In progress',
  Completed: 'Completed',
  Verified: 'Verified',
}

interface Draft {
  id?: string
  name: string
  weight: string
  plannedDate: string
  forecastDate: string
}

const emptyDraft: Draft = { name: '', weight: '1', plannedDate: '', forecastDate: '' }

export function MilestonesTab({ projectId, readOnly }: { projectId: string; readOnly: boolean }) {
  const { data: milestones, isLoading } = useProjectMilestones(projectId)
  const createMilestone = useCreateMilestone(projectId)
  const updateMilestone = useUpdateMilestone(projectId)
  const setStatus = useSetMilestoneStatus(projectId)
  const deleteMilestone = useDeleteMilestone(projectId)
  const [draft, setDraft] = useState<Draft | null>(null)
  const [deleting, setDeleting] = useState<ProjectMilestone | null>(null)

  const totalWeight = (milestones ?? []).reduce((sum, m) => sum + m.weight, 0)

  const save = async () => {
    if (!draft || !draft.name.trim()) return
    const payload = { name: draft.name.trim(), weight: draft.weight === '' ? 1 : Number(draft.weight), plannedDate: dateOrNull(draft.plannedDate), forecastDate: dateOrNull(draft.forecastDate) }
    try {
      if (draft.id) await updateMilestone.mutateAsync({ milestoneId: draft.id, ...payload })
      else await createMilestone.mutateAsync(payload)
      toast.success(draft.id ? 'Milestone updated' : 'Milestone added')
      setDraft(null)
    } catch {
      // toast from the API client
    }
  }

  const changeStatus = async (milestone: ProjectMilestone, status: MilestoneStatus) => {
    try {
      await setStatus.mutateAsync({ milestoneId: milestone.id, status })
    } catch {
      // toast from the API client
    }
  }

  const columns: DataTableColumn<ProjectMilestone>[] = [
    { key: 'name', header: 'Milestone', cell: (row) => <span className="font-medium text-slate-900">{row.name}</span> },
    { key: 'weight', header: 'Weight', cell: (row) => `${row.weight}${totalWeight > 0 ? ` (${Math.round((row.weight / totalWeight) * 100)}%)` : ''}` },
    {
      key: 'plannedDate',
      header: 'Planned',
      cell: (row) => row.plannedDate ? <span className={row.isOverdue ? 'font-semibold text-rose-600' : ''}>{formatDate(row.plannedDate)}{row.isOverdue ? ' · overdue' : ''}</span> : '—',
    },
    { key: 'forecastDate', header: 'Forecast', cell: (row) => (row.forecastDate ? formatDate(row.forecastDate) : '—') },
    {
      key: 'status',
      header: 'Status',
      cell: (row) => readOnly ? <StatusBadge status={statusLabels[row.status]} /> : (
        <select aria-label={`Status of ${row.name}`} value={row.status} onChange={(event) => void changeStatus(row, event.target.value as MilestoneStatus)} className="h-9 rounded-lg border border-slate-300 bg-white px-2 text-sm">
          {(Object.keys(statusLabels) as MilestoneStatus[]).map((status) => (<option key={status} value={status}>{statusLabels[status]}</option>))}
        </select>
      ),
    },
    ...(readOnly ? [] : [{
      key: 'actions',
      header: '',
      cell: (row: ProjectMilestone) => (
        <div className="flex justify-end gap-2">
          <button type="button" aria-label={`Edit ${row.name}`} className={smallButtonClass} onClick={() => setDraft({ id: row.id, name: row.name, weight: String(row.weight), plannedDate: dateInputValue(row.plannedDate), forecastDate: dateInputValue(row.forecastDate) })}><Pencil className="h-3.5 w-3.5" /></button>
          <button type="button" aria-label={`Delete ${row.name}`} className={smallButtonClass} onClick={() => setDeleting(row)}><Trash2 className="h-3.5 w-3.5" /></button>
        </div>
      ),
    }]),
  ]

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <p className="text-sm text-slate-600">Progress is the share of milestone weight that is complete. Give the bigger stages more weight.</p>
        {!readOnly ? <button type="button" className={primaryButtonClass} onClick={() => setDraft(emptyDraft)}><Plus className="h-4 w-4" />Add milestone</button> : null}
      </div>
      <DataTable columns={columns} data={milestones ?? []} loading={isLoading} emptyTitle="No milestones yet" emptyDescription="Add the stages of the work, such as rough-in, installation, testing and commissioning." />

      <FormDrawer
        open={draft !== null}
        title={draft?.id ? 'Edit milestone' : 'Add milestone'}
        onClose={() => setDraft(null)}
        footer={
          <div className="flex justify-end gap-2">
            <button type="button" className={secondaryButtonClass} onClick={() => setDraft(null)}>Cancel</button>
            <button type="button" className={primaryButtonClass} disabled={!draft?.name.trim() || createMilestone.isPending || updateMilestone.isPending} onClick={() => void save()}>Save</button>
          </div>
        }
      >
        {draft ? (
          <div className="space-y-4">
            <div>
              <label className={labelClass} htmlFor="milestone-name">Name</label>
              <input id="milestone-name" value={draft.name} onChange={(event) => setDraft({ ...draft, name: event.target.value })} className={fieldClass} />
            </div>
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
              <div>
                <label className={labelClass} htmlFor="milestone-weight">Weight</label>
                <input id="milestone-weight" type="number" min="0" step="0.5" value={draft.weight} onChange={(event) => setDraft({ ...draft, weight: event.target.value })} className={fieldClass} />
              </div>
              <div>
                <label className={labelClass} htmlFor="milestone-planned">Planned date</label>
                <input id="milestone-planned" type="date" value={draft.plannedDate} onChange={(event) => setDraft({ ...draft, plannedDate: event.target.value })} className={fieldClass} />
              </div>
              <div>
                <label className={labelClass} htmlFor="milestone-forecast">Forecast date</label>
                <input id="milestone-forecast" type="date" value={draft.forecastDate} onChange={(event) => setDraft({ ...draft, forecastDate: event.target.value })} className={fieldClass} />
              </div>
            </div>
          </div>
        ) : null}
      </FormDrawer>

      <ConfirmDialog
        open={deleting !== null}
        title="Delete milestone"
        message={`Delete "${deleting?.name}"? Its tasks stay on the project without a milestone.`}
        confirmLabel="Delete"
        danger
        onCancel={() => setDeleting(null)}
        onConfirm={() => {
          if (!deleting) return
          deleteMilestone.mutate(deleting.id, { onSuccess: () => toast.success('Milestone deleted') })
          setDeleting(null)
        }}
      />
    </div>
  )
}
