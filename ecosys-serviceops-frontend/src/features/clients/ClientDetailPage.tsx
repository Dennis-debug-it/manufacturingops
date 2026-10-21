import { useMemo, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { Plus } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { EmptyState } from '../../shared/components/EmptyState'
import { Spinner } from '../../shared/components/Spinner'
import { DetailLayout } from '../../shared/components/DetailLayout'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { SummarySection } from '../../shared/pages/ModulePage'
import { usePermission } from '../../shared/hooks/usePermission'
import { useEntitlements } from '../../shared/hooks/useEntitlements'
import { formatDate } from '../../shared/utils/dates'
import { useWorkOrders } from '../work-orders/api'
import { useContracts } from '../contracts/api'
import { useClientSummary } from './api'
import { SiteFormDrawer } from './components/SiteFormDrawer'
import { AssetFormDrawer } from './components/AssetFormDrawer'
import { ClientPortalContactsTab } from './components/ClientPortalContactsTab'
import type { ClientSiteSummary } from './types'
import { RecordActivitiesPanel } from '../activities/RecordActivitiesPanel'

export function ClientDetailPage() {
  const { id } = useParams<{ id: string }>()
  const canEdit = usePermission('clients.edit')
  const { hasModule } = useEntitlements()
  const hasOperations = hasModule('operations')
  const { data, isLoading, isError } = useClientSummary(id)
  const { data: workOrders } = useWorkOrders(undefined, hasOperations)
  const { data: contracts } = useContracts(id ? { clientId: id } : undefined, hasOperations)
  const [showSiteForm, setShowSiteForm] = useState(false)
  const [showAssetForm, setShowAssetForm] = useState(false)

  const clientWorkOrders = useMemo(
    () => (workOrders ?? []).filter((wo) => wo.clientId === id),
    [workOrders, id],
  )

  if (isLoading) {
    return <Spinner label="Loading client" />
  }

  if (isError || !data || !id) {
    return <EmptyState title="Client unavailable" description="The client record could not be loaded." />
  }

  const { client, sites } = data

  const siteColumns: DataTableColumn<ClientSiteSummary & { id: string }>[] = [
    { key: 'siteName', header: 'Site', cell: (row) => <Link className="font-medium text-slate-900 underline decoration-slate-300 underline-offset-4" to={`/sites/${row.siteId}?clientId=${id}`}>{row.siteName}</Link> },
    { key: 'siteType', header: 'Type', cell: (row) => row.siteType ?? '—' },
    { key: 'location', header: 'Location', cell: (row) => row.location ?? '—' },
    { key: 'assetCount', header: 'Assets', cell: (row) => row.assetCount },
    { key: 'openWorkOrderCount', header: 'Open WOs', cell: (row) => row.openWorkOrderCount },
    { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.status} /> },
  ]

  const header = (
    <PageHeader
      title={client.clientName}
      subtitle={client.clientType ?? 'Client'}
      breadcrumbs={[{ label: 'Clients', to: '/clients' }, { label: client.clientName }]}
      secondaryActions={<StatusBadge status={client.isActive ? client.activeStatus : 'Inactive'} />}
    />
  )

  return (
    <>
      <DetailLayout
        header={header}
        tabs={[
          {
            id: 'overview',
            label: 'Overview',
            content: (
              <SummarySection title="Client Overview" description="Contact details and portfolio counts.">
                <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-3">
                  {[
                    ['Client Type', client.clientType ?? '—'],
                    ['Email', client.email ?? '—'],
                    ['Phone', client.phone ?? '—'],
                    ['Location', client.location ?? '—'],
                    ['Contact Person', client.contactPerson ?? '—'],
                    ['Contact Phone', client.contactPhone ?? '—'],
                    ['Sites', String(data.siteCount)],
                    ['Assets', String(data.assetCount)],
                    ['Open Work Orders', String(data.openWorkOrderCount)],
                    ['Upcoming PM', String(data.upcomingPmCount)],
                    ['Created', formatDate(client.createdAt)],
                  ].map(([label, value]) => (
                    <div key={label} className="rounded-xl border border-slate-200 px-4 py-3">
                      <div className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">{label}</div>
                      <div className="mt-2 text-sm text-slate-900">{value}</div>
                    </div>
                  ))}
                </div>
                {client.notes ? (
                  <div className="mt-4 rounded-xl border border-slate-200 px-4 py-3">
                    <div className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">Notes</div>
                    <p className="mt-2 text-sm leading-6 text-slate-700">{client.notes}</p>
                  </div>
                ) : null}
              </SummarySection>
            ),
          },
          {
            id: 'portal-access',
            label: 'Client Portal',
            content: <ClientPortalContactsTab clientId={id} />,
          },
          {
            id: 'sites',
            label: 'Sites',
            content: (
              <div className="space-y-4">
                {canEdit ? (
                  <div className="flex justify-end">
                    <button type="button" onClick={() => setShowSiteForm(true)} className="inline-flex h-11 items-center gap-2 rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)]">
                      <Plus className="h-4 w-4" />New Site
                    </button>
                  </div>
                ) : null}
                <DataTable columns={siteColumns} data={sites.map((site) => ({ id: site.siteId, ...site }))} emptyTitle="No sites yet" emptyDescription="Add the client's first site to begin tracking assets and work orders." />
              </div>
            ),
          },
          {
            id: 'assets',
            label: 'Assets',
            content: (
              <div className="space-y-4">
                {canEdit ? (
                  <div className="flex justify-end">
                    <button type="button" onClick={() => setShowAssetForm(true)} className="inline-flex h-11 items-center gap-2 rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)]">
                      <Plus className="h-4 w-4" />New Asset
                    </button>
                  </div>
                ) : null}
                <EmptyState title={`${data.assetCount} asset(s) on record`} description="Open the Assets module and filter by this client to view the full register, or add a new asset above." actionLabel="View in Assets" onAction={() => { window.location.href = `/assets?clientId=${id}` }} />
              </div>
            ),
          },
          {
            id: 'work-orders',
            label: 'Work Orders',
            content: (
              <DataTable
                columns={[
                  { key: 'workOrderNumber', header: 'WO No', cell: (row) => <Link className="font-mono text-slate-900 underline decoration-slate-300 underline-offset-4" to={`/work-orders/${row.id}`}>{row.workOrderNumber}</Link> },
                  { key: 'title', header: 'Title', cell: (row) => row.title },
                  { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.status} /> },
                  { key: 'priority', header: 'Priority', cell: (row) => <StatusBadge status={row.priority} /> },
                  { key: 'dueDate', header: 'Due Date', cell: (row) => (row.dueDate ? formatDate(row.dueDate) : '—') },
                ]}
                data={clientWorkOrders}
                emptyTitle="No work orders"
                emptyDescription="Work orders created for this client will appear here."
              />
            ),
          },
          {
            id: 'contracts',
            label: 'Contracts',
            content: (
              <DataTable
                columns={[
                  { key: 'contractNumber', header: 'Contract No', cell: (row) => <Link className="font-mono text-slate-900 underline decoration-slate-300 underline-offset-4" to={`/contracts/${row.id}`}>{row.contractNumber}</Link> },
                  { key: 'name', header: 'Name', cell: (row) => row.name },
                  { key: 'contractType', header: 'Type', cell: (row) => row.contractType },
                  { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.status} /> },
                  { key: 'endDate', header: 'Expires', cell: (row) => formatDate(row.endDate) },
                  { key: 'renewalRisk', header: 'Renewal Risk', cell: (row) => <StatusBadge status={row.renewalRisk} /> },
                ]}
                data={contracts ?? []}
                emptyTitle="No contracts"
                emptyDescription="Contracts covering this client will appear here once created in the Contracts module."
              />
            ),
          },
          {
            id: 'activities',
            label: 'Meetings & actions',
            content: <RecordActivitiesPanel entityType="Client" entityId={id} clientId={id} />,
          },
          {
            id: 'documents',
            label: 'Documents',
            content: <EmptyState title="No documents" description="Client documents and attachments will appear here." />,
          },
        ].filter((tab) => hasOperations || !['sites', 'assets', 'work-orders', 'contracts'].includes(tab.id))}
      />

      <SiteFormDrawer open={showSiteForm} clientId={id} onClose={() => setShowSiteForm(false)} />
      <AssetFormDrawer open={showAssetForm} onClose={() => setShowAssetForm(false)} defaultClientId={id} />
    </>
  )
}
