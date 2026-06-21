import { FileText, Paperclip, Trash2, Upload, X } from 'lucide-react'
import { useRef, useState } from 'react'
import { attachmentService, validateAttachmentFile } from '../../services/attachmentService'
import type { AttachmentRecord } from '../../types/api'
import { useToast } from './ToastProvider'

function formatBytes(bytes: number) {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}

function fileBadge(mimeType: string) {
  if (mimeType.startsWith('image/')) return 'IMG'
  if (mimeType === 'application/pdf') return 'PDF'
  if (mimeType.includes('spreadsheet') || mimeType.includes('excel') || mimeType === 'text/csv') return 'XLS'
  if (mimeType.includes('word')) return 'DOC'
  return 'FILE'
}

interface AttachmentPanelProps {
  entityType: string
  entityId: string
  attachments: AttachmentRecord[]
  canDelete?: boolean
  onUploaded: (attachment: AttachmentRecord) => void
  onDeleted: (id: string) => void
}

export function AttachmentPanel({
  entityType,
  entityId,
  attachments,
  canDelete = true,
  onUploaded,
  onDeleted,
}: AttachmentPanelProps) {
  const { pushToast } = useToast()
  const fileInputRef = useRef<HTMLInputElement>(null)
  const [uploading, setUploading] = useState(false)
  const [uploadError, setUploadError] = useState<string | null>(null)
  const [deletingId, setDeletingId] = useState<string | null>(null)

  async function handleFileChange(event: React.ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0]
    if (!file) return

    const validationError = validateAttachmentFile(file)
    if (validationError) {
      setUploadError(validationError)
      event.target.value = ''
      return
    }

    setUploadError(null)
    setUploading(true)
    try {
      const result = await attachmentService.upload(entityType, entityId, file)
      onUploaded(result)
    } catch (error) {
      const msg = error instanceof Error ? error.message : 'Upload failed. Please try again.'
      setUploadError(msg)
      pushToast({ title: 'Upload failed', description: msg, tone: 'danger' })
    } finally {
      setUploading(false)
      event.target.value = ''
    }
  }

  async function handleDelete(id: string) {
    setDeletingId(id)
    try {
      await attachmentService.delete(id)
      onDeleted(id)
    } catch (error) {
      const msg = error instanceof Error ? error.message : 'Delete failed.'
      setUploadError(msg)
      pushToast({ title: 'Delete failed', description: msg, tone: 'danger' })
    } finally {
      setDeletingId(null)
    }
  }

  return (
    <div className="space-y-4" data-testid="attachment-panel">
      <div className="rounded-2xl border border-dashed border-app/40 bg-app/5 p-4">
        <div className="flex flex-wrap items-center gap-3">
          <button
            type="button"
            data-testid="attachment-upload-btn"
            className="button-primary flex items-center gap-2"
            disabled={uploading}
            onClick={() => fileInputRef.current?.click()}
          >
            <Upload className="size-4" />
            {uploading ? 'Uploading...' : 'Upload file'}
          </button>
          <p className="text-xs text-muted">
            Images (JPG, PNG, WebP), PDF, Word, Excel, CSV, max 5 MB
          </p>
          <input
            ref={fileInputRef}
            type="file"
            className="sr-only"
            data-testid="attachment-file-input"
            accept=".jpg,.jpeg,.png,.webp,.pdf,.doc,.docx,.xls,.xlsx,.csv"
            onChange={handleFileChange}
          />
        </div>
        {uploadError ? (
          <div className="mt-3 flex items-start gap-2 rounded-xl border border-red-500/30 bg-red-500/10 px-3 py-2 text-sm text-red-300">
            <X className="mt-0.5 size-4 shrink-0" />
            <span>{uploadError}</span>
          </div>
        ) : null}
      </div>

      {attachments.length === 0 ? (
        <div
          className="flex flex-col items-center gap-2 rounded-2xl border border-app/20 py-8 text-center text-muted"
          data-testid="attachment-empty"
        >
          <Paperclip className="size-8 opacity-40" />
          <p className="text-sm">No files attached yet.</p>
        </div>
      ) : (
        <ul className="space-y-2" data-testid="attachment-list">
          {attachments.map((attachment) => (
            <li
              key={attachment.id}
              data-testid={`attachment-item-${attachment.id}`}
              className="flex items-center gap-3 rounded-2xl border border-app/20 bg-app/5 px-4 py-3"
            >
              <span
                className="inline-flex min-w-11 justify-center rounded-full bg-app/10 px-2 py-1 text-[11px] font-semibold uppercase tracking-[0.16em] text-muted"
                aria-hidden
              >
                {fileBadge(attachment.mimeType)}
              </span>
              <div className="min-w-0 flex-1">
                <a
                  href={attachment.publicUrl}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="block truncate text-sm font-medium text-app hover:underline"
                  data-testid="attachment-download-link"
                >
                  {attachment.fileName}
                </a>
                <p className="text-xs text-muted">
                  {formatBytes(attachment.fileSize)} - {new Date(attachment.createdAt).toLocaleDateString()}
                </p>
              </div>
              {canDelete ? (
                <div data-testid="attachment-delete-btn">
                  <button
                    type="button"
                    data-testid={`attachment-delete-${attachment.id}`}
                    aria-label={`Delete ${attachment.fileName}`}
                    className="button-ghost shrink-0 p-2 text-red-400 hover:text-red-300"
                    disabled={deletingId === attachment.id}
                    onClick={() => void handleDelete(attachment.id)}
                    title="Delete attachment"
                  >
                  {deletingId === attachment.id ? (
                    <span className="text-xs text-muted">Deleting...</span>
                  ) : (
                    <Trash2 className="size-4" />
                  )}
                  </button>
                </div>
              ) : null}
            </li>
          ))}
        </ul>
      )}

      {attachments.length > 0 ? (
        <p className="text-right text-xs text-muted">
          <FileText className="mr-1 inline size-3" />
          {attachments.length} {attachments.length === 1 ? 'file' : 'files'}
        </p>
      ) : null}
    </div>
  )
}
