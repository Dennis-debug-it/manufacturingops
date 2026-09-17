import { useState } from 'react'
import toast from 'react-hot-toast'
import { Download, ExternalLink, FileText, Package, Receipt } from 'lucide-react'
import { Link } from 'react-router-dom'
import { apiClient } from '../../../shared/api/apiClient'
import { Spinner } from '../../../shared/components/Spinner'
import { cn } from '../../../shared/utils/cn'
import { useWorkOrderDocuments } from '../api'
import type { WorkOrderDocument } from '../types'
import { useEntitlements } from '../../../shared/hooks/useEntitlements'

const documentMeta: Record<WorkOrderDocument['documentType'], { label: string; icon: typeof FileText; openTo: (doc: WorkOrderDocument, workOrderId: string) => string }> = {
  Quote: { label: 'Quote', icon: FileText, openTo: (doc) => `/finance/quotes/${doc.id}` },
  ServiceReport: { label: 'Service Report', icon: FileText, openTo: (_doc, workOrderId) => `/work-orders/${workOrderId}?tab=report` },
  Invoice: { label: 'Invoice', icon: Receipt, openTo: (doc) => `/finance/invoices/${doc.id}` },
  MaterialRequest: { label: 'Material Request', icon: Package, openTo: () => '/inventory/parts-requests' },
}

export function DocumentsPanel({ workOrderId }: { workOrderId: string }) {
  const { canAccessPath } = useEntitlements()
  const { data, isLoading } = useWorkOrderDocuments(workOrderId)
  const [downloadingId, setDownloadingId] = useState<string | null>(null)

  const downloadPdf = async (doc: WorkOrderDocument) => {
    if (!doc.pdfUrl) return
    setDownloadingId(doc.id)
    try {
      const response = await apiClient.get(doc.pdfUrl, { responseType: 'blob', suppressGlobalErrorToast: true })
      const url = URL.createObjectURL(response.data as Blob)
      const link = document.createElement('a')
      link.href = url
      link.download = `${doc.number}.pdf`
      link.click()
      URL.revokeObjectURL(url)
    } catch {
      toast.error('Could not download the PDF')
    } finally {
      setDownloadingId(null)
    }
  }

  if (isLoading) {
    return <Spinner label="Loading documents" />
  }

  const documents = (data ?? []).filter((doc) => canAccessPath(documentMeta[doc.documentType].openTo(doc, workOrderId)))

  return (
    <div className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 shadow-sm">
      <h3 className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">Documents</h3>
      {documents.length === 0 ? (
        <p className="mt-3 text-sm text-slate-500">No linked documents yet.</p>
      ) : (
        <ul className="mt-3 space-y-2">
          {documents.map((doc) => {
            const meta = documentMeta[doc.documentType]
            const Icon = meta.icon
            return (
              <li key={`${doc.documentType}-${doc.id}`} className="rounded-xl border border-slate-200 p-3">
                <div className="flex items-start gap-2">
                  <Icon className="mt-0.5 h-4 w-4 shrink-0 text-slate-400" />
                  <div className="min-w-0 flex-1">
                    <div className="flex flex-wrap items-center gap-1.5">
                      <span className="text-sm font-medium text-slate-900">{meta.label}</span>
                      <span
                        className={cn(
                          'rounded-full px-2 py-0.5 text-[10px] font-medium ring-1 ring-inset',
                          'bg-slate-100 text-slate-600 ring-slate-200',
                        )}
                      >
                        {doc.status}
                      </span>
                    </div>
                    <p className="truncate text-xs text-slate-500">{doc.number}</p>
                  </div>
                </div>
                <div className="mt-2 flex items-center gap-3">
                  <Link
                    to={meta.openTo(doc, workOrderId)}
                    className="inline-flex items-center gap-1 text-xs font-medium text-slate-700 hover:text-slate-900"
                  >
                    <ExternalLink className="h-3.5 w-3.5" />
                    Open
                  </Link>
                  {doc.pdfUrl ? (
                    <button
                      type="button"
                      onClick={() => void downloadPdf(doc)}
                      disabled={downloadingId === doc.id}
                      className="inline-flex items-center gap-1 text-xs font-medium text-slate-700 hover:text-slate-900 disabled:opacity-60"
                    >
                      <Download className="h-3.5 w-3.5" />
                      {downloadingId === doc.id ? 'Downloading…' : 'Download PDF'}
                    </button>
                  ) : null}
                </div>
              </li>
            )
          })}
        </ul>
      )}
    </div>
  )
}
