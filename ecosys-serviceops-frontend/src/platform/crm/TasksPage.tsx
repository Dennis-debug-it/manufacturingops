import { useState } from 'react'
import toast from 'react-hot-toast'
import { Plus } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { FormDrawer } from '../../shared/components/FormDrawer'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { formatDate } from '../../shared/utils/dates'
import { useCompleteCrmTask, useCreateCrmTask, useCrmTasks, type CrmTask, type CrmTaskPriority, type CrmTaskStatus } from './crmStubApi'
import { CrmSubNav } from './CrmSubNav'

const backendGapNote = 'No Tasks resource exists yet in the current backend. This is the full intended task-tracking UI wired against typed stub data until a backend Tasks endpoint is connected.'
const statusOptions: Array<CrmTaskStatus | 'All'> = ['All', 'Open', 'InProgress', 'Done']

export function TasksPage() {
  const [statusFilter, setStatusFilter] = useState<CrmTaskStatus | 'All'>('All')
  const { data, isLoading } = useCrmTasks(statusFilter)
  const createTask = useCreateCrmTask()
  const [showNewTask, setShowNewTask] = useState(false)
  const [title, setTitle] = useState('')
  const [dueDate, setDueDate] = useState('')
  const [priority, setPriority] = useState<CrmTaskPriority>('Medium')

  const submit = async () => {
    try {
      await createTask.mutateAsync({ title, dueDate: dueDate || undefined, priority })
      toast.success('Task created')
      setShowNewTask(false)
    } catch {
      toast.error('CRM tasks are not yet backed by a live API — this is a UI stub.')
    }
  }

  const columns: DataTableColumn<CrmTask>[] = [
    { key: 'title', header: 'Task', cell: (row) => <span className="font-medium text-slate-900">{row.title}</span> },
    { key: 'relatedContactName', header: 'Contact', cell: (row) => row.relatedContactName ?? '—' },
    { key: 'assignedToName', header: 'Assigned To', cell: (row) => row.assignedToName },
    { key: 'dueDate', header: 'Due', cell: (row) => (row.dueDate ? formatDate(row.dueDate) : '—') },
    { key: 'priority', header: 'Priority', cell: (row) => <StatusBadge status={row.priority} /> },
    { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.status} /> },
    { key: 'actions', header: '', cell: (row) => <TaskRowActions task={row} /> },
  ]

  return (
    <div className="space-y-6">
      <PageHeader
        title="Tasks"
        subtitle="Follow-ups and to-dos tracked against CRM contacts and leads."
        primaryAction={
          <button type="button" onClick={() => setShowNewTask(true)} className="inline-flex h-11 items-center gap-2 rounded-lg bg-slate-900 px-4 text-sm font-medium text-white transition hover:bg-slate-800">
            <Plus className="h-4 w-4" />New Task
          </button>
        }
      />
      <CrmSubNav />
      <div className="rounded-2xl border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-900">{backendGapNote}</div>
      <div className="flex flex-wrap gap-2 rounded-2xl border border-slate-200 bg-white p-3 shadow-sm">
        {statusOptions.map((status) => (
          <button
            key={status}
            type="button"
            onClick={() => setStatusFilter(status)}
            className={`inline-flex h-10 items-center rounded-lg px-4 text-sm font-medium transition ${statusFilter === status ? 'bg-slate-900 text-white' : 'text-slate-600 hover:bg-slate-100'}`}
          >
            {status === 'InProgress' ? 'In Progress' : status}
          </button>
        ))}
      </div>
      <DataTable
        columns={columns}
        data={data ?? []}
        loading={isLoading}
        emptyTitle="No tasks yet"
        emptyDescription="Follow-up tasks will appear here once a backend Tasks endpoint is connected."
      />

      <FormDrawer open={showNewTask} title="New Task" onClose={() => setShowNewTask(false)}>
        <div className="space-y-4">
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Title</label>
            <input value={title} onChange={(event) => setTitle(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900" />
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Due Date (optional)</label>
            <input type="date" value={dueDate} onChange={(event) => setDueDate(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900" />
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Priority</label>
            <select value={priority} onChange={(event) => setPriority(event.target.value as CrmTaskPriority)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900">
              <option>Low</option>
              <option>Medium</option>
              <option>High</option>
            </select>
          </div>
          <button
            type="button"
            disabled={!title.trim() || createTask.isPending}
            onClick={() => void submit()}
            className="inline-flex h-11 w-full items-center justify-center rounded-lg bg-slate-900 px-4 text-sm font-medium text-white hover:bg-slate-800 disabled:opacity-60"
          >
            {createTask.isPending ? 'Creating…' : 'Create Task'}
          </button>
        </div>
      </FormDrawer>
    </div>
  )
}

function TaskRowActions({ task }: { task: CrmTask }) {
  const complete = useCompleteCrmTask(task.id)

  if (task.status === 'Done') {
    return null
  }

  const markDone = async () => {
    try {
      await complete.mutateAsync()
      toast.success('Task marked done')
    } catch {
      toast.error('CRM tasks are not yet backed by a live API — this is a UI stub.')
    }
  }

  return (
    <div className="flex justify-end">
      <button type="button" disabled={complete.isPending} onClick={() => void markDone()} className="rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-xs font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-60">
        Mark Done
      </button>
    </div>
  )
}
