import { useRef, useState } from 'react'
import toast from 'react-hot-toast'
import { Download, FileText, Trash2, Upload } from 'lucide-react'
import { apiClient } from '../../../shared/api/apiClient'
import { useAuth } from '../../../shared/hooks/useAuth'
import { formatDate } from '../../../shared/utils/dates'
import { useDeleteTenderDocument, useTenderDocuments, useUploadTenderDocument } from '../api'

const ACCEPT = '.pdf,.doc,.docx,.xls,.xlsx,.csv,.jpg,.jpeg,.png,.webp'

function formatSize(bytes: number) {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${Math.round(bytes / 1024)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}

/** The tender pack, clarifications and the bid as sent — stored with the shared attachments (5 MB each). */
export function TenderDocuments({ tenderId, canManage }: { tenderId: string; canManage: boolean }) {
  const { user } = useAuth()
  const { data: documents, isLoading } = useTenderDocuments(tenderId)
  const upload = useUploadTenderDocument(tenderId)
  const remove = useDeleteTenderDocument(tenderId)
  const inputRef = useRef<HTMLInputElement>(null)
  const [downloadingId, setDownloadingId] = useState<string | null>(null)

  const onFiles = async (files: FileList | null) => {
    for (const file of Array.from(files ?? [])) {
      try {
        await upload.mutateAsync(file)
        toast.success(`${file.name} uploaded`)
      } catch {
        // interceptor surfaces the error toast
      }
    }
    if (inputRef.current) inputRef.current.value = ''
  }

  const download = async (id: string, fileName: string, url: string) => {
    setDownloadingId(id)
    try {
      const response = await apiClient.get(url, { responseType: 'blob', suppressGlobalErrorToast: true })
      const objectUrl = URL.createObjectURL(response.data as Blob)
      const link = document.createElement('a')
      link.href = objectUrl
      link.download = fileName
      link.click()
      URL.revokeObjectURL(objectUrl)
    } catch {
      toast.error('Could not download the file')
    } finally {
      setDownloadingId(null)
    }
  }

  return (
    <div className="space-y-3">
      {canManage ? (
        <>
          <input ref={inputRef} type="file" multiple accept={ACCEPT} className="hidden" onChange={(event) => void onFiles(event.target.files)} />
          <button type="button" disabled={upload.isPending} onClick={() => inputRef.current?.click()} className="inline-flex h-10 items-center gap-2 rounded-lg border border-dashed border-slate-300 px-3 text-sm font-medium text-slate-600 transition hover:bg-slate-50 disabled:opacity-60">
            <Upload className="h-4 w-4" />{upload.isPending ? 'Uploading…' : 'Upload documents'}
          </button>
        </>
      ) : null}
      {isLoading ? <p className="text-sm text-slate-500">Loading…</p> : null}
      {!isLoading && (documents ?? []).length === 0 ? <p className="text-sm text-slate-500">No documents yet. Upload the tender pack and your submission here.</p> : null}
      <ul className="divide-y divide-slate-100">
        {(documents ?? []).map((doc) => (
          <li key={doc.id} className="flex items-center gap-3 py-2.5 text-sm">
            <FileText className="h-4 w-4 shrink-0 text-slate-400" />
            <div className="min-w-0 flex-1">
              <div className="truncate text-slate-900">{doc.fileName}</div>
              <div className="text-xs text-slate-500">{formatSize(doc.fileSize)} · {formatDate(doc.createdAt)}</div>
            </div>
            <button type="button" aria-label={`Download ${doc.fileName}`} disabled={downloadingId === doc.id} onClick={() => void download(doc.id, doc.fileName, doc.publicUrl)} className="rounded-lg p-2 text-slate-500 transition hover:bg-slate-100 hover:text-slate-900">
              <Download className="h-4 w-4" />
            </button>
            {canManage && (user?.role === 'admin' || doc.uploadedByUserId === user?.userId) ? (
              <button type="button" aria-label={`Delete ${doc.fileName}`} disabled={remove.isPending} onClick={() => remove.mutate(doc.id)} className="rounded-lg p-2 text-slate-500 transition hover:bg-rose-50 hover:text-rose-600">
                <Trash2 className="h-4 w-4" />
              </button>
            ) : null}
          </li>
        ))}
      </ul>
    </div>
  )
}
