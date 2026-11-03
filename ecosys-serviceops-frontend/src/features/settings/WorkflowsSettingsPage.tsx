import { useEffect, useMemo, useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import toast from 'react-hot-toast'
import { ArrowRight, ArrowDown, ArrowUp, GitBranch, Pencil, Plus, Trash2 } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { Spinner } from '../../shared/components/Spinner'
import { EmptyState } from '../../shared/components/EmptyState'
import { FormDrawer } from '../../shared/components/FormDrawer'
import { ConfirmDialog } from '../../shared/components/ConfirmDialog'
import { usePermission } from '../../shared/hooks/usePermission'
import { api } from '../../shared/api/apiClient'
import { endpoints } from '../../shared/api/endpoints'
import {
  useCreateWorkflow,
  useCreateWorkflowStatus,
  useDeleteWorkflow,
  useDeleteWorkflowStatus,
  useUpdateWorkflow,
  useUpdateWorkflowStatus,
  useWorkflows,
  type UpsertWorkflowStatusPayload,
  type WorkflowDefinition,
  type WorkflowStatus,
} from './workflowsApi'

const workflowsKey = ['settings', 'workflows'] as const

function statusUpdatePayload(status: WorkflowStatus, overrides: Partial<UpsertWorkflowStatusPayload> = {}): UpsertWorkflowStatusPayload {
  return {
    name: status.name,
    color: status.color,
    sortOrder: status.sortOrder,
    isInitial: status.isInitial,
    isFinal: status.isFinal,
    allowedNextStatusIds: status.allowedNextStatusIds,
    ...overrides,
  }
}

const colorSwatches = ['#64748b', '#2563eb', '#f59e0b', '#16a34a', '#334155', '#dc2626', '#7c3aed', '#0891b2']

function WorkflowEditor({ workflow, onClose }: { workflow: WorkflowDefinition | null; onClose: () => void }) {
  const canManage = usePermission('settings.manage')
  const create = useCreateWorkflow()
  const update = useUpdateWorkflow(workflow?.id ?? '')

  const [name, setName] = useState(workflow?.name ?? '')
  const [isDefault, setIsDefault] = useState(workflow?.isDefault ?? false)
  const [isActive, setIsActive] = useState(workflow?.isActive ?? true)

  const isPending = create.isPending || update.isPending

  const save = async () => {
    const payload = { name: name.trim(), isDefault, isActive }
    try {
      if (workflow) {
        await update.mutateAsync(payload)
      } else {
        await create.mutateAsync(payload)
      }
      toast.success('Workflow saved')
      onClose()
    } catch {
      // interceptor surfaces the error toast
    }
  }

  return (
    <FormDrawer
      open
      title={workflow ? `Edit ${workflow.name}` : 'New Workflow'}
      onClose={onClose}
      footer={
        canManage ? (
          <div className="flex items-center justify-end gap-2">
            <button type="button" onClick={onClose} className="inline-flex h-11 items-center justify-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">Cancel</button>
            <button type="button" onClick={save} disabled={isPending || !name.trim()} className="inline-flex h-11 items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:opacity-60">
              {isPending ? 'Saving…' : 'Save'}
            </button>
          </div>
        ) : null
      }
    >
      <div className="space-y-5">
        <div>
          <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="workflow-name">Name</label>
          <input id="workflow-name" value={name} disabled={!canManage} onChange={(event) => setName(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)] disabled:bg-slate-50" />
        </div>
        <label className="flex items-center gap-2 text-sm text-slate-700">
          <input type="checkbox" checked={isDefault} disabled={!canManage} onChange={(event) => setIsDefault(event.target.checked)} className="h-4 w-4 rounded border-slate-300" />
          Use as the default workflow for new work orders
        </label>
        <label className="flex items-center gap-2 text-sm text-slate-700">
          <input type="checkbox" checked={isActive} disabled={!canManage} onChange={(event) => setIsActive(event.target.checked)} className="h-4 w-4 rounded border-slate-300" />
          Active
        </label>
      </div>
    </FormDrawer>
  )
}

function StatusEditor({
  workflowId,
  status,
  onClose,
}: {
  workflowId: string
  status: WorkflowStatus | null
  onClose: () => void
}) {
  const canManage = usePermission('settings.manage')
  const create = useCreateWorkflowStatus(workflowId)
  const update = useUpdateWorkflowStatus(workflowId, status?.id ?? '')

  const [name, setName] = useState(status?.name ?? '')
  const [color, setColor] = useState(status?.color ?? colorSwatches[0]!)

  const isPending = create.isPending || update.isPending

  const save = async () => {
    const payload: UpsertWorkflowStatusPayload = {
      name: name.trim(),
      color,
      sortOrder: status?.sortOrder ?? null,
      isInitial: status?.isInitial ?? false,
      isFinal: status?.isFinal ?? false,
      allowedNextStatusIds: status?.allowedNextStatusIds ?? null,
    }

    try {
      if (status) {
        await update.mutateAsync(payload)
      } else {
        await create.mutateAsync(payload)
      }
      toast.success('Status saved')
      onClose()
    } catch {
      // interceptor surfaces the error toast
    }
  }

  return (
    <FormDrawer
      open
      title={status ? `Edit ${status.name}` : 'Add Status'}
      onClose={onClose}
      footer={
        canManage ? (
          <div className="flex items-center justify-end gap-2">
            <button type="button" onClick={onClose} className="inline-flex h-11 items-center justify-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">Cancel</button>
            <button type="button" onClick={save} disabled={isPending || !name.trim()} className="inline-flex h-11 items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:opacity-60">
              {isPending ? 'Saving…' : 'Save'}
            </button>
          </div>
        ) : null
      }
    >
      <div className="space-y-5">
        <div>
          <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="status-name">Name</label>
          <input id="status-name" value={name} disabled={!canManage} onChange={(event) => setName(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)] disabled:bg-slate-50" />
        </div>
        <div>
          <label className="mb-1.5 block text-sm font-medium text-slate-700">Colour</label>
          <div className="flex flex-wrap items-center gap-2">
            {colorSwatches.map((swatch) => (
              <button
                key={swatch}
                type="button"
                disabled={!canManage}
                onClick={() => setColor(swatch)}
                className={`h-8 w-8 rounded-full ring-2 ring-offset-2 transition ${color === swatch ? 'ring-[var(--app-primary)]' : 'ring-transparent'}`}
                style={{ backgroundColor: swatch }}
                aria-label={`Use colour ${swatch}`}
              />
            ))}
            <input
              type="color"
              value={color}
              disabled={!canManage}
              onChange={(event) => setColor(event.target.value)}
              className="h-8 w-10 cursor-pointer rounded border border-slate-300 bg-white p-0.5"
            />
          </div>
        </div>
      </div>
    </FormDrawer>
  )
}

function FlowPreview({ statuses }: { statuses: WorkflowStatus[] }) {
  const ordered = [...statuses].sort((a, b) => a.sortOrder - b.sortOrder)

  if (ordered.length === 0) {
    return null
  }

  return (
    <div className="flex flex-wrap items-center gap-2 overflow-x-auto rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 shadow-sm">
      {ordered.map((status, index) => (
        <div key={status.id} className="flex items-center gap-2">
          <span
            className="inline-flex items-center gap-1.5 whitespace-nowrap rounded-full px-3 py-1.5 text-xs font-medium text-white shadow-sm"
            style={{ backgroundColor: status.color }}
          >
            {status.isInitial ? '● ' : null}
            {status.name}
            {status.isFinal ? ' ●' : null}
          </span>
          {index < ordered.length - 1 ? <ArrowRight className="h-4 w-4 shrink-0 text-slate-400" /> : null}
        </div>
      ))}
    </div>
  )
}

function StatusCard({
  workflowId,
  status,
  allStatuses,
  isFirst,
  isLast,
  onMove,
  onEdit,
  onDelete,
}: {
  workflowId: string
  status: WorkflowStatus
  allStatuses: WorkflowStatus[]
  isFirst: boolean
  isLast: boolean
  onMove: (direction: 'up' | 'down') => void
  onEdit: () => void
  onDelete: () => void
}) {
  const canManage = usePermission('settings.manage')
  const update = useUpdateWorkflowStatus(workflowId, status.id)

  const toggleTransition = async (targetId: string) => {
    const next = status.allowedNextStatusIds.includes(targetId)
      ? status.allowedNextStatusIds.filter((id) => id !== targetId)
      : [...status.allowedNextStatusIds, targetId]

    try {
      await update.mutateAsync(statusUpdatePayload(status, { allowedNextStatusIds: next }))
    } catch {
      // interceptor surfaces the error toast
    }
  }

  const toggleFlag = async (field: 'isInitial' | 'isFinal') => {
    try {
      await update.mutateAsync(statusUpdatePayload(status, { [field]: !status[field] }))
    } catch {
      // interceptor surfaces the error toast
    }
  }

  const otherStatuses = allStatuses.filter((s) => s.id !== status.id)

  return (
    <div className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 shadow-sm">
      <div className="flex items-start justify-between gap-3">
        <div className="flex items-center gap-3">
          <span className="h-4 w-4 shrink-0 rounded-full" style={{ backgroundColor: status.color }} />
          <div>
            <p className="text-sm font-semibold text-slate-900">{status.name}</p>
            <div className="mt-1 flex flex-wrap gap-1.5">
              {status.isInitial ? (
                <span className="inline-flex items-center rounded-full bg-emerald-50 px-2 py-0.5 text-[11px] font-medium text-emerald-700 ring-1 ring-inset ring-emerald-200">Initial</span>
              ) : null}
              {status.isFinal ? (
                <span className="inline-flex items-center rounded-full bg-slate-100 px-2 py-0.5 text-[11px] font-medium text-slate-700 ring-1 ring-inset ring-slate-200">Final</span>
              ) : null}
            </div>
          </div>
        </div>

        {canManage ? (
          <div className="flex items-center gap-1">
            <button type="button" onClick={() => onMove('up')} disabled={isFirst} className="rounded p-1.5 text-slate-400 hover:bg-slate-50 hover:text-slate-700 disabled:opacity-30" aria-label="Move up">
              <ArrowUp className="h-3.5 w-3.5" />
            </button>
            <button type="button" onClick={() => onMove('down')} disabled={isLast} className="rounded p-1.5 text-slate-400 hover:bg-slate-50 hover:text-slate-700 disabled:opacity-30" aria-label="Move down">
              <ArrowDown className="h-3.5 w-3.5" />
            </button>
            <button type="button" onClick={onEdit} className="rounded p-1.5 text-slate-400 hover:bg-slate-50 hover:text-slate-700" aria-label="Edit status">
              <Pencil className="h-3.5 w-3.5" />
            </button>
            <button type="button" onClick={onDelete} className="rounded p-1.5 text-slate-400 hover:bg-rose-50 hover:text-rose-600" aria-label="Delete status">
              <Trash2 className="h-3.5 w-3.5" />
            </button>
          </div>
        ) : null}
      </div>

      <div className="mt-3 flex flex-wrap gap-2 border-t border-slate-100 pt-3">
        <label className="flex items-center gap-1.5 text-xs text-slate-600">
          <input type="checkbox" checked={status.isInitial} disabled={!canManage} onChange={() => toggleFlag('isInitial')} className="h-3.5 w-3.5 rounded border-slate-300" />
          Set as Initial
        </label>
        <label className="flex items-center gap-1.5 text-xs text-slate-600">
          <input type="checkbox" checked={status.isFinal} disabled={!canManage} onChange={() => toggleFlag('isFinal')} className="h-3.5 w-3.5 rounded border-slate-300" />
          Set as Final
        </label>
      </div>

      {otherStatuses.length > 0 ? (
        <div className="mt-3 border-t border-slate-100 pt-3">
          <p className="mb-1.5 text-xs font-medium text-slate-500">Can transition to</p>
          <div className="flex flex-wrap gap-1.5">
            {otherStatuses.map((target) => {
              const active = status.allowedNextStatusIds.includes(target.id)
              return (
                <button
                  key={target.id}
                  type="button"
                  disabled={!canManage}
                  onClick={() => toggleTransition(target.id)}
                  className={`rounded-full px-2.5 py-1 text-[11px] font-medium ring-1 ring-inset transition ${active ? 'bg-[var(--app-primary)] text-white ring-[var(--app-primary)]' : 'bg-white text-slate-600 ring-slate-200 hover:bg-slate-50'}`}
                >
                  {target.name}
                </button>
              )
            })}
          </div>
        </div>
      ) : null}
    </div>
  )
}

export function WorkflowsSettingsPage() {
  const { data, isLoading, isError } = useWorkflows()
  const canManage = usePermission('settings.manage')
  const deleteWorkflow = useDeleteWorkflow()
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [editingWorkflow, setEditingWorkflow] = useState<{ workflow: WorkflowDefinition | null } | null>(null)
  const [editingStatus, setEditingStatus] = useState<{ status: WorkflowStatus | null } | null>(null)
  const [confirmDeleteWorkflow, setConfirmDeleteWorkflow] = useState<WorkflowDefinition | null>(null)
  const [confirmDeleteStatus, setConfirmDeleteStatus] = useState<WorkflowStatus | null>(null)

  const workflows = useMemo(() => data ?? [], [data])
  const selected = workflows.find((w) => w.id === selectedId) ?? workflows[0] ?? null
  const deleteStatus = useDeleteWorkflowStatus(selected?.id ?? '')
  const queryClient = useQueryClient()

  useEffect(() => {
    if (!selectedId && workflows[0]) {
      setSelectedId(workflows[0].id)
    }
  }, [selectedId, workflows])

  if (isLoading) {
    return <Spinner label="Loading workflows" />
  }

  if (isError) {
    return <EmptyState icon={GitBranch} title="Workflows unavailable" description="Status workflows could not be loaded." />
  }

  const orderedStatuses = selected ? [...selected.statuses].sort((a, b) => a.sortOrder - b.sortOrder) : []

  const moveStatus = async (status: WorkflowStatus, direction: 'up' | 'down') => {
    const index = orderedStatuses.findIndex((s) => s.id === status.id)
    const swapWith = direction === 'up' ? orderedStatuses[index - 1] : orderedStatuses[index + 1]
    if (!swapWith || !selected) return

    try {
      await Promise.all([
        api.put(endpoints.workflows.updateStatus(selected.id, status.id), statusUpdatePayload(status, { sortOrder: swapWith.sortOrder })),
        api.put(endpoints.workflows.updateStatus(selected.id, swapWith.id), statusUpdatePayload(swapWith, { sortOrder: status.sortOrder })),
      ])
      await queryClient.invalidateQueries({ queryKey: workflowsKey })
    } catch {
      // interceptor surfaces the error toast
    }
  }

  return (
    <div className="space-y-4">
      <PageHeader
        title="Status Workflows"
        subtitle="Define custom status chains for work orders, with per-status transitions, colours, and initial/final markers."
        primaryAction={
          canManage ? (
            <button
              type="button"
              onClick={() => setEditingWorkflow({ workflow: null })}
              className="inline-flex h-11 items-center gap-1.5 rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)]"
            >
              <Plus className="h-4 w-4" />
              New Workflow
            </button>
          ) : undefined
        }
      />

      {workflows.length === 0 ? (
        <EmptyState icon={GitBranch} title="No workflows yet" description="Create a workflow to define a custom status chain." />
      ) : (
        <div className="grid grid-cols-1 gap-4 lg:grid-cols-[16rem_1fr]">
          <div className="space-y-2">
            {workflows.map((workflow) => (
              <button
                key={workflow.id}
                type="button"
                onClick={() => setSelectedId(workflow.id)}
                className={`w-full rounded-xl border px-3 py-2.5 text-left text-sm transition ${
                  selected?.id === workflow.id ? 'border-[var(--app-primary)] bg-[var(--app-primary)] text-white' : 'border-slate-200 bg-white text-slate-700 hover:bg-slate-50'
                }`}
              >
                <span className="font-medium">{workflow.name}</span>
                <span className="mt-1 flex flex-wrap gap-1">
                  {workflow.isDefault ? (
                    <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-[10px] font-medium ${selected?.id === workflow.id ? 'bg-white/20 text-white' : 'bg-emerald-50 text-emerald-700'}`}>
                      Default
                    </span>
                  ) : null}
                  {!workflow.isActive ? (
                    <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-[10px] font-medium ${selected?.id === workflow.id ? 'bg-white/20 text-white' : 'bg-slate-100 text-slate-600'}`}>
                      Inactive
                    </span>
                  ) : null}
                </span>
              </button>
            ))}
          </div>

          {selected ? (
            <div className="space-y-4">
              <div className="flex items-center justify-between">
                <div>
                  <h2 className="text-lg font-semibold text-slate-900">{selected.name}</h2>
                  <p className="text-sm text-slate-500">{orderedStatuses.length} status{orderedStatuses.length === 1 ? '' : 'es'}</p>
                </div>
                {canManage ? (
                  <div className="flex items-center gap-2">
                    <button type="button" onClick={() => setEditingWorkflow({ workflow: selected })} className="text-xs font-medium text-slate-600 hover:text-slate-900">Edit workflow</button>
                    <button type="button" onClick={() => setConfirmDeleteWorkflow(selected)} className="text-xs font-medium text-rose-500 hover:text-rose-700">Delete</button>
                  </div>
                ) : null}
              </div>

              <FlowPreview statuses={orderedStatuses} />

              <div className="space-y-3">
                {orderedStatuses.map((status, index) => (
                  <StatusCard
                    key={status.id}
                    workflowId={selected.id}
                    status={status}
                    allStatuses={orderedStatuses}
                    isFirst={index === 0}
                    isLast={index === orderedStatuses.length - 1}
                    onMove={(direction) => void moveStatus(status, direction)}
                    onEdit={() => setEditingStatus({ status })}
                    onDelete={() => setConfirmDeleteStatus(status)}
                  />
                ))}
              </div>

              {canManage ? (
                <button
                  type="button"
                  onClick={() => setEditingStatus({ status: null })}
                  className="inline-flex items-center gap-1.5 rounded-lg border border-dashed border-slate-300 px-4 py-2.5 text-sm font-medium text-slate-600 hover:bg-slate-50"
                >
                  <Plus className="h-4 w-4" />
                  Add Status
                </button>
              ) : null}
            </div>
          ) : null}
        </div>
      )}

      {editingWorkflow ? <WorkflowEditor workflow={editingWorkflow.workflow} onClose={() => setEditingWorkflow(null)} /> : null}
      {editingStatus && selected ? (
        <StatusEditor workflowId={selected.id} status={editingStatus.status} onClose={() => setEditingStatus(null)} />
      ) : null}

      <ConfirmDialog
        open={confirmDeleteWorkflow !== null}
        title="Delete Workflow"
        message={`This removes the "${confirmDeleteWorkflow?.name ?? ''}" workflow and all of its statuses.`}
        confirmLabel="Delete Workflow"
        danger
        onCancel={() => setConfirmDeleteWorkflow(null)}
        onConfirm={async () => {
          if (!confirmDeleteWorkflow) return
          try {
            await deleteWorkflow.mutateAsync(confirmDeleteWorkflow.id)
            toast.success('Workflow deleted')
            if (selectedId === confirmDeleteWorkflow.id) {
              setSelectedId(null)
            }
          } catch {
            // interceptor surfaces the error toast
          }
          setConfirmDeleteWorkflow(null)
        }}
      />

      <ConfirmDialog
        open={confirmDeleteStatus !== null}
        title="Delete Status"
        message={`This removes the "${confirmDeleteStatus?.name ?? ''}" status from the workflow.`}
        confirmLabel="Delete Status"
        danger
        onCancel={() => setConfirmDeleteStatus(null)}
        onConfirm={async () => {
          if (!confirmDeleteStatus) return
          try {
            await deleteStatus.mutateAsync(confirmDeleteStatus.id)
            toast.success('Status deleted')
          } catch {
            // interceptor surfaces the error toast
          }
          setConfirmDeleteStatus(null)
        }}
      />
    </div>
  )
}
