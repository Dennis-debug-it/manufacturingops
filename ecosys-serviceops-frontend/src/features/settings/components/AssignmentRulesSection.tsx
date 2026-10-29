import { useState } from 'react'
import toast from 'react-hot-toast'
import { ArrowDown, ArrowUp, Plus } from 'lucide-react'
import { workTypeOptions } from '../../work-orders/schemas'
import { useAssignmentRules, useDeleteAssignmentRule, useSalesOwners, useSaveAssignmentRule } from '../../crm/api'
import { sourceOptions, type SalesAssignmentRule, type UpsertAssignmentRulePayload } from '../../crm/types'

const fieldClass = 'h-10 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]'
const emptyRule: UpsertAssignmentRulePayload = { name: '', isActive: true, source: null, serviceType: null, locationContains: null, method: 'RoundRobin', ownerUserIds: [] }

function describe(rule: SalesAssignmentRule) {
  const when = [
    rule.source ? `source is ${rule.source}` : null,
    rule.serviceType ? `service is ${workTypeOptions.find((x) => x.value === rule.serviceType)?.label ?? rule.serviceType}` : null,
    rule.locationContains ? `location mentions "${rule.locationContains}"` : null,
  ].filter(Boolean)
  const who = rule.method === 'RoundRobin' && rule.owners.length > 1 ? `each of ${rule.owners.map((x) => x.fullName).join(', ')} in turn` : rule.owners[0]?.fullName ?? 'nobody'
  return `${when.length ? `When ${when.join(' and ')}` : 'Any lead'}: ${who}.`
}

/**
 * Who gets new leads the system takes in (technician leads, imports without an owner). The first
 * matching rule wins; rules only apply when the client has no sales owner already.
 */
export function AssignmentRulesSection() {
  const { data: rules } = useAssignmentRules()
  const { data: owners } = useSalesOwners()
  const save = useSaveAssignmentRule()
  const remove = useDeleteAssignmentRule()
  const [editing, setEditing] = useState<(UpsertAssignmentRulePayload & { id?: string }) | null>(null)

  const submit = async () => {
    if (!editing) return
    if (!editing.name.trim()) return void toast.error('Give the rule a name')
    if (editing.ownerUserIds.length === 0) return void toast.error('Pick at least one person')
    try {
      await save.mutateAsync(editing)
      toast.success('Rule saved')
      setEditing(null)
    } catch {
      // interceptor surfaces the error toast
    }
  }

  const move = (rule: SalesAssignmentRule, direction: -1 | 1) => {
    const list = rules ?? []
    const index = list.findIndex((x) => x.id === rule.id)
    const other = list[index + direction]
    if (!other) return
    const payload = (r: SalesAssignmentRule, sortOrder: number) => ({
      id: r.id, name: r.name, isActive: r.isActive, source: r.source, serviceType: r.serviceType, locationContains: r.locationContains,
      method: r.method, ownerUserIds: r.owners.map((x) => x.id), sortOrder,
    })
    void save.mutateAsync(payload(rule, other.sortOrder)).then(() => save.mutateAsync(payload(other, rule.sortOrder)))
  }

  return (
    <section className="space-y-3 rounded-[var(--radius-card)] border border-slate-200 bg-white p-4">
      <div className="flex items-start justify-between gap-3">
        <div>
          <h2 className="text-sm font-semibold text-slate-900">Lead assignment rules</h2>
          <p className="mt-1 text-sm text-slate-500">For new leads whose client has no sales owner yet. The first rule that matches decides; with none, the default owner above gets them.</p>
        </div>
        {!editing ? (
          <button type="button" onClick={() => setEditing({ ...emptyRule })} className="inline-flex h-9 shrink-0 items-center gap-1.5 rounded-lg border border-slate-200 px-3 text-sm font-medium text-slate-700 hover:bg-slate-50">
            <Plus className="h-4 w-4" />Add rule
          </button>
        ) : null}
      </div>

      {editing ? (
        <div className="grid gap-3 rounded-xl border border-slate-200 p-3 md:grid-cols-2">
          <input aria-label="Rule name" placeholder="Rule name, e.g. Coast region" value={editing.name} onChange={(event) => setEditing({ ...editing, name: event.target.value })} className={fieldClass} />
          <select aria-label="Method" value={editing.method} onChange={(event) => setEditing({ ...editing, method: event.target.value as UpsertAssignmentRulePayload['method'] })} className={fieldClass}>
            <option value="RoundRobin">Share between people in turn</option>
            <option value="Fixed">Always the same person</option>
          </select>
          <select aria-label="Lead source" value={editing.source ?? ''} onChange={(event) => setEditing({ ...editing, source: event.target.value || null })} className={fieldClass}>
            <option value="">Any source</option>
            {sourceOptions.map((source) => <option key={source} value={source}>{source}</option>)}
          </select>
          <select aria-label="Service type" value={editing.serviceType ?? ''} onChange={(event) => setEditing({ ...editing, serviceType: event.target.value || null })} className={fieldClass}>
            <option value="">Any service</option>
            {workTypeOptions.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
          </select>
          <input aria-label="Location contains" placeholder="Location contains, e.g. Mombasa (optional)" value={editing.locationContains ?? ''} onChange={(event) => setEditing({ ...editing, locationContains: event.target.value || null })} className={fieldClass} />
          <label className="flex items-center gap-2 text-sm text-slate-700">
            <input type="checkbox" checked={editing.isActive} onChange={(event) => setEditing({ ...editing, isActive: event.target.checked })} />
            Rule is on
          </label>
          <fieldset className="md:col-span-2">
            <legend className="text-sm font-medium text-slate-700">{editing.method === 'Fixed' ? 'Person (the first ticked)' : 'People, in turn'}</legend>
            <div className="mt-1 flex flex-wrap gap-x-4 gap-y-1">
              {(owners ?? []).map((owner) => (
                <label key={owner.id} className="flex items-center gap-1.5 text-sm text-slate-700">
                  <input
                    type="checkbox"
                    checked={editing.ownerUserIds.includes(owner.id)}
                    onChange={(event) => setEditing({
                      ...editing,
                      ownerUserIds: event.target.checked ? [...editing.ownerUserIds, owner.id] : editing.ownerUserIds.filter((id) => id !== owner.id),
                    })}
                  />
                  {owner.fullName}
                </label>
              ))}
            </div>
          </fieldset>
          <div className="flex gap-2 md:col-span-2">
            <button type="button" disabled={save.isPending} onClick={() => void submit()} className="inline-flex h-10 items-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white disabled:opacity-60">Save rule</button>
            <button type="button" onClick={() => setEditing(null)} className="inline-flex h-10 items-center rounded-lg border border-slate-200 px-4 text-sm font-medium text-slate-700">Cancel</button>
          </div>
        </div>
      ) : null}

      {rules && rules.length === 0 && !editing ? <p className="text-sm text-slate-500">No rules yet.</p> : null}
      <ol className="divide-y divide-slate-100">
        {(rules ?? []).map((rule, index) => (
          <li key={rule.id} className="flex flex-col gap-2 py-3 sm:flex-row sm:items-center sm:justify-between">
            <div className="min-w-0 text-sm">
              <div className="font-medium text-slate-900">{index + 1}. {rule.name} {rule.isActive ? null : <span className="text-xs font-normal text-slate-400">(off)</span>}</div>
              <div className="text-slate-500">{describe(rule)}</div>
            </div>
            <div className="flex shrink-0 items-center gap-2">
              <button type="button" aria-label="Move up" disabled={index === 0 || save.isPending} onClick={() => move(rule, -1)} className="rounded p-1 text-slate-500 hover:bg-slate-100 disabled:opacity-30"><ArrowUp className="h-4 w-4" /></button>
              <button type="button" aria-label="Move down" disabled={index === (rules?.length ?? 0) - 1 || save.isPending} onClick={() => move(rule, 1)} className="rounded p-1 text-slate-500 hover:bg-slate-100 disabled:opacity-30"><ArrowDown className="h-4 w-4" /></button>
              <button type="button" onClick={() => setEditing({ id: rule.id, name: rule.name, isActive: rule.isActive, source: rule.source, serviceType: rule.serviceType, locationContains: rule.locationContains, method: rule.method, ownerUserIds: rule.owners.map((x) => x.id) })} className="text-xs font-medium text-slate-600 hover:underline">Edit</button>
              <button type="button" disabled={remove.isPending} onClick={() => remove.mutate(rule.id)} className="text-xs font-medium text-rose-600 hover:underline">Delete</button>
            </div>
          </li>
        ))}
      </ol>
    </section>
  )
}
