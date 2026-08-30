import { useState } from 'react'
import toast from 'react-hot-toast'
import { Plus, Upload } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { FormDrawer } from '../../shared/components/FormDrawer'
import { usePermission } from '../../shared/hooks/usePermission'
import { formatCurrency } from '../../shared/utils/format'
import { useCreateRateCard, useFinanceRateCards } from './api'
import { RateCardBulkImportDrawer } from './components/RateCardBulkImportDrawer'
import { FinanceWorkspaceNav } from './FinanceWorkspaceNav'
import type { RateCard } from './types'

const categories = ['Labour', 'Callout', 'Service', 'Transport', 'Material', 'Other']
const timeTypes = ['Standard', 'Overtime', 'Emergency', 'Weekend', 'Holiday']

export function RateCardsPage() {
  const canManage = usePermission('finance.rates.manage')
  const { data, isLoading, isError } = useFinanceRateCards()
  const createRateCard = useCreateRateCard()

  const [showForm, setShowForm] = useState(false)
  const [showImport, setShowImport] = useState(false)
  const [name, setName] = useState('')
  const [category, setCategory] = useState('Labour')
  const [unitRate, setUnitRate] = useState('')
  const [unit, setUnit] = useState('hour')
  const [timeType, setTimeType] = useState('Standard')

  const close = () => {
    setShowForm(false)
    setName('')
    setUnitRate('')
  }

  const submit = async () => {
    if (!name.trim() || !unitRate) {
      toast.error('Name and unit rate are required')
      return
    }
    try {
      await createRateCard.mutateAsync({ name: name.trim(), category, unitRate: Number(unitRate), unit, timeType, isDefault: false })
      toast.success('Rate card created')
      close()
    } catch {
      // interceptor surfaces the error toast
    }
  }

  const columns: DataTableColumn<RateCard>[] = [
    { key: 'name', header: 'Name', cell: (row) => row.name },
    { key: 'category', header: 'Category', cell: (row) => row.category },
    { key: 'unitRate', header: 'Rate', cell: (row) => `${formatCurrency(row.unitRate)} / ${row.unit}` },
    { key: 'timeType', header: 'Time Type', cell: (row) => row.timeType },
    { key: 'isDefault', header: 'Default', cell: (row) => (row.isDefault ? <StatusBadge status="Default" /> : '—') },
    { key: 'isActive', header: 'Status', cell: (row) => <StatusBadge status={row.isActive ? 'Active' : 'Inactive'} /> },
  ]

  return (
    <div className="space-y-6">
      <PageHeader
        title="Rate Cards"
        subtitle="Labour and material rate tables."
        primaryAction={
          canManage ? (
            <button type="button" onClick={() => setShowForm(true)} className="inline-flex h-11 items-center gap-2 rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)]">
              <Plus className="h-4 w-4" />New Rate Card
            </button>
          ) : undefined
        }
        secondaryActions={
          canManage ? (
            <button type="button" onClick={() => setShowImport(true)} className="inline-flex h-11 items-center gap-2 rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">
              <Upload className="h-4 w-4" />Bulk Import
            </button>
          ) : undefined
        }
      />
      <FinanceWorkspaceNav />
      <DataTable columns={columns} data={data ?? []} loading={isLoading} emptyTitle={isError ? 'Rate cards unavailable' : 'No rate cards yet'} emptyDescription={isError ? 'Rate cards could not be loaded.' : 'Create your first rate card.'} />

      <FormDrawer
        open={showForm}
        title="New Rate Card"
        onClose={close}
        footer={
          <div className="flex items-center justify-end gap-2">
            <button type="button" onClick={close} className="inline-flex h-11 items-center justify-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">Cancel</button>
            <button type="button" onClick={submit} disabled={createRateCard.isPending} className="inline-flex h-11 items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:opacity-60">
              {createRateCard.isPending ? 'Saving…' : 'Create'}
            </button>
          </div>
        }
      >
        <div className="space-y-4">
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="rate-name">Name</label>
            <input id="rate-name" value={name} onChange={(event) => setName(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]" />
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="rate-category">Category</label>
              <select id="rate-category" value={category} onChange={(event) => setCategory(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]">
                {categories.map((option) => (<option key={option} value={option}>{option}</option>))}
              </select>
            </div>
            <div>
              <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="rate-time-type">Time Type</label>
              <select id="rate-time-type" value={timeType} onChange={(event) => setTimeType(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]">
                {timeTypes.map((option) => (<option key={option} value={option}>{option}</option>))}
              </select>
            </div>
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="rate-unit-rate">Unit Rate</label>
              <input id="rate-unit-rate" type="number" step="0.01" value={unitRate} onChange={(event) => setUnitRate(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]" />
            </div>
            <div>
              <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="rate-unit">Unit</label>
              <input id="rate-unit" value={unit} onChange={(event) => setUnit(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]" />
            </div>
          </div>
        </div>
      </FormDrawer>

      <RateCardBulkImportDrawer open={showImport} onClose={() => setShowImport(false)} onImport={(payload) => createRateCard.mutateAsync(payload)} />
    </div>
  )
}
