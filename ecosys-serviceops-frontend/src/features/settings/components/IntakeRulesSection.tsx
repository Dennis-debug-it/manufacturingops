import { useEffect, useState } from 'react'
import toast from 'react-hot-toast'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { FlaskConical, Plus, Trash2 } from 'lucide-react'
import { api } from '../../../shared/api/apiClient'
import { DataTable, type DataTableColumn } from '../../../shared/components/DataTable'
import { ErrorState } from '../../../shared/components/ErrorState'
import { StatusBadge } from '../../../shared/components/StatusBadge'
import { FormDrawer } from '../../../shared/components/FormDrawer'
import { ConfirmDialog } from '../../../shared/components/ConfirmDialog'
import { formatDateTime } from '../../../shared/utils/dates'
import { useClients } from '../../clients/api'
import { useAssignmentGroups } from '../../team/api'

const baseUrl = '/api/settings/intake-protocols'
const fieldClass = 'h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none transition focus:border-[var(--app-primary)]'
const labelClass = 'mb-1.5 block text-sm font-medium text-slate-700'

const criteriaFields = ['Subject', 'Body', 'Sender'] as const
const criteriaOperators = ['Contains', 'Equals', 'Is Not Empty'] as const
const priorities = ['Critical', 'High', 'Medium', 'Low']

interface IntakeProtocol {
  id: string
  name: string
  sourceType: string
  isActive: boolean
  description: string | null
  criteriaJson: string
  actionsJson: string
  sourceConfigJson: string
  lastTriggeredAt: string | null
  lastTriggerStatus: string | null
  lastError: string | null
}

interface CriterionRow {
  field: (typeof criteriaFields)[number]
  operator: (typeof criteriaOperators)[number]
  value: string
}

interface RuleFormState {
  name: string
  sourceType: 'Email' | 'Monitoring'
  isActive: boolean
  description: string
  criteria: CriterionRow[]
  priority: string
  titleTemplate: string
  clientId: string
  assignmentGroupId: string
}

const emptyForm: RuleFormState = {
  name: '',
  sourceType: 'Email',
  isActive: true,
  description: '',
  criteria: [{ field: 'Subject', operator: 'Is Not Empty', value: '' }],
  priority: 'Medium',
  titleTemplate: '{{subject}}',
  clientId: '',
  assignmentGroupId: '',
}

function parseProtocolToForm(protocol: IntakeProtocol): RuleFormState {
  let criteria: CriterionRow[] = emptyForm.criteria
  let priority = 'Medium'
  let titleTemplate = '{{subject}}'
  let clientId = ''
  let assignmentGroupId = ''
  try {
    const groups = JSON.parse(protocol.criteriaJson) as Array<{ criteria?: Array<{ field?: string; operator?: string; value?: string }> }>
    const rows = (groups ?? []).flatMap((group) => group.criteria ?? [])
    if (rows.length > 0) {
      criteria = rows.map((row) => ({
        field: (criteriaFields as readonly string[]).includes(row.field ?? '') ? (row.field as CriterionRow['field']) : 'Subject',
        operator: (criteriaOperators as readonly string[]).includes(row.operator ?? '') ? (row.operator as CriterionRow['operator']) : 'Contains',
        value: row.value ?? '',
      }))
    }
  } catch {
    // fall back to defaults for unparseable legacy JSON
  }
  try {
    const actions = JSON.parse(protocol.actionsJson) as { createWorkOrder?: { priority?: string; workOrderTitleTemplate?: string; clientId?: string | null; assignmentGroupId?: string | null } }
    priority = actions.createWorkOrder?.priority ?? priority
    titleTemplate = actions.createWorkOrder?.workOrderTitleTemplate ?? titleTemplate
    clientId = actions.createWorkOrder?.clientId ?? ''
    assignmentGroupId = actions.createWorkOrder?.assignmentGroupId ?? ''
  } catch {
    // fall back to defaults for unparseable legacy JSON
  }
  return {
    name: protocol.name,
    sourceType: protocol.sourceType === 'Monitoring' ? 'Monitoring' : 'Email',
    isActive: protocol.isActive,
    description: protocol.description ?? '',
    criteria,
    priority,
    titleTemplate,
    clientId,
    assignmentGroupId,
  }
}

function buildPayload(form: RuleFormState, existing?: IntakeProtocol) {
  const criteriaJson = JSON.stringify([
    {
      id: 'group-1',
      logic: 'AND',
      criteria: form.criteria.map((row, index) => ({
        id: `criterion-${index + 1}`,
        field: row.field,
        operator: row.operator,
        value: row.operator === 'Is Not Empty' ? '' : row.value,
      })),
    },
  ])
  const actionsJson = JSON.stringify({
    createWorkOrder: {
      enabled: true,
      workOrderTitleTemplate: form.titleTemplate.trim() || '{{subject}}',
      workOrderDescriptionTemplate: '{{body}}',
      workOrderType: 'Corrective',
      priority: form.priority,
      clientId: form.clientId || null,
      branchId: null,
      assetId: null,
      assignmentGroupId: form.assignmentGroupId || null,
      assignedUserId: null,
      dueDateRule: 'None',
      tags: '',
      autoCreateImmediately: true,
    },
    sendNotification: { enabled: false },
    attachMetadata: { enabled: true, storeSenderEmail: true, storeSourceChannel: true, storeMatchedRuleName: true },
  })
  return {
    name: form.name.trim(),
    sourceType: form.sourceType,
    isActive: form.isActive,
    description: form.description.trim() || null,
    criteriaJson,
    actionsJson,
    // The mailbox / webhook connection is configured globally above; per-rule source
    // overrides keep whatever the rule already had.
    sourceConfigJson: existing?.sourceConfigJson ?? '{}',
  }
}

export function IntakeRulesSection() {
  const queryClient = useQueryClient()
  const { data: protocols, isLoading, isError, refetch } = useQuery({
    queryKey: ['intake-protocols'],
    queryFn: () => api.get<IntakeProtocol[]>(baseUrl),
  })
  const { data: clients } = useClients()
  const { data: groups } = useAssignmentGroups()

  const [editing, setEditing] = useState<IntakeProtocol | null>(null)
  const [creating, setCreating] = useState(false)
  const [deleting, setDeleting] = useState<IntakeProtocol | null>(null)
  const [form, setForm] = useState<RuleFormState>(emptyForm)

  useEffect(() => {
    if (creating) {
      setForm(emptyForm)
    } else if (editing) {
      setForm(parseProtocolToForm(editing))
    }
  }, [creating, editing])

  const invalidate = () => void queryClient.invalidateQueries({ queryKey: ['intake-protocols'] })

  const save = useMutation({
    mutationFn: () => {
      const payload = buildPayload(form, editing ?? undefined)
      return editing ? api.put(`${baseUrl}/${editing.id}`, payload) : api.post(baseUrl, payload)
    },
    onSuccess: () => {
      toast.success(editing ? 'Intake rule updated' : 'Intake rule created')
      invalidate()
      setEditing(null)
      setCreating(false)
    },
  })

  const remove = useMutation({
    mutationFn: (id: string) => api.delete(`${baseUrl}/${id}`),
    onSuccess: () => {
      toast.success('Intake rule deleted')
      invalidate()
    },
  })

  const test = useMutation({
    mutationFn: (id: string) => api.post<{ success: boolean; lastError: string | null }>(`${baseUrl}/${id}/test`),
    onSuccess: (result) => {
      if (result.success) {
        toast.success('Connection test passed')
      } else {
        toast.error(result.lastError ?? 'Connection test failed')
      }
      invalidate()
    },
  })

  const drawerOpen = creating || Boolean(editing)

  const columns: DataTableColumn<IntakeProtocol>[] = [
    {
      key: 'name',
      header: 'Rule',
      cell: (row) => (
        <button type="button" onClick={() => { setCreating(false); setEditing(row) }} className="font-medium text-slate-900 underline decoration-slate-300 underline-offset-4">
          {row.name}
        </button>
      ),
    },
    { key: 'sourceType', header: 'Source', cell: (row) => row.sourceType },
    { key: 'isActive', header: 'Status', cell: (row) => <StatusBadge status={row.isActive ? 'Active' : 'Inactive'} /> },
    { key: 'lastTriggeredAt', header: 'Last Triggered', cell: (row) => (row.lastTriggeredAt ? formatDateTime(row.lastTriggeredAt) : '—') },
    { key: 'lastTriggerStatus', header: 'Health', cell: (row) => (row.lastError ? <StatusBadge status="Error" /> : row.lastTriggerStatus ? <StatusBadge status={row.lastTriggerStatus} /> : '—') },
    {
      key: 'actions',
      header: '',
      cell: (row) => (
        <div className="flex items-center gap-1">
          <button type="button" onClick={() => test.mutate(row.id)} className="inline-flex h-8 w-8 items-center justify-center rounded-lg text-slate-500 transition hover:bg-slate-100" aria-label={`Test ${row.name}`} title="Test connection">
            <FlaskConical className="h-4 w-4" />
          </button>
          <button type="button" onClick={() => setDeleting(row)} className="inline-flex h-8 w-8 items-center justify-center rounded-lg text-rose-500 transition hover:bg-rose-50" aria-label={`Delete ${row.name}`} title="Delete rule">
            <Trash2 className="h-4 w-4" />
          </button>
        </div>
      ),
    },
  ]

  const updateCriterion = (index: number, patch: Partial<CriterionRow>) => {
    setForm((current) => ({
      ...current,
      criteria: current.criteria.map((row, i) => (i === index ? { ...row, ...patch } : row)),
    }))
  }

  return (
    <section className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 shadow-sm">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h2 className="text-base font-semibold text-slate-900">Intake Rules</h2>
          <p className="mt-1 text-sm text-slate-600">Multiple rules can match incoming email or monitoring alerts and create work orders with different defaults.</p>
        </div>
        <button type="button" onClick={() => { setEditing(null); setCreating(true) }} className="inline-flex h-10 items-center gap-2 rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)]">
          <Plus className="h-4 w-4" />New Rule
        </button>
      </div>

      <div className="mt-4">
        {isError ? (
          <ErrorState
            title="Intake rules unavailable"
            description="We couldn’t load the configured rules, so this is not a confirmation that there are none."
            onRetry={() => void refetch()}
          />
        ) : (
          <DataTable columns={columns} data={protocols ?? []} loading={isLoading} emptyTitle="No intake rules" emptyDescription="Create a rule to auto-create work orders from incoming email or monitoring alerts." />
        )}
      </div>

      <FormDrawer
        open={drawerOpen}
        title={editing ? `Edit ${editing.name}` : 'New Intake Rule'}
        onClose={() => { setEditing(null); setCreating(false) }}
        footer={
          <div className="flex items-center justify-end gap-2">
            <button type="button" onClick={() => { setEditing(null); setCreating(false) }} className="inline-flex h-11 items-center justify-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">Cancel</button>
            <button
              type="button"
              onClick={() => {
                if (!form.name.trim()) {
                  toast.error('Rule name is required')
                  return
                }
                save.mutate()
              }}
              disabled={save.isPending}
              className="inline-flex h-11 items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:opacity-60"
            >
              {save.isPending ? 'Saving…' : editing ? 'Save Changes' : 'Create Rule'}
            </button>
          </div>
        }
      >
        <div className="space-y-4">
          <div>
            <label className={labelClass} htmlFor="rule-name">Rule Name</label>
            <input id="rule-name" value={form.name} onChange={(event) => setForm((c) => ({ ...c, name: event.target.value }))} className={fieldClass} />
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className={labelClass} htmlFor="rule-source">Source</label>
              <select id="rule-source" value={form.sourceType} onChange={(event) => setForm((c) => ({ ...c, sourceType: event.target.value as RuleFormState['sourceType'] }))} className={fieldClass}>
                <option value="Email">Email</option>
                <option value="Monitoring">Monitoring</option>
              </select>
            </div>
            <div className="flex items-end pb-3">
              <label className="flex items-center gap-2 text-sm text-slate-700">
                <input type="checkbox" checked={form.isActive} onChange={(event) => setForm((c) => ({ ...c, isActive: event.target.checked }))} className="h-4 w-4 rounded border-slate-300" />
                Active
              </label>
            </div>
          </div>
          <div>
            <label className={labelClass} htmlFor="rule-description">Description</label>
            <input id="rule-description" value={form.description} onChange={(event) => setForm((c) => ({ ...c, description: event.target.value }))} className={fieldClass} />
          </div>

          <div>
            <span className={labelClass}>Match Criteria (all must match)</span>
            <div className="space-y-2">
              {form.criteria.map((row, index) => (
                <div key={index} className="flex items-center gap-2">
                  <select value={row.field} onChange={(event) => updateCriterion(index, { field: event.target.value as CriterionRow['field'] })} className={`${fieldClass} w-28`} aria-label="Criterion field">
                    {criteriaFields.map((field) => (<option key={field} value={field}>{field}</option>))}
                  </select>
                  <select value={row.operator} onChange={(event) => updateCriterion(index, { operator: event.target.value as CriterionRow['operator'] })} className={`${fieldClass} w-36`} aria-label="Criterion operator">
                    {criteriaOperators.map((op) => (<option key={op} value={op}>{op}</option>))}
                  </select>
                  <input
                    value={row.value}
                    onChange={(event) => updateCriterion(index, { value: event.target.value })}
                    disabled={row.operator === 'Is Not Empty'}
                    placeholder={row.operator === 'Is Not Empty' ? '—' : 'e.g. urgent'}
                    className={`${fieldClass} flex-1 disabled:bg-slate-50`}
                    aria-label="Criterion value"
                  />
                  {form.criteria.length > 1 ? (
                    <button type="button" onClick={() => setForm((c) => ({ ...c, criteria: c.criteria.filter((_, i) => i !== index) }))} className="inline-flex h-9 w-9 items-center justify-center rounded-lg text-rose-500 transition hover:bg-rose-50" aria-label="Remove criterion">
                      <Trash2 className="h-4 w-4" />
                    </button>
                  ) : null}
                </div>
              ))}
            </div>
            <button type="button" onClick={() => setForm((c) => ({ ...c, criteria: [...c.criteria, { field: 'Subject', operator: 'Contains', value: '' }] }))} className="mt-2 text-sm font-medium text-slate-700 underline decoration-slate-300 underline-offset-4">
              + Add criterion
            </button>
          </div>

          <div className="rounded-xl border border-slate-200 bg-slate-50 p-4">
            <div className="text-sm font-semibold text-slate-900">Work Order Defaults</div>
            <div className="mt-3 space-y-3">
              <div>
                <label className={labelClass} htmlFor="rule-title-template">Title Template</label>
                <input id="rule-title-template" value={form.titleTemplate} onChange={(event) => setForm((c) => ({ ...c, titleTemplate: event.target.value }))} className={fieldClass} />
                <p className="mt-1 text-xs text-slate-500">Placeholders: {'{{subject}}'}, {'{{body}}'}</p>
              </div>
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className={labelClass} htmlFor="rule-priority">Priority</label>
                  <select id="rule-priority" value={form.priority} onChange={(event) => setForm((c) => ({ ...c, priority: event.target.value }))} className={fieldClass}>
                    {priorities.map((priority) => (<option key={priority} value={priority}>{priority}</option>))}
                  </select>
                </div>
                <div>
                  <label className={labelClass} htmlFor="rule-group">Assignment Group</label>
                  <select id="rule-group" value={form.assignmentGroupId} onChange={(event) => setForm((c) => ({ ...c, assignmentGroupId: event.target.value }))} className={fieldClass}>
                    <option value="">None</option>
                    {(groups ?? []).map((group) => (<option key={group.id} value={group.id}>{group.name}</option>))}
                  </select>
                </div>
              </div>
              <div>
                <label className={labelClass} htmlFor="rule-client">Default Client</label>
                <select id="rule-client" value={form.clientId} onChange={(event) => setForm((c) => ({ ...c, clientId: event.target.value }))} className={fieldClass}>
                  <option value="">None — resolve from sender</option>
                  {(clients ?? []).map((client) => (<option key={client.id} value={client.id}>{client.clientName}</option>))}
                </select>
              </div>
            </div>
          </div>
        </div>
      </FormDrawer>

      <ConfirmDialog
        open={Boolean(deleting)}
        title="Delete Intake Rule"
        message={`Delete "${deleting?.name ?? ''}"? Incoming messages will no longer match this rule.`}
        confirmLabel="Delete"
        danger
        onCancel={() => setDeleting(null)}
        onConfirm={() => {
          if (deleting) {
            remove.mutate(deleting.id)
          }
          setDeleting(null)
        }}
      />
    </section>
  )
}
