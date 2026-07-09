import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { SectionHeader } from '../../shared/components/SectionHeader'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { formatCurrency } from '../../shared/utils/format'
import { useFinanceApprovalRules, useInvoices } from './api'
import { FinanceWorkspaceNav } from './FinanceWorkspaceNav'
import type { FinanceApprovalRule, InvoiceListItem } from './types'

export function InvoiceApprovalsPage() {
  const { data: rules, isLoading: rulesLoading } = useFinanceApprovalRules()
  const { data: invoices, isLoading: invoicesLoading } = useInvoices({ status: 'Draft' })

  const ruleColumns: DataTableColumn<FinanceApprovalRule>[] = [
    { key: 'approvalArea', header: 'Area', cell: (row) => row.approvalArea },
    { key: 'thresholdAmount', header: 'Threshold', cell: (row) => formatCurrency(row.thresholdAmount, row.currency) },
    { key: 'requiredRole', header: 'Required Role', cell: (row) => row.requiredRole },
    { key: 'isActive', header: 'Status', cell: (row) => <StatusBadge status={row.isActive ? 'Active' : 'Inactive'} /> },
  ]

  const invoiceColumns: DataTableColumn<InvoiceListItem>[] = [
    { key: 'invoiceNumber', header: 'Invoice', cell: (row) => row.invoiceNumber },
    { key: 'clientName', header: 'Client', cell: (row) => row.clientName ?? '—' },
    { key: 'totalAmount', header: 'Total', cell: (row) => formatCurrency(row.totalAmount, row.currency) },
    { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.status} /> },
  ]

  return (
    <div className="space-y-6">
      <PageHeader title="Invoice Approvals" subtitle="Contract invoice approval queue, driven by finance approval rules." />

      <FinanceWorkspaceNav />
      <section className="space-y-4 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
        <SectionHeader title="Approval Rules" description="Thresholds that route invoices for approval." />
        <DataTable columns={ruleColumns} data={rules ?? []} loading={rulesLoading} emptyTitle="No approval rules" emptyDescription="Configure approval rules under Finance settings." />
      </section>

      <section className="space-y-4 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
        <SectionHeader title="Pending Invoices" description="Draft invoices awaiting review before sending." />
        <DataTable columns={invoiceColumns} data={invoices ?? []} loading={invoicesLoading} emptyTitle="No pending invoices" emptyDescription="Invoices awaiting approval will appear here." />
      </section>
    </div>
  )
}
