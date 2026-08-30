import { useEffect, useState } from 'react'
import toast from 'react-hot-toast'
import { AlertTriangle, Plus } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { Spinner } from '../../shared/components/Spinner'
import { EmptyState } from '../../shared/components/EmptyState'
import { FormDrawer } from '../../shared/components/FormDrawer'
import { ConfirmDialog } from '../../shared/components/ConfirmDialog'
import { usePermission } from '../../shared/hooks/usePermission'
import { useTeamUsers } from '../team/api'
import {
  escalationActions,
  escalationTriggerConditions,
  useCreateEscalationRule,
  useDeleteEscalationRule,
  useEscalationRules,
  useUpdateEscalationRule,
  type EscalationAction,
  type EscalationRule,
  type EscalationTriggerCondition,
  type UpsertEscalationRulePayload,
} from './escalationRulesApi'

const priorities = ['Critical', 'High', 'Medium', 'Low']

const conditionLabels: Record<EscalationTriggerCondition, string> = {
  UnassignedAfterMins: 'Unassigned after (minutes)',
  InProgressOverEstimatePercent: 'In progress over estimate (%)',
  StatusUnchangedMins: 'Status unchanged for (minutes)',
  OverdueSinceMins: 'Overdue since due date (minutes)',
}

const actionLabels: Record<EscalationAction, string> = {
  NotifyDispatcher: 'Notify dispatcher',
  NotifyManager: 'Notify manager',
  NotifyTechnician: 'Notify assigned technician',
  ReassignToUserId: 'Reassign to user',
  ChangePriority: 'Change priority',
}

function describeRule(rule: EscalationRule): string {
  const condition = `${conditionLabels[rule.triggerCondition]}: ${rule.triggerValue}`
  const action = actionLabels[rule.action]
  return `${condition} → ${action}`
}

function RuleEditor({ rule, onClose }: { rule: EscalationRule | null; onClose: () => void }) {
  const canManage = usePermission('settings.manage')
  const { data: users } = useTeamUsers()
  const create = useCreateEscalationRule()
  const update = useUpdateEscalationRule(rule?.id ?? '')

  const [name, setName] = useState(rule?.name ?? '')
  const [isActive, setIsActive] = useState(rule?.isActive ?? true)
  const [triggerCondition, setTriggerCondition] = useState<EscalationTriggerCondition>(rule?.triggerCondition ?? escalationTriggerConditions[0])
  const [triggerValue, setTriggerValue] = useState(rule?.triggerValue ?? 60)
  const [action, setAction] = useState<EscalationAction>(rule?.action ?? escalationActions[0])
  const [actionValue, setActionValue] = useState(rule?.actionValue ?? '')
  const [cooldownMins, setCooldownMins] = useState(rule?.cooldownMins ?? 60)

  useEffect(() => {
    if (!rule) return
    setName(rule.name)
    setIsActive(rule.isActive)
    setTriggerCondition(rule.triggerCondition)
    setTriggerValue(rule.triggerValue)
    setAction(rule.action)
    setActionValue(rule.actionValue ?? '')
    setCooldownMins(rule.cooldownMins)
  }, [rule])

  const isPending = create.isPending || update.isPending

  const save = async () => {
    const payload: UpsertEscalationRulePayload = {
      name: name.trim(),
      isActive,
      triggerCondition,
      triggerValue,
      action,
      actionValue: actionValue.trim() || null,
      cooldownMins,
    }

    try {
      if (rule) {
        await update.mutateAsync(payload)
      } else {
        await create.mutateAsync(payload)
      }
      toast.success('Escalation rule saved')
      onClose()
    } catch {
      // interceptor surfaces the error toast
    }
  }

  return (
    <FormDrawer
      open
      title={rule ? `Edit ${rule.name}` : 'New Escalation Rule'}
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
          <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="rule-name">Name</label>
          <input id="rule-name" value={name} disabled={!canManage} onChange={(event) => setName(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)] disabled:bg-slate-50" />
        </div>

        <label className="flex items-center gap-2 text-sm text-slate-700">
          <input type="checkbox" checked={isActive} disabled={!canManage} onChange={(event) => setIsActive(event.target.checked)} className="h-4 w-4 rounded border-slate-300" />
          Active
        </label>

        <div className="border-t border-slate-200 pt-4">
          <h4 className="mb-3 text-sm font-semibold text-slate-900">Trigger</h4>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="rule-condition">Condition</label>
              <select
                id="rule-condition"
                value={triggerCondition}
                disabled={!canManage}
                onChange={(event) => setTriggerCondition(event.target.value as EscalationTriggerCondition)}
                className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)] disabled:bg-slate-50"
              >
                {escalationTriggerConditions.map((condition) => (
                  <option key={condition} value={condition}>{conditionLabels[condition]}</option>
                ))}
              </select>
            </div>
            <div>
              <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="rule-value">Value</label>
              <input
                id="rule-value"
                type="number"
                min={1}
                value={triggerValue}
                disabled={!canManage}
                onChange={(event) => setTriggerValue(Number(event.target.value))}
                className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)] disabled:bg-slate-50"
              />
            </div>
          </div>
        </div>

        <div className="border-t border-slate-200 pt-4">
          <h4 className="mb-3 text-sm font-semibold text-slate-900">Action</h4>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="rule-action">Action</label>
            <select
              id="rule-action"
              value={action}
              disabled={!canManage}
              onChange={(event) => {
                setAction(event.target.value as EscalationAction)
                setActionValue('')
              }}
              className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)] disabled:bg-slate-50"
            >
              {escalationActions.map((a) => (
                <option key={a} value={a}>{actionLabels[a]}</option>
              ))}
            </select>
          </div>

          {action === 'ReassignToUserId' ? (
            <div className="mt-3">
              <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="rule-user">Reassign to</label>
              <select
                id="rule-user"
                value={actionValue}
                disabled={!canManage}
                onChange={(event) => setActionValue(event.target.value)}
                className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)] disabled:bg-slate-50"
              >
                <option value="">Select a user</option>
                {(users ?? []).map((u) => (
                  <option key={u.id} value={u.id}>{u.fullName}</option>
                ))}
              </select>
            </div>
          ) : null}

          {action === 'ChangePriority' ? (
            <div className="mt-3">
              <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="rule-priority">New priority</label>
              <select
                id="rule-priority"
                value={actionValue}
                disabled={!canManage}
                onChange={(event) => setActionValue(event.target.value)}
                className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)] disabled:bg-slate-50"
              >
                <option value="">Select a priority</option>
                {priorities.map((p) => (
                  <option key={p} value={p}>{p}</option>
                ))}
              </select>
            </div>
          ) : null}
        </div>

        <div className="border-t border-slate-200 pt-4">
          <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="rule-cooldown">Cooldown (minutes)</label>
          <input
            id="rule-cooldown"
            type="number"
            min={0}
            value={cooldownMins}
            disabled={!canManage}
            onChange={(event) => setCooldownMins(Number(event.target.value))}
            className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)] disabled:bg-slate-50"
          />
          <p className="mt-1 text-xs text-slate-500">Minimum time before this rule can fire again for the same work order.</p>
        </div>
      </div>
    </FormDrawer>
  )
}

export function EscalationRulesSettingsPage() {
  const { data, isLoading, isError } = useEscalationRules()
  const canManage = usePermission('settings.manage')
  const deleteRule = useDeleteEscalationRule()
  const [editing, setEditing] = useState<{ rule: EscalationRule | null } | null>(null)
  const [confirmDelete, setConfirmDelete] = useState<EscalationRule | null>(null)

  if (isLoading) {
    return <Spinner label="Loading escalation rules" />
  }

  if (isError) {
    return <EmptyState icon={AlertTriangle} title="Escalation rules unavailable" description="Escalation rules could not be loaded." />
  }

  const rules = data ?? []

  return (
    <div className="space-y-6">
      <PageHeader
        title="Escalation Rules"
        subtitle="Automatically notify or escalate work orders that are stuck, unassigned, or at risk of breaching SLA."
        primaryAction={
          canManage ? (
            <button
              type="button"
              onClick={() => setEditing({ rule: null })}
              className="inline-flex h-11 items-center gap-1.5 rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)]"
            >
              <Plus className="h-4 w-4" />
              New Rule
            </button>
          ) : undefined
        }
      />

      {rules.length === 0 ? (
        <EmptyState icon={AlertTriangle} title="No escalation rules yet" description="Create a rule to automatically notify or reassign stuck work orders." />
      ) : (
        <div className="space-y-3">
          {rules.map((rule) => (
            <div key={rule.id} className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 shadow-sm">
              <div className="flex items-start justify-between gap-3">
                <div>
                  <div className="flex items-center gap-2">
                    <p className="text-sm font-semibold text-slate-900">{rule.name}</p>
                    <span
                      className={`inline-flex items-center rounded-full px-2 py-0.5 text-[11px] font-medium ring-1 ring-inset ${
                        rule.isActive ? 'bg-emerald-50 text-emerald-700 ring-emerald-200' : 'bg-slate-100 text-slate-600 ring-slate-200'
                      }`}
                    >
                      {rule.isActive ? 'Active' : 'Inactive'}
                    </span>
                  </div>
                  <p className="mt-1 text-xs text-slate-600">{describeRule(rule)}</p>
                  <p className="mt-1 text-xs text-slate-400">Cooldown: {rule.cooldownMins}m</p>
                </div>
                {canManage ? (
                  <div className="flex items-center gap-2">
                    <button type="button" onClick={() => setEditing({ rule })} className="text-xs font-medium text-slate-600 hover:text-slate-900">Edit</button>
                    <button type="button" onClick={() => setConfirmDelete(rule)} className="text-xs font-medium text-rose-500 hover:text-rose-700">Delete</button>
                  </div>
                ) : null}
              </div>
            </div>
          ))}
        </div>
      )}

      {editing ? <RuleEditor rule={editing.rule} onClose={() => setEditing(null)} /> : null}

      <ConfirmDialog
        open={confirmDelete !== null}
        title="Delete Escalation Rule"
        message={`This removes the "${confirmDelete?.name ?? ''}" escalation rule.`}
        confirmLabel="Delete Rule"
        danger
        onCancel={() => setConfirmDelete(null)}
        onConfirm={async () => {
          if (!confirmDelete) return
          try {
            await deleteRule.mutateAsync(confirmDelete.id)
            toast.success('Escalation rule deleted')
          } catch {
            // interceptor surfaces the error toast
          }
          setConfirmDelete(null)
        }}
      />
    </div>
  )
}
