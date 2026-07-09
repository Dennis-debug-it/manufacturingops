import { useNavigate } from 'react-router-dom'
import { EmptyState } from '../../components/ui/EmptyState'
import { ErrorState } from '../../components/ui/ErrorState'
import { LoadingState } from '../../components/ui/LoadingState'
import { MetricCard, MetricGrid, SectionCard } from '../../components/ui/Workspace'
import { useAsyncData } from '../../hooks/useAsyncData'
import { financeReportsService } from '../../services/financeReportsService'
import type { FinanceDashboard } from '../../types/api'

const emptyDash: FinanceDashboard = {
  totalInvoicedYtd: 0,
  totalCollectedYtd: 0,
  outstandingBalance: 0,
  overdueBalance: 0,
  quoteConversionRate: 0,
  totalQuotations: 0,
  convertedQuotations: 0,
  billableWorkItemsCount: 0,
  estimatedBillableAmount: 0,
  currency: 'KES',
}

function groupHasActivity(data: FinanceDashboard) {
  return Boolean(
    data.totalInvoicedYtd
    || data.totalCollectedYtd
    || data.outstandingBalance
    || data.overdueBalance
    || data.totalQuotations
    || data.convertedQuotations
    || data.billableWorkItemsCount
    || data.estimatedBillableAmount
    || data.quoteConversionRate,
  )
}

export function TenantFinanceOverviewPage() {
  const navigate = useNavigate()
  const { data, loading, error } = useAsyncData<FinanceDashboard>(
    (signal) => financeReportsService.getDashboard(signal),
    emptyDash,
    [],
  )

  const c = data.currency
  const hasActivity = groupHasActivity(data)

  if (loading) return <LoadingState label="Loading finance overview" />
  if (error) return <ErrorState title="Unable to load finance overview" description={error} />

  if (!hasActivity) {
    return (
      <SectionCard title="Finance overview" description="A grouped view of billing, collections, costs, and receivables.">
        <EmptyState
          title="No finance activity yet"
          description="Create your first quote, invoice, or billable work item to populate this overview. The grouped finance navigation is ready when you are."
          action={
            <button type="button" className="button-primary" onClick={() => navigate('/finance/documents/quotes')}>
              Open Documents
            </button>
          }
        />
      </SectionCard>
    )
  }

  return (
    <div className="space-y-4">
      <SectionCard title="Revenue & Collections" description="Current billing throughput and outstanding balances.">
        <MetricGrid className="xl:grid-cols-4">
          <button type="button" className="text-left" onClick={() => navigate('/finance/documents/invoices')}>
            <MetricCard
              label="Invoiced YTD"
              value={`${c} ${data.totalInvoicedYtd.toLocaleString(undefined, { minimumFractionDigits: 0 })}`}
              meta="Total invoiced this year"
              emphasis="accent"
            />
          </button>
          <button type="button" className="text-left" onClick={() => navigate('/finance/documents/invoices')}>
            <MetricCard
              label="Collected YTD"
              value={`${c} ${data.totalCollectedYtd.toLocaleString(undefined, { minimumFractionDigits: 0 })}`}
              meta="Payments received this year"
              emphasis="accent"
            />
          </button>
          <button type="button" className="text-left" onClick={() => navigate('/finance/tax-receivables/aging')}>
            <MetricCard
              label="Outstanding"
              value={`${c} ${data.outstandingBalance.toLocaleString(undefined, { minimumFractionDigits: 0 })}`}
              meta="Unpaid invoice balance"
            />
          </button>
          <button type="button" className="text-left" onClick={() => navigate('/finance/tax-receivables/aging')}>
            <MetricCard
              label="Overdue"
              value={`${c} ${data.overdueBalance.toLocaleString(undefined, { minimumFractionDigits: 0 })}`}
              meta="Past due balance"
              emphasis="warning"
            />
          </button>
        </MetricGrid>
      </SectionCard>

      <SectionCard title="Billing Pipeline" description="Quote conversion and billable work ready for invoicing.">
        <MetricGrid className="xl:grid-cols-4">
          <button type="button" className="text-left" onClick={() => navigate('/finance/documents/quotes')}>
            <MetricCard
              label="Quotes"
              value={data.totalQuotations}
              meta="Quotations created in the workspace"
              emphasis="accent"
            />
          </button>
          <button type="button" className="text-left" onClick={() => navigate('/finance/documents/quotes')}>
            <MetricCard
              label="Quote Conversion"
              value={`${data.quoteConversionRate}%`}
              meta={`${data.convertedQuotations} of ${data.totalQuotations} quotes converted`}
            />
          </button>
          <button type="button" className="text-left" onClick={() => navigate('/finance/costing-rates/billable-work')}>
            <MetricCard
              label="Billable Work"
              value={data.billableWorkItemsCount}
              meta="Work items ready for billing"
            />
          </button>
          <button type="button" className="text-left" onClick={() => navigate('/finance/costing-rates/billable-work')}>
            <MetricCard
              label="Estimated Billable"
              value={`${c} ${data.estimatedBillableAmount.toLocaleString(undefined, { minimumFractionDigits: 0 })}`}
              meta="Current estimate across billable work"
              emphasis="accent"
            />
          </button>
        </MetricGrid>
      </SectionCard>

      <SectionCard title="Adjustments" description="Credit notes, debit notes, and other invoice corrections.">
        <EmptyState
          title="No adjustments yet"
          description="Credit notes and debit notes will appear here once the finance module has issued adjustments."
          action={
            <button type="button" className="button-secondary" onClick={() => navigate('/finance/documents')}>
              Open Documents
            </button>
          }
        />
      </SectionCard>

      <SectionCard title="Cost Exposure" description="Operational costs and the work driving billable exposure.">
        <MetricGrid className="xl:grid-cols-2">
          <button type="button" className="text-left" onClick={() => navigate('/finance/costing-rates/billable-work')}>
            <MetricCard
              label="Billable Exposure"
              value={`${c} ${data.estimatedBillableAmount.toLocaleString(undefined, { minimumFractionDigits: 0 })}`}
              meta="Estimated chargeable work in motion"
              emphasis="accent"
            />
          </button>
          <button type="button" className="text-left" onClick={() => navigate('/finance/costing-rates/material-costing')}>
            <MetricCard
              label="Material Costing"
              value="Pending"
              meta="Material issue costing is staged for future support"
            />
          </button>
        </MetricGrid>
      </SectionCard>

      <SectionCard title="Needs Attention" description="Late balances and finance follow-up items.">
        <MetricGrid className="xl:grid-cols-2">
          <button type="button" className="text-left" onClick={() => navigate('/finance/tax-receivables/aging')}>
            <MetricCard
              label="Outstanding Balance"
              value={`${c} ${data.outstandingBalance.toLocaleString(undefined, { minimumFractionDigits: 0 })}`}
              meta="Amounts still awaiting settlement"
              emphasis="warning"
            />
          </button>
          <button type="button" className="text-left" onClick={() => navigate('/finance/tax-receivables/aging')}>
            <MetricCard
              label="Overdue Balance"
              value={`${c} ${data.overdueBalance.toLocaleString(undefined, { minimumFractionDigits: 0 })}`}
              meta="Past due invoices needing follow-up"
              emphasis="danger"
            />
          </button>
        </MetricGrid>
      </SectionCard>
    </div>
  )
}
