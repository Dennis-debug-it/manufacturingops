import { useMemo, useState } from 'react'
import { differenceInDays } from 'date-fns'
import { Link, useParams } from 'react-router-dom'
import toast from 'react-hot-toast'
import { Archive, Pencil, RotateCcw } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { EmptyState } from '../../shared/components/EmptyState'
import { Spinner } from '../../shared/components/Spinner'
import { DetailLayout } from '../../shared/components/DetailLayout'
import { DataTable } from '../../shared/components/DataTable'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { SummarySection } from '../../shared/pages/ModulePage'
import { usePermission } from '../../shared/hooks/usePermission'
import { formatDate } from '../../shared/utils/dates'
import { useWorkOrders } from '../work-orders/api'
import { useAsset, useReactivateAsset, useRetireAsset, type RetireAssetPayload } from './api'
import { AssetFormDrawer } from './components/AssetFormDrawer'
import { RetireAssetDialog } from './components/RetireAssetDialog'
import type { Asset } from './types'

function warrantyBadge(expiry: string | null) {
  if (!expiry) {
    return <StatusBadge status="No Warranty" />
  }
  const days = differenceInDays(new Date(expiry), new Date())
  if (days < 0) {
    return <StatusBadge status="Warranty Expired" />
  }
  if (days <= 30) {
    return <StatusBadge status="Warranty Expiring Soon" />
  }
  return <StatusBadge status="Under Warranty" />
}

function maintenanceFrequencyLabel(asset: Asset) {
  if (asset.pmFrequencyValue && asset.pmFrequencyUnit) {
    const unit = asset.pmFrequencyValue === 1 ? asset.pmFrequencyUnit.replace(/s$/, '') : asset.pmFrequencyUnit
    return `Every ${asset.pmFrequencyValue} ${unit}`
  }
  return asset.recommendedPmFrequency ?? '—'
}

function nextPmLabel(nextPmDate: string | null) {
  if (!nextPmDate) return '—'
  const days = differenceInDays(new Date(nextPmDate), new Date())
  const suffix = days < 0 ? ` (overdue ${Math.abs(days)}d)` : days === 0 ? ' (due today)' : ` (in ${days}d)`
  return `${formatDate(nextPmDate)}${suffix}`
}

const CLOSED_WORK_ORDER_STATUS = /complete|accepted|closed|cancel|reject/i

export function AssetDetailPage() {
  const { id } = useParams<{ id: string }>()
  const canEdit = usePermission('clients.edit')
  const canManageAssets = usePermission('assets.manage')
  const { data: asset, isLoading, isError } = useAsset(id)
  const { data: workOrders } = useWorkOrders()
  const [showEditForm, setShowEditForm] = useState(false)
  const [showRetire, setShowRetire] = useState(false)
  const retireAsset = useRetireAsset(id ?? '')
  const reactivateAsset = useReactivateAsset(id ?? '')

  const assetWorkOrders = useMemo(
    () => (workOrders ?? []).filter((wo) => wo.assetId === id),
    [workOrders, id],
  )
  const openWorkOrderCount = assetWorkOrders.filter((wo) => !CLOSED_WORK_ORDER_STATUS.test(wo.status ?? '')).length

  const retire = async (payload: RetireAssetPayload) => {
    try {
      await retireAsset.mutateAsync(payload)
      toast.success('Asset retired')
      setShowRetire(false)
    } catch {
      // The API client already shows the error toast; keep the dialog open to retry.
    }
  }

  const reactivate = async () => {
    try {
      await reactivateAsset.mutateAsync()
      toast.success('Asset reactivated')
    } catch {
      // Error toast shown by the API client.
    }
  }

  if (isLoading) {
    return <Spinner label="Loading asset" />
  }

  if (isError || !asset || !id) {
    return <EmptyState title="Asset unavailable" description="The asset record could not be loaded." />
  }

  const isRetired = asset.status === 'Inactive'

  const header = (
    <PageHeader
      title={asset.assetName}
      subtitle={asset.assetCode}
      breadcrumbs={[
        { label: 'Assets', to: '/assets' },
        { label: asset.assetName },
      ]}
      secondaryActions={
        <div className="flex items-center gap-2">
          <StatusBadge status={isRetired ? 'Retired' : asset.status ?? 'Active'} />
          {isRetired ? null : asset.supportStatus === 'Under Contract' || asset.supportStatus === 'Out of Support'
            ? <StatusBadge status={asset.supportStatus} />
            : warrantyBadge(asset.warrantyExpiryDate)}
          {canEdit && !isRetired ? (
            <button type="button" onClick={() => setShowEditForm(true)} className="inline-flex h-9 items-center gap-1.5 rounded-lg border border-slate-200 bg-white px-3 text-sm font-medium text-slate-700 transition hover:bg-slate-50">
              <Pencil className="h-3.5 w-3.5" />Edit
            </button>
          ) : null}
          {canManageAssets && !isRetired ? (
            <button type="button" onClick={() => setShowRetire(true)} className="inline-flex h-9 items-center gap-1.5 rounded-lg border border-rose-200 bg-white px-3 text-sm font-medium text-rose-700 transition hover:bg-rose-50">
              <Archive className="h-3.5 w-3.5" />Retire
            </button>
          ) : null}
          {canManageAssets && isRetired ? (
            <button type="button" disabled={reactivateAsset.isPending} onClick={() => void reactivate()} className="inline-flex h-9 items-center gap-1.5 rounded-lg border border-slate-200 bg-white px-3 text-sm font-medium text-slate-700 transition hover:bg-slate-50 disabled:opacity-60">
              <RotateCcw className="h-3.5 w-3.5" />{reactivateAsset.isPending ? 'Reactivating…' : 'Reactivate'}
            </button>
          ) : null}
        </div>
      }
    />
  )

  return (
    <>
    <DetailLayout
      header={header}
      tabs={[
        {
          id: 'specifications',
          label: 'Specifications',
          content: (
            <SummarySection title="Specifications" description="Asset identity, location, maintenance, and warranty details.">
              {isRetired ? (
                <div className="mb-4 rounded-xl border border-slate-300 bg-slate-50 px-4 py-3 text-sm text-slate-700">
                  <span className="font-semibold text-slate-900">Retired</span>
                  {asset.decommissionDate ? <> on {formatDate(asset.decommissionDate)}</> : null}
                  {asset.decommissionReason ? <> — {asset.decommissionReason}</> : null}.
                  {' '}It is hidden from the active register and has no automatic PM scheduling.
                </div>
              ) : null}
              <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-3">
                {[
                  ['Client', asset.clientName ?? '—'],
                  ['Site', asset.siteName ?? '—'],
                  ['Category', asset.assetCategoryName ?? '—'],
                  ['Type', asset.assetType ?? '—'],
                  ['Location', asset.location ?? '—'],
                  ['Serial Number', asset.serialNumber ?? '—'],
                  ['Manufacturer', asset.manufacturer ?? '—'],
                  ['Model', asset.model ?? '—'],
                  ['Installation Date', asset.installationDate ? formatDate(asset.installationDate) : '—'],
                ].map(([label, value]) => (
                  <div key={label} className="rounded-xl border border-slate-200 px-4 py-3">
                    <div className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">{label}</div>
                    <div className="mt-2 text-sm text-slate-900">{value}</div>
                  </div>
                ))}
              </div>
              <h3 className="mt-6 text-sm font-semibold text-slate-900">Maintenance</h3>
              <div className="mt-3 grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-4">
                {[
                  ['Maintenance Frequency', maintenanceFrequencyLabel(asset)],
                  ['Last Maintenance', asset.lastPmDate ? formatDate(asset.lastPmDate) : 'Not recorded'],
                  ['Next Maintenance Due', nextPmLabel(asset.nextPmDate)],
                  ['Auto-Schedule PM', asset.autoSchedulePm ? 'On' : 'Off'],
                ].map(([label, value]) => (
                  <div key={label} className="rounded-xl border border-slate-200 px-4 py-3">
                    <div className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">{label}</div>
                    <div className="mt-2 text-sm text-slate-900">{value}</div>
                  </div>
                ))}
              </div>
              <h3 className="mt-6 text-sm font-semibold text-slate-900">Warranty</h3>
              <div className="mt-3 grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-5">
                {[
                  ['Under Warranty', asset.warrantyExpiryDate ? (asset.isUnderWarranty ? 'Yes' : 'No — expired') : 'No'],
                  ['Contract Cover', asset.hasContractCover ? 'Yes' : 'No'],
                  ['Warranty Period', asset.warrantyPeriodMonths ? `${asset.warrantyPeriodMonths} month${asset.warrantyPeriodMonths === 1 ? '' : 's'}` : '—'],
                  ['Warranty Start', asset.warrantyStartDate ? formatDate(asset.warrantyStartDate) : '—'],
                  ['Warranty Expiry', asset.warrantyExpiryDate ? formatDate(asset.warrantyExpiryDate) : '—'],
                ].map(([label, value]) => (
                  <div key={label} className="rounded-xl border border-slate-200 px-4 py-3">
                    <div className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">{label}</div>
                    <div className="mt-2 text-sm text-slate-900">{value}</div>
                  </div>
                ))}
              </div>
              {(asset.customFieldValues ?? []).length > 0 ? (
                <div className="mt-4" data-testid="asset-datasheet">
                  <div className="mb-2 text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">{asset.assetCategoryName ?? 'Category'} datasheet</div>
                  <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-3">
                    {(asset.customFieldValues ?? []).map((entry) => (
                      <div key={entry.fieldDefinitionId} className="rounded-xl border border-slate-200 px-4 py-3">
                        <div className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">{entry.fieldLabel ?? 'Field'}</div>
                        <div className="mt-2 text-sm text-slate-900">
                          {entry.fieldType?.toLowerCase() === 'date' && entry.value ? formatDate(entry.value) : entry.value}
                          {entry.unit ? ` ${entry.unit}` : ''}
                        </div>
                      </div>
                    ))}
                  </div>
                </div>
              ) : null}
              {asset.notes ? (
                <div className="mt-4 rounded-xl border border-slate-200 px-4 py-3">
                  <div className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">Notes</div>
                  <p className="mt-2 text-sm leading-6 text-slate-700">{asset.notes}</p>
                </div>
              ) : null}
            </SummarySection>
          ),
        },
        {
          id: 'history',
          label: 'History',
          content: <EmptyState title="Maintenance history pending" description="Full PM and service history for this asset will appear here once the Maintenance module is wired." />,
        },
        {
          id: 'open-work-orders',
          label: 'Open Work Orders',
          content: (
            <DataTable
              columns={[
                { key: 'woNo', header: 'WO No', cell: (row) => <Link className="font-mono text-slate-900 underline decoration-slate-300 underline-offset-4" to={`/work-orders/${row.id}`}>{row.woNo}</Link> },
                { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.status} /> },
                { key: 'priority', header: 'Priority', cell: (row) => <StatusBadge status={row.priority} /> },
                { key: 'dueDate', header: 'Due Date', cell: (row) => row.dueDate ?? '—' },
              ]}
              data={assetWorkOrders.map((row) => ({
                id: row.id,
                woNo: row.workOrderNumber,
                status: row.status,
                priority: row.priority,
                dueDate: row.dueDate ? formatDate(row.dueDate) : null,
              }))}
              emptyTitle="No work orders"
              emptyDescription="Work orders raised against this asset will appear here."
            />
          ),
        },
        {
          id: 'pm-schedules',
          label: 'PM Schedules',
          content: <EmptyState title="Maintenance module pending" description="PM schedules for this asset will appear here once the Maintenance module is wired." />,
        },
        {
          id: 'documents',
          label: 'Documents',
          content: <EmptyState title="No documents" description="Asset documents and attachments will appear here." />,
        },
      ]}
    />
    <AssetFormDrawer open={showEditForm} onClose={() => setShowEditForm(false)} asset={asset} />
    <RetireAssetDialog
      open={showRetire}
      count={1}
      assetName={asset.assetName}
      openWorkOrders={openWorkOrderCount}
      defaultReason={asset.supportStatus === 'Out of Support' ? 'No longer under support' : ''}
      submitting={retireAsset.isPending}
      onCancel={() => setShowRetire(false)}
      onConfirm={(payload) => void retire(payload)}
    />
    </>
  )
}
