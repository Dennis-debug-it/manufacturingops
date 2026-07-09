import { useMemo, useState } from 'react'
import { subDays } from 'date-fns'
import { Download } from 'lucide-react'
import { apiClient } from '../../shared/api/apiClient'
import { endpoints } from '../../shared/api/endpoints'
import { PageHeader } from '../../shared/components/PageHeader'
import { Spinner } from '../../shared/components/Spinner'
import { StatCard } from '../../shared/components/StatCard'
import { SectionHeader } from '../../shared/components/SectionHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { formatDate } from '../../shared/utils/dates'
import { formatPercent } from '../../shared/utils/format'
import { usePmComplianceReport } from './api'
import { useAssetReliability, useTechnicianProductivity } from '../reports/api'
import { useClients } from '../clients/api'

const periodOptions = [
  { value: '30', label: 'Last 30 Days' },
  { value: '90', label: 'Last 90 Days' },
  { value: '365', label: 'Last Year' },
]

export function CompliancePage() {
  const [period, setPeriod] = useState('30')
  const from = useMemo(() => subDays(new Date(), Number(period)).toISOString(), [period])
  const to = useMemo(() => new Date().toISOString(), [period])

  const { data: compliance, isLoading } = usePmComplianceReport({ from, to })
  const { data: assetReliability } = useAssetReliability({ from, to })
  const { data: technicianProductivity } = useTechnicianProductivity({ from, to })
  const { data: clients } = useClients()

  const clientNameById = useMemo(() => new Map((clients ?? []).map((client) => [client.id, client.clientName])), [clients])

  const byCategory = useMemo(() => {
    const groups = new Map<string, { total: number; compliance: number }>()
    for (const asset of assetReliability?.assets ?? []) {
      const key = asset.categoryName ?? 'Uncategorised'
      const entry = groups.get(key) ?? { total: 0, compliance: 0 }
      entry.total += 1
      entry.compliance += asset.pmCompliance
      groups.set(key, entry)
    }
    return Array.from(groups.entries()).map(([category, { total, compliance: sum }]) => ({
      id: category,
      category,
      assetCount: total,
      complianceRate: total > 0 ? Math.round((sum / total) * 10) / 10 : 0,
    }))
  }, [assetReliability])

  const exportCsv = async () => {
    const response = await apiClient.get(endpoints.reports.pmComplianceExport, { params: { from, to }, responseType: 'blob' })
    const url = URL.createObjectURL(response.data as Blob)
    const link = document.createElement('a')
    link.href = url
    link.download = 'pm-compliance.csv'
    link.click()
    URL.revokeObjectURL(url)
  }

  const clientColumns: DataTableColumn<{ id: string; clientName: string; due: number; completed: number; complianceRate: number }>[] = [
    { key: 'clientName', header: 'Client', cell: (row) => row.clientName },
    { key: 'due', header: 'Due', cell: (row) => row.due },
    { key: 'completed', header: 'Completed', cell: (row) => row.completed },
    { key: 'complianceRate', header: 'Compliance', cell: (row) => formatPercent(row.complianceRate) },
  ]

  const categoryColumns: DataTableColumn<(typeof byCategory)[number]>[] = [
    { key: 'category', header: 'Asset Category', cell: (row) => row.category },
    { key: 'assetCount', header: 'Assets', cell: (row) => row.assetCount },
    { key: 'complianceRate', header: 'Avg PM Compliance', cell: (row) => formatPercent(row.complianceRate) },
  ]

  const technicianColumns: DataTableColumn<{ id: string; name: string; pmJobs: number; onTimeRate: number }>[] = [
    { key: 'name', header: 'Technician', cell: (row) => row.name },
    { key: 'pmJobs', header: 'PM Jobs', cell: (row) => row.pmJobs },
    { key: 'onTimeRate', header: 'On-Time Rate', cell: (row) => formatPercent(row.onTimeRate) },
  ]

  const missedColumns: DataTableColumn<{ id: string; assetName: string; clientName: string; nextPmDate: string; daysOverdue: number }>[] = [
    { key: 'assetName', header: 'Asset', cell: (row) => row.assetName },
    { key: 'clientName', header: 'Client', cell: (row) => row.clientName },
    { key: 'nextPmDate', header: 'Was Due', cell: (row) => formatDate(row.nextPmDate) },
    { key: 'daysOverdue', header: 'Days Overdue', cell: (row) => row.daysOverdue },
  ]

  if (isLoading) {
    return <Spinner label="Loading compliance report" />
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title="PM Compliance"
        subtitle="Compliance rates by client, asset type, and technician."
        secondaryActions={
          <div className="flex items-center gap-2">
            <select value={period} onChange={(event) => setPeriod(event.target.value)} className="h-11 rounded-lg border border-slate-300 bg-white px-3 text-sm outline-none focus:border-slate-900">
              {periodOptions.map((option) => (
                <option key={option.value} value={option.value}>{option.label}</option>
              ))}
            </select>
            <button type="button" onClick={exportCsv} className="inline-flex h-11 items-center gap-2 rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">
              <Download className="h-4 w-4" />Export CSV
            </button>
          </div>
        }
      />

      <div className="grid grid-cols-2 gap-3 sm:grid-cols-5">
        <StatCard label="Active Plans" value={compliance?.activePlans ?? 0} />
        <StatCard label="Due In Period" value={compliance?.dueInPeriod ?? 0} />
        <StatCard label="Completed On Time" value={compliance?.completedOnTime ?? 0} tone="success" />
        <StatCard label="Overdue" value={compliance?.overdue ?? 0} tone="danger" />
        <StatCard label="Compliance Rate" value={formatPercent(compliance?.complianceRate ?? 0)} tone={((compliance?.complianceRate ?? 0) >= 80) ? 'success' : 'warning'} />
      </div>

      <section className="space-y-4 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
        <SectionHeader title="By Client" description="PM completion rate per client for the selected period." />
        <DataTable
          columns={clientColumns}
          data={(compliance?.byClient ?? []).map((row) => ({ id: row.clientId, clientName: clientNameById.get(row.clientId) ?? 'Unknown', due: row.due, completed: row.completed, complianceRate: row.complianceRate }))}
          emptyTitle="No data"
          emptyDescription="No PM work orders were due for any client in this period."
        />
      </section>

      <section className="space-y-4 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
        <SectionHeader title="By Asset Category" description="Average PM compliance grouped by asset category." />
        <DataTable columns={categoryColumns} data={byCategory} emptyTitle="No data" emptyDescription="No asset category data available." />
      </section>

      <section className="space-y-4 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
        <SectionHeader title="By Technician" description="PM job volume and on-time rate per technician." />
        <DataTable
          columns={technicianColumns}
          data={(technicianProductivity?.technicians ?? []).filter((row) => row.pmJobs > 0).map((row) => ({ id: row.technicianId ?? row.name, name: row.name, pmJobs: row.pmJobs, onTimeRate: row.onTimeRate }))}
          emptyTitle="No PM jobs"
          emptyDescription="No technicians completed PM jobs in this period."
        />
      </section>

      <section className="space-y-4 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
        <SectionHeader title="Missed PMs" description="Overdue preventive maintenance plans." />
        <DataTable
          columns={missedColumns}
          data={(compliance?.overduePlans ?? []).map((row) => ({ id: row.id, assetName: row.assetName ?? '—', clientName: row.clientName ?? '—', nextPmDate: row.nextPmDate ?? '', daysOverdue: row.daysOverdue }))}
          emptyTitle="No missed PMs"
          emptyDescription="All preventive maintenance is on schedule."
        />
      </section>
    </div>
  )
}
