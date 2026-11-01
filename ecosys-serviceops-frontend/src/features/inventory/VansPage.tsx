import { useState } from 'react'
import { Link } from 'react-router-dom'
import toast from 'react-hot-toast'
import { Plus, Truck } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { FormDrawer } from '../../shared/components/FormDrawer'
import { useAuth } from '../../shared/hooks/useAuth'
import { usePermission } from '../../shared/hooks/usePermission'
import { useTechnicians } from '../team/api'
import { useBranches } from './api'
import { useCreateVan, useVans, type Van } from './vansApi'

const fieldClass = 'h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none transition focus:border-[var(--app-primary)]'
const labelClass = 'mb-1.5 block text-sm font-medium text-slate-700'

export function VansPage() {
  const canManage = usePermission('inventory.materials.manage')
  const { data, isLoading, isError } = useVans()
  const { data: technicians } = useTechnicians()
  // Only admins can list branches (the API refuses others); everyone else sees just "None".
  const { data: branches } = useBranches({ enabled: useAuth().user?.role === 'admin' })
  const createVan = useCreateVan()

  const [showNew, setShowNew] = useState(false)
  const [name, setName] = useState('')
  const [registration, setRegistration] = useState('')
  const [technicianId, setTechnicianId] = useState('')
  const [homeBranchId, setHomeBranchId] = useState('')

  const submit = async () => {
    try {
      await createVan.mutateAsync({
        name: name.trim(),
        registrationNumber: registration.trim() || undefined,
        technicianId: technicianId || undefined,
        homeBranchId: homeBranchId || undefined,
      })
      toast.success('Van created')
      setShowNew(false)
      setName('')
      setRegistration('')
      setTechnicianId('')
      setHomeBranchId('')
    } catch {
      // interceptor surfaces the error toast
    }
  }

  const columns: DataTableColumn<Van>[] = [
    { key: 'name', header: 'Van', cell: (row) => <Link className="inline-flex items-center gap-2 font-medium text-slate-900 underline decoration-slate-300 underline-offset-4" to={`/inventory/vans/${row.id}`}><Truck className="h-4 w-4 text-slate-400" />{row.name}</Link> },
    { key: 'registrationNumber', header: 'Reg. No.', cell: (row) => <span className="font-mono text-xs">{row.registrationNumber ?? '—'}</span> },
    { key: 'technicianName', header: 'Technician', cell: (row) => row.technicianName ?? 'Unassigned' },
    { key: 'homeBranchName', header: 'Home Branch', cell: (row) => row.homeBranchName ?? '—' },
    { key: 'distinctItemCount', header: 'Items Held', cell: (row) => row.distinctItemCount },
    { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.isActive ? 'Active' : 'Inactive'} /> },
  ]

  return (
    <div className="space-y-4">
      <PageHeader
        title="Vans"
        subtitle="Mobile stock locations. Load parts from a branch, consume on jobs, return the rest."
        primaryAction={canManage ? (
          <button type="button" onClick={() => setShowNew(true)} className="inline-flex h-11 items-center gap-2 rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)]">
            <Plus className="h-4 w-4" />New Van
          </button>
        ) : undefined}
      />

      <DataTable
        columns={columns}
        data={data ?? []}
        loading={isLoading}
        emptyTitle={isError ? 'Vans unavailable' : 'No vans yet'}
        emptyDescription={isError ? 'The van list could not be loaded.' : 'Register a van to start tracking mobile stock.'}
      />

      <FormDrawer open={showNew} title="New Van" onClose={() => setShowNew(false)}>
        <div className="space-y-4">
          <div>
            <label className={labelClass}>Van Name</label>
            <input value={name} onChange={(event) => setName(event.target.value)} placeholder="e.g. Van Alpha" className={fieldClass} />
          </div>
          <div>
            <label className={labelClass}>Registration Number</label>
            <input value={registration} onChange={(event) => setRegistration(event.target.value)} placeholder="e.g. KDA 001A" className={fieldClass} />
          </div>
          <div>
            <label className={labelClass}>Assigned Technician</label>
            <select value={technicianId} onChange={(event) => setTechnicianId(event.target.value)} className={fieldClass}>
              <option value="">Unassigned</option>
              {(technicians ?? []).map((tech) => (
                <option key={tech.id} value={tech.id}>{tech.fullName}</option>
              ))}
            </select>
          </div>
          <div>
            <label className={labelClass}>Home Branch</label>
            <select value={homeBranchId} onChange={(event) => setHomeBranchId(event.target.value)} className={fieldClass}>
              <option value="">None</option>
              {(branches ?? []).map((branch) => (
                <option key={branch.id} value={branch.id}>{branch.name}</option>
              ))}
            </select>
          </div>
          <button type="button" disabled={!name.trim() || createVan.isPending} onClick={() => void submit()} className="inline-flex h-11 w-full items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:opacity-60">
            Create Van
          </button>
        </div>
      </FormDrawer>
    </div>
  )
}
