import { useState } from 'react'
import toast from 'react-hot-toast'
import { useParams } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { api } from '../../shared/api/apiClient'
import { endpoints } from '../../shared/api/endpoints'
import { PageHeader } from '../../shared/components/PageHeader'
import { EmptyState } from '../../shared/components/EmptyState'
import { Spinner } from '../../shared/components/Spinner'
import { DetailLayout } from '../../shared/components/DetailLayout'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { ConfirmDialog } from '../../shared/components/ConfirmDialog'
import { SummarySection } from '../../shared/pages/ModulePage'
import { usePermission } from '../../shared/hooks/usePermission'
import { formatDate, formatDateTime } from '../../shared/utils/dates'
import { formatCurrency } from '../../shared/utils/format'
import {
  useActivateContract,
  useApproveContract,
  useCancelContract,
  useContract,
  useResumeContract,
  useStartContractRenewal,
  useSubmitContractApproval,
  useSuspendContract,
  useTerminateContract,
} from './api'
import type { ContractLifecycleEvent } from './types'

interface PreferentialRate {
  id: string
  clientId: string
  rateName: string
  itemType: string
  unit: string
  defaultRate: number
  preferentialRate: number
  status: string
  effectiveFrom: string
  effectiveTo: string | null
}

function usePreferentialRatesForClient(clientId: string | undefined) {
  return useQuery({
    queryKey: ['finance', 'preferential-rates', clientId],
    queryFn: () => api.get<PreferentialRate[]>(endpoints.finance.preferentialRates),
    enabled: Boolean(clientId),
    select: (data) => data.filter((rate) => rate.clientId === clientId),
  })
}

const lifecycleActions = [
  { key: 'submit-approval', label: 'Submit for Approval', from: ['Draft'] },
  { key: 'approve', label: 'Approve', from: ['AwaitingApproval'] },
  { key: 'activate', label: 'Activate', from: ['Approved'] },
  { key: 'start-renewal', label: 'Start Renewal', from: ['Active'] },
  { key: 'suspend', label: 'Suspend', from: ['Active'] },
  { key: 'resume', label: 'Resume', from: ['Suspended'] },
  { key: 'terminate', label: 'Terminate', from: ['Active', 'Suspended'] },
  { key: 'cancel', label: 'Cancel', from: ['Draft', 'AwaitingApproval', 'Approved'] },
] as const

export function ContractDetailPage() {
  const { id } = useParams<{ id: string }>()
  const canApprove = usePermission('contracts.approve')
  const { data: contract, isLoading, isError } = useContract(id)
  const { data: preferentialRates } = usePreferentialRatesForClient(contract?.clientId)

  const submitApproval = useSubmitContractApproval(id ?? '')
  const approve = useApproveContract(id ?? '')
  const activate = useActivateContract(id ?? '')
  const startRenewal = useStartContractRenewal(id ?? '')
  const suspend = useSuspendContract(id ?? '')
  const resume = useResumeContract(id ?? '')
  const terminate = useTerminateContract(id ?? '')
  const cancel = useCancelContract(id ?? '')

  const [pendingAction, setPendingAction] = useState<(typeof lifecycleActions)[number] | null>(null)

  const mutationByKey: Record<string, ReturnType<typeof useSubmitContractApproval>> = {
    'submit-approval': submitApproval,
    approve,
    activate,
    'start-renewal': startRenewal,
    suspend,
    resume,
    terminate,
    cancel,
  }

  const runAction = async () => {
    if (!pendingAction) return
    try {
      await mutationByKey[pendingAction.key]?.mutateAsync(undefined)
      toast.success(`Contract ${pendingAction.label.toLowerCase()}d`)
    } catch {
      // interceptor surfaces the error toast
    } finally {
      setPendingAction(null)
    }
  }

  if (isLoading) {
    return <Spinner label="Loading contract" />
  }

  if (isError || !contract || !id) {
    return <EmptyState title="Contract unavailable" description="This contract could not be loaded." />
  }

  const availableActions = lifecycleActions.filter((action) => action.from.includes(contract.status as never))

  const header = (
    <PageHeader
      title={contract.contractNumber}
      subtitle={contract.name}
      breadcrumbs={[{ label: 'Contracts', to: '/contracts' }, { label: contract.contractNumber }]}
      secondaryActions={<StatusBadge status={contract.status} />}
      primaryAction={
        canApprove && availableActions.length > 0 ? (
          <div className="flex flex-wrap gap-2">
            {availableActions.map((action) => (
              <button
                key={action.key}
                type="button"
                onClick={() => setPendingAction(action)}
                className="inline-flex h-11 items-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50"
              >
                {action.label}
              </button>
            ))}
          </div>
        ) : undefined
      }
    />
  )

  const lifecycleColumns: DataTableColumn<ContractLifecycleEvent>[] = [
    { key: 'eventType', header: 'Event', cell: (row) => row.eventType },
    { key: 'transition', header: 'Transition', cell: (row) => `${row.oldStatus ?? '—'} → ${row.newStatus ?? '—'}` },
    { key: 'note', header: 'Note', cell: (row) => row.note ?? row.reason ?? '—' },
    { key: 'createdByUserName', header: 'By', cell: (row) => row.createdByUserName ?? 'System' },
    { key: 'createdAt', header: 'Date', cell: (row) => formatDateTime(row.createdAt) },
  ]

  return (
    <>
      <DetailLayout
        header={header}
        tabs={[
          {
            id: 'overview',
            label: 'Overview',
            content: (
              <div className="space-y-6">
                <SummarySection title="Contract Overview" description="Core terms and ownership.">
                  <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-3">
                    {[
                      ['Client', contract.clientName ?? '—'],
                      ['Type', contract.contractType],
                      ['Billing Model', contract.billingModel],
                      ['Contract Value', contract.contractValue != null ? formatCurrency(contract.contractValue, contract.currency) : '—'],
                      ['Start Date', formatDate(contract.startDate)],
                      ['End Date', formatDate(contract.endDate)],
                      ['Renewal Notice', `${contract.renewalNoticeDays} days`],
                      ['Renewal Risk', contract.renewalRisk],
                      ['Account Manager', contract.accountManagerName ?? '—'],
                    ].map(([label, value]) => (
                      <div key={label} className="rounded-xl border border-slate-200 px-4 py-3">
                        <div className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">{label}</div>
                        <div className="mt-2 text-sm text-slate-900">{value}</div>
                      </div>
                    ))}
                  </div>
                  {contract.description ? <p className="mt-4 text-sm leading-6 text-slate-700">{contract.description}</p> : null}
                </SummarySection>
                <SummarySection title="Lifecycle History" description="Status transitions for this contract.">
                  <DataTable columns={lifecycleColumns} data={contract.lifecycleEvents} emptyTitle="No lifecycle events" emptyDescription="Status changes will appear here." />
                </SummarySection>
              </div>
            ),
          },
          {
            id: 'coverage',
            label: 'Covered Sites/Assets',
            content: (
              <div className="grid grid-cols-1 gap-6 xl:grid-cols-2">
                <DataTable
                  columns={[{ key: 'siteName', header: 'Site', cell: (row) => row.siteName ?? row.siteCode ?? '—' }]}
                  data={contract.sites.map((site) => ({ ...site, id: site.siteId }))}
                  emptyTitle="No covered sites"
                  emptyDescription="Sites covered by this contract will appear here."
                />
                <DataTable
                  columns={[{ key: 'assetName', header: 'Asset', cell: (row) => row.assetName ?? row.assetCode ?? '—' }]}
                  data={contract.assets.map((asset) => ({ ...asset, id: asset.assetId }))}
                  emptyTitle="No covered assets"
                  emptyDescription="Assets covered by this contract will appear here."
                />
              </div>
            ),
          },
          {
            id: 'sla',
            label: 'SLA Terms',
            content: (
              <SummarySection title="SLA Terms" description="Response and resolution commitments.">
                <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-3">
                  {[
                    ['Response Time', contract.responseTimeHours != null ? `${contract.responseTimeHours}h` : '—'],
                    ['Resolution Time', contract.resolutionTimeHours != null ? `${contract.resolutionTimeHours}h` : '—'],
                    ['Working Hours Policy', contract.workingHoursPolicy ?? '—'],
                    ['Escalation Email', contract.escalationEmail ?? '—'],
                    ['Priority Level', contract.priorityLevel ?? '—'],
                    ['Pause SLA on Hold', contract.pauseSlaOnHold ? 'Yes' : 'No'],
                  ].map(([label, value]) => (
                    <div key={label} className="rounded-xl border border-slate-200 px-4 py-3">
                      <div className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">{label}</div>
                      <div className="mt-2 text-sm text-slate-900">{value}</div>
                    </div>
                  ))}
                </div>
                {contract.slaMatrix.length > 0 ? (
                  <div className="mt-4">
                    <DataTable
                      columns={[
                        { key: 'priority', header: 'Priority', cell: (row) => <StatusBadge status={row.priority} /> },
                        { key: 'responseTimeHours', header: 'Response (hrs)', cell: (row) => row.responseTimeHours ?? '—' },
                        { key: 'resolutionTimeHours', header: 'Resolution (hrs)', cell: (row) => row.resolutionTimeHours ?? '—' },
                      ]}
                      data={contract.slaMatrix.map((row) => ({ ...row, id: row.priority }))}
                      emptyTitle="No SLA matrix"
                      emptyDescription="Priority-specific SLA rows will appear here."
                    />
                  </div>
                ) : null}
              </SummarySection>
            ),
          },
          {
            id: 'preferential-rates',
            label: 'Preferential Rates',
            content: (
              <DataTable
                columns={[
                  { key: 'rateName', header: 'Rate', cell: (row) => row.rateName },
                  { key: 'itemType', header: 'Item Type', cell: (row) => row.itemType },
                  { key: 'defaultRate', header: 'Default', cell: (row) => formatCurrency(row.defaultRate) },
                  { key: 'preferentialRate', header: 'Preferential', cell: (row) => formatCurrency(row.preferentialRate) },
                  { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.status} /> },
                ]}
                data={preferentialRates ?? []}
                emptyTitle="No preferential rates"
                emptyDescription="Client-specific rate overrides for this contract's client will appear here (managed under Finance → Preferential Rates)."
              />
            ),
          },
          {
            id: 'pm-terms',
            label: 'PM Terms',
            content: (
              <div className="space-y-4">
                <div className="rounded-2xl border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-900">
                  PM entitlements define visit allowances per period — they don't automatically create PM schedules. Use the
                  Maintenance module to attach a matching PM schedule for assets covered by this contract.
                </div>
                <DataTable
                  columns={[
                    { key: 'assetName', header: 'Asset / Category', cell: (row) => row.assetName ?? row.assetCategory ?? 'All Assets' },
                    { key: 'workOrderType', header: 'Work Type', cell: (row) => row.workOrderType },
                    { key: 'periodType', header: 'Period', cell: (row) => row.periodType },
                    { key: 'includedVisits', header: 'Included', cell: (row) => row.includedVisits },
                    { key: 'usedVisits', header: 'Used', cell: (row) => row.usedVisits },
                    { key: 'remainingVisits', header: 'Remaining', cell: (row) => row.remainingVisits },
                  ]}
                  data={contract.pmEntitlements}
                  emptyTitle="No PM entitlements"
                  emptyDescription="Preventive maintenance visit entitlements for this contract will appear here."
                />
              </div>
            ),
          },
          {
            id: 'billing',
            label: 'Billing',
            content: (
              <div className="space-y-4">
                {contract.rateCards.length === 0 ? (
                  <EmptyState title="No rate cards" description="Auto-generation billing rules (rate cards) for this contract will appear here." />
                ) : (
                  contract.rateCards.map((card) => (
                    <SummarySection key={card.id} title={card.name} {...(card.isDefault ? { description: 'Default rate card' } : {})}>
                      <DataTable
                        columns={[
                          { key: 'billingModel', header: 'Model', cell: (row) => row.billingModel },
                          { key: 'workOrderTypeName', header: 'Work Type', cell: (row) => row.workOrderTypeName ?? 'Any' },
                          { key: 'labourHourlyRate', header: 'Labour Rate', cell: (row) => (row.labourHourlyRate != null ? formatCurrency(row.labourHourlyRate) : '—') },
                          { key: 'calloutFee', header: 'Callout Fee', cell: (row) => (row.calloutFee != null ? formatCurrency(row.calloutFee) : '—') },
                        ]}
                        data={card.rules}
                        emptyTitle="No rate rules"
                        emptyDescription="Pricing rules for this rate card will appear here."
                      />
                    </SummarySection>
                  ))
                )}
              </div>
            ),
          },
          {
            id: 'pnl',
            label: 'P&L',
            content: (
              <div className="space-y-4">
                <div className="rounded-2xl border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-900">
                  There is no dedicated contract P&amp;L endpoint. This is a client-side estimate composed from the work orders
                  linked to this contract's estimated charges — not a backend-authoritative financial figure.
                </div>
                <DataTable
                  columns={[
                    { key: 'workOrderNumber', header: 'WO No', cell: (row) => row.workOrderNumber },
                    { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.status} /> },
                    { key: 'billingModel', header: 'Billing Model', cell: (row) => row.billingModel ?? '—' },
                    { key: 'estimatedCharge', header: 'Estimated Charge', cell: (row) => (row.estimatedCharge != null ? formatCurrency(row.estimatedCharge) : '—') },
                  ]}
                  data={contract.workOrders}
                  emptyTitle="No linked work orders"
                  emptyDescription="Work orders billed under this contract will appear here."
                />
                <div className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 text-sm text-slate-700 shadow-sm">
                  Estimated total: <span className="font-semibold text-slate-900">{formatCurrency(contract.workOrders.reduce((sum, wo) => sum + (wo.estimatedCharge ?? 0), 0), contract.currency)}</span>
                </div>
              </div>
            ),
          },
        ]}
      />

      <ConfirmDialog
        open={pendingAction !== null}
        title={pendingAction?.label ?? ''}
        message={`This will ${pendingAction?.label.toLowerCase()} contract ${contract.contractNumber}.`}
        {...(pendingAction?.key === 'terminate' ? { confirmText: contract.contractNumber } : {})}
        danger={pendingAction?.key === 'terminate' || pendingAction?.key === 'cancel'}
        onCancel={() => setPendingAction(null)}
        onConfirm={runAction}
      />
    </>
  )
}
