import { useNavigate } from 'react-router-dom'
import { ErrorState } from '../../components/ui/ErrorState'
import { LoadingState } from '../../components/ui/LoadingState'
import { MetricCard, MetricGrid } from '../../components/ui/Workspace'
import { useAsyncData } from '../../hooks/useAsyncData'
import { financeReportsService } from '../../services/financeReportsService'
import type { FinanceDashboard } from '../../types/api'
import { TenantFinanceOverviewSection } from './TenantFinanceShared'

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

export function TenantFinanceOverviewPage() {
  const navigate = useNavigate()
  const { data, loading, error } = useAsyncData<FinanceDashboard>(
    (signal) => financeReportsService.getDashboard(signal),
    emptyDash,
    [],
  )

  const c = data.currency

  return (
    <div className="space-y-6">
      {loading ? <LoadingState label="Loading dashboard" /> : null}
      {!loading && error ? <ErrorState title="Unable to load dashboard" description={error} /> : null}
      {!loading && !error ? (
        <MetricGrid className="xl:grid-cols-5">
          <button type="button" className="text-left" onClick={() => navigate('/finance/invoices?status=All')}>
            <MetricCard
              label="Invoiced YTD"
              value={`${c} ${data.totalInvoicedYtd.toLocaleString(undefined, { minimumFractionDigits: 0 })}`}
              meta="Total invoiced this year"
              emphasis="accent"
            />
          </button>
          <button type="button" className="text-left" onClick={() => navigate('/finance/invoices?status=Paid')}>
            <MetricCard
              label="Collected YTD"
              value={`${c} ${data.totalCollectedYtd.toLocaleString(undefined, { minimumFractionDigits: 0 })}`}
              meta="Payments received this year"
              emphasis="accent"
            />
          </button>
          <button type="button" className="text-left" onClick={() => navigate('/finance/invoices?status=Sent')}>
            <MetricCard
              label="Outstanding"
              value={`${c} ${data.outstandingBalance.toLocaleString(undefined, { minimumFractionDigits: 0 })}`}
              meta="Unpaid invoice balance"
            />
          </button>
          <button type="button" className="text-left" onClick={() => navigate('/finance/reports')}>
            <MetricCard
              label="Overdue"
              value={`${c} ${data.overdueBalance.toLocaleString(undefined, { minimumFractionDigits: 0 })}`}
              meta="Past due date, unpaid"
              emphasis="warning"
            />
          </button>
          <button type="button" className="text-left" onClick={() => navigate('/finance/quotes')}>
            <MetricCard
              label="Quote Conversion"
              value={`${data.quoteConversionRate}%`}
              meta={`${data.convertedQuotations} of ${data.totalQuotations} quotes converted`}
            />
          </button>
        </MetricGrid>
      ) : null}

      <TenantFinanceOverviewSection />
    </div>
  )
}
