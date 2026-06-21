import { FileText, Plus } from 'lucide-react'
import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { PageHeader } from '../../components/ui/PageHeader'
import { DataTableWrapper, SectionPanel } from '../../components/ui/Workspace'
import { ErrorState } from '../../components/ui/ErrorState'
import { LoadingState } from '../../components/ui/LoadingState'
import { useAsyncData } from '../../hooks/useAsyncData'
import { contractService } from '../../services/contractService'
import type { ContractListItem } from '../../types/api'
import { formatDateOnly } from '../../utils/date'
import { contractRenewalRiskOptions, contractStatusOptions, formatContractLabel, renderBillingModelBadge, renderContractStatusBadge, renderRenewalRiskBadge } from './contractUi'

export function ContractsPage() {
  const [statusFilter, setStatusFilter] = useState('')
  const [riskFilter, setRiskFilter] = useState('')
  const [expiringWithinDays, setExpiringWithinDays] = useState('')

  const { data, loading, error, reload } = useAsyncData<ContractListItem[]>(
    (signal) => contractService.list({
      status: statusFilter || null,
      renewalRisk: riskFilter || null,
      expiringWithinDays: expiringWithinDays ? Number(expiringWithinDays) : null,
    }, signal),
    [],
    [statusFilter, riskFilter, expiringWithinDays],
  )

  const metrics = useMemo(() => ({
    active: data.filter((item) => item.status === 'Active').length,
    expiringSoon: data.filter((item) => item.daysToExpiry <= 30).length,
    highRisk: data.filter((item) => item.renewalRisk === 'High' || item.renewalRisk === 'Critical').length,
    expired: data.filter((item) => item.status === 'Expired' || item.daysToExpiry < 0).length,
  }), [data])

  return (
    <div className="space-y-4">
      <PageHeader
        eyebrow="Contracts"
        title="Contract management"
        description="Operational contracts that drive coverage, SLA, renewal risk, and preferential billing."
        actions={(
          <div className="flex flex-wrap gap-2">
            <button type="button" className="button-secondary" onClick={() => void reload()}>Refresh</button>
            <Link to="/contracts/new" className="button-primary">
              <Plus className="h-4 w-4" />
              New contract
            </Link>
          </div>
        )}
      />

      <SectionPanel title="Contract risk" description="Keep visibility on renewal exposure without leaving the list view.">
        <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
          <Metric label="Active" value={metrics.active} />
          <Metric label="Expiring in 30 days" value={metrics.expiringSoon} tone="warning" />
          <Metric label="High / critical risk" value={metrics.highRisk} tone="danger" />
          <Metric label="Expired" value={metrics.expired} tone="danger" />
        </div>
      </SectionPanel>

      <SectionPanel title="Contracts" description="Use filters to focus on expiring contracts, owners, and coverage exposure.">
        <div className="mb-4 grid gap-3 md:grid-cols-3 xl:grid-cols-4">
          <label className="space-y-2">
            <span className="text-sm font-medium text-app">Status</span>
            <select value={statusFilter} onChange={(event) => setStatusFilter(event.target.value)} className="field-input">
              <option value="">All statuses</option>
              {contractStatusOptions.map((item) => <option key={item} value={item}>{formatContractLabel(item)}</option>)}
            </select>
          </label>
          <label className="space-y-2">
            <span className="text-sm font-medium text-app">Renewal risk</span>
            <select value={riskFilter} onChange={(event) => setRiskFilter(event.target.value)} className="field-input">
              <option value="">All risks</option>
              {contractRenewalRiskOptions.map((item) => <option key={item} value={item}>{item}</option>)}
            </select>
          </label>
          <label className="space-y-2">
            <span className="text-sm font-medium text-app">Expiring within</span>
            <select value={expiringWithinDays} onChange={(event) => setExpiringWithinDays(event.target.value)} className="field-input">
              <option value="">Any time</option>
              <option value="90">90 days</option>
              <option value="60">60 days</option>
              <option value="30">30 days</option>
            </select>
          </label>
          <div className="flex items-end">
            <button type="button" className="button-secondary" onClick={() => { setStatusFilter(''); setRiskFilter(''); setExpiringWithinDays('') }}>
              Clear filters
            </button>
          </div>
        </div>

        {loading ? <LoadingState label="Loading contracts" /> : null}
        {!loading && error ? <ErrorState title="Unable to load contracts" description={error} /> : null}
        {!loading && !error ? (
          <DataTableWrapper>
            <table className="w-full min-w-[1100px]">
              <thead className="table-head">
                <tr>
                  {['Contract No', 'Client', 'Type', 'Status', 'Start Date', 'End Date', 'Days to Expiry', 'Renewal Risk', 'Billing Model', 'Owner', 'Actions'].map((header) => (
                    <th key={header} className="px-4 py-3 text-left text-[11px] font-semibold uppercase tracking-[0.16em] text-muted">{header}</th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {data.map((item) => (
                  <tr key={item.id} className="table-row border-t border-app">
                    <td className="px-4 py-3">
                      <div>
                        <Link to={`/contracts/${item.id}`} className="font-semibold text-accent-strong hover:underline">{item.contractNumber}</Link>
                        <p className="mt-1 text-xs text-muted">{item.name}</p>
                      </div>
                    </td>
                    <td className="px-4 py-3 text-sm text-app">{item.clientName || '-'}</td>
                    <td className="px-4 py-3 text-sm text-secondary">{formatContractLabel(item.contractType)}</td>
                    <td className="px-4 py-3">{renderContractStatusBadge(item.status)}</td>
                    <td className="px-4 py-3 text-sm text-secondary">{formatDateOnly(item.startDate)}</td>
                    <td className="px-4 py-3 text-sm text-secondary">{formatDateOnly(item.endDate)}</td>
                    <td className="px-4 py-3 text-sm text-app">{item.daysToExpiry}</td>
                    <td className="px-4 py-3">{renderRenewalRiskBadge(item.renewalRisk)}</td>
                    <td className="px-4 py-3">{renderBillingModelBadge(item.billingModel)}</td>
                    <td className="px-4 py-3 text-sm text-secondary">{item.ownerName || '-'}</td>
                    <td className="px-4 py-3">
                      <Link to={`/contracts/${item.id}`} className="button-secondary px-3 py-1.5 text-xs">Open</Link>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
            {data.length === 0 ? (
              <div className="flex flex-col items-center justify-center gap-2 px-6 py-12 text-center">
                <FileText className="h-8 w-8 text-muted" />
                <p className="font-medium text-app">No contracts match the current filters.</p>
                <p className="text-sm text-muted">Create a new contract or clear the filters to widen the view.</p>
              </div>
            ) : null}
          </DataTableWrapper>
        ) : null}
      </SectionPanel>
    </div>
  )
}

function Metric({ label, value, tone = 'default' }: { label: string; value: number; tone?: 'default' | 'warning' | 'danger' }) {
  const toneClass = tone === 'danger'
    ? 'border-[color:color-mix(in_srgb,var(--app-danger)_20%,white)] bg-[color:color-mix(in_srgb,var(--app-danger)_8%,white)]'
    : tone === 'warning'
      ? 'border-[color:color-mix(in_srgb,var(--app-warning)_20%,white)] bg-[color:color-mix(in_srgb,var(--app-warning)_8%,white)]'
      : 'border-app bg-[var(--app-subtle)]'

  return (
    <div className={`rounded-[20px] border px-4 py-3 ${toneClass}`}>
      <p className="text-[11px] font-semibold uppercase tracking-[0.16em] text-muted">{label}</p>
      <p className="mt-2 text-2xl font-semibold text-app">{value}</p>
    </div>
  )
}
