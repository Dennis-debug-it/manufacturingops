import { useState } from 'react'
import toast from 'react-hot-toast'
import { Plus, Upload } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { FormDrawer } from '../../shared/components/FormDrawer'
import { usePermission } from '../../shared/hooks/usePermission'
import { formatCurrency } from '../../shared/utils/format'
import { useClients } from '../clients/api'
import type { Client } from '../clients/types'
import { useApprovePreferentialRate, useCreatePreferentialRate, usePreferentialRates } from './api'
import { PreferentialRateBulkImportDrawer } from './components/PreferentialRateBulkImportDrawer'
import { FinanceWorkspaceNav } from './FinanceWorkspaceNav'
import type { PreferentialRate } from './types'
import { getActiveCurrency } from '../../shared/utils/currency'

function ApproveButton({ rate }: { rate: PreferentialRate }) {
  const canApprove = usePermission('finance.approvals.manage')
  const approve = useApprovePreferentialRate(rate.id)
  if (!canApprove || rate.status !== 'Pending') return null
  return (
    <button
      type="button"
      onClick={async () => {
        try {
          await approve.mutateAsync()
          toast.success('Rate approved')
        } catch {
          // interceptor surfaces the error toast
        }
      }}
      className="rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-xs font-medium text-slate-700 hover:bg-slate-50"
    >
      Approve
    </button>
  )
}

export function PreferentialRatesPage() {
  const canManage = usePermission('finance.rates.manage')
  const { data, isLoading, isError } = usePreferentialRates()
  const { data: clients } = useClients()
  const createRate = useCreatePreferentialRate()

  const [showForm, setShowForm] = useState(false)
  const [showImport, setShowImport] = useState(false)
  const [clientId, setClientId] = useState('')
  const [rateName, setRateName] = useState('')
  const [defaultRate, setDefaultRate] = useState('')
  const [preferentialRate, setPreferentialRate] = useState('')

  const close = () => {
    setShowForm(false)
    setClientId('')
    setRateName('')
    setDefaultRate('')
    setPreferentialRate('')
  }

  const submit = async () => {
    if (!clientId || !rateName.trim()) {
      toast.error('Client and rate name are required')
      return
    }
    try {
      await createRate.mutateAsync({
        clientId,
        rateName: rateName.trim(),
        rateType: 'Labour',
        itemType: 'Labour',
        unit: 'hour',
        defaultRate: Number(defaultRate) || 0,
        preferentialRate: Number(preferentialRate) || 0,
        currency: getActiveCurrency(),
        effectiveFrom: new Date().toISOString(),
        status: 'Pending',
        requiresApproval: true,
      })
      toast.success('Preferential rate created')
      close()
    } catch {
      // interceptor surfaces the error toast
    }
  }

  const columns: DataTableColumn<PreferentialRate>[] = [
    { key: 'clientName', header: 'Client', cell: (row) => row.clientName ?? '—' },
    { key: 'rateName', header: 'Rate', cell: (row) => row.rateName },
    { key: 'defaultRate', header: 'Default', cell: (row) => formatCurrency(row.defaultRate, row.currency) },
    { key: 'preferentialRate', header: 'Preferential', cell: (row) => formatCurrency(row.preferentialRate, row.currency) },
    { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.status} /> },
    { key: 'actions', header: '', cell: (row) => <ApproveButton rate={row} /> },
  ]

  return (
    <div className="space-y-6">
      <PageHeader
        title="Preferential Rates"
        subtitle="Client-specific rate overrides."
        primaryAction={
          canManage ? (
            <button type="button" onClick={() => setShowForm(true)} className="inline-flex h-11 items-center gap-2 rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)]">
              <Plus className="h-4 w-4" />New Rate
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
      <DataTable columns={columns} data={data ?? []} loading={isLoading} emptyTitle={isError ? 'Rates unavailable' : 'No preferential rates'} emptyDescription={isError ? 'Preferential rates could not be loaded.' : 'Create a client-specific rate override.'} />

      <FormDrawer
        open={showForm}
        title="New Preferential Rate"
        onClose={close}
        footer={
          <div className="flex items-center justify-end gap-2">
            <button type="button" onClick={close} className="inline-flex h-11 items-center justify-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">Cancel</button>
            <button type="button" onClick={submit} disabled={createRate.isPending} className="inline-flex h-11 items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:opacity-60">
              {createRate.isPending ? 'Saving…' : 'Create'}
            </button>
          </div>
        }
      >
        <div className="space-y-4">
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="pref-client">Client</label>
            <select id="pref-client" value={clientId} onChange={(event) => setClientId(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]">
              <option value="">Select a client</option>
              {(clients ?? []).map((client) => (<option key={client.id} value={client.id}>{client.clientName}</option>))}
            </select>
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="pref-name">Rate Name</label>
            <input id="pref-name" value={rateName} onChange={(event) => setRateName(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]" />
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="pref-default">Default Rate</label>
              <input id="pref-default" type="number" step="0.01" value={defaultRate} onChange={(event) => setDefaultRate(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]" />
            </div>
            <div>
              <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="pref-preferential">Preferential Rate</label>
              <input id="pref-preferential" type="number" step="0.01" value={preferentialRate} onChange={(event) => setPreferentialRate(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]" />
            </div>
          </div>
        </div>
      </FormDrawer>

      <PreferentialRateBulkImportDrawer
        open={showImport}
        onClose={() => setShowImport(false)}
        clients={(clients ?? []) as Client[]}
        onImport={(payload) => createRate.mutateAsync(payload)}
      />
    </div>
  )
}
