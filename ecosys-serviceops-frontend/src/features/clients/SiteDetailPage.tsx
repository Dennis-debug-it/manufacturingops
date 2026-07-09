import { Link, useParams, useSearchParams } from 'react-router-dom'
import { PageHeader } from '../../shared/components/PageHeader'
import { EmptyState } from '../../shared/components/EmptyState'
import { Spinner } from '../../shared/components/Spinner'
import { DetailLayout } from '../../shared/components/DetailLayout'
import { DataTable } from '../../shared/components/DataTable'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { SummarySection } from '../../shared/pages/ModulePage'
import { useSiteDetail } from './api'

export function SiteDetailPage() {
  const { id } = useParams<{ id: string }>()
  const [searchParams] = useSearchParams()
  const clientId = searchParams.get('clientId') ?? undefined

  const { data, isLoading, isError } = useSiteDetail(clientId, id)

  if (!clientId) {
    return (
      <EmptyState
        title="Open this site from its client"
        description="Direct site links need client context, which the backend site-detail endpoint requires. Navigate from the client's Sites tab or the Sites list to view this record."
      />
    )
  }

  if (isLoading) {
    return <Spinner label="Loading site" />
  }

  if (isError || !data || !id) {
    return <EmptyState title="Site unavailable" description="The site record could not be loaded." />
  }

  const { site, clientName, systems, assets, workOrders } = data

  const header = (
    <PageHeader
      title={site.siteName}
      subtitle={`${site.siteCode} · ${clientName ?? 'Unknown client'}`}
      breadcrumbs={[
        { label: 'Clients', to: '/clients' },
        { label: clientName ?? 'Client', to: `/clients/${clientId}` },
        { label: site.siteName },
      ]}
      secondaryActions={<StatusBadge status={site.status} />}
    />
  )

  return (
    <DetailLayout
      header={header}
      tabs={[
        {
          id: 'summary',
          label: 'Summary',
          content: (
            <SummarySection title="Site Summary" description="Location, contact, and access details.">
              <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-3">
                {[
                  ['Type', site.siteType ?? '—'],
                  ['Address', site.streetAddress ?? '—'],
                  ['Town/City', site.townCity ?? '—'],
                  ['County', site.county ?? '—'],
                  ['Contact Person', site.contactPerson ?? '—'],
                  ['Contact Phone', site.contactPhone ?? '—'],
                  ['Operating Hours', site.operatingHours ?? '—'],
                  ['Systems', String(data.systemCount)],
                  ['Assets', String(data.assetCount)],
                  ['Open Work Orders', String(data.openWorkOrders)],
                ].map(([label, value]) => (
                  <div key={label} className="rounded-xl border border-slate-200 px-4 py-3">
                    <div className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">{label}</div>
                    <div className="mt-2 text-sm text-slate-900">{value}</div>
                  </div>
                ))}
              </div>
              {site.accessNotes ? (
                <div className="mt-4 rounded-xl border border-slate-200 px-4 py-3">
                  <div className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">Access Notes</div>
                  <p className="mt-2 text-sm leading-6 text-slate-700">{site.accessNotes}</p>
                </div>
              ) : null}
            </SummarySection>
          ),
        },
        {
          id: 'systems',
          label: 'Systems',
          content: (
            <DataTable
              columns={[
                { key: 'systemName', header: 'System', cell: (row) => row.systemName },
                { key: 'systemType', header: 'Type', cell: (row) => row.systemType },
                { key: 'manufacturer', header: 'Manufacturer', cell: (row) => row.manufacturer ?? '—' },
                { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.status} /> },
              ]}
              data={systems.map((row) => ({ id: row.systemId, ...row }))}
              emptyTitle="No systems"
              emptyDescription="Systems installed at this site will appear here."
            />
          ),
        },
        {
          id: 'assets',
          label: 'Assets',
          content: (
            <DataTable
              columns={[
                { key: 'assetName', header: 'Asset', cell: (row) => <Link className="font-medium text-slate-900 underline decoration-slate-300 underline-offset-4" to={`/assets/${row.assetId}`}>{row.assetName}</Link> },
                { key: 'assetCode', header: 'Code', cell: (row) => <span className="font-mono text-xs text-slate-600">{row.assetCode}</span> },
                { key: 'assetType', header: 'Type', cell: (row) => row.assetType ?? '—' },
                { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.status ?? 'Active'} /> },
              ]}
              data={assets.map((row) => ({ id: row.assetId, ...row }))}
              emptyTitle="No assets"
              emptyDescription="Assets at this site will appear here."
            />
          ),
        },
        {
          id: 'pm-schedules',
          label: 'PM Schedules',
          content: <EmptyState title="Maintenance module pending" description="PM schedules for this site will appear here once the Maintenance module is wired." />,
        },
        {
          id: 'work-orders',
          label: 'Work Orders',
          content: (
            <DataTable
              columns={[
                { key: 'workOrderNumber', header: 'WO No', cell: (row) => <Link className="font-mono text-slate-900 underline decoration-slate-300 underline-offset-4" to={`/work-orders/${row.workOrderId}`}>{row.workOrderNumber}</Link> },
                { key: 'title', header: 'Title', cell: (row) => row.title },
                { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.status} /> },
                { key: 'priority', header: 'Priority', cell: (row) => <StatusBadge status={row.priority} /> },
              ]}
              data={workOrders.map((row) => ({ id: row.workOrderId, ...row }))}
              emptyTitle="No work orders"
              emptyDescription="Work orders raised for this site will appear here."
            />
          ),
        },
      ]}
    />
  )
}
