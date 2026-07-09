import { useState } from 'react'
import toast from 'react-hot-toast'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { usePermission } from '../../shared/hooks/usePermission'
import { formatCurrency } from '../../shared/utils/format'
import { apiClient } from '../../shared/api/apiClient'
import { endpoints } from '../../shared/api/endpoints'
import { useBillableWork } from './api'
import { FinanceWorkspaceNav } from './FinanceWorkspaceNav'
import type { BillableWorkItem } from './types'

export function BillableWorkPage() {
  const canManage = usePermission('finance.manage')
  const { data, isLoading, isError } = useBillableWork()
  const [selected, setSelected] = useState<Set<string>>(new Set())
  const [generating, setGenerating] = useState(false)

  const toggle = (workOrderId: string) => {
    setSelected((current) => {
      const next = new Set(current)
      if (next.has(workOrderId)) next.delete(workOrderId)
      else next.add(workOrderId)
      return next
    })
  }

  const generateInvoices = async () => {
    setGenerating(true)
    try {
      for (const workOrderId of selected) {
        await apiClient.post(endpoints.invoices.generateFromWorkOrder(workOrderId))
      }
      toast.success(`Generated ${selected.size} invoice(s)`)
      setSelected(new Set())
    } catch {
      // interceptor surfaces the error toast
    } finally {
      setGenerating(false)
    }
  }

  const columns: DataTableColumn<BillableWorkItem & { id: string }>[] = [
    {
      key: 'select',
      header: '',
      cell: (row) => (canManage ? <input type="checkbox" checked={selected.has(row.workOrderId)} onChange={() => toggle(row.workOrderId)} className="h-4 w-4 rounded border-slate-300" /> : null),
    },
    { key: 'workOrderNumber', header: 'WO No', cell: (row) => row.workOrderNumber ?? '—' },
    { key: 'clientName', header: 'Client', cell: (row) => row.clientName ?? '—' },
    { key: 'description', header: 'Description', cell: (row) => row.description },
    { key: 'appliedRate', header: 'Rate', cell: (row) => formatCurrency(row.appliedRate, row.currency) },
    { key: 'totalAmount', header: 'Total', cell: (row) => formatCurrency(row.totalAmount, row.currency) },
    { key: 'preferentialRateApplied', header: 'Preferential', cell: (row) => (row.preferentialRateApplied ? <StatusBadge status="Applied" /> : '—') },
  ]

  return (
    <div className="space-y-6">
      <PageHeader
        title="Billable Work"
        subtitle="Completed work orders ready for billing."
        primaryAction={
          canManage && selected.size > 0 ? (
            <button type="button" onClick={generateInvoices} disabled={generating} className="inline-flex h-11 items-center gap-2 rounded-lg bg-slate-900 px-4 text-sm font-medium text-white transition hover:bg-slate-800 disabled:opacity-60">
              {generating ? 'Generating…' : `Generate ${selected.size} Invoice(s)`}
            </button>
          ) : undefined
        }
      />
      <FinanceWorkspaceNav />
      <DataTable
        columns={columns}
        data={(data ?? []).map((item) => ({ ...item, id: item.workOrderId }))}
        loading={isLoading}
        emptyTitle={isError ? 'Billable work unavailable' : 'No billable work'}
        emptyDescription={isError ? 'Billable work items could not be loaded.' : 'Completed work orders ready for billing will appear here.'}
      />
    </div>
  )
}
