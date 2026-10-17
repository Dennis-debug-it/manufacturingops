import { useEffect, useRef, useState } from 'react'
import toast from 'react-hot-toast'
import { Camera, Check, ImagePlus, Pencil } from 'lucide-react'
import { AuthenticatedImage } from '../../shared/components/AuthenticatedImage'
import { cn } from '../../shared/utils/cn'
import { formatRelative } from '../../shared/utils/dates'
import { useServiceReportLayout, useUpdatePhoto } from '../work-orders/api'
import type { WorkOrder, WorkOrderPhoto } from '../work-orders/types'
import { savedMessage, useTechActions } from './offline/actions'
import { useTechBundle } from './offline/hooks'
import { isPendingPhoto, type PendingPhoto } from './offline/overlay'

const CATEGORIES = ['Before', 'During', 'After'] as const
const MAX_BYTES = 5 * 1024 * 1024

/** A photo still on the phone: shown from the stored file until it uploads. */
function LocalPhoto({ photo, className }: { photo: PendingPhoto; className: string }) {
  const [url, setUrl] = useState<string | null>(null)
  useEffect(() => {
    const objectUrl = URL.createObjectURL(photo.localBlob)
    setUrl(objectUrl)
    return () => URL.revokeObjectURL(objectUrl)
  }, [photo.localBlob])
  return url ? <img src={url} alt={photo.caption} className={className} /> : <div className={className} />
}

/** A photo's caption, editable once it has uploaded (captions show in the report and fill its photo slots). */
function PhotoCaption({ workOrderId, photo, editable }: { workOrderId: string; photo: WorkOrderPhoto; editable: boolean }) {
  const update = useUpdatePhoto(workOrderId)
  const [editing, setEditing] = useState(false)
  const [caption, setCaption] = useState(photo.caption)
  const pending = isPendingPhoto(photo)

  const save = async () => {
    try {
      await update.mutateAsync({ photoId: photo.id, caption: caption.trim() || null, category: photo.category, includeInReport: photo.includeInReport })
      setEditing(false)
      toast.success('Caption updated')
    } catch {
      toast.error(navigator.onLine ? 'Could not update the caption' : 'Connect to the internet to edit a caption')
    }
  }

  if (editing) {
    return (
      <div className="space-y-1">
        <input
          autoFocus
          value={caption}
          onChange={(event) => setCaption(event.target.value)}
          onKeyDown={(event) => { if (event.key === 'Enter') void save() }}
          aria-label="Edit caption"
          className="h-9 w-full rounded-md border border-slate-300 px-2 text-xs outline-none focus:border-[var(--app-primary)]"
        />
        <div className="grid grid-cols-2 gap-1">
          <button type="button" onClick={() => { setEditing(false); setCaption(photo.caption) }} className="h-8 rounded-md border border-slate-300 text-xs text-slate-700">Cancel</button>
          <button type="button" onClick={() => void save()} disabled={update.isPending} className="h-8 rounded-md bg-slate-900 text-xs font-medium text-white disabled:opacity-60">{update.isPending ? 'Saving…' : 'Save'}</button>
        </div>
      </div>
    )
  }

  return (
    <button
      type="button"
      disabled={!editable || pending}
      onClick={() => setEditing(true)}
      aria-label={`Edit caption for ${photo.caption || photo.fileName}`}
      className="flex w-full items-center gap-1 text-left disabled:cursor-default"
    >
      <span className="min-w-0 flex-1 truncate text-xs font-medium text-slate-800">{photo.caption || photo.fileName}</span>
      {editable && !pending ? <Pencil className="h-3 w-3 shrink-0 text-slate-400" /> : null}
    </button>
  )
}

export function TechPhotos({ job, editable }: { job: Pick<WorkOrder, 'id' | 'workOrderNumber'>; editable: boolean }) {
  const { data: bundle } = useTechBundle(job.id)
  const actions = useTechActions()
  const inputRef = useRef<HTMLInputElement | null>(null)
  const [file, setFile] = useState<File | null>(null)
  const [category, setCategory] = useState<(typeof CATEGORIES)[number]>('During')
  const [caption, setCaption] = useState('')
  const photos = bundle?.photos ?? []
  // Jobs with a client report layout have named photo slots; a photo fills the slot whose name is its caption.
  const { data: layout } = useServiceReportLayout(job.id)
  const slotCaptions = layout?.photoCaptions ?? []
  const takenCaptions = new Set(photos.map((photo) => photo.caption.trim().toLowerCase()))

  const choose = (picked: File | undefined) => {
    if (!picked) return
    if (picked.size > MAX_BYTES) {
      toast.error('Photo is larger than 5 MB')
      return
    }
    setFile(picked)
  }

  const submit = async () => {
    if (!file) return
    try {
      toast.success(savedMessage('Evidence uploaded', await actions.uploadPhoto(job, file, { category, caption: caption.trim() || file.name })))
      setFile(null)
      setCaption('')
      if (inputRef.current) inputRef.current.value = ''
    } catch {
      // interceptor surfaces the error toast
    }
  }

  return (
    <div className="space-y-3">
      {editable ? (
        <section className="space-y-3 rounded-xl border border-slate-200 bg-white p-4">
          <h2 className="text-sm font-semibold text-slate-900">Add a photo</h2>
          <div className="grid grid-cols-3 gap-1 rounded-lg border border-slate-200 p-1" role="radiogroup" aria-label="Photo stage">
            {CATEGORIES.map((option) => (
              <button
                key={option}
                type="button"
                role="radio"
                aria-checked={category === option}
                onClick={() => setCategory(option)}
                className={cn('h-9 rounded-md text-sm font-medium', category === option ? 'bg-[var(--app-primary)] text-white' : 'text-slate-600')}
              >
                {option}
              </button>
            ))}
          </div>
          <label data-testid="browse-evidence-control" className="flex h-24 cursor-pointer flex-col items-center justify-center gap-1 rounded-lg border-2 border-dashed border-slate-300 text-sm text-slate-600 active:bg-slate-50">
            {file ? <ImagePlus className="h-5 w-5 text-emerald-600" /> : <Camera className="h-5 w-5" />}
            <span className="max-w-full truncate px-3">{file ? file.name : 'Take or choose a photo'}</span>
            <input ref={inputRef} type="file" accept="image/jpeg,image/png,image/webp" capture="environment" className="sr-only" onChange={(event) => choose(event.target.files?.[0])} />
          </label>
          {slotCaptions.length > 0 ? (
            <div className="space-y-1.5">
              <p className="text-xs text-slate-500">Photos the report needs. Tap one to use it as the caption:</p>
              <div className="flex flex-wrap gap-1.5">
                {slotCaptions.map((slot) => {
                  const taken = takenCaptions.has(slot.toLowerCase())
                  return (
                    <button
                      key={slot}
                      type="button"
                      onClick={() => setCaption(slot)}
                      aria-pressed={caption === slot}
                      className={cn('inline-flex h-8 items-center gap-1 rounded-full border px-2.5 text-xs font-medium', caption === slot ? 'border-[var(--app-primary)] bg-[var(--app-primary)] text-white' : taken ? 'border-emerald-200 bg-emerald-50 text-emerald-800' : 'border-slate-300 bg-white text-slate-700')}
                    >
                      {taken ? <Check className="h-3 w-3" /> : null}{slot}
                    </button>
                  )
                })}
              </div>
            </div>
          ) : null}
          <input value={caption} onChange={(event) => setCaption(event.target.value)} placeholder="Caption (optional)" aria-label="Photo caption" className="h-10 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]" />
          <button type="button" onClick={() => void submit()} disabled={!file || actions.busy} className="inline-flex h-11 w-full items-center justify-center rounded-lg bg-slate-900 text-sm font-medium text-white disabled:opacity-50">
            {actions.busy ? 'Uploading…' : 'Upload evidence'}
          </button>
        </section>
      ) : null}

      {photos.length === 0 ? (
        <p className="rounded-xl border border-dashed border-slate-300 bg-white p-6 text-center text-sm text-slate-500">No photos yet.</p>
      ) : (
        <ul className="grid grid-cols-2 gap-2">
          {photos.map((photo) => (
            <li key={photo.id} className="overflow-hidden rounded-xl border border-slate-200 bg-white">
              {isPendingPhoto(photo)
                ? <LocalPhoto photo={photo} className="h-32 w-full object-cover" />
                : <AuthenticatedImage src={photo.publicUrl ?? `/api/attachments/${photo.attachmentId}/download`} alt={photo.caption} className="h-32 w-full object-cover" />}
              <div className="p-2">
                <PhotoCaption workOrderId={job.id} photo={photo} editable={editable} />
                <p className="text-[11px] text-slate-500">{photo.category} · {isPendingPhoto(photo) ? 'waiting to upload' : formatRelative(photo.uploadedAt)}</p>
              </div>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
