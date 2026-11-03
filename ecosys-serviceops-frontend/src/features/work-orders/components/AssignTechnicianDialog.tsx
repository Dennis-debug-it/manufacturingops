import { useEffect, useState } from 'react'
import { X } from 'lucide-react'
import { useTechnicianOptions } from '../pickerApi'

export interface AssignTechnicianDialogProps {
  open: boolean
  title?: string
  description?: string
  submitting?: boolean
  onClose: () => void
  onSubmit: (technicianId: string, notes: string) => void
}

const fieldClass = 'h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none transition focus:border-[var(--app-primary)]'
const labelClass = 'mb-1.5 block text-sm font-medium text-slate-700'

export function AssignTechnicianDialog({ open, title = 'Assign technician', description, submitting, onClose, onSubmit }: AssignTechnicianDialogProps) {
  const { data: technicians } = useTechnicianOptions()
  const [technicianId, setTechnicianId] = useState('')
  const [notes, setNotes] = useState('')

  useEffect(() => {
    if (!open) {
      setTechnicianId('')
      setNotes('')
    }
  }, [open])

  if (!open) {
    return null
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center px-4 py-6">
      <button type="button" aria-label="Close dialog" className="absolute inset-0 bg-[var(--app-primary)]/40" onClick={onClose} />
      <div className="relative w-full max-w-md rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-6 shadow-xl">
        <div className="flex items-start justify-between gap-4">
          <div>
            <h3 className="text-lg font-semibold text-slate-900">{title}</h3>
            {description ? <p className="mt-1 text-sm leading-6 text-slate-600">{description}</p> : null}
          </div>
          <button type="button" onClick={onClose} className="rounded-lg p-2 text-slate-500 transition hover:bg-slate-100 hover:text-slate-900">
            <X className="h-4 w-4" />
          </button>
        </div>

        <div className="mt-4 space-y-4">
          <div>
            <label className={labelClass}>Technician</label>
            <select className={fieldClass} value={technicianId} onChange={(event) => setTechnicianId(event.target.value)}>
              <option value="">Select technician…</option>
              {(technicians ?? []).map((technician) => (
                <option key={technician.id} value={technician.id}>{technician.fullName}</option>
              ))}
            </select>
          </div>
          <div>
            <label className={labelClass}>Notes (optional)</label>
            <textarea
              className="min-h-[80px] w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none transition focus:border-[var(--app-primary)]"
              value={notes}
              onChange={(event) => setNotes(event.target.value)}
              placeholder="Assignment notes"
            />
          </div>
        </div>

        <div className="mt-6 flex items-center justify-end gap-2">
          <button type="button" onClick={onClose} className="inline-flex h-11 items-center justify-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">
            Cancel
          </button>
          <button
            type="button"
            disabled={!technicianId || submitting}
            onClick={() => onSubmit(technicianId, notes)}
            className="inline-flex h-11 items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:bg-slate-300"
          >
            Assign
          </button>
        </div>
      </div>
    </div>
  )
}
