import { useMemo, useState } from 'react'
import toast from 'react-hot-toast'
import { Plus, Pencil, Trash2 } from 'lucide-react'
import { DataTable, type DataTableColumn } from '../../../shared/components/DataTable'
import { FormDrawer } from '../../../shared/components/FormDrawer'
import { StatusBadge } from '../../../shared/components/StatusBadge'
import { formatDate } from '../../../shared/utils/dates'
import { useCreateTask, useDeleteTask, useProjectMilestones, useProjectTasks, useUpdateTask } from '../api'
import type { ProjectDetail, ProjectTask, TaskStatus } from '../types'
import { dateInputValue, dateOrNull, fieldClass, labelClass, primaryButtonClass, secondaryButtonClass, smallButtonClass, textAreaClass, textOrNull } from './formStyles'

const taskStatuses: TaskStatus[] = ['Open', 'InProgress', 'Done', 'Cancelled']
const statusLabels: Record<TaskStatus, string> = { Open: 'Open', InProgress: 'In progress', Done: 'Done', Cancelled: 'Cancelled' }

interface Draft {
  id?: string
  title: string
  description: string
  milestoneId: string
  assigneeUserId: string
  dueDate: string
  priority: string
  status: TaskStatus
}

const emptyDraft: Draft = { title: '', description: '', milestoneId: '', assigneeUserId: '', dueDate: '', priority: 'Medium', status: 'Open' }

export function TasksTab({ project, readOnly }: { project: ProjectDetail; readOnly: boolean }) {
  const projectId = project.summary.id
  const { data: tasks, isLoading } = useProjectTasks(projectId)
  const { data: milestones } = useProjectMilestones(projectId)
  const createTask = useCreateTask(projectId)
  const updateTask = useUpdateTask(projectId)
  const deleteTask = useDeleteTask(projectId)
  const [draft, setDraft] = useState<Draft | null>(null)
  const [showDone, setShowDone] = useState(false)

  // Tasks are assigned to people on the project team who have a login.
  const assignees = project.members.filter((member) => member.userId)
  const visible = useMemo(() => (tasks ?? []).filter((task) => showDone || (task.status !== 'Done' && task.status !== 'Cancelled')), [tasks, showDone])

  const toPayload = (d: Draft) => ({
    title: d.title.trim(),
    description: textOrNull(d.description),
    milestoneId: d.milestoneId || null,
    assigneeUserId: d.assigneeUserId || null,
    dueDate: dateOrNull(d.dueDate),
    priority: d.priority,
    status: d.status,
  })

  const save = async () => {
    if (!draft || !draft.title.trim()) return
    try {
      if (draft.id) await updateTask.mutateAsync({ taskId: draft.id, ...toPayload(draft) })
      else await createTask.mutateAsync(toPayload(draft))
      toast.success(draft.id ? 'Task updated' : 'Task added')
      setDraft(null)
    } catch {
      // toast from the API client
    }
  }

  const toDraft = (task: ProjectTask): Draft => ({
    id: task.id,
    title: task.title,
    description: task.description ?? '',
    milestoneId: task.milestoneId ?? '',
    assigneeUserId: task.assigneeUserId ?? '',
    dueDate: dateInputValue(task.dueDate),
    priority: task.priority,
    status: task.status,
  })

  const markDone = (task: ProjectTask) => updateTask.mutate({ taskId: task.id, ...toPayload({ ...toDraft(task), status: task.status === 'Done' ? 'Open' : 'Done' }) })

  const columns: DataTableColumn<ProjectTask>[] = [
    {
      key: 'title',
      header: 'Task',
      cell: (row) => (
        <label className="flex items-start gap-2">
          {!readOnly ? <input type="checkbox" checked={row.status === 'Done'} onChange={() => markDone(row)} className="mt-0.5 h-4 w-4 rounded border-slate-300" aria-label={`Mark ${row.title} done`} /> : null}
          <span className={row.status === 'Done' ? 'text-slate-400 line-through' : 'font-medium text-slate-900'}>{row.title}</span>
        </label>
      ),
    },
    { key: 'milestoneName', header: 'Milestone', cell: (row) => row.milestoneName ?? '—' },
    { key: 'assigneeName', header: 'Assignee', cell: (row) => row.assigneeName ?? 'Unassigned' },
    { key: 'dueDate', header: 'Due', cell: (row) => (row.dueDate ? formatDate(row.dueDate) : '—') },
    { key: 'priority', header: 'Priority', cell: (row) => row.priority },
    { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={statusLabels[row.status]} /> },
    ...(readOnly ? [] : [{
      key: 'actions',
      header: '',
      cell: (row: ProjectTask) => (
        <div className="flex justify-end gap-2">
          <button type="button" aria-label={`Edit ${row.title}`} className={smallButtonClass} onClick={() => setDraft(toDraft(row))}><Pencil className="h-3.5 w-3.5" /></button>
          <button type="button" aria-label={`Delete ${row.title}`} className={smallButtonClass} onClick={() => deleteTask.mutate(row.id, { onSuccess: () => toast.success('Task deleted') })}><Trash2 className="h-3.5 w-3.5" /></button>
        </div>
      ),
    }]),
  ]

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <label className="flex items-center gap-2 text-sm text-slate-600">
          <input type="checkbox" checked={showDone} onChange={(event) => setShowDone(event.target.checked)} className="h-4 w-4 rounded border-slate-300" />
          Show done and cancelled
        </label>
        {!readOnly ? <button type="button" className={primaryButtonClass} onClick={() => setDraft(emptyDraft)}><Plus className="h-4 w-4" />Add task</button> : null}
      </div>
      <DataTable columns={columns} data={visible} loading={isLoading} emptyTitle="No open tasks" emptyDescription="Tasks assigned on this project will appear here." />

      <FormDrawer
        open={draft !== null}
        title={draft?.id ? 'Edit task' : 'Add task'}
        onClose={() => setDraft(null)}
        footer={
          <div className="flex justify-end gap-2">
            <button type="button" className={secondaryButtonClass} onClick={() => setDraft(null)}>Cancel</button>
            <button type="button" className={primaryButtonClass} disabled={!draft?.title.trim() || createTask.isPending || updateTask.isPending} onClick={() => void save()}>Save</button>
          </div>
        }
      >
        {draft ? (
          <div className="space-y-4">
            <div>
              <label className={labelClass} htmlFor="task-title">Task</label>
              <input id="task-title" value={draft.title} onChange={(event) => setDraft({ ...draft, title: event.target.value })} className={fieldClass} />
            </div>
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
              <div>
                <label className={labelClass} htmlFor="task-milestone">Milestone</label>
                <select id="task-milestone" value={draft.milestoneId} onChange={(event) => setDraft({ ...draft, milestoneId: event.target.value })} className={fieldClass}>
                  <option value="">None</option>
                  {(milestones ?? []).map((m) => (<option key={m.id} value={m.id}>{m.name}</option>))}
                </select>
              </div>
              <div>
                <label className={labelClass} htmlFor="task-assignee">Assignee</label>
                <select id="task-assignee" value={draft.assigneeUserId} onChange={(event) => setDraft({ ...draft, assigneeUserId: event.target.value })} className={fieldClass}>
                  <option value="">Unassigned</option>
                  {assignees.map((member) => (<option key={member.id} value={member.userId ?? ''}>{member.name}</option>))}
                </select>
                {assignees.length === 0 ? <p className="mt-1 text-xs text-slate-500">Add people to the team first.</p> : null}
              </div>
              <div>
                <label className={labelClass} htmlFor="task-due">Due date</label>
                <input id="task-due" type="date" value={draft.dueDate} onChange={(event) => setDraft({ ...draft, dueDate: event.target.value })} className={fieldClass} />
              </div>
              <div>
                <label className={labelClass} htmlFor="task-priority">Priority</label>
                <select id="task-priority" value={draft.priority} onChange={(event) => setDraft({ ...draft, priority: event.target.value })} className={fieldClass}>
                  {['Low', 'Medium', 'High'].map((p) => (<option key={p} value={p}>{p}</option>))}
                </select>
              </div>
              {draft.id ? (
                <div>
                  <label className={labelClass} htmlFor="task-status">Status</label>
                  <select id="task-status" value={draft.status} onChange={(event) => setDraft({ ...draft, status: event.target.value as TaskStatus })} className={fieldClass}>
                    {taskStatuses.map((s) => (<option key={s} value={s}>{statusLabels[s]}</option>))}
                  </select>
                </div>
              ) : null}
            </div>
            <div>
              <label className={labelClass} htmlFor="task-description">Notes</label>
              <textarea id="task-description" rows={3} value={draft.description} onChange={(event) => setDraft({ ...draft, description: event.target.value })} className={textAreaClass} />
            </div>
          </div>
        ) : null}
      </FormDrawer>
    </div>
  )
}
