import { Link, useParams } from 'react-router-dom'
import { Badge } from '../../components/ui/Badge'
import { DataTable } from '../../components/ui/DataTable'
import { ErrorState } from '../../components/ui/ErrorState'
import { LoadingState } from '../../components/ui/LoadingState'
import { MetricCard, MetricGrid, PageScaffold, SectionCard } from '../../components/ui/Workspace'
import { useAsyncData } from '../../hooks/useAsyncData'
import { siteService } from '../../services/siteService'
import { formatDateOnly } from '../../utils/date'

export function SiteDetailPage() {
  const { clientId, siteId } = useParams<{ clientId: string; siteId: string }>()
  const { data, loading, error } = useAsyncData(
    (signal) => clientId && siteId ? siteService.get(clientId, siteId, signal) : Promise.reject(new Error('Client and site are required.')),
    null,
    [clientId, siteId],
  )

  if (loading) return <LoadingState label="Loading site summary" />
  if (error || !data) return <ErrorState title="Unable to load site" description={error || 'Site summary is unavailable.'} />

  return (
    <PageScaffold
      eyebrow="Site"
      title={data.site.siteName}
      description={`Operational view for ${data.clientName || 'the selected client'} site.`}
      actions={<Link to={`/clients/${clientId}`} className="button-secondary">Back to client</Link>}
    >
      <MetricGrid className="xl:grid-cols-5">
        <MetricCard label="Systems" value={data.systemCount} meta="Operational groupings at this site" emphasis="accent" />
        <MetricCard label="Assets / Components" value={data.assetCount} meta="Active serviceable items at this site" />
        <MetricCard label="Open Work Orders" value={data.openWorkOrders} meta="Current service demand at this site" />
        <MetricCard label="Upcoming PMs" value={data.upcomingPmCount} meta="Due in the next 30 days" />
        <MetricCard label="Status" value={data.site.status} meta={[data.site.townCity, data.site.county, data.site.country].filter(Boolean).join(', ') || data.clientName || 'Site status'} />
      </MetricGrid>

      <SectionCard title="Site Summary" description="Client, location, and service coordination details for the selected site.">
        <div className="grid gap-3 md:grid-cols-3">
          <Detail label="Client" value={data.clientName || '-'} />
          <Detail label="Location" value={[data.site.townCity, data.site.county, data.site.country].filter(Boolean).join(', ') || '-'} />
          <Detail label="Contact person" value={data.site.contactPerson || '-'} />
        </div>
      </SectionCard>

      <SectionCard title="Systems" description="Open a system to view its component assets and related work orders.">
        <DataTable
          rows={data.systems}
          rowKey={(row) => row.systemId}
          pageSize={10}
          emptyTitle="No systems found"
          emptyDescription="This site does not have any systems yet."
          columns={[
            {
              key: 'name',
              header: 'System',
              cell: (row) => (
                <div>
                  <Link to={`/clients/${clientId}/sites/${siteId}/systems/${row.systemId}`} className="font-semibold text-accent-strong hover:underline">
                    {row.systemName}
                  </Link>
                  <p className="mt-1 text-xs text-muted">{row.systemType}</p>
                </div>
              ),
            },
            { key: 'assets', header: 'Assets / Components', cell: (row) => String(row.assetCount) },
            { key: 'workOrders', header: 'Open Work Orders', cell: (row) => String(row.openWorkOrderCount) },
            { key: 'status', header: 'Status', cell: (row) => <Badge tone={row.status === 'Active' ? 'success' : 'neutral'}>{row.status}</Badge> },
          ]}
        />
      </SectionCard>

      <SectionCard title="Assets / Components" description="All active serviceable items linked to this site, with system context where available.">
        <DataTable
          rows={data.assets}
          rowKey={(row) => row.assetId}
          pageSize={10}
          emptyTitle="No assets found"
          emptyDescription="This site does not have any active assets or components yet."
          columns={[
            { key: 'asset', header: 'Asset / Component', cell: (row) => <div><p className="font-semibold text-app">{row.assetName}</p><p className="mt-1 text-xs text-muted">{row.assetCode}</p></div> },
            { key: 'system', header: 'System', cell: (row) => row.systemName || '-' },
            { key: 'type', header: 'Asset Type', cell: (row) => row.assetType || '-' },
            { key: 'status', header: 'Status', cell: (row) => <Badge tone={row.status === 'Active' ? 'success' : 'neutral'}>{row.status}</Badge> },
            { key: 'lastPm', header: 'Last PM', cell: (row) => formatDateOnly(row.lastPmServiceDate || undefined) },
            { key: 'nextPm', header: 'Next PM', cell: (row) => formatDateOnly(row.nextPmDueDate || undefined) },
          ]}
        />
      </SectionCard>
    </PageScaffold>
  )
}

function Detail({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-2xl border border-app bg-app/20 px-3 py-3">
      <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-muted">{label}</p>
      <p className="mt-1 text-sm text-app">{value}</p>
    </div>
  )
}
