import { useState } from 'react'
import { Link } from 'react-router-dom'
import { Plus } from 'lucide-react'
import { DataTable, type DataTableColumn } from '../../../shared/components/DataTable'
import { StatusBadge } from '../../../shared/components/StatusBadge'
import { SummarySection } from '../../../shared/pages/ModulePage'
import { usePermission } from '../../../shared/hooks/usePermission'
import { formatDate } from '../../../shared/utils/dates'
import { formatCurrency } from '../../../shared/utils/format'
import { useOpportunities, useTenders } from '../api'
import { OPEN_STAGES, stageLabels, tenderStatusLabels, type SalesOpportunityListItem, type SalesTenderListItem } from '../types'
import { OpportunityFormDrawer } from './OpportunityFormDrawer'

/** A client's deals and RFPs/RFQs, so the account's sales history sits next to its sites and work orders. */
export function ClientSalesTab({ clientId }: { clientId: string }) {
  const canManage = usePermission('crm.manage')
  const [showForm, setShowForm] = useState(false)
  const { data: deals, isLoading: dealsLoading } = useOpportunities({ clientId })
  const { data: tenders, isLoading: tendersLoading } = useTenders({ clientId })

  const open = (deals ?? []).filter((deal) => OPEN_STAGES.includes(deal.stage))
  const openValue = open.reduce((sum, deal) => sum + (deal.convertedValue ?? 0), 0)
  const won = (deals ?? []).filter((deal) => deal.stage === 'Won')

  const dealColumns: DataTableColumn<SalesOpportunityListItem>[] = [
    { key: 'title', header: 'Deal', cell: (row) => <Link className="font-medium text-slate-900 underline decoration-slate-300 underline-offset-4" to={`/crm/opportunities/${row.id}`}>{row.title}</Link> },
    { key: 'stage', header: 'Stage', cell: (row) => <StatusBadge status={stageLabels[row.stage]} /> },
    { key: 'value', header: 'Value', cell: (row) => formatCurrency(row.estimatedValue, row.currency) },
    { key: 'close', header: 'Expected close', cell: (row) => (row.expectedCloseDate ? formatDate(row.expectedCloseDate) : '—') },
    { key: 'owner', header: 'Owner', cell: (row) => row.ownerName ?? '—' },
  ]
  const tenderColumns: DataTableColumn<SalesTenderListItem>[] = [
    { key: 'title', header: 'RFP / RFQ', cell: (row) => <Link className="font-medium text-slate-900 underline decoration-slate-300 underline-offset-4" to={`/crm/tenders/${row.id}`}>{row.type} · {row.title}</Link> },
    { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={tenderStatusLabels[row.status]} /> },
    { key: 'deadline', header: 'Submission deadline', cell: (row) => formatDate(row.submissionDeadline) },
    { key: 'value', header: 'Value', cell: (row) => formatCurrency(row.submittedValue ?? row.estimatedValue, row.currency) },
  ]

  return (
    <div className="space-y-4">
      <SummarySection
        title="Deals"
        description={deals ? `${open.length} open (${formatCurrency(openValue)}) · ${won.length} won` : 'Deals with this client.'}
      >
        {canManage ? (
          <div className="mb-3 flex justify-end">
            <button type="button" onClick={() => setShowForm(true)} className="inline-flex h-10 items-center gap-2 rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)]">
              <Plus className="h-4 w-4" />New deal for this client
            </button>
          </div>
        ) : null}
        <DataTable columns={dealColumns} data={deals ?? []} loading={dealsLoading} emptyTitle="No deals yet" emptyDescription="Deals raised for this client will appear here." />
      </SummarySection>
      <SummarySection title="RFPs & RFQs" description="Requests for proposal or quotation from this client.">
        <DataTable columns={tenderColumns} data={tenders ?? []} loading={tendersLoading} emptyTitle="No RFPs or RFQs" emptyDescription="Requests this client sends for bids will appear here." />
      </SummarySection>
      <OpportunityFormDrawer open={showForm} onClose={() => setShowForm(false)} defaultClientId={clientId} />
    </div>
  )
}
