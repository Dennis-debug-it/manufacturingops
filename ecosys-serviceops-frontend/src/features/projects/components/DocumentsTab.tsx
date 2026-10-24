import { useRef } from 'react'
import toast from 'react-hot-toast'
import { Download, Trash2, Upload } from 'lucide-react'
import { DataTable, type DataTableColumn } from '../../../shared/components/DataTable'
import { formatDateTime } from '../../../shared/utils/dates'
import { downloadProjectDocument, useDeleteProjectDocument, useProjectDocuments, useSetDocumentSharing, useUploadProjectDocument } from '../api'
import type { ProjectDetail, ProjectDocument } from '../types'
import { primaryButtonClass, smallButtonClass } from './formStyles'

const formatSize = (bytes: number) => (bytes >= 1_048_576 ? `${(bytes / 1_048_576).toFixed(1)} MB` : `${Math.max(1, Math.round(bytes / 1024))} KB`)

/** Drawings, approvals and handover files. Internal by default; ticking "Client" shows a file in the portal. */
export function DocumentsTab({ project, readOnly }: { project: ProjectDetail; readOnly: boolean }) {
  const projectId = project.summary.id
  const inputRef = useRef<HTMLInputElement>(null)
  const { data: documents, isLoading } = useProjectDocuments(projectId)
  const upload = useUploadProjectDocument(projectId)
  const setSharing = useSetDocumentSharing(projectId)
  const remove = useDeleteProjectDocument()

  const onFiles = async (files: FileList | null) => {
    for (const file of Array.from(files ?? [])) {
      try {
        await upload.mutateAsync(file)
        toast.success(`${file.name} uploaded`)
      } catch {
        // toast from the API client (size and type limits are enforced by the server)
      }
    }
    if (inputRef.current) inputRef.current.value = ''
  }

  const columns: DataTableColumn<ProjectDocument>[] = [
    { key: 'fileName', header: 'File', cell: (row) => (<div><div className="font-medium text-slate-900">{row.fileName}</div><div className="text-xs text-slate-500">{formatSize(row.fileSize)}</div></div>) },
    { key: 'uploadedByName', header: 'Uploaded', cell: (row) => (<div><div>{row.uploadedByName ?? '—'}</div><div className="text-xs text-slate-500">{formatDateTime(row.createdAt)}</div></div>) },
    {
      key: 'sharedWithClient',
      header: 'Client',
      cell: (row) => (
        <label className="flex items-center gap-2 text-sm">
          <input
            type="checkbox"
            checked={row.sharedWithClient}
            disabled={readOnly || !project.visibleInClientPortal}
            onChange={(event) => setSharing.mutate({ attachmentId: row.id, sharedWithClient: event.target.checked })}
            className="h-4 w-4 rounded border-slate-300"
            aria-label={`Share ${row.fileName} with the client`}
          />
          {row.sharedWithClient ? 'Shared' : 'Internal'}
        </label>
      ),
    },
    {
      key: 'actions',
      header: '',
      cell: (row) => (
        <div className="flex justify-end gap-2">
          <button type="button" aria-label={`Download ${row.fileName}`} className={smallButtonClass} onClick={() => void downloadProjectDocument(row)}><Download className="h-3.5 w-3.5" /></button>
          {!readOnly ? <button type="button" aria-label={`Delete ${row.fileName}`} className={smallButtonClass} onClick={() => remove.mutate(row.id, { onSuccess: () => toast.success('Document deleted') })}><Trash2 className="h-3.5 w-3.5" /></button> : null}
        </div>
      ),
    },
  ]

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <p className="text-sm text-slate-600">
          {project.visibleInClientPortal
            ? 'Files are internal unless you tick Client. Shared files appear in the client portal.'
            : 'This project is hidden from the client portal, so nothing here is shared.'}
        </p>
        {!readOnly ? (
          <>
            <input ref={inputRef} type="file" multiple className="hidden" onChange={(event) => void onFiles(event.target.files)} accept=".pdf,.png,.jpg,.jpeg,.webp,.doc,.docx,.xls,.xlsx" />
            <button type="button" className={primaryButtonClass} disabled={upload.isPending} onClick={() => inputRef.current?.click()}><Upload className="h-4 w-4" />{upload.isPending ? 'Uploading…' : 'Upload'}</button>
          </>
        ) : null}
      </div>
      <DataTable columns={columns} data={documents ?? []} loading={isLoading} emptyTitle="No documents" emptyDescription="Drawings, approvals, test certificates and handover files go here." />
    </div>
  )
}
