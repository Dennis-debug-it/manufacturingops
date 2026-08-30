import { useEffect, useMemo, useState } from 'react'
import toast from 'react-hot-toast'
import { Plus, Trash2 } from 'lucide-react'
import { SummarySection } from '../../../shared/pages/ModulePage'
import { EmptyState } from '../../../shared/components/EmptyState'
import { Spinner } from '../../../shared/components/Spinner'
import { usePermission } from '../../../shared/hooks/usePermission'
import { getActiveCurrency } from '../../../shared/utils/currency'
import {
  SCOPE_TYPES,
  capabilityLabel,
  useAccessCapabilities,
  useApprovalLimits,
  useAccessScopes,
  usePermissionGroups,
  useSetUserApprovalLimits,
  useSetUserGroups,
  useSetUserOverrides,
  useSetUserScopes,
  useUserEffectiveAccess,
  useUserGroups,
  type UserApprovalLimit,
  type UserOverride,
  type UserScope,
} from '../accessApi'

/**
 * Per-user dynamic access: group membership, direct overrides, scopes and approval limits, plus the
 * resolved view that explains the outcome.
 *
 * These four PUTs are the only way to attach a permission group, scope or limit to a person —
 * without them the whole dynamic-access feature is inert, which is what EC-009 amounted to once the
 * definition pages existed.
 *
 * Precedence, per `DynamicAccessService.GetEffectiveAccessAsync`: a direct override is the last
 * word (allow or deny), then tenant owner, then role matrix, then group membership. The resolved
 * table below reports which of those actually decided each capability, so an admin can answer
 * "why can this person do that?" without reading the source.
 */
export function UserAccessPanel({ userId, isAdminUser }: { userId: string; isAdminUser: boolean }) {
  const canManage = usePermission('settings.manage')

  const { data: capabilities } = useAccessCapabilities()
  const { data: groups, isLoading: groupsLoading } = usePermissionGroups()
  const { data: effective, isLoading: effectiveLoading, isError: effectiveError } = useUserEffectiveAccess(userId)
  const { data: memberships, isLoading: membershipsLoading } = useUserGroups(userId)
  const { data: allScopes } = useAccessScopes(canManage)
  const { data: allLimits } = useApprovalLimits(canManage)

  const setGroups = useSetUserGroups(userId)
  const setOverrides = useSetUserOverrides(userId)
  const setScopes = useSetUserScopes(userId)
  const setLimits = useSetUserApprovalLimits(userId)

  // Server state, projected to just this user.
  const serverScopes = useMemo<UserScope[]>(
    () => (allScopes ?? [])
      .filter((s) => s.userId === userId)
      .map((s) => ({ capability: s.capability, scopeType: s.scopeType, scopeEntityId: s.scopeEntityId })),
    [allScopes, userId],
  )
  const serverLimits = useMemo<UserApprovalLimit[]>(
    () => (allLimits ?? [])
      .filter((l) => l.userId === userId)
      .map((l) => ({ capability: l.capability, maximumAmount: l.maximumAmount, currency: l.currency })),
    [allLimits, userId],
  )
  const serverMemberGroupIds = useMemo(() => memberships ?? [], [memberships])

  const [memberGroupIds, setMemberGroupIds] = useState<string[]>([])
  const [overrides, setOverrideRows] = useState<UserOverride[]>([])
  const [scopes, setScopeRows] = useState<UserScope[]>([])
  const [limits, setLimitRows] = useState<UserApprovalLimit[]>([])

  useEffect(() => { setMemberGroupIds(serverMemberGroupIds) }, [serverMemberGroupIds])
  useEffect(() => { setScopeRows(serverScopes) }, [serverScopes])
  useEffect(() => { setLimitRows(serverLimits) }, [serverLimits])
  useEffect(() => {
    setOverrideRows(
      (effective ?? [])
        .filter((e) => e.source === 'Direct allow' || e.source === 'Explicit deny')
        .map((e) => ({ capability: e.capability, effect: e.source === 'Direct allow' ? 'allow' : 'deny' })),
    )
  }, [effective])

  const disabled = !canManage
  const capabilityKeys = (capabilities ?? []).map((c) => c.key)

  const save = async (label: string, run: () => Promise<unknown>) => {
    try {
      await run()
      toast.success(`${label} saved. This user has been signed out so the change takes effect.`)
    } catch {
      // interceptor surfaces the error toast
    }
  }

  if (effectiveLoading || groupsLoading || membershipsLoading) {
    return <Spinner label="Loading access" />
  }

  if (effectiveError) {
    return (
      <SummarySection title="Access Control" description="Groups, overrides, scopes and limits for this user.">
        <div className="rounded-2xl border border-dashed border-slate-300 bg-white p-8 text-center text-sm text-slate-600">
          This user&rsquo;s access could not be loaded.
        </div>
      </SummarySection>
    )
  }

  const activeGroups = (groups ?? []).filter((g) => g.isActive)

  return (
    <>
      <SummarySection
        title="Permission Groups"
        description="Groups grant capabilities on top of the role matrix. A user receives the union of every group they belong to."
      >
        {isAdminUser ? (
          <div className="mb-4 rounded-xl border border-slate-200 bg-slate-50 px-4 py-3 text-sm text-slate-600">
            This user is an <strong>Admin</strong> and already holds every capability, so group membership will not change what they can do.
          </div>
        ) : null}
        {activeGroups.length === 0 ? (
          <EmptyState
            title="No permission groups yet"
            description="Create a group under Users & Access → Permission Groups, then assign people to it here."
          />
        ) : (
          <>
            <div className="grid grid-cols-1 gap-2 sm:grid-cols-2">
              {activeGroups.map((group) => {
                const checked = memberGroupIds.includes(group.id)
                return (
                  <label
                    key={group.id}
                    className="flex items-start gap-3 rounded-xl border border-slate-200 px-4 py-3 text-sm text-slate-700"
                  >
                    <input
                      type="checkbox"
                      className="mt-0.5 h-4 w-4 rounded border-slate-300"
                      checked={checked}
                      disabled={disabled}
                      onChange={() =>
                        setMemberGroupIds((current) =>
                          checked ? current.filter((id) => id !== group.id) : [...current, group.id])
                      }
                    />
                    <span>
                      <span className="font-medium text-slate-900">{group.name}</span>
                      <span className="mt-0.5 block text-xs text-slate-500">
                        {group.capabilities.length} {group.capabilities.length === 1 ? 'capability' : 'capabilities'}
                        {group.description ? ` · ${group.description}` : ''}
                      </span>
                    </span>
                  </label>
                )
              })}
            </div>
            {canManage ? (
              <SaveRow
                pending={setGroups.isPending}
                onSave={() => save('Group membership', () => setGroups.mutateAsync(memberGroupIds))}
              />
            ) : null}
          </>
        )}
      </SummarySection>

      <SummarySection
        title="Direct Overrides"
        description="An override is the last word — it beats the role matrix and every group. Use it sparingly."
      >
        <RowEditor
          rows={overrides}
          disabled={disabled}
          addLabel="Add override"
          onAdd={() => setOverrideRows((r) => [...r, { capability: capabilityKeys[0] ?? '', effect: 'allow' }])}
          onRemove={(index) => setOverrideRows((r) => r.filter((_, i) => i !== index))}
          renderRow={(row, index) => (
            <>
              <CapabilitySelect
                value={row.capability}
                disabled={disabled}
                capabilities={capabilityKeys}
                capabilityNames={capabilities}
                onChange={(value) => setOverrideRows((r) => r.map((x, i) => (i === index ? { ...x, capability: value } : x)))}
              />
              <select
                value={row.effect}
                disabled={disabled}
                onChange={(event) =>
                  setOverrideRows((r) => r.map((x, i) => (i === index ? { ...x, effect: event.target.value as 'allow' | 'deny' } : x)))
                }
                className="h-11 rounded-lg border border-slate-300 bg-white px-3 text-sm outline-none focus:border-[var(--app-primary)] disabled:bg-slate-50"
              >
                <option value="allow">Allow</option>
                <option value="deny">Deny</option>
              </select>
            </>
          )}
        />
        {canManage ? (
          <SaveRow
            pending={setOverrides.isPending}
            onSave={() => save('Overrides', () => setOverrides.mutateAsync(overrides))}
          />
        ) : null}
      </SummarySection>

      <SummarySection
        title="Access Scopes"
        description="Where a capability applies. With no scope, a granted capability applies across the whole workspace."
      >
        <RowEditor
          rows={scopes}
          disabled={disabled}
          addLabel="Add scope"
          onAdd={() => setScopeRows((r) => [...r, { capability: capabilityKeys[0] ?? '', scopeType: 'Organization', scopeEntityId: null }])}
          onRemove={(index) => setScopeRows((r) => r.filter((_, i) => i !== index))}
          renderRow={(row, index) => (
            <>
              <CapabilitySelect
                value={row.capability}
                disabled={disabled}
                capabilities={capabilityKeys}
                capabilityNames={capabilities}
                onChange={(value) => setScopeRows((r) => r.map((x, i) => (i === index ? { ...x, capability: value } : x)))}
              />
              <select
                value={row.scopeType}
                disabled={disabled}
                onChange={(event) => setScopeRows((r) => r.map((x, i) => (i === index ? { ...x, scopeType: event.target.value } : x)))}
                className="h-11 rounded-lg border border-slate-300 bg-white px-3 text-sm outline-none focus:border-[var(--app-primary)] disabled:bg-slate-50"
              >
                {SCOPE_TYPES.map((type) => <option key={type} value={type}>{type}</option>)}
              </select>
            </>
          )}
        />
        {canManage ? (
          <SaveRow pending={setScopes.isPending} onSave={() => save('Scopes', () => setScopes.mutateAsync(scopes))} />
        ) : null}
      </SummarySection>

      <SummarySection
        title="Approval Limits"
        description="Monetary ceiling per capability. Above the limit the action is refused even when the capability is granted."
      >
        <RowEditor
          rows={limits}
          disabled={disabled}
          addLabel="Add limit"
          onAdd={() => setLimitRows((r) => [...r, { capability: capabilityKeys[0] ?? '', maximumAmount: 0, currency: getActiveCurrency() }])}
          onRemove={(index) => setLimitRows((r) => r.filter((_, i) => i !== index))}
          renderRow={(row, index) => (
            <>
              <CapabilitySelect
                value={row.capability}
                disabled={disabled}
                capabilities={capabilityKeys}
                capabilityNames={capabilities}
                onChange={(value) => setLimitRows((r) => r.map((x, i) => (i === index ? { ...x, capability: value } : x)))}
              />
              <div className="flex gap-2">
                <input
                  type="number"
                  min={0}
                  step="0.01"
                  value={row.maximumAmount}
                  disabled={disabled}
                  onChange={(event) =>
                    setLimitRows((r) => r.map((x, i) => (i === index ? { ...x, maximumAmount: Number(event.target.value) } : x)))
                  }
                  className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm tabular-nums outline-none focus:border-[var(--app-primary)] disabled:bg-slate-50"
                />
                <input
                  type="text"
                  maxLength={3}
                  value={row.currency}
                  disabled={disabled}
                  onChange={(event) =>
                    setLimitRows((r) => r.map((x, i) => (i === index ? { ...x, currency: event.target.value.toUpperCase() } : x)))
                  }
                  className="h-11 w-20 rounded-lg border border-slate-300 px-3 text-sm uppercase outline-none focus:border-[var(--app-primary)] disabled:bg-slate-50"
                />
              </div>
            </>
          )}
        />
        {canManage ? (
          <SaveRow pending={setLimits.isPending} onSave={() => save('Approval limits', () => setLimits.mutateAsync(limits))} />
        ) : null}
      </SummarySection>

      <SummarySection
        title="Resolved Access"
        description="What this user can actually do right now, and which rule decided it."
      >
        {(effective ?? []).length === 0 ? (
          <EmptyState title="Nothing resolved yet" description="This user has no capabilities from any source." />
        ) : (
          <div className="overflow-x-auto rounded-xl border border-slate-200">
            <table className="w-full min-w-[560px] text-sm">
              <thead>
                <tr className="bg-slate-50 text-left text-xs font-semibold uppercase tracking-[0.12em] text-slate-500">
                  <th className="px-4 py-3">Capability</th>
                  <th className="px-4 py-3">Allowed</th>
                  <th className="px-4 py-3">Decided by</th>
                  <th className="px-4 py-3">Limit</th>
                </tr>
              </thead>
              <tbody>
                {(effective ?? []).map((row) => (
                  <tr key={row.capability} className="border-t border-slate-100">
                    <td className="px-4 py-2.5 text-slate-900">{capabilityLabel(row.capability, capabilities)}</td>
                    <td className="px-4 py-2.5">
                      <span className={row.allowed ? 'font-medium text-emerald-700' : 'text-slate-400'}>
                        {row.allowed ? 'Yes' : 'No'}
                      </span>
                    </td>
                    <td className="px-4 py-2.5 text-slate-600">{row.source}</td>
                    <td className="px-4 py-2.5 tabular-nums text-slate-600">
                      {row.maximumAmount === null ? '—' : `${row.currency ?? ''} ${row.maximumAmount.toLocaleString()}`.trim()}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </SummarySection>
    </>
  )
}

function SaveRow({ pending, onSave }: { pending: boolean; onSave: () => void }) {
  return (
    <div className="mt-4 flex items-center justify-end gap-3">
      <span className="text-xs text-slate-500">Saving signs this user out of all sessions.</span>
      <button
        type="button"
        onClick={onSave}
        disabled={pending}
        className="inline-flex h-11 items-center rounded-lg bg-[var(--app-primary)] px-5 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:opacity-50"
      >
        {pending ? 'Saving…' : 'Save'}
      </button>
    </div>
  )
}

function CapabilitySelect({
  value, disabled, capabilities, capabilityNames, onChange,
}: {
  value: string
  disabled: boolean
  capabilities: string[]
  capabilityNames: { key: string; name: string }[] | undefined
  onChange: (value: string) => void
}) {
  return (
    <select
      value={value}
      disabled={disabled}
      onChange={(event) => onChange(event.target.value)}
      className="h-11 w-full rounded-lg border border-slate-300 bg-white px-3 text-sm outline-none focus:border-[var(--app-primary)] disabled:bg-slate-50"
    >
      {capabilities.map((key) => (
        <option key={key} value={key}>{capabilityLabel(key, capabilityNames)}</option>
      ))}
    </select>
  )
}

/** Shared add/remove list shell so the three row editors stay visually identical. */
function RowEditor<T>({
  rows, disabled, addLabel, onAdd, onRemove, renderRow,
}: {
  rows: T[]
  disabled: boolean
  addLabel: string
  onAdd: () => void
  onRemove: (index: number) => void
  renderRow: (row: T, index: number) => React.ReactNode
}) {
  return (
    <div className="space-y-2">
      {rows.length === 0 ? (
        <p className="rounded-xl border border-dashed border-slate-300 px-4 py-6 text-center text-sm text-slate-500">
          None set.
        </p>
      ) : (
        rows.map((row, index) => (
          <div key={index} className="grid grid-cols-1 items-center gap-2 sm:grid-cols-[1fr_1fr_auto]">
            {renderRow(row, index)}
            <button
              type="button"
              onClick={() => onRemove(index)}
              disabled={disabled}
              aria-label="Remove row"
              className="inline-flex h-11 w-11 items-center justify-center rounded-lg border border-slate-200 text-slate-500 transition hover:bg-slate-50 disabled:opacity-50"
            >
              <Trash2 className="h-4 w-4" />
            </button>
          </div>
        ))
      )}
      {!disabled ? (
        <button
          type="button"
          onClick={onAdd}
          className="inline-flex h-10 items-center gap-2 rounded-lg border border-slate-200 px-3 text-sm font-medium text-slate-700 transition hover:bg-slate-50"
        >
          <Plus className="h-4 w-4" />
          {addLabel}
        </button>
      ) : null}
    </div>
  )
}
