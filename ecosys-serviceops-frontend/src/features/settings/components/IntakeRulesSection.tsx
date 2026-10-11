import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import toast from 'react-hot-toast'
import { Plus, Trash2 } from 'lucide-react'
import { api } from '../../../shared/api/apiClient'
import { endpoints } from '../../../shared/api/endpoints'
import { formatDateTime } from '../../../shared/utils/dates'
import { useClients } from '../../clients/api'
import { useAssignmentGroups } from '../../team/api'

const FIELDS = ['Subject or Body', 'Subject', 'Body', 'Sender Email', 'Sender Domain', 'Sender Name'] as const
const OPERATORS = ['Contains Any Of', 'Contains', 'Does Not Contain', 'Equals', 'Starts With', 'Ends With', 'Is Empty', 'Is Not Empty'] as const
const NO_VALUE = new Set(['Is Empty', 'Is Not Empty'])
const PRIORITIES = ['Low', 'Medium', 'High', 'Critical']
const WORK_TYPES = [
  { value: 'CorrectiveMaintenance', label: 'Breakdown / corrective' },
  { value: 'PreventiveMaintenance', label: 'Preventive maintenance' },
  { value: 'NewInstallation', label: 'New installation' },
  { value: 'InspectionAssessment', label: 'Inspection / assessment' },
]
const workTypeLabel = (value?: string | null) => WORK_TYPES.find((type) => type.value === value)?.label ?? value

interface Condition { field: string; operator: string; value: string }
interface RuleGroup { logic?: string; criteria?: Condition[] }
interface RuleActions {
  createWorkOrder?: { priority?: string | null; workOrderType?: string | null; clientId?: string | null; assignmentGroupId?: string | null; workOrderTitleTemplate?: string | null } | null
  ignore?: { enabled?: boolean } | null
}
interface IntakeRule {
  id: string
  name: string
  description: string | null
  isActive: boolean
  sourceType: string
  criteriaJson: string
  actionsJson: string
  lastTriggeredAt: string | null
  lastTriggerStatus: string | null
}
interface RuleTemplate { key: string; name: string; description: string }
interface Preview { ruleName: string | null; result: string; title: string | null; priority: string | null; workOrderType: string | null }

const parse = <T,>(json: string, fallback: T): T => {
  try { return (JSON.parse(json) as T) ?? fallback } catch { return fallback }
}

const inputClass = 'h-10 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]'

function describe(rule: IntakeRule) {
  const groups = parse<RuleGroup[]>(rule.criteriaJson, [])
  const group = groups.find((item) => (item.criteria ?? []).length > 0)
  const joiner = group?.logic === 'OR' ? ' or ' : ' and '
  const when = (group?.criteria ?? []).map((c) => `${c.field} ${c.operator.toLowerCase()}${NO_VALUE.has(c.operator) ? '' : ` “${c.value}”`}`).join(joiner)
  const actions = parse<RuleActions>(rule.actionsJson, {})
  if (actions.ignore?.enabled) return { when, then: 'ignore the email' }
  const create = actions.createWorkOrder ?? {}
  const parts = [
    create.priority ? `${create.priority} priority` : null,
    create.workOrderType ? workTypeLabel(create.workOrderType) : null,
    create.workOrderTitleTemplate ? `title “${create.workOrderTitleTemplate}”` : null,
    create.clientId ? 'a set client' : null,
    create.assignmentGroupId ? 'a set team' : null,
  ].filter(Boolean)
  return { when, then: parts.length ? `create a work order with ${parts.join(', ')}` : 'create a work order with the defaults' }
}

function RuleEditor({ rule, onDone }: { rule?: IntakeRule; onDone: () => void }) {
  const queryClient = useQueryClient()
  const { data: clients } = useClients()
  const { data: groups } = useAssignmentGroups()
  const initialGroup = parse<RuleGroup[]>(rule?.criteriaJson ?? '[]', [])[0]
  const initialActions = parse<RuleActions>(rule?.actionsJson ?? '{}', {})
  const [name, setName] = useState(rule?.name ?? '')
  const [logic, setLogic] = useState(initialGroup?.logic === 'OR' ? 'OR' : 'AND')
  const [conditions, setConditions] = useState<Condition[]>(initialGroup?.criteria?.length ? initialGroup.criteria : [{ field: 'Subject or Body', operator: 'Contains Any Of', value: '' }])
  const [ignore, setIgnore] = useState(Boolean(initialActions.ignore?.enabled))
  const create = initialActions.createWorkOrder ?? {}
  const [priority, setPriority] = useState(create.priority ?? '')
  const [workType, setWorkType] = useState(create.workOrderType ?? '')
  const [clientId, setClientId] = useState(create.clientId ?? '')
  const [groupId, setGroupId] = useState(create.assignmentGroupId ?? '')
  const [title, setTitle] = useState(create.workOrderTitleTemplate ?? '')

  const save = useMutation({
    mutationFn: (payload: object) => (rule ? api.put(endpoints.intakeRules.detail(rule.id), payload) : api.post(endpoints.intakeRules.list, payload)),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['intake-rules'] }),
  })

  const setCondition = (index: number, patch: Partial<Condition>) =>
    setConditions((prev) => prev.map((condition, i) => (i === index ? { ...condition, ...patch } : condition)))

  const submit = async (event: React.FormEvent) => {
    event.preventDefault()
    if (!name.trim()) return toast.error('Give the rule a name')
    const payload = {
      name: name.trim(),
      sourceType: 'Email',
      isActive: rule?.isActive ?? true,
      description: rule?.description ?? null,
      criteriaJson: JSON.stringify([{ logic, criteria: conditions.map((c) => ({ ...c, value: NO_VALUE.has(c.operator) ? '' : c.value.trim() })) }]),
      actionsJson: JSON.stringify(ignore
        ? { ignore: { enabled: true } }
        : { createWorkOrder: { priority: priority || null, workOrderType: workType || null, clientId: clientId || null, assignmentGroupId: groupId || null, workOrderTitleTemplate: title.trim() || null } }),
      sourceConfigJson: '{}',
    }
    try {
      await save.mutateAsync(payload)
      toast.success(rule ? 'Rule saved' : 'Rule added')
      onDone()
    } catch {
      // interceptor surfaces the error toast (e.g. a condition without a value)
    }
  }

  return (
    <form onSubmit={submit} className="space-y-4 rounded-xl border border-slate-200 bg-slate-50 p-4" aria-label="Intake rule">
      <label className="block space-y-1 text-sm text-slate-700">
        <span className="font-medium">Rule name</span>
        <input value={name} onChange={(event) => setName(event.target.value)} placeholder="e.g. Generator faults" className={inputClass} />
      </label>

      <div className="space-y-2">
        <div className="flex flex-wrap items-center gap-2 text-sm text-slate-700">
          <span className="font-medium">When an email matches</span>
          <select aria-label="Match" value={logic} onChange={(event) => setLogic(event.target.value)} className="h-9 rounded-lg border border-slate-300 bg-white px-2 text-sm">
            <option value="AND">all of these</option>
            <option value="OR">any of these</option>
          </select>
        </div>
        {conditions.map((condition, index) => (
          <div key={index} className="grid grid-cols-1 gap-2 sm:grid-cols-[180px_170px_minmax(0,1fr)_40px]">
            <select aria-label={`Condition ${index + 1} field`} value={condition.field} onChange={(event) => setCondition(index, { field: event.target.value })} className={inputClass}>
              {FIELDS.map((field) => <option key={field} value={field}>{field}</option>)}
            </select>
            <select aria-label={`Condition ${index + 1} test`} value={condition.operator} onChange={(event) => setCondition(index, { operator: event.target.value })} className={inputClass}>
              {OPERATORS.map((operator) => <option key={operator} value={operator}>{operator.toLowerCase()}</option>)}
            </select>
            {NO_VALUE.has(condition.operator) ? <span /> : (
              <input
                aria-label={`Condition ${index + 1} value`}
                value={condition.value}
                onChange={(event) => setCondition(index, { value: event.target.value })}
                placeholder={condition.operator === 'Contains Any Of' ? 'Words separated by commas' : 'Text'}
                className={inputClass}
              />
            )}
            <button type="button" aria-label={`Remove condition ${index + 1}`} disabled={conditions.length === 1} onClick={() => setConditions((prev) => prev.filter((_, i) => i !== index))} className="inline-flex h-10 items-center justify-center rounded-lg border border-slate-200 bg-white text-slate-500 disabled:opacity-40">
              <Trash2 className="h-4 w-4" />
            </button>
          </div>
        ))}
        <button type="button" onClick={() => setConditions((prev) => [...prev, { field: 'Subject', operator: 'Contains', value: '' }])} className="text-sm font-medium text-slate-700 underline underline-offset-2">+ Add condition</button>
      </div>

      <fieldset className="space-y-3">
        <legend className="text-sm font-medium text-slate-700">Then</legend>
        <label className="flex items-center gap-2 text-sm text-slate-700"><input type="radio" checked={!ignore} onChange={() => setIgnore(false)} /> Create a work order</label>
        {!ignore ? (
          <div className="grid grid-cols-1 gap-3 pl-6 sm:grid-cols-2">
            <label className="space-y-1 text-sm text-slate-700"><span>Priority</span>
              <select value={priority} onChange={(event) => setPriority(event.target.value)} className={inputClass}><option value="">Workspace default</option>{PRIORITIES.map((p) => <option key={p} value={p}>{p}</option>)}</select>
            </label>
            <label className="space-y-1 text-sm text-slate-700"><span>Work type</span>
              <select value={workType} onChange={(event) => setWorkType(event.target.value)} className={inputClass}><option value="">Breakdown (default)</option>{WORK_TYPES.map((t) => <option key={t.value} value={t.value}>{t.label}</option>)}</select>
            </label>
            <label className="space-y-1 text-sm text-slate-700"><span>Team</span>
              <select value={groupId} onChange={(event) => setGroupId(event.target.value)} className={inputClass}><option value="">Workspace default</option>{(groups ?? []).map((g) => <option key={g.id} value={g.id}>{g.name}</option>)}</select>
            </label>
            <label className="space-y-1 text-sm text-slate-700"><span>Client, for senders who aren't contacts</span>
              <select value={clientId} onChange={(event) => setClientId(event.target.value)} className={inputClass}><option value="">Don't accept unknown senders</option>{(clients ?? []).map((c) => <option key={c.id} value={c.id}>{c.clientName}</option>)}</select>
            </label>
            <label className="space-y-1 text-sm text-slate-700 sm:col-span-2"><span>Title</span>
              <input value={title} onChange={(event) => setTitle(event.target.value)} placeholder="e.g. [Generator] {{subject}} — leave blank to use the subject" className={inputClass} />
            </label>
          </div>
        ) : null}
        <label className="flex items-center gap-2 text-sm text-slate-700"><input type="radio" checked={ignore} onChange={() => setIgnore(true)} /> Ignore the email (no work order, no reply)</label>
      </fieldset>

      <div className="flex gap-2">
        <button type="submit" disabled={save.isPending} className="inline-flex h-10 items-center rounded-lg bg-slate-900 px-4 text-sm font-medium text-white disabled:opacity-60">{save.isPending ? 'Saving…' : 'Save rule'}</button>
        <button type="button" onClick={onDone} className="inline-flex h-10 items-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700">Cancel</button>
      </div>
    </form>
  )
}

function SampleEmail() {
  const [subject, setSubject] = useState('')
  const [from, setFrom] = useState('')
  const [result, setResult] = useState<Preview | null>(null)
  const preview = useMutation({ mutationFn: () => api.post<Preview>(endpoints.intakeRules.preview, { subject, body: '', fromAddress: from, fromName: null }) })
  const run = async (event: React.FormEvent) => {
    event.preventDefault()
    try { setResult(await preview.mutateAsync()) } catch { /* interceptor surfaces the error toast */ }
  }
  return (
    <form onSubmit={run} className="space-y-2 rounded-xl border border-dashed border-slate-300 p-3" aria-label="Try a sample email">
      <p className="text-sm font-medium text-slate-700">Try a sample email</p>
      <div className="grid grid-cols-1 gap-2 sm:grid-cols-[minmax(0,1fr)_220px_auto]">
        <input aria-label="Sample subject" value={subject} onChange={(event) => setSubject(event.target.value)} placeholder="Subject, e.g. Generator not starting" className={inputClass} />
        <input aria-label="Sample sender" value={from} onChange={(event) => setFrom(event.target.value)} placeholder="sender@client.com" className={inputClass} />
        <button disabled={preview.isPending} className="inline-flex h-10 items-center justify-center rounded-lg border border-slate-200 bg-white px-3 text-sm font-medium text-slate-700">Test this email</button>
      </div>
      {result ? (
        <p className="text-sm text-slate-700" data-testid="intake-rule-preview">
          {result.ruleName ? <><strong>{result.ruleName}</strong>: </> : null}{result.result}
          {result.title ? ` Title “${result.title}”` : ''}{result.priority ? `, ${result.priority} priority` : ''}{result.workOrderType ? `, ${workTypeLabel(result.workOrderType)}` : ''}.
        </p>
      ) : null}
    </form>
  )
}

/**
 * Intake rules: conditions on incoming emails and what to do with matches. Tried top to bottom; the
 * first match wins. Ready-made rules cover the common cases and can be edited after adding.
 */
export function IntakeRulesSection({ className }: { className: string }) {
  const queryClient = useQueryClient()
  const rules = useQuery({ queryKey: ['intake-rules'], queryFn: () => api.get<IntakeRule[]>(endpoints.intakeRules.list) })
  const templates = useQuery({ queryKey: ['intake-rule-templates'], queryFn: () => api.get<RuleTemplate[]>(endpoints.intakeRules.templates), staleTime: Infinity })
  const [editing, setEditing] = useState<'new' | string | null>(null)
  const invalidate = () => void queryClient.invalidateQueries({ queryKey: ['intake-rules'] })
  const addTemplate = useMutation({ mutationFn: (key: string) => api.post(endpoints.intakeRules.addTemplate(key), {}), onSuccess: invalidate })
  const update = useMutation({ mutationFn: ({ rule, isActive }: { rule: IntakeRule; isActive: boolean }) => api.put(endpoints.intakeRules.detail(rule.id), { ...rule, isActive }), onSuccess: invalidate })
  const remove = useMutation({ mutationFn: (id: string) => api.delete(endpoints.intakeRules.detail(id)), onSuccess: invalidate })

  const list = (Array.isArray(rules.data) ? rules.data : []).filter((rule) => rule.sourceType === 'Email')
  const added = new Set(list.map((rule) => rule.name))
  const available = (Array.isArray(templates.data) ? templates.data : []).filter((template) => !added.has(template.name))

  return (
    <section className={className} aria-label="Intake rules">
      <div className="flex flex-wrap items-start justify-between gap-2">
        <div>
          <h2 className="text-sm font-semibold text-slate-900">Intake rules</h2>
          <p className="mt-1 text-sm text-slate-600">Sort incoming emails automatically: set the priority, work type, team or title, or ignore mail that isn't a request. Rules are tried top to bottom; the first match wins.</p>
        </div>
        {editing === null ? (
          <button type="button" onClick={() => setEditing('new')} className="inline-flex h-9 items-center gap-1 rounded-lg border border-slate-200 px-3 text-sm font-medium text-slate-700"><Plus className="h-4 w-4" /> New rule</button>
        ) : null}
      </div>

      {editing === 'new' ? <RuleEditor onDone={() => setEditing(null)} /> : null}

      {list.length === 0 ? <p className="text-sm text-slate-500">No rules yet. Add a ready-made one below or create your own.</p> : (
        <ol className="space-y-2" data-testid="intake-rules">
          {list.map((rule, index) => {
            const { when, then } = describe(rule)
            return editing === rule.id ? (
              <li key={rule.id}><RuleEditor rule={rule} onDone={() => setEditing(null)} /></li>
            ) : (
              <li key={rule.id} className={`rounded-xl border border-slate-200 p-3 ${rule.isActive ? '' : 'opacity-60'}`}>
                <div className="flex flex-wrap items-start justify-between gap-2">
                  <div className="min-w-0">
                    <p className="text-sm font-medium text-slate-900">{index + 1}. {rule.name}</p>
                    <p className="text-xs text-slate-600">When {when || '—'}, {then}.</p>
                    {rule.lastTriggeredAt ? <p className="text-xs text-slate-400">Last used {formatDateTime(rule.lastTriggeredAt)}{rule.lastTriggerStatus ? ` · ${rule.lastTriggerStatus}` : ''}</p> : null}
                  </div>
                  <div className="flex items-center gap-2">
                    <label className="flex items-center gap-1 text-xs text-slate-600">
                      <input type="checkbox" checked={rule.isActive} onChange={(event) => update.mutate({ rule, isActive: event.target.checked })} /> On
                    </label>
                    <button type="button" onClick={() => setEditing(rule.id)} className="h-8 rounded-lg border border-slate-200 px-2 text-xs text-slate-700">Edit</button>
                    <button type="button" aria-label={`Delete ${rule.name}`} onClick={() => { if (window.confirm(`Delete the rule "${rule.name}"?`)) remove.mutate(rule.id) }} className="h-8 rounded-lg border border-slate-200 px-2 text-xs text-rose-700">Delete</button>
                  </div>
                </div>
              </li>
            )
          })}
        </ol>
      )}

      {available.length > 0 ? (
        <div className="space-y-2">
          <p className="text-xs font-semibold uppercase tracking-wide text-slate-500">Ready-made rules</p>
          <ul className="grid grid-cols-1 gap-2 md:grid-cols-2">
            {available.map((template) => (
              <li key={template.key} className="flex items-start justify-between gap-2 rounded-xl border border-slate-200 bg-slate-50/60 p-3">
                <div>
                  <p className="text-sm font-medium text-slate-900">{template.name}</p>
                  <p className="text-xs text-slate-600">{template.description}</p>
                </div>
                <button
                  type="button"
                  disabled={addTemplate.isPending}
                  onClick={() => addTemplate.mutate(template.key, { onSuccess: () => toast.success(`Added “${template.name}”`) })}
                  className="h-8 shrink-0 rounded-lg bg-slate-900 px-3 text-xs font-medium text-white disabled:opacity-60"
                >
                  Add
                </button>
              </li>
            ))}
          </ul>
        </div>
      ) : null}

      {list.length > 0 ? <SampleEmail /> : null}
    </section>
  )
}
