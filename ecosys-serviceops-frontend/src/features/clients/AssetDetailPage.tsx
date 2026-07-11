import { useMemo, useState } from 'react'
import { differenceInDays } from 'date-fns'
import { Link, useParams } from 'react-router-dom'
import { Pencil } from 'lucide-react'
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
import { useAsset } from './api'
import { AssetFormDrawer } from './components/AssetFormDrawer'

function warrantyBadge(expiry: string | null) {
  if (!expiry) {
    return <StatusBadge status="No Warranty Data" />
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

export function AssetDetailPage() {
  const { id } = useParams<{ id: string }>()
  const canEdit = usePermission('clients.edit')
  const { data: asset, isLoading, isError } = useAsset(id)
  const { data: workOrders } = useWorkOrders()
  const [showEditForm, setShowEditForm] = useState(false)

  const assetWorkOrders = useMemo(
    () => (workOrders ?? []).filter((wo) => wo.assetId === id),
    [workOrders, id],
  )

  if (isLoading) {
    return <Spinner label="Loading asset" />
  }

  if (isError || !asset || !id) {
    return <EmptyState title="Asset unavailable" description="The asset record could not be loaded." />
  }

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
          <StatusBadge status={asset.status ?? 'Active'} />
          {warrantyBadge(asset.warrantyExpiryDate)}
          {canEdit ? (
            <button type="button" onClick={() => setShowEditForm(true)} className="inline-flex h-9 items-center gap-1.5 rounded-lg border border-slate-200 bg-white px-3 text-sm font-medium text-slate-700 transition hover:bg-slate-50">
              <Pencil className="h-3.5 w-3.5" />Edit
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
            <SummarySection title="Specifications" description="Asset identity, location, and warranty details.">
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
                  ['Warranty Expiry', asset.warrantyExpiryDate ? formatDate(asset.warrantyExpiryDate) : '—'],
                  ['Last PM Date', asset.lastPmDate ? formatDate(asset.lastPmDate) : '—'],
                  ['Next PM Date', asset.nextPmDate ? formatDate(asset.nextPmDate) : '—'],
                ].map(([label, value]) => (
                  <div key={label} className="rounded-xl border border-slate-200 px-4 py-3">
                    <div className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">{label}</div>
                    <div className="mt-2 text-sm text-slate-900">{value}</div>
                  </div>
                ))}
              </div>
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
    </>
  )
}
