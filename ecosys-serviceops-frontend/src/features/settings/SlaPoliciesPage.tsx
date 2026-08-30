import { useEffect, useState } from 'react'
import toast from 'react-hot-toast'
import { ShieldAlert, Plus, Trash2 } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { Spinner } from '../../shared/components/Spinner'
import { EmptyState } from '../../shared/components/EmptyState'
import { FormDrawer } from '../../shared/components/FormDrawer'
import { ConfirmDialog } from '../../shared/components/ConfirmDialog'
import { usePermission } from '../../shared/hooks/usePermission'
import { useTeamUsers } from '../team/api'
import {
  useCreateSlaPolicy,
  useDeleteSlaPolicy,
  useSlaPolicies,
  useUpdateSlaPolicy,
  type SlaPolicy,
  type UpsertSlaEscalationPayload,
} from './slaPoliciesApi'

const priorities = ['Critical', 'High', 'Medium', 'Low'] as const
const notifyRoleOptions = ['Manager', 'Dispatcher', 'Technician', 'finance', 'Viewer']

const priorityStyles: Record<string, string> = {
  Critical: 'bg-rose-50 text-rose-700 ring-rose-200',
  High: 'bg-amber-50 text-amber-700 ring-amber-200',
  Medium: 'bg-sky-50 text-sky-700 ring-sky-200',
  Low: 'bg-slate-100 text-slate-700 ring-slate-200',
}

function minutesToLabel(minutes: number) {
  if (minutes % (60 * 24) === 0) return `${minutes / (60 * 24)}d`
  if (minutes % 60 === 0) return `${minutes / 60}h`
  return `${minutes}m`
}

interface EscalationDraft extends UpsertSlaEscalationPayload {
  key: string
}

function PolicyEditor({
  policy,
  defaultPriority,
  onClose,
}: {
  policy: SlaPolicy | null
  defaultPriority: string
  onClose: () => void
}) {
  const canManage = usePermission('settings.manage')
  const { data: users } = useTeamUsers()
  const create = useCreateSlaPolicy()
  const update = useUpdateSlaPolicy(policy?.id ?? '')

  const [name, setName] = useState(policy?.name ?? `${defaultPriority} priority`)
  const [priority, setPriority] = useState(policy?.priority ?? defaultPriority)
  const [responseHours, setResponseHours] = useState(policy ? policy.responseTargetMinutes / 60 : 4)
  const [resolutionHours, setResolutionHours] = useState(policy ? policy.resolutionTargetMinutes / 60 : 8)
  const [businessHoursOnly, setBusinessHoursOnly] = useState(policy?.businessHoursOnly ?? false)
  const [businessHoursStart, setBusinessHoursStart] = useState(policy?.businessHoursStart ?? '08:00')
  const [businessHoursEnd, setBusinessHoursEnd] = useState(policy?.businessHoursEnd ?? '17:00')
  const [escalations, setEscalations] = useState<EscalationDraft[]>(
    (policy?.escalations ?? []).map((e) => ({ key: e.id, triggerAtPercent: e.triggerAtPercent, notifyRoles: e.notifyRoles, escalateToUserId: e.escalateToUserId })),
  )

  useEffect(() => {
    if (!policy) return
    setName(policy.name ?? `${policy.priority} priority`)
    setPriority(policy.priority)
    setResponseHours(policy.responseTargetMinutes / 60)
    setResolutionHours(policy.resolutionTargetMinutes / 60)
    setBusinessHoursOnly(policy.businessHoursOnly)
    setBusinessHoursStart(policy.businessHoursStart ?? '08:00')
    setBusinessHoursEnd(policy.businessHoursEnd ?? '17:00')
    setEscalations(policy.escalations.map((e) => ({ key: e.id, triggerAtPercent: e.triggerAtPercent, notifyRoles: e.notifyRoles, escalateToUserId: e.escalateToUserId })))
  }, [policy])

  const addEscalation = () => {
    setEscalations((prev) => [...prev, { key: `new-${prev.length}-${Date.now()}`, triggerAtPercent: 80, notifyRoles: 'Manager', escalateToUserId: null }])
  }

  const removeEscalation = (key: string) => {
    setEscalations((prev) => prev.filter((e) => e.key !== key))
  }

  const toggleRole = (escalationKey: string, role: string) => {
    setEscalations((prev) =>
      prev.map((e) => {
        if (e.key !== escalationKey) return e
        const roles = (e.notifyRoles ?? '').split(',').map((r) => r.trim()).filter(Boolean)
        const next = roles.includes(role) ? roles.filter((r) => r !== role) : [...roles, role]
        return { ...e, notifyRoles: next.join(',') }
      }),
    )
  }

  const save = async () => {
    const payload = {
      name: name.trim() || null,
      priority,
      responseTargetMinutes: Math.round(responseHours * 60),
      resolutionTargetMinutes: Math.round(resolutionHours * 60),
      businessHoursOnly,
      businessHoursStart: businessHoursOnly ? businessHoursStart : null,
      businessHoursEnd: businessHoursOnly ? businessHoursEnd : null,
      escalations: escalations.map(({ triggerAtPercent, notifyRoles, escalateToUserId }) => ({ triggerAtPercent, notifyRoles, escalateToUserId })),
    }

    try {
      if (policy) {
        await update.mutateAsync(payload)
      } else {
        await create.mutateAsync(payload)
      }
      toast.success('SLA policy saved')
      onClose()
    } catch {
      // interceptor surfaces the error toast
    }
  }

  const isPending = create.isPending || update.isPending

  return (
    <FormDrawer
      open
      title={policy ? `Edit ${policy.priority} Policy` : 'New SLA Policy'}
      onClose={onClose}
      footer={
        canManage ? (
          <div className="flex items-center justify-end gap-2">
            <button type="button" onClick={onClose} className="inline-flex h-11 items-center justify-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">Cancel</button>
            <button type="button" onClick={save} disabled={isPending} className="inline-flex h-11 items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:opacity-60">
              {isPending ? 'Saving…' : 'Save'}
            </button>
          </div>
        ) : null
      }
    >
      <div className="space-y-5">
        <div>
          <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="sla-name">Name</label>
          <input id="sla-name" value={name} disabled={!canManage} onChange={(event) => setName(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)] disabled:bg-slate-50" />
        </div>

        <div>
          <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="sla-priority">Priority</label>
          <select id="sla-priority" value={priority} disabled={!canManage || Boolean(policy)} onChange={(event) => setPriority(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)] disabled:bg-slate-50">
            {priorities.map((p) => (
              <option key={p} value={p}>{p}</option>
            ))}
          </select>
        </div>

        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="sla-response">Response target (hours)</label>
            <input id="sla-response" type="number" min={0} step={0.5} value={responseHours} disabled={!canManage} onChange={(event) => setResponseHours(Number(event.target.value))} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)] disabled:bg-slate-50" />
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="sla-resolution">Resolution target (hours)</label>
            <input id="sla-resolution" type="number" min={0} step={0.5} value={resolutionHours} disabled={!canManage} onChange={(event) => setResolutionHours(Number(event.target.value))} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)] disabled:bg-slate-50" />
          </div>
        </div>

        <label className="flex items-center gap-2 text-sm text-slate-700">
          <input type="checkbox" checked={businessHoursOnly} disabled={!canManage} onChange={(event) => setBusinessHoursOnly(event.target.checked)} className="h-4 w-4 rounded border-slate-300" />
          Only count business hours
        </label>

        {businessHoursOnly ? (
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="sla-hours-start">Business hours start</label>
              <input id="sla-hours-start" type="time" value={businessHoursStart} disabled={!canManage} onChange={(event) => setBusinessHoursStart(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)] disabled:bg-slate-50" />
            </div>
            <div>
              <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="sla-hours-end">Business hours end</label>
              <input id="sla-hours-end" type="time" value={businessHoursEnd} disabled={!canManage} onChange={(event) => setBusinessHoursEnd(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)] disabled:bg-slate-50" />
            </div>
          </div>
        ) : null}

        <div className="border-t border-slate-200 pt-4">
          <div className="flex items-center justify-between">
            <h4 className="text-sm font-semibold text-slate-900">Escalation Rules</h4>
            {canManage ? (
              <button type="button" onClick={addEscalation} className="inline-flex items-center gap-1 rounded-lg border border-slate-200 bg-white px-2.5 py-1.5 text-xs font-medium text-slate-700 hover:bg-slate-50">
                <Plus className="h-3.5 w-3.5" />Add
              </button>
            ) : null}
          </div>

          {escalations.length === 0 ? (
            <p className="mt-2 text-xs text-slate-500">No escalation rules configured. Breaches are reported to admins by default.</p>
          ) : (
            <div className="mt-3 space-y-3">
              {escalations.map((escalation) => (
                <div key={escalation.key} className="rounded-lg border border-slate-200 p-3">
                  <div className="flex items-center justify-between gap-2">
                    <label className="flex items-center gap-2 text-xs text-slate-600">
                      Trigger at
                      <input
                        type="number"
                        min={1}
                        max={100}
                        value={escalation.triggerAtPercent}
                        disabled={!canManage}
                        onChange={(event) =>
                          setEscalations((prev) => prev.map((e) => (e.key === escalation.key ? { ...e, triggerAtPercent: Number(event.target.value) } : e)))
                        }
                        className="h-8 w-16 rounded border border-slate-300 px-2 text-xs outline-none focus:border-[var(--app-primary)]"
                      />
                      % of SLA elapsed
                    </label>
                    {canManage ? (
                      <button type="button" onClick={() => removeEscalation(escalation.key)} className="rounded p-1 text-slate-400 hover:bg-rose-50 hover:text-rose-600">
                        <Trash2 className="h-3.5 w-3.5" />
                      </button>
                    ) : null}
                  </div>

                  <div className="mt-2 flex flex-wrap gap-1.5">
                    {notifyRoleOptions.map((role) => {
                      const active = (escalation.notifyRoles ?? '').split(',').map((r) => r.trim()).includes(role)
                      return (
                        <button
                          key={role}
                          type="button"
                          disabled={!canManage}
                          onClick={() => toggleRole(escalation.key, role)}
                          className={`rounded-full px-2.5 py-1 text-[11px] font-medium ring-1 ring-inset transition ${active ? 'bg-[var(--app-primary)] text-white ring-[var(--app-primary)]' : 'bg-white text-slate-600 ring-slate-200 hover:bg-slate-50'}`}
                        >
                          {role}
                        </button>
                      )
                    })}
                  </div>

                  <div className="mt-2">
                    <label className="mb-1 block text-[11px] text-slate-500" htmlFor={`escalate-user-${escalation.key}`}>Also escalate to</label>
                    <select
                      id={`escalate-user-${escalation.key}`}
                      value={escalation.escalateToUserId ?? ''}
                      disabled={!canManage}
                      onChange={(event) =>
                        setEscalations((prev) => prev.map((e) => (e.key === escalation.key ? { ...e, escalateToUserId: event.target.value || null } : e)))
                      }
                      className="h-8 w-full rounded border border-slate-300 px-2 text-xs outline-none focus:border-[var(--app-primary)]"
                    >
                      <option value="">No specific user</option>
                      {(users ?? []).map((u) => (
                        <option key={u.id} value={u.id}>{u.fullName}</option>
                      ))}
                    </select>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>
    </FormDrawer>
  )
}

export function SlaPoliciesPage() {
  const { data, isLoading, isError } = useSlaPolicies()
  const canManage = usePermission('settings.manage')
  const deletePolicy = useDeleteSlaPolicy()
  const [editing, setEditing] = useState<{ policy: SlaPolicy | null; priority: string } | null>(null)
  const [confirmDelete, setConfirmDelete] = useState<SlaPolicy | null>(null)

  if (isLoading) {
    return <Spinner label="Loading SLA policies" />
  }

  if (isError) {
    return <EmptyState icon={ShieldAlert} title="SLA policies unavailable" description="SLA policies could not be loaded." />
  }

  const policies = data ?? []
  const byPriority = new Map(policies.map((p) => [p.priority, p]))

  return (
    <div className="space-y-6">
      <PageHeader title="SLA Policies" subtitle="Response and resolution targets per priority, with automatic breach detection and escalation." />

      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
        {priorities.map((priority) => {
          const policy = byPriority.get(priority)
          return (
            <div key={priority} className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 shadow-sm">
              <div className="flex items-center justify-between gap-2">
                <span className={`inline-flex items-center rounded-full px-2.5 py-1 text-xs font-medium ring-1 ring-inset ${priorityStyles[priority]}`}>{priority}</span>
                {policy ? (
                  <div className="flex items-center gap-2">
                    {canManage ? (
                      <button type="button" onClick={() => setEditing({ policy, priority })} className="text-xs font-medium text-slate-600 hover:text-slate-900">Edit</button>
                    ) : null}
                    {canManage ? (
                      <button type="button" onClick={() => setConfirmDelete(policy)} className="text-xs font-medium text-rose-500 hover:text-rose-700">Delete</button>
                    ) : null}
                  </div>
                ) : canManage ? (
                  <button type="button" onClick={() => setEditing({ policy: null, priority })} className="text-xs font-medium text-slate-600 hover:text-slate-900">Add policy</button>
                ) : null}
              </div>

              {policy ? (
                <>
                  <p className="mt-2 text-sm font-medium text-slate-900">{policy.name ?? `${priority} priority`}</p>
                  <div className="mt-2 flex flex-wrap gap-x-4 gap-y-1 text-xs text-slate-600">
                    <span>Response: {minutesToLabel(policy.responseTargetMinutes)}</span>
                    <span>Resolution: {minutesToLabel(policy.resolutionTargetMinutes)}</span>
                    <span>{policy.businessHoursOnly ? `Business hours ${policy.businessHoursStart ?? '08:00'}–${policy.businessHoursEnd ?? '17:00'}` : '24/7'}</span>
                  </div>
                  {policy.escalations.length > 0 ? (
                    <p className="mt-2 text-xs text-slate-500">{policy.escalations.length} escalation rule{policy.escalations.length > 1 ? 's' : ''}</p>
                  ) : null}
                </>
              ) : (
                <p className="mt-2 text-xs text-slate-500">No policy configured for this priority.</p>
              )}
            </div>
          )
        })}
      </div>

      {editing ? <PolicyEditor policy={editing.policy} defaultPriority={editing.priority} onClose={() => setEditing(null)} /> : null}

      <ConfirmDialog
        open={confirmDelete !== null}
        title="Delete SLA Policy"
        message={`This removes the ${confirmDelete?.priority ?? ''} priority SLA policy. Work orders will no longer get automatic targets for this priority.`}
        confirmLabel="Delete Policy"
        danger
        onCancel={() => setConfirmDelete(null)}
        onConfirm={async () => {
          if (!confirmDelete) return
          try {
            await deletePolicy.mutateAsync(confirmDelete.id)
            toast.success('SLA policy deleted')
          } catch {
            // interceptor surfaces the error toast
          }
          setConfirmDelete(null)
        }}
      />
    </div>
  )
}
