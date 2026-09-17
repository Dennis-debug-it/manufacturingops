import { useState, type ReactNode } from 'react'
import toast from 'react-hot-toast'
import { ChevronDown, Download, RefreshCw } from 'lucide-react'
import { apiClient } from '../../../shared/api/apiClient'
import { endpoints } from '../../../shared/api/endpoints'
import { EmptyState } from '../../../shared/components/EmptyState'
import { Spinner } from '../../../shared/components/Spinner'
import { StatusBadge } from '../../../shared/components/StatusBadge'
import { usePermission } from '../../../shared/hooks/usePermission'
import { cn } from '../../../shared/utils/cn'
import { formatCurrency } from '../../../shared/utils/format'
import { useAcknowledgeWorkOrder, useGenerateServiceReport, useServiceReport } from '../api'
import type { WorkOrder } from '../types'

function Section({ title, defaultOpen = false, children }: { title: string; defaultOpen?: boolean; children: ReactNode }) {
  const [open, setOpen] = useState(defaultOpen)
  return (
    <div className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] shadow-sm">
      <button type="button" onClick={() => setOpen((value) => !value)} className="flex w-full items-center justify-between px-5 py-4 text-left">
        <span className="text-sm font-semibold text-slate-900">{title}</span>
        <ChevronDown className={cn('h-4 w-4 text-slate-400 transition-transform', open && 'rotate-180')} />
      </button>
      {open ? <div className="border-t border-slate-100 px-5 py-4">{children}</div> : null}
    </div>
  )
}

function reportStatus(workOrder: WorkOrder): string {
  if (workOrder.acknowledgedByName) return 'Acknowledged'
  if (workOrder.status === 'Completed') return 'Ready for Review'
  if (['Closed', 'Cancelled'].includes(workOrder.status)) return workOrder.status
  return 'Draft'
}

export function ReportTab({ workOrder }: { workOrder: WorkOrder }) {
  const canGenerate = usePermission('work-orders.report.generate')
  const { data: report, isLoading, isError } = useServiceReport(workOrder.id)
  const generate = useGenerateServiceReport(workOrder.id)
  const acknowledge = useAcknowledgeWorkOrder(workOrder.id)

  const [downloading, setDownloading] = useState(false)
  const [ackOpen, setAckOpen] = useState(false)
  const [ackName, setAckName] = useState('')
  const [ackComment, setAckComment] = useState('')

  const regenerate = async () => {
    try {
      await generate.mutateAsync()
      toast.success('Report regenerated')
    } catch {
      // interceptor surfaces the error toast
    }
  }

  const downloadPdf = async () => {
    setDownloading(true)
    try {
      const response = await apiClient.get(endpoints.workOrders.serviceReportPdf(workOrder.id), { responseType: 'blob', suppressGlobalErrorToast: true })
      const url = URL.createObjectURL(response.data as Blob)
      const link = document.createElement('a')
      link.href = url
      link.download = `${workOrder.workOrderNumber}-service-report.pdf`
      link.click()
      URL.revokeObjectURL(url)
    } catch {
      toast.error('Could not download the PDF')
    } finally {
      setDownloading(false)
    }
  }

  const submitAcknowledge = async () => {
    if (!ackName.trim()) {
      toast.error('Acknowledged-by name is required')
      return
    }
    try {
      await acknowledge.mutateAsync({ acknowledgedByName: ackName.trim(), comments: ackComment.trim() || null })
      toast.success('Work order acknowledged')
      setAckOpen(false)
      setAckName('')
      setAckComment('')
    } catch {
      // interceptor surfaces the error toast
    }
  }

  if (isLoading) {
    return <Spinner label="Loading service report" />
  }

  if (isError || !report) {
    return <EmptyState title="Report unavailable" description="The service report could not be generated for this work order." />
  }

  const status = reportStatus(workOrder)

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3 rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 shadow-sm">
        <div className="flex items-center gap-3">
          <StatusBadge status={status} />
          <span className="text-sm text-slate-600">{report.workOrderNumber} — {report.title}</span>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          {canGenerate ? (
            <button type="button" onClick={regenerate} disabled={generate.isPending} className="inline-flex h-10 items-center gap-2 rounded-lg border border-slate-200 bg-white px-3 text-sm font-medium text-slate-700 transition hover:bg-slate-50 disabled:opacity-60">
              <RefreshCw className="h-4 w-4" />
              {generate.isPending ? 'Regenerating…' : 'Regenerate'}
            </button>
          ) : null}
          <button type="button" onClick={downloadPdf} disabled={downloading} className="inline-flex h-10 items-center gap-2 rounded-lg bg-[var(--app-primary)] px-3 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:opacity-60">
            <Download className="h-4 w-4" />
            {downloading ? 'Preparing…' : 'Download PDF'}
          </button>
          {workOrder.status === 'Completed' && !workOrder.acknowledgedByName ? (
            <button type="button" onClick={() => setAckOpen((value) => !value)} className="inline-flex h-10 items-center gap-2 rounded-lg border border-slate-200 bg-white px-3 text-sm font-medium text-slate-700 transition hover:bg-slate-50">
              Acknowledge
            </button>
          ) : null}
        </div>
      </div>

      {ackOpen ? (
        <div className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 shadow-sm">
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
            <div>
              <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="ack-name">Acknowledged By</label>
              <input id="ack-name" value={ackName} onChange={(event) => setAckName(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]" />
            </div>
            <div>
              <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="ack-comment">Comments</label>
              <input id="ack-comment" value={ackComment} onChange={(event) => setAckComment(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]" />
            </div>
          </div>
          <button type="button" onClick={submitAcknowledge} disabled={acknowledge.isPending} className="mt-3 inline-flex h-10 items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:opacity-60">
            {acknowledge.isPending ? 'Saving…' : 'Confirm Acknowledgement'}
          </button>
        </div>
      ) : null}

      <Section title="Company & Job Details" defaultOpen>
        <dl className="grid grid-cols-1 gap-3 text-sm sm:grid-cols-2">
          <div><dt className="text-xs uppercase text-slate-500">Company</dt><dd className="text-slate-900">{report.companyName}</dd></div>
          <div><dt className="text-xs uppercase text-slate-500">Work Order</dt><dd className="text-slate-900">{report.workOrderNumber}</dd></div>
          <div><dt className="text-xs uppercase text-slate-500">Generated</dt><dd className="text-slate-900">{report.generatedAtLabel}</dd></div>
          <div><dt className="text-xs uppercase text-slate-500">Reported Problem</dt><dd className="text-slate-900">{report.reportedProblem ?? '—'}</dd></div>
        </dl>
      </Section>

      <Section title="Client & Site">
        <dl className="grid grid-cols-1 gap-3 text-sm sm:grid-cols-2">
          <div><dt className="text-xs uppercase text-slate-500">Client</dt><dd className="text-slate-900">{report.clientName ?? '—'}</dd></div>
          <div><dt className="text-xs uppercase text-slate-500">Site</dt><dd className="text-slate-900">{report.siteLabel ?? '—'}</dd></div>
        </dl>
      </Section>

      <Section title="Asset">
        <dl className="grid grid-cols-1 gap-3 text-sm sm:grid-cols-2">
          <div><dt className="text-xs uppercase text-slate-500">Asset</dt><dd className="text-slate-900">{report.assetName ?? '—'}</dd></div>
          <div><dt className="text-xs uppercase text-slate-500">Details</dt><dd className="text-slate-900">{report.assetDetails ?? '—'}</dd></div>
        </dl>
      </Section>

      <Section title="Technician Team & Timestamps">
        <p className="text-sm text-slate-700">{report.technicianTeam ?? '—'}</p>
        <ul className="mt-3 grid grid-cols-1 gap-2 text-sm sm:grid-cols-2">
          {report.timestamps.map((timestamp) => (
            <li key={timestamp.label} className="flex justify-between rounded-lg bg-slate-50 px-3 py-2">
              <span className="text-slate-500">{timestamp.label}</span>
              <span className="text-slate-900">{timestamp.value ?? '—'}</span>
            </li>
          ))}
        </ul>
      </Section>

      <Section title="Findings & Work Done">
        <p className="text-sm leading-6 text-slate-700"><span className="font-medium text-slate-900">Findings: </span>{report.findings ?? '—'}</p>
        <p className="mt-2 text-sm leading-6 text-slate-700"><span className="font-medium text-slate-900">Work Done: </span>{report.workDone ?? '—'}</p>
      </Section>

      <Section title={`Checklist (${report.templateName ?? 'No template'})`}>
        {report.checklistSections.length === 0 ? (
          <p className="text-sm text-slate-500">No checklist items recorded.</p>
        ) : (
          <div className="space-y-4">
            {report.checklistSections.map((section) => (
              <div key={section.sectionName}>
                <div className="mb-2 text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">{section.sectionName}</div>
                <ul className="space-y-1 text-sm text-slate-700">
                  {section.items.map((item) => (
                    <li key={item.question} className="flex items-center justify-between rounded-lg bg-slate-50 px-3 py-2">
                      <span>{item.question}</span>
                      <span className="font-medium text-slate-900">{item.responseValue ?? '—'}{item.unit ? ` ${item.unit}` : ''}</span>
                    </li>
                  ))}
                </ul>
              </div>
            ))}
          </div>
        )}
      </Section>

      <Section title="Materials Used">
        {report.materials.length === 0 ? (
          <p className="text-sm text-slate-500">No materials logged.</p>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="text-left text-xs uppercase text-slate-500">
                  <th className="py-1">Item</th>
                  <th className="py-1">Qty</th>
                  <th className="py-1">Unit Cost</th>
                </tr>
              </thead>
              <tbody>
                {report.materials.map((material) => (
                  <tr key={material.name} className="border-t border-slate-100">
                    <td className="py-2 text-slate-900">{material.name}</td>
                    <td className="py-2 text-slate-700">{material.quantityUsed} {material.unitOfMeasure}</td>
                    <td className="py-2 text-slate-700">{material.unitCost != null ? formatCurrency(material.unitCost) : '—'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Section>

      <Section title="Photos">
        {report.photoGroups.length === 0 ? (
          <p className="text-sm text-slate-500">No photos included in this report.</p>
        ) : (
          <div className="space-y-3">
            {report.photoGroups.map((group) => (
              <div key={group.category}>
                <div className="mb-1 text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">{group.category}</div>
                <p className="text-sm text-slate-600">{group.photos.length} photo(s)</p>
              </div>
            ))}
          </div>
        )}
      </Section>

      <Section title="Signatures">
        {report.signatures.length === 0 ? (
          <p className="text-sm text-slate-500">No signatures captured.</p>
        ) : (
          <ul className="space-y-2 text-sm text-slate-700">
            {report.signatures.map((signature) => (
              <li key={`${signature.signatureType}-${signature.signerName}`} className="flex items-center justify-between rounded-lg bg-slate-50 px-3 py-2">
                <span>{signature.signatureType} — {signature.signerName}</span>
                <span className="text-xs text-slate-500">{signature.capturedAtLabel}</span>
              </li>
            ))}
          </ul>
        )}
      </Section>
    </div>
  )
}
