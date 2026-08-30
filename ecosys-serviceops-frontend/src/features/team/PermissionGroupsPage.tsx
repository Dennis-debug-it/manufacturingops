import { useMemo, useState } from 'react'
import toast from 'react-hot-toast'
import { Lock, Plus, Search, ShieldCheck } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { AsyncPanel } from '../../shared/components/AsyncPanel'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { FormDrawer } from '../../shared/components/FormDrawer'
import { ConfirmDialog } from '../../shared/components/ConfirmDialog'
import { EmptyState } from '../../shared/components/EmptyState'
import { Badge } from '../../shared/components/Badge'
import { Spinner } from '../../shared/components/Spinner'
import { usePermission } from '../../shared/hooks/usePermission'
import { TeamTabNav } from './components/TeamTabNav'
import {
  capabilityLabel,
  useAccessCapabilities,
  useCreatePermissionGroup,
  useDeletePermissionGroup,
  usePermissionGroups,
  useUpdatePermissionGroup,
  type PermissionGroupSummary,
} from './accessApi'

/**
 * Create/edit drawer. Mounted only when the signed-in user has `settings.manage`, which is the
 * same gate `EnsureCanAssign()` applies on POST/PUT `/api/access/permission-groups`.
 */
function GroupEditor({ group, onClose }: { group: PermissionGroupSummary | null; onClose: () => void }) {
  const capabilitiesQuery = useAccessCapabilities()
  const create = useCreatePermissionGroup()
  const update = useUpdatePermissionGroup(group?.id ?? '')

  const [name, setName] = useState(group?.name ?? '')
  const [description, setDescription] = useState(group?.description ?? '')
  const [isActive, setIsActive] = useState(group?.isActive ?? true)
  const [selected, setSelected] = useState<string[]>(group?.capabilities ?? [])
  const [search, setSearch] = useState('')

  const capabilities = capabilitiesQuery.data
  const visible = useMemo(() => {
    const query = search.trim().toLowerCase()
    if (!query) return capabilities ?? []
    return (capabilities ?? []).filter((item) => `${item.name} ${item.key}`.toLowerCase().includes(query))
  }, [capabilities, search])

  const toggle = (key: string) =>
    setSelected((prev) => (prev.includes(key) ? prev.filter((value) => value !== key) : [...prev, key]))

  const isPending = create.isPending || update.isPending
  const trimmedName = name.trim()

  // A single reason string drives both the disabled state and the visible explanation, so the
  // Save button is never disabled without the user being told why.
  const blockedReason = !trimmedName
    ? 'Enter a group name to save.'
    : capabilitiesQuery.isError
      ? 'Capabilities could not be loaded, so this group cannot be saved yet.'
      : capabilitiesQuery.isLoading
        ? 'Loading capabilities…'
        : null

  const save = async () => {
    if (blockedReason) return
    const payload = {
      name: trimmedName,
      description: description.trim() || null,
      isActive,
      capabilities: selected,
    }
    try {
      if (group) {
        await update.mutateAsync(payload)
      } else {
        await create.mutateAsync(payload)
      }
      toast.success(group ? 'Permission group updated' : 'Permission group created')
      onClose()
    } catch {
      // interceptor surfaces the error toast
    }
  }

  return (
    <FormDrawer
      open
      title={group ? `Edit ${group.name}` : 'New Permission Group'}
      onClose={onClose}
      footer={
        <div className="flex items-center justify-between gap-3">
          <span className="text-xs text-slate-500">{blockedReason ?? `${selected.length} capabilities selected`}</span>
          <div className="flex items-center gap-2">
            <button type="button" onClick={onClose} className="inline-flex h-11 items-center justify-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">
              Cancel
            </button>
            <button
              type="button"
              onClick={save}
              disabled={isPending || blockedReason !== null}
              className="inline-flex h-11 items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:opacity-60"
            >
              {isPending ? 'Saving…' : 'Save group'}
            </button>
          </div>
        </div>
      }
    >
      <div className="space-y-5">
        <div>
          <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="group-name">Name</label>
          <input
            id="group-name"
            value={name}
            onChange={(event) => setName(event.target.value)}
            placeholder="e.g. Nairobi Operations Team"
            className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]"
          />
        </div>

        <div>
          <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="group-description">Description</label>
          <textarea
            id="group-description"
            value={description}
            onChange={(event) => setDescription(event.target.value)}
            rows={3}
            placeholder="What this group is for and who should be in it."
            className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[var(--app-primary)]"
          />
        </div>

        <label className="flex items-center gap-2 text-sm text-slate-700">
          <input
            type="checkbox"
            checked={isActive}
            onChange={(event) => setIsActive(event.target.checked)}
            className="h-4 w-4 rounded border-slate-300"
          />
          Active — inactive groups cannot be assigned to users
        </label>

        <div className="border-t border-slate-200 pt-4">
          <div className="flex items-center justify-between gap-2">
            <h4 className="text-sm font-semibold text-slate-900">Capabilities</h4>
            <span className="text-xs text-slate-500">{selected.length} selected</span>
          </div>

          {capabilitiesQuery.isLoading ? (
            <Spinner label="Loading capabilities" />
          ) : capabilitiesQuery.isError ? (
            <EmptyState
              className="mt-3"
              title="Capabilities unavailable"
              description="The capability list could not be loaded, so capabilities cannot be chosen right now."
              actionLabel="Retry"
              onAction={() => { void capabilitiesQuery.refetch() }}
            />
          ) : (capabilities ?? []).length === 0 ? (
            <p className="mt-3 text-xs text-slate-500">The server returned no capabilities, so this group cannot grant anything yet.</p>
          ) : (
            <>
              <label className="mt-3 flex items-center gap-2 rounded-lg border border-slate-200 px-3 text-sm text-slate-600">
                <Search className="h-4 w-4 shrink-0 text-slate-400" />
                <input
                  value={search}
                  onChange={(event) => setSearch(event.target.value)}
                  placeholder="Filter capabilities"
                  className="h-10 w-full border-0 bg-transparent p-0 outline-none placeholder:text-slate-400"
                />
              </label>

              <div className="mt-2 flex items-center gap-3 text-xs">
                <button
                  type="button"
                  onClick={() => setSelected((prev) => Array.from(new Set([...prev, ...visible.map((item) => item.key)])))}
                  className="font-medium text-slate-600 hover:text-slate-900"
                >
                  Select all shown
                </button>
                <button
                  type="button"
                  onClick={() => setSelected((prev) => prev.filter((key) => !visible.some((item) => item.key === key)))}
                  className="font-medium text-slate-600 hover:text-slate-900"
                >
                  Clear shown
                </button>
              </div>

              {visible.length === 0 ? (
                <p className="mt-3 text-xs text-slate-500">No capability matches “{search.trim()}”.</p>
              ) : (
                <div className="mt-3 space-y-1">
                  {visible.map((item) => (
                    <label key={item.key} className="flex items-start gap-2 rounded-lg px-2 py-1.5 text-sm text-slate-700 hover:bg-slate-50">
                      <input
                        type="checkbox"
                        checked={selected.includes(item.key)}
                        onChange={() => toggle(item.key)}
                        className="mt-0.5 h-4 w-4 rounded border-slate-300"
                      />
                      <span>
                        {item.name}
                        <span className="ml-2 font-mono text-[11px] text-slate-400">{item.key}</span>
                      </span>
                    </label>
                  ))}
                </div>
              )}
            </>
          )}
        </div>
      </div>
    </FormDrawer>
  )
}

export function PermissionGroupsPage() {
  const canManage = usePermission('settings.manage')
  const groupsQuery = usePermissionGroups()
  const capabilitiesQuery = useAccessCapabilities()
  const deleteGroup = useDeletePermissionGroup()

  const [editing, setEditing] = useState<{ group: PermissionGroupSummary | null } | null>(null)
  const [confirmDelete, setConfirmDelete] = useState<PermissionGroupSummary | null>(null)

  const columns: DataTableColumn<PermissionGroupSummary>[] = [
    {
      key: 'name',
      header: 'Group',
      cell: (row) => (
        <div>
          <p className="font-medium text-slate-900">{row.name}</p>
          {row.description ? <p className="mt-0.5 text-xs text-slate-500">{row.description}</p> : null}
        </div>
      ),
    },
    {
      key: 'status',
      header: 'Status',
      cell: (row) => <Badge tone={row.isActive ? 'success' : 'neutral'}>{row.isActive ? 'Active' : 'Inactive'}</Badge>,
    },
    {
      key: 'capabilities',
      header: 'Capabilities',
      cell: (row) => (
        <div>
          <p className="font-medium text-slate-900">{row.capabilities.length}</p>
          {row.capabilities.length > 0 ? (
            <p className="mt-0.5 max-w-xs text-xs text-slate-500">
              {row.capabilities
                .slice(0, 3)
                .map((key) => capabilityLabel(key, capabilitiesQuery.data))
                .join(', ')}
              {row.capabilities.length > 3 ? ` +${row.capabilities.length - 3} more` : ''}
            </p>
          ) : (
            <p className="mt-0.5 text-xs text-slate-500">Grants nothing yet</p>
          )}
        </div>
      ),
    },
    {
      key: 'userCount',
      header: 'Users',
      cell: (row) => <span className="text-slate-700">{row.userCount}</span>,
    },
    ...(canManage
      ? [
          {
            key: 'actions',
            header: '',
            className: 'text-right',
            cell: (row: PermissionGroupSummary) => (
              <div className="flex items-center justify-end gap-3">
                <button type="button" onClick={() => setEditing({ group: row })} className="text-xs font-medium text-slate-600 hover:text-slate-900">
                  Edit
                </button>
                <button type="button" onClick={() => setConfirmDelete(row)} className="text-xs font-medium text-rose-500 hover:text-rose-700">
                  Delete
                </button>
              </div>
            ),
          } satisfies DataTableColumn<PermissionGroupSummary>,
        ]
      : []),
  ]

  return (
    <div className="space-y-6">
      <PageHeader
        title="Permission Groups"
        subtitle="Reusable capability bundles. A user can belong to several groups and receives the union of their capabilities."
        primaryAction={
          canManage ? (
            <button
              type="button"
              onClick={() => setEditing({ group: null })}
              className="inline-flex h-11 items-center gap-2 rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)]"
            >
              <Plus className="h-4 w-4" />
              New group
            </button>
          ) : undefined
        }
      />
      <TeamTabNav />

      {canManage ? null : (
        <div className="flex items-start gap-3 rounded-2xl border border-slate-200 bg-slate-50 p-4 text-sm text-slate-700">
          <Lock className="mt-0.5 h-5 w-5 shrink-0 text-slate-400" />
          <p>You can view permission groups but not change them. Creating, editing, and deleting groups requires the <strong>Manage settings</strong> capability.</p>
        </div>
      )}

      <AsyncPanel
        query={groupsQuery}
        loadingLabel="Loading permission groups"
        errorTitle="Permission groups unavailable"
        errorDescription="The permission groups could not be loaded."
      >
        {(groups) => (
          <DataTable
            columns={columns}
            data={groups}
            emptyTitle="No permission groups yet"
            emptyDescription={
              canManage
                ? 'Create a group to bundle capabilities — for example a Finance Approvers group that can approve invoices and view rate cards.'
                : 'No permission groups have been created for this workspace yet.'
            }
            {...(canManage ? { emptyActionLabel: 'New group', onEmptyAction: () => setEditing({ group: null }) } : {})}
          />
        )}
      </AsyncPanel>

      {editing && canManage ? <GroupEditor group={editing.group} onClose={() => setEditing(null)} /> : null}

      <ConfirmDialog
        open={confirmDelete !== null}
        title="Delete permission group"
        message={
          confirmDelete
            ? `Deleting “${confirmDelete.name}” removes its ${confirmDelete.capabilities.length} capabilities from ${confirmDelete.userCount} user${confirmDelete.userCount === 1 ? '' : 's'}. Those users are signed out immediately so the change takes effect.`
            : ''
        }
        confirmLabel="Delete group"
        danger
        icon={<ShieldCheck className="mt-0.5 h-5 w-5 shrink-0 text-rose-500" />}
        confirmDisabled={deleteGroup.isPending}
        onCancel={() => setConfirmDelete(null)}
        onConfirm={async () => {
          if (!confirmDelete) return
          try {
            await deleteGroup.mutateAsync(confirmDelete.id)
            toast.success('Permission group deleted')
          } catch {
            // interceptor surfaces the error toast
          }
          setConfirmDelete(null)
        }}
      />
    </div>
  )
}
