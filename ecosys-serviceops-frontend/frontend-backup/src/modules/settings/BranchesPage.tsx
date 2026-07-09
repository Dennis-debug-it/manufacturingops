import { Plus } from 'lucide-react'
import { useState, type ReactNode } from 'react'
import { Badge } from '../../components/ui/Badge'
import { DataTable } from '../../components/ui/DataTable'
import { Drawer } from '../../components/ui/Drawer'
import { EmptyState } from '../../components/ui/EmptyState'
import { ErrorState } from '../../components/ui/ErrorState'
import { LoadingState } from '../../components/ui/LoadingState'
import { PageHeader } from '../../components/ui/PageHeader'
import { useToast } from '../../components/ui/ToastProvider'
import { useAsyncData } from '../../hooks/useAsyncData'
import { assetService } from '../../services/assetService'
import { branchService } from '../../services/branchService'
import { workOrderService } from '../../services/workOrderService'
import type { BranchRecord, UpsertBranchInput } from '../../types/api'
import { formatDateOnly } from '../../utils/date'

type BranchView = BranchRecord & {
  assetCount: number
  workOrderCount: number
}

export function SettingsBranchesPage() {
  const { pushToast } = useToast()
  const [drawerOpen, setDrawerOpen] = useState(false)
  const [editing, setEditing] = useState<BranchRecord | null>(null)
  const [form, setForm] = useState<UpsertBranchInput>({
    name: '',
    code: '',
    location: '',
    address: '',
    contactPerson: '',
    phone: '',
    email: '',
    isActive: true,
  })

  const { data, loading, error, reload } = useAsyncData<BranchView[]>(
    async (signal) => {
      const [branches, assets, workOrders] = await Promise.all([
        branchService.list(signal),
        assetService.list(undefined, signal),
        workOrderService.list(undefined, signal),
      ])

      return branches.map((branch) => ({
        ...branch,
        assetCount: assets.filter((asset) => asset.branchId === branch.id).length,
        workOrderCount: workOrders.filter((workOrder) => workOrder.branchId === branch.id).length,
      }))
    },
    [],
    [],
  )

  function openEditor(branch?: BranchRecord) {
    setEditing(branch ?? null)
    setForm({
      name: branch?.name ?? '',
      code: branch?.code ?? '',
      location: branch?.location ?? '',
      address: branch?.address ?? '',
      contactPerson: branch?.contactPerson ?? '',
      phone: branch?.phone ?? '',
      email: branch?.email ?? '',
      isActive: branch?.isActive ?? true,
    })
    setDrawerOpen(true)
  }

  async function saveBranch() {
    try {
      if (editing) {
        await branchService.update(editing.id, form)
        pushToast({ title: 'Branch updated', description: 'Branch details saved successfully.', tone: 'success' })
      } else {
        await branchService.create(form)
        pushToast({ title: 'Branch created', description: 'The new branch is now available in your workspace.', tone: 'success' })
      }
      setDrawerOpen(false)
      await reload()
    } catch (error) {
      pushToast({ title: 'Save failed', description: error instanceof Error ? error.message : 'Unable to save branch.', tone: 'danger' })
    }
  }

  return (
    <div className="space-y-4">
      <PageHeader
        eyebrow="Settings"
        title="Branches / outlets"
        description="Manage branch details, visibility, and numbering rules for your workspace."
        actions={
          <button type="button" className="button-primary" onClick={() => openEditor()}>
            <Plus className="h-4 w-4" />
            Add branch
          </button>
        }
      />

      <section className="surface-card">
        {loading ? <LoadingState label="Loading branches" /> : null}
        {!loading && error ? <ErrorState title="Unable to load branches" description={error} /> : null}
        {!loading && !error && data.length === 0 ? (
          <EmptyState title="No branches yet" description="Create the first branch or outlet to scope users, assets, and work orders." actionLabel="Refresh" onAction={() => void reload()} />
        ) : null}
        {!loading && !error && data.length > 0 ? (
          <DataTable
            rows={data}
            rowKey={(row) => row.id}
            pageSize={10}
            columns={[
              {
                key: 'branch',
                header: 'Branch',
                cell: (row) => (
                  <div>
                    <p className="font-semibold text-app">{row.name}</p>
                    <p className="mt-1 text-xs text-muted">{row.code}</p>
                  </div>
                ),
              },
              { key: 'location', header: 'Location', cell: (row) => <span>{row.location || 'Not set'}</span> },
              { key: 'assets', header: 'Assets', cell: (row) => <span>{row.assetCount}</span> },
              { key: 'workOrders', header: 'Work Orders', cell: (row) => <span>{row.workOrderCount}</span> },
              { key: 'status', header: 'Status', cell: (row) => <Badge tone={row.isActive ? 'success' : 'danger'}>{row.isActive ? 'Active' : 'Inactive'}</Badge> },
              { key: 'created', header: 'Created', cell: (row) => <span>{formatDateOnly(row.createdAt)}</span> },
              { key: 'actions', header: 'Actions', cell: (row) => <button type="button" className="button-secondary px-3 py-2" onClick={() => openEditor(row)}>Edit</button> },
            ]}
          />
        ) : null}
      </section>

      <Drawer open={drawerOpen} title={editing ? 'Edit branch' : 'Add branch'} description="Set up branch details and numbering preferences for your workspace." onClose={() => setDrawerOpen(false)}>
        <div className="grid gap-4 md:grid-cols-2">
          <Field label="Branch name"><input value={form.name} onChange={(event) => setForm((current) => ({ ...current, name: event.target.value }))} className="field-input" /></Field>
          <Field label="Code"><input value={form.code} onChange={(event) => setForm((current) => ({ ...current, code: event.target.value.toUpperCase() }))} className="field-input" /></Field>
          <Field label="Location"><input value={form.location || ''} onChange={(event) => setForm((current) => ({ ...current, location: event.target.value }))} className="field-input" /></Field>
          <Field label="Address"><input value={form.address || ''} onChange={(event) => setForm((current) => ({ ...current, address: event.target.value }))} className="field-input" /></Field>
          <Field label="Contact person"><input value={form.contactPerson || ''} onChange={(event) => setForm((current) => ({ ...current, contactPerson: event.target.value }))} className="field-input" /></Field>
          <Field label="Phone"><input value={form.phone || ''} onChange={(event) => setForm((current) => ({ ...current, phone: event.target.value }))} className="field-input" /></Field>
          <Field label="Email"><input value={form.email || ''} onChange={(event) => setForm((current) => ({ ...current, email: event.target.value }))} className="field-input" /></Field>
        </div>
        <label className="panel-subtle mt-4 flex items-center justify-between rounded-2xl px-4 py-3">
          <span className="text-sm text-app">Branch is active</span>
          <input type="checkbox" checked={form.isActive} onChange={(event) => setForm((current) => ({ ...current, isActive: event.target.checked }))} />
        </label>
        <div className="mt-6 flex justify-end gap-3">
          <button type="button" className="button-secondary" onClick={() => setDrawerOpen(false)}>Cancel</button>
          <button type="button" className="button-primary" onClick={() => void saveBranch()}>Save branch</button>
        </div>
      </Drawer>
    </div>
  )
}

function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <label className="block space-y-2">
      <span className="text-sm font-medium text-app">{label}</span>
      {children}
    </label>
  )
}
