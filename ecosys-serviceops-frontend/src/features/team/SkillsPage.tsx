import { useState } from 'react'
import toast from 'react-hot-toast'
import { Plus, Trash2 } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { ConfirmDialog } from '../../shared/components/ConfirmDialog'
import { FormDrawer } from '../../shared/components/FormDrawer'
import { usePermission } from '../../shared/hooks/usePermission'
import { useCreateCertification, useDeleteCertification, useTeamManagementOverview } from './api'
import type { CertificationDefinition } from './types'
import { TeamTabNav } from './components/TeamTabNav'

export function SkillsPage() {
  const canManage = usePermission('team.manage')
  const { data, isLoading, isError } = useTeamManagementOverview()
  const createCertification = useCreateCertification()
  const deleteCertification = useDeleteCertification()

  const [showForm, setShowForm] = useState(false)
  const [name, setName] = useState('')
  const [issuer, setIssuer] = useState('')
  const [renewalMonths, setRenewalMonths] = useState('')
  const [pendingDelete, setPendingDelete] = useState<CertificationDefinition | null>(null)

  const closeForm = () => {
    setShowForm(false)
    setName('')
    setIssuer('')
    setRenewalMonths('')
  }

  const submit = async () => {
    if (!name.trim()) {
      toast.error('Skill/certification name is required')
      return
    }
    try {
      await createCertification.mutateAsync({ name: name.trim(), issuer: issuer.trim() || null, renewalPeriodMonths: renewalMonths ? Number(renewalMonths) : null, isActive: true })
      toast.success('Skill added')
      closeForm()
    } catch {
      // interceptor surfaces the error toast
    }
  }

  const confirmDelete = async () => {
    if (!pendingDelete) return
    try {
      await deleteCertification.mutateAsync(pendingDelete.id)
      toast.success('Skill removed')
    } catch {
      // interceptor surfaces the error toast
    } finally {
      setPendingDelete(null)
    }
  }

  const columns: DataTableColumn<CertificationDefinition>[] = [
    { key: 'name', header: 'Skill / Certification', cell: (row) => row.name },
    { key: 'issuer', header: 'Issuer', cell: (row) => row.issuer ?? '—' },
    { key: 'renewalPeriodMonths', header: 'Renewal (months)', cell: (row) => row.renewalPeriodMonths ?? '—' },
    { key: 'isActive', header: 'Status', cell: (row) => <StatusBadge status={row.isActive ? 'Active' : 'Inactive'} /> },
    {
      key: 'actions',
      header: '',
      cell: (row) => (canManage ? <button type="button" onClick={() => setPendingDelete(row)} className="rounded-lg p-1.5 text-slate-400 transition hover:bg-rose-50 hover:text-rose-600"><Trash2 className="h-4 w-4" /></button> : null),
    },
  ]

  return (
    <div className="space-y-6">
      <PageHeader
        title="Skills"
        subtitle="Skill and certification catalogue mapped to the technician certification tracking backend."
        primaryAction={
          canManage ? (
            <button type="button" onClick={() => setShowForm(true)} className="inline-flex h-11 items-center gap-2 rounded-lg bg-slate-900 px-4 text-sm font-medium text-white transition hover:bg-slate-800">
              <Plus className="h-4 w-4" />New Skill
            </button>
          ) : undefined
        }
      />
      <TeamTabNav />

      <DataTable columns={columns} data={data?.certifications ?? []} loading={isLoading} emptyTitle={isError ? 'Skills unavailable' : 'No skills yet'} emptyDescription={isError ? 'Certifications could not be loaded.' : 'Add your first skill or certification type.'} />

      <FormDrawer
        open={showForm}
        title="New Skill"
        onClose={closeForm}
        footer={
          <div className="flex items-center justify-end gap-2">
            <button type="button" onClick={closeForm} className="inline-flex h-11 items-center justify-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">Cancel</button>
            <button type="button" onClick={submit} disabled={createCertification.isPending} className="inline-flex h-11 items-center justify-center rounded-lg bg-slate-900 px-4 text-sm font-medium text-white transition hover:bg-slate-800 disabled:opacity-60">
              {createCertification.isPending ? 'Saving…' : 'Create'}
            </button>
          </div>
        }
      >
        <div className="space-y-4">
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="skill-name">Name</label>
            <input id="skill-name" value={name} onChange={(event) => setName(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900" />
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="skill-issuer">Issuer</label>
            <input id="skill-issuer" value={issuer} onChange={(event) => setIssuer(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900" />
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="skill-renewal">Renewal Period (months)</label>
            <input id="skill-renewal" type="number" min="0" value={renewalMonths} onChange={(event) => setRenewalMonths(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900" />
          </div>
        </div>
      </FormDrawer>

      <ConfirmDialog open={pendingDelete !== null} title="Remove Skill" message={`This will deactivate "${pendingDelete?.name}".`} danger onCancel={() => setPendingDelete(null)} onConfirm={confirmDelete} />
    </div>
  )
}
