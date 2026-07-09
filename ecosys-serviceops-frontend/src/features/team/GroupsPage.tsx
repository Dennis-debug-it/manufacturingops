import { useState } from 'react'
import toast from 'react-hot-toast'
import { Plus } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { FormDrawer } from '../../shared/components/FormDrawer'
import { usePermission } from '../../shared/hooks/usePermission'
import { useAssignmentGroups, useCreateAssignmentGroup } from './api'
import type { AssignmentGroup } from './types'

export function GroupsPage() {
  const canManage = usePermission('team.manage')
  const { data, isLoading, isError } = useAssignmentGroups()
  const createGroup = useCreateAssignmentGroup()

  const [showForm, setShowForm] = useState(false)
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [skillArea, setSkillArea] = useState('')

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
    { key: 'name', header: 'Group', cell: (row) => row.name },
    { key: 'skillArea', header: 'Skill Area', cell: (row) => row.skillArea ?? '—' },
    { key: 'branchName', header: 'Branch', cell: (row) => row.branchName ?? 'All Branches' },
    { key: 'members', header: 'Members', cell: (row) => row.members.length },
    { key: 'isActive', header: 'Status', cell: (row) => <StatusBadge status={row.isActive ? 'Active' : 'Inactive'} /> },
  ]

  return (
    <div className="space-y-6">
      <PageHeader
        title="Groups"
        subtitle="Default assignment groups and members."
        primaryAction={
          canManage ? (
            <button type="button" onClick={() => setShowForm(true)} className="inline-flex h-11 items-center gap-2 rounded-lg bg-slate-900 px-4 text-sm font-medium text-white transition hover:bg-slate-800">
              <Plus className="h-4 w-4" />New Group
            </button>
          ) : undefined
        }
      />

      <DataTable columns={columns} data={data ?? []} loading={isLoading} emptyTitle={isError ? 'Groups unavailable' : 'No groups yet'} emptyDescription={isError ? 'Assignment groups could not be loaded.' : 'Create your first assignment group.'} />

      <FormDrawer
        open={showForm}
        title="New Group"
        onClose={close}
        footer={
          <div className="flex items-center justify-end gap-2">
            <button type="button" onClick={close} className="inline-flex h-11 items-center justify-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">Cancel</button>
            <button type="button" onClick={submit} disabled={createGroup.isPending} className="inline-flex h-11 items-center justify-center rounded-lg bg-slate-900 px-4 text-sm font-medium text-white transition hover:bg-slate-800 disabled:opacity-60">
              {createGroup.isPending ? 'Creating…' : 'Create Group'}
            </button>
          </div>
        }
      >
        <div className="space-y-4">
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="group-name">Name</label>
            <input id="group-name" value={name} onChange={(event) => setName(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900" />
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="group-skill">Skill Area</label>
            <input id="group-skill" value={skillArea} onChange={(event) => setSkillArea(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900" />
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="group-description">Description</label>
            <textarea id="group-description" value={description} onChange={(event) => setDescription(event.target.value)} rows={3} className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-slate-900" />
          </div>
        </div>
      </FormDrawer>
    </div>
  )
}
