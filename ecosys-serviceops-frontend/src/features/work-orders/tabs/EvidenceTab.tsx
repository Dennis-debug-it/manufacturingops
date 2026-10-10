import { useRef, useState, type ChangeEvent } from 'react'
import toast from 'react-hot-toast'
import { Upload, X } from 'lucide-react'
import { AuthenticatedImage } from '../../../shared/components/AuthenticatedImage'
import { EmptyState } from '../../../shared/components/EmptyState'
import { Spinner } from '../../../shared/components/Spinner'
import { usePermission } from '../../../shared/hooks/usePermission'
import { formatDateTime } from '../../../shared/utils/dates'
import { useExecutionBundle, useUpdatePhoto, useUploadPhoto } from '../api'
import { PhotoAnnotationCanvas } from '../components/PhotoAnnotationCanvas'
import type { WorkOrderPhoto } from '../types'

const photoCategories = ['Before', 'During', 'After', 'Defect', 'Completion', 'Spare Part', 'Client Sign-off', 'Other']

function PhotoCard({ photo, workOrderId, canEdit, onPreview }: { photo: WorkOrderPhoto; workOrderId: string; canEdit: boolean; onPreview: () => void }) {
  const updatePhoto = useUpdatePhoto(workOrderId)
  const [caption, setCaption] = useState(photo.caption)
  const [category, setCategory] = useState(photo.category)

  const saveEdits = async () => {
    try {
      await updatePhoto.mutateAsync({ photoId: photo.id, caption, category, includeInReport: photo.includeInReport })
      toast.success('Photo updated')
    } catch {
      // interceptor surfaces the error toast
    }
  }

  const downloadUrl = photo.publicUrl ?? `/api/attachments/${photo.attachmentId}/download`

  return (
    <div className="overflow-hidden rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] shadow-sm">
      <button type="button" onClick={onPreview} className="block w-full">
        <AuthenticatedImage src={downloadUrl} alt={photo.caption} className="h-40 w-full cursor-zoom-in object-cover" />
      </button>
      <div className="space-y-2 p-3">
        <input
          value={caption}
          disabled={!canEdit}
          onChange={(event) => setCaption(event.target.value)}
          onBlur={saveEdits}
          className="h-9 w-full rounded-lg border border-slate-300 px-2 text-xs outline-none focus:border-[var(--app-primary)] disabled:bg-slate-50"
        />
        <select
          value={category}
          disabled={!canEdit}
          onChange={(event) => {
            setCategory(event.target.value)
          }}
          onBlur={saveEdits}
          className="h-9 w-full rounded-lg border border-slate-300 px-2 text-xs outline-none focus:border-[var(--app-primary)] disabled:bg-slate-50"
        >
          {photoCategories.map((option) => (
            <option key={option} value={option}>{option}</option>
          ))}
        </select>
        <p className="text-[11px] text-slate-500">{photo.uploadedByName ?? 'Unknown'} · {formatDateTime(photo.uploadedAt)}</p>
      </div>
    </div>
  )
}

export function EvidenceTab({ workOrderId }: { workOrderId: string }) {
  const canUpload = usePermission('work-orders.evidence.upload')
  const { data, isLoading, isError } = useExecutionBundle(workOrderId)
  const uploadPhoto = useUploadPhoto(workOrderId)
  const fileInputRef = useRef<HTMLInputElement | null>(null)
  const [preview, setPreview] = useState<WorkOrderPhoto | null>(null)
  const [annotating, setAnnotating] = useState<File | null>(null)

  const onFileSelected = (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0]
    event.target.value = ''
    if (!file) return
    setAnnotating(file)
  }

  const onAnnotationDone = async (blob: Blob) => {
    setAnnotating(null)
    const annotatedFile = new File([blob], 'annotated-photo.jpg', { type: blob.type || 'image/jpeg' })
    try {
      await uploadPhoto.mutateAsync({ file: annotatedFile, category: 'Other' })
      toast.success('Photo uploaded')
    } catch {
      // interceptor surfaces the error toast
    }
  }

  if (isLoading) {
    return <Spinner label="Loading evidence" />
  }

  if (isError || !data) {
    return <EmptyState title="Evidence unavailable" description="Photo evidence could not be loaded." />
  }

  return (
    <div className="space-y-4">
      {canUpload ? (
        <div className="flex items-center justify-between rounded-2xl border border-dashed border-slate-300 bg-white p-4">
          <p className="text-sm text-slate-600">Upload before/after photos, defects, or client sign-off evidence (JPG, PNG, WebP — max 5MB).</p>
          <input ref={fileInputRef} type="file" accept="image/jpeg,image/png,image/webp" className="hidden" onChange={onFileSelected} />
          <button
            type="button"
            disabled={uploadPhoto.isPending}
            onClick={() => fileInputRef.current?.click()}
            className="inline-flex h-11 shrink-0 items-center gap-2 rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:cursor-not-allowed disabled:opacity-60"
          >
            <Upload className="h-4 w-4" />
            {uploadPhoto.isPending ? 'Uploading…' : 'Upload Photo'}
          </button>
        </div>
      ) : null}

      {data.photos.length === 0 ? (
        <EmptyState title="No evidence yet" description="Photos uploaded for this work order will appear here." />
      ) : (
        <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-4">
          {data.photos.map((photo) => (
            <PhotoCard key={photo.id} photo={photo} workOrderId={workOrderId} canEdit={canUpload} onPreview={() => setPreview(photo)} />
          ))}
        </div>
      )}

      {preview ? (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-[var(--app-primary)]/80 p-4" onClick={() => setPreview(null)}>
          <button type="button" onClick={() => setPreview(null)} className="absolute right-4 top-4 rounded-full bg-white/10 p-2 text-white hover:bg-white/20">
            <X className="h-5 w-5" />
          </button>
          <AuthenticatedImage
            src={preview.publicUrl ?? `/api/attachments/${preview.attachmentId}/download`}
            alt={preview.caption}
            className="max-h-[85vh] max-w-[90vw] rounded-lg object-contain"
          />
        </div>
      ) : null}

      {annotating ? (
        <PhotoAnnotationCanvas file={annotating} onCancel={() => setAnnotating(null)} onDone={onAnnotationDone} />
      ) : null}
    </div>
  )
}
