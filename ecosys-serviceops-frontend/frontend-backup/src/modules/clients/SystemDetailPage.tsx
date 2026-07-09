import { Link, useParams } from 'react-router-dom'
import { Badge } from '../../components/ui/Badge'
import { DataTable } from '../../components/ui/DataTable'
import { ErrorState } from '../../components/ui/ErrorState'
import { LoadingState } from '../../components/ui/LoadingState'
import { MetricCard, MetricGrid, PageScaffold, SectionCard } from '../../components/ui/Workspace'
import { useAsyncData } from '../../hooks/useAsyncData'
import { systemService } from '../../services/systemService'
import { formatDateOnly } from '../../utils/date'
import { formatWorkTypeLabel } from '../work-orders/workOrderWorkflowUi'

export function SystemDetailPage() {
  const { clientId, siteId, systemId } = useParams<{ clientId: string; siteId: string; systemId: string }>()
  const { data, loading, error } = useAsyncData(
    (signal) => clientId && siteId && systemId ? systemService.get(clientId, siteId, systemId, signal) : Promise.reject(new Error('Client, site, and system are required.')),
    null,
    [clientId, siteId, systemId],
  )

  if (loading) return <LoadingState label="Loading system summary" />
  if (error || !data) return <ErrorState title="Unable to load system" description={error || 'System summary is unavailable.'} />

  return (
    <PageScaffold
      eyebrow="System"
      title={data.system.systemName}
      description="Assets, work orders, PM activity, and related documents for the selected system."
      actions={<Link to={`/clients/${clientId}/sites/${siteId}`} className="button-secondary">Back to site</Link>}
    >
      <MetricGrid className="xl:grid-cols-4">
        <MetricCard label="Assets / Components" value={data.assets.length} meta="Serviceable items under this system" emphasis="accent" />
        <MetricCard label="Open Work Orders" value={data.system.openWorkOrders} meta="Current system-level workload" />
        <MetricCard label="Upcoming PMs" value={data.upcomingPmCount} meta="Due in the next 30 days" />
        <MetricCard label="Documents" value={data.relatedDocuments.length} meta="Related manuals and uploaded files" />
      </MetricGrid>

      <SectionCard title="System Summary" description="Core identification and install details for the selected system.">
        <div className="grid gap-3 md:grid-cols-3">
          <Detail label="System type" value={data.system.systemType} />
          <Detail label="Client" value={data.system.clientName || '-'} />
          <Detail label="Site" value={data.system.siteName || '-'} />
          <Detail label="Status" value={data.system.status} />
          <Detail label="Manufacturer" value={data.system.manufacturer || '-'} />
          <Detail label="Model" value={data.system.model || '-'} />
          <Detail label="Serial number" value={data.system.serialNumber || '-'} />
          <Detail label="Installation date" value={formatDateOnly(data.system.installationDate || undefined)} />
          <Detail label="Notes" value={data.system.notes || '-'} />
        </div>
      </SectionCard>

      <SectionCard title="Assets / Components" description="Component-level service items under this system.">
        <DataTable
          rows={data.assets}
          rowKey={(row) => row.id}
          pageSize={10}
          emptyTitle="No system assets found"
          emptyDescription="There are no active assets or components linked to this system yet."
          columns={[
            { key: 'asset', header: 'Asset / Component', cell: (row) => <div><p className="font-semibold text-app">{row.assetName}</p><p className="mt-1 text-xs text-muted">{row.assetCode}</p></div> },
            { key: 'type', header: 'Asset Type', cell: (row) => row.assetType || '-' },
            { key: 'category', header: 'Category', cell: (row) => row.assetCategory || '-' },
            { key: 'status', header: 'Status', cell: (row) => <Badge tone={row.status === 'Active' ? 'success' : 'neutral'}>{row.status}</Badge> },
            { key: 'lastPm', header: 'Last PM', cell: (row) => formatDateOnly(row.lastPmServiceDate || undefined) },
            { key: 'nextPm', header: 'Next PM', cell: (row) => formatDateOnly(row.nextPmDueDate || undefined) },
            { key: 'workOrders', header: 'Open Work Orders', cell: (row) => String(row.openWorkOrders) },
          ]}
        />
      </SectionCard>

      <SectionCard title="Work Orders" description="Recent work orders linked directly to this system.">
        <DataTable
          rows={data.workOrders}
          rowKey={(row) => row.id}
          pageSize={10}
          emptyTitle="No work orders found"
          emptyDescription="No work orders are currently linked to this system."
          columns={[
            { key: 'number', header: 'Work Order', cell: (row) => row.workOrderNumber },
            { key: 'title', header: 'Title', cell: (row) => row.title },
            { key: 'asset', header: 'Asset', cell: (row) => row.assetName || '-' },
            { key: 'workType', header: 'Work Type', cell: (row) => formatWorkTypeLabel(row.workType) },
            { key: 'priority', header: 'Priority', cell: (row) => row.priority },
            { key: 'status', header: 'Status', cell: (row) => <Badge tone={row.status === 'Completed' || row.status === 'Closed' ? 'success' : 'info'}>{row.status}</Badge> },
          ]}
        />
      </SectionCard>

      <SectionCard title="Related Documents" description="Knowledge documents already linked to this system.">
        <DataTable
          rows={data.relatedDocuments}
          rowKey={(row) => row.id}
          pageSize={10}
          emptyTitle="No related documents"
          emptyDescription="There are no linked manuals or related documents for this system yet."
          columns={[
            { key: 'title', header: 'Title', cell: (row) => row.title },
            { key: 'fileName', header: 'File', cell: (row) => row.fileName },
            { key: 'status', header: 'Status', cell: (row) => <Badge tone={row.status === 'published' ? 'success' : 'neutral'}>{row.status}</Badge> },
            { key: 'uploadedAt', header: 'Uploaded', cell: (row) => formatDateOnly(row.uploadedAt) },
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
