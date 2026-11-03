import { useEffect, useState } from 'react'
import toast from 'react-hot-toast'
import { Plus, Trash2 } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { FormDrawer } from '../../shared/components/FormDrawer'
import { ConfirmDialog } from '../../shared/components/ConfirmDialog'
import { usePermission } from '../../shared/hooks/usePermission'
import {
  useAddAssignmentGroupMember,
  useAssignmentGroups,
  useCreateAssignmentGroup,
  useDeleteAssignmentGroup,
  useRemoveAssignmentGroupMember,
  useTechnicians,
  useUpdateAssignmentGroup,
} from './api'
import type { AssignmentGroup } from './types'
import { TeamTabNav } from './components/TeamTabNav'

const fieldClass = 'h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none transition focus:border-[var(--app-primary)]'
const labelClass = 'mb-1.5 block text-sm font-medium text-slate-700'

function ManageGroupDrawer({ group, onClose }: { group: AssignmentGroup; onClose: () => void }) {
  const updateGroup = useUpdateAssignmentGroup(group.id)
  const addMember = useAddAssignmentGroupMember(group.id)
  const removeMember = useRemoveAssignmentGroupMember(group.id)
  const { data: technicians } = useTechnicians()

  const [name, setName] = useState(group.name)
  const [description, setDescription] = useState(group.description ?? '')
  const [skillArea, setSkillArea] = useState(group.skillArea ?? '')
  const [isActive, setIsActive] = useState(group.isActive)
  const [newTechnicianId, setNewTechnicianId] = useState('')

  useEffect(() => {
    setName(group.name)
    setDescription(group.description ?? '')
    setSkillArea(group.skillArea ?? '')
    setIsActive(group.isActive)
  }, [group])

  const memberTechnicianIds = new Set(group.members.map((member) => member.technicianId))
  const availableTechnicians = (technicians ?? []).filter((technician) => !memberTechnicianIds.has(technician.id))

  const saveDetails = async () => {
    if (!name.trim()) {
      toast.error('Group name is required')
      return
    }
    try {
      await updateGroup.mutateAsync({
        name: name.trim(),
        description: description.trim() || null,
        skillArea: skillArea.trim() || null,
        branchId: group.branchId ?? null,
        isActive,
      })
      toast.success('Group updated')
    } catch {
      // interceptor surfaces the error toast
    }
  }

  const submitAddMember = async () => {
    if (!newTechnicianId) {
      toast.error('Select a technician to add')
      return
    }
    try {
      await addMember.mutateAsync({ technicianId: newTechnicianId, isLead: false })
      toast.success('Member added')
      setNewTechnicianId('')
    } catch {
      // interceptor surfaces the error toast
    }
  }

  return (
    <FormDrawer
      open
      title={`Manage ${group.name}`}
      onClose={onClose}
      footer={
        <div className="flex items-center justify-end gap-2">
          <button type="button" onClick={onClose} className="inline-flex h-11 items-center justify-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">Close</button>
          <button type="button" onClick={() => void saveDetails()} disabled={updateGroup.isPending} className="inline-flex h-11 items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:opacity-60">
            {updateGroup.isPending ? 'Saving…' : 'Save Changes'}
          </button>
        </div>
      }
    >
      <div className="space-y-4">
        <div className="space-y-4">
          <div>
            <label className={labelClass} htmlFor="group-edit-name">Name</label>
            <input id="group-edit-name" value={name} onChange={(event) => setName(event.target.value)} className={fieldClass} />
          </div>
          <div>
            <label className={labelClass} htmlFor="group-edit-skill">Skill Area</label>
            <input id="group-edit-skill" value={skillArea} onChange={(event) => setSkillArea(event.target.value)} className={fieldClass} />
          </div>
          <div>
            <label className={labelClass} htmlFor="group-edit-description">Description</label>
            <textarea id="group-edit-description" value={description} onChange={(event) => setDescription(event.target.value)} rows={3} className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[var(--app-primary)]" />
          </div>
          <label className="flex items-center gap-2 text-sm text-slate-700">
            <input type="checkbox" checked={isActive} onChange={(event) => setIsActive(event.target.checked)} className="h-4 w-4 rounded border-slate-300" />
            Active — available for work order assignment
          </label>
        </div>

        <div>
          <div className="mb-2 text-sm font-semibold text-slate-900">Members ({group.members.length})</div>
          <ul className="space-y-2">
            {group.members.length === 0 ? (
              <li className="rounded-lg border border-dashed border-slate-200 px-3 py-4 text-center text-sm text-slate-500">No members yet.</li>
            ) : (
              group.members.map((member) => (
                <li key={member.id} className="flex items-center justify-between rounded-lg border border-slate-200 px-3 py-2">
                  <div>
                    <span className="text-sm font-medium text-slate-900">{member.memberName ?? 'Member'}</span>
                    {member.isLead ? <span className="ml-2 text-xs font-semibold uppercase text-slate-500">Lead</span> : null}
                    {member.email ? <div className="text-xs text-slate-500">{member.email}</div> : null}
                  </div>
                  <button
                    type="button"
                    onClick={async () => {
                      try {
                        await removeMember.mutateAsync(member.technicianId)
                        toast.success('Member removed')
                      } catch {
                        // interceptor surfaces the error toast
                      }
                    }}
                    className="inline-flex h-8 w-8 items-center justify-center rounded-lg text-rose-500 transition hover:bg-rose-50"
                    aria-label={`Remove ${member.memberName ?? 'member'}`}
                  >
                    <Trash2 className="h-4 w-4" />
                  </button>
                </li>
              ))
            )}
          </ul>
          <div className="mt-3 flex gap-2">
            <select value={newTechnicianId} onChange={(event) => setNewTechnicianId(event.target.value)} className={`${fieldClass} flex-1`}>
              <option value="">Add a technician…</option>
              {availableTechnicians.map((technician) => (
                <option key={technician.id} value={technician.id}>{technician.fullName}</option>
              ))}
            </select>
            <button type="button" onClick={() => void submitAddMember()} disabled={addMember.isPending} className="inline-flex h-11 items-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50 disabled:opacity-60">
              Add
            </button>
          </div>
        </div>
      </div>
    </FormDrawer>
  )
}

export function GroupsPage() {
  const canManage = usePermission('team.manage')
  const { data, isLoading, isError } = useAssignmentGroups()
  const createGroup = useCreateAssignmentGroup()
  const deleteGroup = useDeleteAssignmentGroup()

  const [showForm, setShowForm] = useState(false)
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [skillArea, setSkillArea] = useState('')
  const [managingId, setManagingId] = useState<string | null>(null)
  const [deletingGroup, setDeletingGroup] = useState<AssignmentGroup | null>(null)

  const managing = (data ?? []).find((group) => group.id === managingId) ?? null

  const close = () => {
    setShowForm(false)
    setName('')
    setDescription('')
    setSkillArea('')
  }

  const submit = async () => {
    if (!name.trim()) {
      toast.error('Group name is required')
      return
    }
    try {
      await createGroup.mutateAsync({ name: name.trim(), description: description.trim() || null, skillArea: skillArea.trim() || null, isActive: true })
      toast.success('Group created')
      close()
    } catch {
      // interceptor surfaces the error toast
    }
  }

  const columns: DataTableColumn<AssignmentGroup>[] = [
    {
      key: 'name',
      header: 'Group',
      cell: (row) =>
        canManage ? (
          <button type="button" onClick={() => setManagingId(row.id)} className="font-medium text-slate-900 underline decoration-slate-300 underline-offset-4">
            {row.name}
          </button>
        ) : (
          row.name
        ),
    },
    { key: 'skillArea', header: 'Skill Area', cell: (row) => row.skillArea ?? '—' },
    { key: 'branchName', header: 'Branch', cell: (row) => row.branchName ?? 'All Branches' },
    { key: 'members', header: 'Members', cell: (row) => row.members.length },
    { key: 'isActive', header: 'Status', cell: (row) => <StatusBadge status={row.isActive ? 'Active' : 'Inactive'} /> },
    ...(canManage
      ? [
          {
            key: 'actions',
            header: '',
            cell: (row: AssignmentGroup) => (
              <button type="button" onClick={() => setDeletingGroup(row)} className="inline-flex h-8 w-8 items-center justify-center rounded-lg text-rose-500 transition hover:bg-rose-50" aria-label={`Delete ${row.name}`}>
                <Trash2 className="h-4 w-4" />
              </button>
            ),
          } satisfies DataTableColumn<AssignmentGroup>,
        ]
      : []),
  ]

  return (
    <div className="space-y-4">
      <PageHeader
        title="Groups"
        subtitle="Default assignment groups and members. Click a group to edit details and manage members."
        primaryAction={
          canManage ? (
            <button type="button" onClick={() => setShowForm(true)} className="inline-flex h-11 items-center gap-2 rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)]">
              <Plus className="h-4 w-4" />New Group
            </button>
          ) : undefined
        }
      />
      <TeamTabNav />

      <DataTable columns={columns} data={data ?? []} loading={isLoading} emptyTitle={isError ? 'Groups unavailable' : 'No groups yet'} emptyDescription={isError ? 'Assignment groups could not be loaded.' : 'Create your first assignment group.'} />

      {managing ? <ManageGroupDrawer group={managing} onClose={() => setManagingId(null)} /> : null}

      <ConfirmDialog
        open={Boolean(deletingGroup)}
        title="Delete Group"
        message={`Delete "${deletingGroup?.name ?? ''}"? Members are unlinked but technicians are not affected.`}
        confirmLabel="Delete"
        danger
        onCancel={() => setDeletingGroup(null)}
        onConfirm={async () => {
          if (!deletingGroup) return
          try {
            await deleteGroup.mutateAsync(deletingGroup.id)
            toast.success('Group deleted')
          } catch {
            // interceptor surfaces the error toast
          } finally {
            setDeletingGroup(null)
          }
        }}
      />

      <FormDrawer
        open={showForm}
        title="New Group"
        onClose={close}
        footer={
          <div className="flex items-center justify-end gap-2">
            <button type="button" onClick={close} className="inline-flex h-11 items-center justify-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">Cancel</button>
            <button type="button" onClick={submit} disabled={createGroup.isPending} className="inline-flex h-11 items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:opacity-60">
              {createGroup.isPending ? 'Creating…' : 'Create Group'}
            </button>
          </div>
        }
      >
        <div className="space-y-4">
          <div>
            <label className={labelClass} htmlFor="group-name">Name</label>
            <input id="group-name" value={name} onChange={(event) => setName(event.target.value)} className={fieldClass} />
          </div>
          <div>
            <label className={labelClass} htmlFor="group-skill">Skill Area</label>
            <input id="group-skill" value={skillArea} onChange={(event) => setSkillArea(event.target.value)} className={fieldClass} />
          </div>
          <div>
            <label className={labelClass} htmlFor="group-description">Description</label>
            <textarea id="group-description" value={description} onChange={(event) => setDescription(event.target.value)} rows={3} className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[var(--app-primary)]" />
          </div>
        </div>
      </FormDrawer>
    </div>
  )
}
