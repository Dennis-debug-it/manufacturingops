import { useEffect, useState } from 'react'
import toast from 'react-hot-toast'
import { Plus, Trash2 } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { Spinner } from '../../shared/components/Spinner'
import { ErrorState } from '../../shared/components/ErrorState'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { useTeamUsers } from '../team/api'
import { useApprovalMatrices, useUpsertApprovalMatrix, type ApprovalMatrix } from '../approvals/api'

const fieldClass = 'h-10 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none transition focus:border-[var(--app-primary)]'
const labelClass = 'mb-1 block text-xs font-medium text-slate-600'

const documentTypeLabels: Record<string, string> = {
  PurchaseOrder: 'Purchase Orders',
  Expense: 'Expenses',
  ProjectVariation: 'Project Variations',
  Quotation: 'Quotations (before sending)',
}

// Curated permission options (map to backend PermissionNames constants).
const permissionOptions = [
  { value: 'finance.manage_approvals', label: 'Finance — Manage Approvals' },
  { value: 'finance.manage', label: 'Finance — Manage' },
  { value: 'can_manage_materials', label: 'Inventory — Manage Materials' },
  { value: 'can_manage_settings', label: 'Settings — Manage' },
  { value: 'projects.variations.approve', label: 'Projects — Approve Variations' },
]
const defaultPermission = 'finance.manage_approvals'

interface EditableLevel {
  name: string
  minAmount: string
  maxAmount: string
  approverType: 'Permission' | 'SpecificUsers'
  requiredPermission: string
  approverUserIds: string[]
  reminderAfterHours: string
}

function toEditable(matrix: ApprovalMatrix): EditableLevel[] {
  if (matrix.levels.length === 0) {
    return [{ name: 'Approval', minAmount: '0', maxAmount: '', approverType: 'Permission', requiredPermission: defaultPermission, approverUserIds: [], reminderAfterHours: '48' }]
  }
  return matrix.levels.map((level) => ({
    name: level.name,
    minAmount: String(level.minAmount),
    maxAmount: level.maxAmount != null ? String(level.maxAmount) : '',
    approverType: level.approverType,
    requiredPermission: level.requiredPermission ?? defaultPermission,
    approverUserIds: level.approverUserIds,
    reminderAfterHours: level.reminderAfterHours != null ? String(level.reminderAfterHours) : '',
  }))
}

function MatrixEditor({ matrix }: { matrix: ApprovalMatrix }) {
  const save = useUpsertApprovalMatrix(matrix.documentType)
  const { data: users } = useTeamUsers()
  const [isActive, setIsActive] = useState(matrix.isActive)
  const [levels, setLevels] = useState<EditableLevel[]>(() => toEditable(matrix))

  useEffect(() => {
    setIsActive(matrix.isActive)
    setLevels(toEditable(matrix))
  }, [matrix])

  const updateLevel = (index: number, patch: Partial<EditableLevel>) => {
    setLevels((current) => current.map((l, i) => (i === index ? { ...l, ...patch } : l)))
  }

  const submit = async () => {
    const payloadLevels = levels.map((level) => ({
      name: level.name.trim(),
      minAmount: Number(level.minAmount) || 0,
      maxAmount: level.maxAmount.trim() ? Number(level.maxAmount) : null,
      approverType: level.approverType,
      requiredPermission: level.approverType === 'Permission' ? level.requiredPermission : null,
      approverUserIds: level.approverType === 'SpecificUsers' ? level.approverUserIds : [],
      reminderAfterHours: level.reminderAfterHours.trim() ? Number(level.reminderAfterHours) : null,
    }))

    // Client-side guard mirroring the server's contiguity rule for a friendlier message.
    const sorted = [...payloadLevels].sort((a, b) => a.minAmount - b.minAmount)
    for (let i = 1; i < sorted.length; i++) {
      const prev = sorted[i - 1]!
      const curr = sorted[i]!
      if (prev.maxAmount == null) {
        toast.error(`"${prev.name}" is unbounded but is not the highest band.`)
        return
      }
      if (prev.maxAmount !== curr.minAmount) {
        toast.error(`Bands must be contiguous: "${curr.name}" should start where "${prev.name}" ends.`)
        return
      }
    }

    try {
      await save.mutateAsync({ currency: matrix.currency, isActive, levels: payloadLevels })
      toast.success(`${documentTypeLabels[matrix.documentType] ?? matrix.documentType} approval matrix saved`)
    } catch {
      // interceptor surfaces the error toast
    }
  }

  return (
    <section className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 shadow-sm">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h2 className="text-base font-semibold text-slate-900">{documentTypeLabels[matrix.documentType] ?? matrix.documentType}</h2>
          <p className="mt-1 text-sm text-slate-600">Amount bands must be contiguous — each level starts where the previous ends. Leave the top band's max blank for “and above”.</p>
        </div>
        <div className="flex items-center gap-3">
          {matrix.isConfigured ? <StatusBadge status={matrix.isActive ? 'Active' : 'Inactive'} /> : <StatusBadge status="Not Configured" />}
          <label className="flex items-center gap-2 text-sm text-slate-700">
            <input type="checkbox" checked={isActive} onChange={(event) => setIsActive(event.target.checked)} className="h-4 w-4 rounded border-slate-300" />
            Active
          </label>
        </div>
      </div>

      <div className="mt-4 space-y-3">
        {levels.map((level, index) => (
          <div key={index} className="rounded-xl border border-slate-200 p-4">
            <div className="flex items-center justify-between">
              <span className="text-sm font-semibold text-slate-900">Level {index + 1}</span>
              {levels.length > 1 ? (
                <button type="button" onClick={() => setLevels((current) => current.filter((_, i) => i !== index))} className="inline-flex h-8 w-8 items-center justify-center rounded-lg text-rose-500 transition hover:bg-rose-50" aria-label="Remove level">
                  <Trash2 className="h-4 w-4" />
                </button>
              ) : null}
            </div>
            <div className="mt-3 grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-4">
              <div>
                <label className={labelClass}>Name</label>
                <input value={level.name} onChange={(event) => updateLevel(index, { name: event.target.value })} className={fieldClass} />
              </div>
              <div>
                <label className={labelClass}>Min Amount ({matrix.currency})</label>
                <input type="number" min="0" value={level.minAmount} onChange={(event) => updateLevel(index, { minAmount: event.target.value })} className={fieldClass} />
              </div>
              <div>
                <label className={labelClass}>Max Amount (blank = ∞)</label>
                <input type="number" min="0" value={level.maxAmount} onChange={(event) => updateLevel(index, { maxAmount: event.target.value })} className={fieldClass} />
              </div>
              <div>
                <label className={labelClass}>Reminder after (hrs)</label>
                <input type="number" min="0" value={level.reminderAfterHours} onChange={(event) => updateLevel(index, { reminderAfterHours: event.target.value })} className={fieldClass} placeholder="none" />
              </div>
            </div>
            <div className="mt-3 grid grid-cols-1 gap-3 sm:grid-cols-2">
              <div>
                <label className={labelClass}>Approver Type</label>
                <select value={level.approverType} onChange={(event) => updateLevel(index, { approverType: event.target.value as EditableLevel['approverType'] })} className={fieldClass}>
                  <option value="Permission">Anyone with a permission</option>
                  <option value="SpecificUsers">Specific users</option>
                </select>
              </div>
              {level.approverType === 'Permission' ? (
                <div>
                  <label className={labelClass}>Required Permission</label>
                  <select value={level.requiredPermission} onChange={(event) => updateLevel(index, { requiredPermission: event.target.value })} className={fieldClass}>
                    {permissionOptions.map((option) => (<option key={option.value} value={option.value}>{option.label}</option>))}
                  </select>
                </div>
              ) : (
                <div>
                  <label className={labelClass}>Approvers</label>
                  <div className="max-h-28 space-y-1 overflow-y-auto rounded-lg border border-slate-300 p-2">
                    {(users ?? []).map((user) => (
                      <label key={user.id} className="flex items-center gap-2 rounded px-1.5 py-1 text-sm text-slate-700 hover:bg-slate-50">
                        <input
                          type="checkbox"
                          checked={level.approverUserIds.includes(user.id)}
                          onChange={(event) => updateLevel(index, {
                            approverUserIds: event.target.checked
                              ? [...level.approverUserIds, user.id]
                              : level.approverUserIds.filter((id) => id !== user.id),
                          })}
                          className="h-4 w-4 rounded border-slate-300"
                        />
                        {user.fullName}
                      </label>
                    ))}
                  </div>
                </div>
              )}
            </div>
          </div>
        ))}
      </div>

      <div className="mt-4 flex items-center justify-between">
        <button
          type="button"
          onClick={() => {
            const last = levels[levels.length - 1]
            const nextMin = last?.maxAmount?.trim() ? last.maxAmount : ''
            setLevels((current) => [...current, { name: `Level ${current.length + 1}`, minAmount: nextMin, maxAmount: '', approverType: 'Permission', requiredPermission: defaultPermission, approverUserIds: [], reminderAfterHours: '48' }])
          }}
          className="inline-flex h-10 items-center gap-2 rounded-lg border border-slate-200 bg-white px-3.5 text-sm font-medium text-slate-700 transition hover:bg-slate-50"
        >
          <Plus className="h-4 w-4" />Add Level
        </button>
        <button type="button" onClick={() => void submit()} disabled={save.isPending} className="inline-flex h-10 items-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:opacity-60">
          {save.isPending ? 'Saving…' : 'Save Matrix'}
        </button>
      </div>
    </section>
  )
}

export function ApprovalMatrixSettingsPage() {
  const { data, isLoading, isError, refetch } = useApprovalMatrices()

  return (
    <div className="space-y-4">
      <PageHeader
        title="Approval Matrix"
        subtitle="Route purchase orders and expenses through amount-based approval chains. With no active matrix, documents keep their current single-step approval."
        breadcrumbs={[{ label: 'Settings', to: '/settings' }, { label: 'Approval Matrix' }]}
      />
      {isLoading ? (
        <Spinner label="Loading approval matrices" />
      ) : isError || !data ? (
        <ErrorState
          title="Approval matrices unavailable"
          description="We couldn’t load the configured matrices — this is not a confirmation that none exist."
          onRetry={() => void refetch()}
        />
      ) : (
        <div className="space-y-4">
          {data.map((matrix) => (
            <MatrixEditor key={matrix.documentType} matrix={matrix} />
          ))}
        </div>
      )}
    </div>
  )
}
