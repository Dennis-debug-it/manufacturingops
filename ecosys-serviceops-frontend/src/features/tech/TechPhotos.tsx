import { useRef, useState } from 'react'
import toast from 'react-hot-toast'
import { Camera, ImagePlus } from 'lucide-react'
import { AuthenticatedImage } from '../../shared/components/AuthenticatedImage'
import { cn } from '../../shared/utils/cn'
import { formatRelative } from '../../shared/utils/dates'
import { useExecutionBundle, useUploadPhoto } from '../work-orders/api'

const CATEGORIES = ['Before', 'During', 'After'] as const
const MAX_BYTES = 5 * 1024 * 1024

export function TechPhotos({ workOrderId, editable }: { workOrderId: string; editable: boolean }) {
  const { data: bundle } = useExecutionBundle(workOrderId)
  const upload = useUploadPhoto(workOrderId)
  const inputRef = useRef<HTMLInputElement | null>(null)
  const [file, setFile] = useState<File | null>(null)
  const [category, setCategory] = useState<(typeof CATEGORIES)[number]>('During')
  const [caption, setCaption] = useState('')
  const photos = bundle?.photos ?? []

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
      await upload.mutateAsync({ file, category, caption: caption.trim() || file.name })
      toast.success('Evidence uploaded')
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
          <input value={caption} onChange={(event) => setCaption(event.target.value)} placeholder="Caption (optional)" aria-label="Photo caption" className="h-10 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]" />
          <button type="button" onClick={() => void submit()} disabled={!file || upload.isPending} className="inline-flex h-11 w-full items-center justify-center rounded-lg bg-slate-900 text-sm font-medium text-white disabled:opacity-50">
            {upload.isPending ? 'Uploading…' : 'Upload evidence'}
          </button>
        </section>
      ) : null}

      {photos.length === 0 ? (
        <p className="rounded-xl border border-dashed border-slate-300 bg-white p-6 text-center text-sm text-slate-500">No photos yet.</p>
      ) : (
        <ul className="grid grid-cols-2 gap-2">
          {photos.map((photo) => (
            <li key={photo.id} className="overflow-hidden rounded-xl border border-slate-200 bg-white">
              <AuthenticatedImage src={photo.publicUrl ?? `/api/attachments/${photo.attachmentId}/download`} alt={photo.caption} className="h-32 w-full object-cover" />
              <div className="p-2">
                <p className="truncate text-xs font-medium text-slate-800">{photo.caption || photo.fileName}</p>
                <p className="text-[11px] text-slate-500">{photo.category} · {formatRelative(photo.uploadedAt)}</p>
              </div>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
