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
import { normalizeError } from '../shared/api/apiError'
import {
  downloadPortalProjectDocument,
  usePortalProject,
  usePortalProjectBilling,
  usePortalProjects,
  usePortalSettings,
  type PortalInvoiceListItem,
  type PortalProjectDetail,
  type PortalProjectListItem,
  type PortalProjectValuation,
} from './api'

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

      <PortalProjectBillingSection projectId={projectId} />

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

/** Read-only billing: certified valuations, the project's invoices and retention held. Only shown when the portal shows invoices. */
function PortalProjectBillingSection({ projectId }: { projectId: string }) {
  const { data: settings } = usePortalSettings()
  const showInvoices = settings?.showInvoices === true
  const { data: billing, isLoading, isError, error } = usePortalProjectBilling(projectId, showInvoices)

  if (!showInvoices) return null
  // 403: no Finance module or invoices hidden for this portal — hide the section quietly.
  if (isError && normalizeError(error).status === 403) return null

  if (isLoading) {
    return (
      <SummarySection title="Billing">
        <Spinner label="Loading billing" />
      </SummarySection>
    )
  }

  if (isError || !billing) {
    return (
      <SummarySection title="Billing">
        <p className="text-sm text-slate-500">Billing details could not be loaded right now.</p>
      </SummarySection>
    )
  }

  const money = (value: number, currency = billing.currency) => formatCurrency(value, currency)
  const valuations = billing.valuations.map((v) => ({ ...v, id: v.valuationNumber }))

  const valuationColumns: DataTableColumn<PortalProjectValuation & { id: string }>[] = [
    { key: 'valuationNumber', header: 'Valuation', cell: (row) => <span className="font-mono text-slate-900">{row.valuationNumber}</span> },
    { key: 'periodEnd', header: 'Period to', cell: (row) => formatDate(row.periodEnd) },
    { key: 'certificateReference', header: 'Certificate', cell: (row) => <ValuationCertificate valuation={row} /> },
    { key: 'certifiedAmount', header: 'Certified amount', className: 'text-right', cell: (row) => money(row.certifiedAmount) },
  ]

  const invoiceColumns: DataTableColumn<PortalInvoiceListItem>[] = [
    { key: 'invoiceNumber', header: 'Invoice #', cell: (row) => <InvoiceLink invoice={row} /> },
    { key: 'invoiceDate', header: 'Date', cell: (row) => formatDate(row.invoiceDate) },
    { key: 'totalAmount', header: 'Total', className: 'text-right', cell: (row) => money(row.totalAmount, row.currency) },
    { key: 'balance', header: 'Balance', className: 'text-right', cell: (row) => money(row.balance, row.currency) },
    { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.status} /> },
  ]

  return (
    <SummarySection title="Billing" description="Certified valuations and invoices for this project.">
      <div className="space-y-5">
        <div className="rounded-xl border border-slate-200 bg-white/70 px-4 py-3">
          <div className="text-xs uppercase tracking-[0.18em] text-slate-500">Retention held</div>
          <div className="mt-1 text-lg font-semibold text-slate-900">{money(billing.retentionHeld)}</div>
          <p className="mt-1 text-xs text-slate-500">Retention deducted from certified valuations and not yet released.</p>
        </div>

        <div>
          <h3 className="mb-2 text-sm font-semibold text-slate-900">Certified valuations</h3>
          {valuations.length === 0 ? (
            <p className="text-sm text-slate-500">No valuations have been certified yet.</p>
          ) : (
            <>
              <ul className="space-y-2 sm:hidden">
                {valuations.map((row) => (
                  <li key={row.id} className="rounded-xl border border-slate-200 bg-white/70 px-4 py-3 text-sm">
                    <div className="flex items-start justify-between gap-3">
                      <span className="min-w-0 break-words font-mono text-slate-900">{row.valuationNumber}</span>
                      <span className="shrink-0 font-medium text-slate-900">{money(row.certifiedAmount)}</span>
                    </div>
                    <div className="mt-1 text-xs text-slate-500">Period to {formatDate(row.periodEnd)}</div>
                    <div className="mt-1 break-words text-xs text-slate-500">Certificate: <ValuationCertificate valuation={row} /></div>
                  </li>
                ))}
              </ul>
              <div className="hidden sm:block">
                <DataTable columns={valuationColumns} data={valuations} />
              </div>
            </>
          )}
        </div>

        <div>
          <h3 className="mb-2 text-sm font-semibold text-slate-900">Invoices</h3>
          {billing.invoices.length === 0 ? (
            <p className="text-sm text-slate-500">No invoices have been raised for this project yet.</p>
          ) : (
            <>
              <ul className="space-y-2 sm:hidden">
                {billing.invoices.map((row) => (
                  <li key={row.id} className="rounded-xl border border-slate-200 bg-white/70 px-4 py-3 text-sm">
                    <div className="flex items-start justify-between gap-3">
                      <span className="min-w-0 break-words"><InvoiceLink invoice={row} /></span>
                      <StatusBadge status={row.status} />
                    </div>
                    <div className="mt-1 text-xs text-slate-500">{formatDate(row.invoiceDate)}</div>
                    <div className="mt-2 flex flex-wrap justify-between gap-x-4 gap-y-1 text-xs text-slate-600">
                      <span>Total {money(row.totalAmount, row.currency)}</span>
                      <span>Balance <span className="font-medium text-slate-900">{money(row.balance, row.currency)}</span></span>
                    </div>
                  </li>
                ))}
              </ul>
              <div className="hidden sm:block">
                <DataTable columns={invoiceColumns} data={billing.invoices} />
              </div>
            </>
          )}
        </div>
      </div>
    </SummarySection>
  )
}

function ValuationCertificate({ valuation }: { valuation: PortalProjectValuation }) {
  if (!valuation.certificateReference && !valuation.certifiedOn) return <>—</>
  return (
    <>
      {valuation.certificateReference ?? 'Certified'}
      {valuation.certifiedOn ? <span className="text-slate-500"> · {formatDate(valuation.certifiedOn)}</span> : null}
    </>
  )
}

function InvoiceLink({ invoice }: { invoice: PortalInvoiceListItem }) {
  return (
    <Link to={`/portal/invoices/${invoice.id}`} className="font-medium text-slate-900 underline decoration-slate-300 underline-offset-4">
      {invoice.invoiceNumber}
    </Link>
  )
}
