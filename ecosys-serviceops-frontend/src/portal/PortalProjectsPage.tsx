import { Link, useParams } from 'react-router-dom'
import { Download } from 'lucide-react'
import { PageHeader } from '../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../shared/components/DataTable'
import { EmptyState } from '../shared/components/EmptyState'
import { Spinner } from '../shared/components/Spinner'
import { StatusBadge } from '../shared/components/StatusBadge'
import { SummarySection } from '../shared/pages/ModulePage'
import { formatCurrency } from '../shared/utils/format'
import { formatDate } from '../shared/utils/dates'
import { ProgressBar, StageBadge } from '../features/projects/components/ProjectBadges'
import { downloadPortalProjectDocument, usePortalProject, usePortalProjects, type PortalProjectDetail, type PortalProjectListItem } from './api'

const milestoneLabels: Record<string, string> = { NotStarted: 'Not started', InProgress: 'In progress', Completed: 'Completed', Verified: 'Verified' }

export function PortalProjectsPage() {
  const { data, isLoading, isError } = usePortalProjects()

  const columns: DataTableColumn<PortalProjectListItem>[] = [
    { key: 'name', header: 'Project', cell: (row) => (<div><Link to={`/portal/projects/${row.id}`} className="font-medium text-slate-900 underline decoration-slate-300 underline-offset-4">{row.name}</Link><div className="font-mono text-xs text-slate-500">{row.projectNumber}{row.siteName ? ` · ${row.siteName}` : ''}</div></div>) },
    { key: 'stageName', header: 'Stage', cell: (row) => <StageBadge name={row.stageName} category={row.stageCategory} /> },
    { key: 'progressPercent', header: 'Progress', cell: (row) => <ProgressBar actual={row.progressPercent} planned={row.plannedProgressPercent} behind={row.progressPercent < row.plannedProgressPercent} compact /> },
    { key: 'plannedEndDate', header: 'Planned completion', cell: (row) => (row.plannedEndDate ? formatDate(row.plannedEndDate) : '—') },
    { key: 'contractValue', header: 'Contract value', cell: (row) => formatCurrency(row.contractValue, row.currency) },
  ]

  return (
    <div className="space-y-4">
      <PageHeader title="Projects" subtitle="Progress on the installations and projects we're delivering for you." />
      <DataTable columns={columns} data={data ?? []} loading={isLoading} emptyTitle={isError ? 'Projects unavailable' : 'No projects yet'} emptyDescription={isError ? 'Projects could not be loaded.' : 'Projects we are delivering for you will appear here.'} />
    </div>
  )
}

export function PortalProjectDetailPage() {
  const { id } = useParams<{ id: string }>()
  const { data: project, isLoading, isError } = usePortalProject(id)

  if (isLoading) return <Spinner label="Loading project" />
  if (isError || !project || !id) return <EmptyState title="Project unavailable" description="This project could not be loaded." />

  return <PortalProjectView projectId={id} project={project} />
}

function PortalProjectView({ projectId, project }: { projectId: string; project: PortalProjectDetail }) {
  const { summary } = project
  const money = (value: number) => formatCurrency(value, summary.currency)

  return (
    <div className="space-y-4">
      <PageHeader
        title={summary.name}
        subtitle={`${summary.projectNumber}${summary.siteName ? ` · ${summary.siteName}` : ''}`}
        breadcrumbs={[{ label: 'Projects', to: '/portal/projects' }, { label: summary.projectNumber }]}
        titleAdornment={<StageBadge name={summary.stageName} category={summary.stageCategory} />}
      />

      <SummarySection title="Progress">
        <ProgressBar actual={summary.progressPercent} planned={summary.plannedProgressPercent} behind={summary.progressPercent < summary.plannedProgressPercent} />
        <div className="mt-4 grid grid-cols-1 gap-3 text-sm sm:grid-cols-3">
          <div><div className="text-xs uppercase tracking-[0.18em] text-slate-500">Planned</div><div className="mt-1 text-slate-900">{summary.plannedStartDate ? formatDate(summary.plannedStartDate) : '—'} → {summary.plannedEndDate ? formatDate(summary.plannedEndDate) : '—'}</div></div>
          <div><div className="text-xs uppercase tracking-[0.18em] text-slate-500">Started</div><div className="mt-1 text-slate-900">{project.actualStartDate ? formatDate(project.actualStartDate) : 'Not yet'}</div></div>
          <div><div className="text-xs uppercase tracking-[0.18em] text-slate-500">Contract value</div><div className="mt-1 text-slate-900">{money(summary.contractValue)}</div></div>
        </div>
        {project.description ? <p className="mt-4 whitespace-pre-line text-sm text-slate-700">{project.description}</p> : null}
      </SummarySection>

      <SummarySection title="Milestones">
        <DataTable
          columns={[
            { key: 'name', header: 'Milestone', cell: (row) => row.name },
            { key: 'plannedDate', header: 'Planned', cell: (row) => (row.plannedDate ? <span className={row.isOverdue ? 'text-rose-600' : ''}>{formatDate(row.plannedDate)}</span> : '—') },
            { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={milestoneLabels[row.status] ?? row.status} /> },
          ]}
          data={project.milestones.map((m, index) => ({ ...m, id: `${index}` }))}
          emptyTitle="No milestones shared"
          emptyDescription="Milestones will appear here as the project is planned."
        />
      </SummarySection>

      <SummarySection title="Approved variations" description={`Additions ${money(project.approvedAdditions)} · Omissions ${money(project.approvedOmissions)}`}>
        <DataTable
          columns={[
            { key: 'variationNumber', header: 'Variation', cell: (row) => (<div><div className="text-slate-900">{row.title}</div><div className="font-mono text-xs text-slate-500">{row.variationNumber}{row.clientReference ? ` · ref ${row.clientReference}` : ''}</div></div>) },
            { key: 'kind', header: 'Type', cell: (row) => row.kind },
            { key: 'amount', header: 'Amount', cell: (row) => `${row.kind === 'Omission' ? '− ' : '+ '}${money(row.amount)}` },
            { key: 'approvedAt', header: 'Approved', cell: (row) => (row.approvedAt ? formatDate(row.approvedAt) : '—') },
          ]}
          data={project.variations.map((v) => ({ ...v, id: v.variationNumber }))}
          emptyTitle="No approved variations"
          emptyDescription="Approved additions and omissions will appear here."
        />
      </SummarySection>

      <SummarySection title="Documents">
        <DataTable
          columns={[
            { key: 'fileName', header: 'File', cell: (row) => row.fileName },
            { key: 'createdAt', header: 'Added', cell: (row) => formatDate(row.createdAt) },
            {
              key: 'download',
              header: '',
              cell: (row) => (
                <div className="flex justify-end">
                  <button type="button" aria-label={`Download ${row.fileName}`} onClick={() => void downloadPortalProjectDocument(projectId, row)} className="inline-flex h-9 items-center gap-1.5 rounded-lg border border-slate-200 bg-white px-3 text-xs font-medium text-slate-700 hover:bg-slate-50"><Download className="h-3.5 w-3.5" />Download</button>
                </div>
              ),
            },
          ]}
          data={project.documents}
          emptyTitle="No documents shared"
          emptyDescription="Drawings, certificates and handover files we share will appear here."
        />
      </SummarySection>
    </div>
  )
}
