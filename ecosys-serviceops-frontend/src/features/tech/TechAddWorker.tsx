import { useState } from 'react'
import toast from 'react-hot-toast'
import { UserPlus, X } from 'lucide-react'
import { useOnlineStatus } from '../../shared/hooks/useOnlineStatus'
import { cn } from '../../shared/utils/cn'
import { useSaveWorker } from '../projects/siteApi'
import type { WorkerEngagement } from '../projects/types'

const inputClass = 'h-11 w-full rounded-lg border border-slate-300 bg-white px-3 text-base outline-none focus:border-[var(--app-primary)] sm:text-sm'
const ENGAGEMENTS: WorkerEngagement[] = ['Casual', 'Permanent', 'Subcontractor']

/**
 * Adds someone to a site's roster from the phone: who they are and how they're engaged. Presence
 * only; pay rates are set in the office and never asked for or shown here. Needs a signal (the new
 * worker has to exist on the server before they can be marked).
 */
export function TechAddWorker({ projectId }: { projectId: string }) {
  const online = useOnlineStatus()
  const save = useSaveWorker(projectId)
  const [open, setOpen] = useState(false)
  const [fullName, setFullName] = useState('')
  const [phone, setPhone] = useState('')
  const [trade, setTrade] = useState('')
  const [engagementType, setEngagementType] = useState<WorkerEngagement>('Casual')

  const reset = () => {
    setFullName('')
    setPhone('')
    setTrade('')
    setEngagementType('Casual')
  }

  const submit = async (event: React.FormEvent) => {
    event.preventDefault()
    const name = fullName.trim()
    if (!name) return
    try {
      await save.mutateAsync({ fullName: name, phone: phone.trim() || null, trade: trade.trim() || null, engagementType, technicianId: null })
      toast.success(`${name} added to the roster`)
      reset()
      setOpen(false)
    } catch {
      // interceptor surfaces the error toast
    }
  }

  if (!open) {
    return (
      <button type="button" onClick={() => setOpen(true)} className="inline-flex h-11 w-full items-center justify-center gap-2 rounded-lg border border-dashed border-slate-300 bg-white text-sm font-medium text-slate-700 hover:bg-slate-50">
        <UserPlus className="h-4 w-4" /> Add worker
      </button>
    )
  }

  return (
    <form onSubmit={(event) => void submit(event)} className="space-y-3 rounded-xl border border-slate-200 bg-white p-4" aria-label="Add worker">
      <div className="flex items-center justify-between gap-2">
        <h2 className="text-sm font-semibold text-slate-900">Add worker</h2>
        <button type="button" aria-label="Close" onClick={() => setOpen(false)} className="inline-flex h-11 w-11 items-center justify-center rounded-lg text-slate-500 hover:bg-slate-50">
          <X className="h-4 w-4" />
        </button>
      </div>
      <label className="block text-sm font-medium text-slate-700">
        Full name
        <input value={fullName} onChange={(event) => setFullName(event.target.value)} required autoComplete="off" className={cn(inputClass, 'mt-1')} />
      </label>
      <label className="block text-sm font-medium text-slate-700">
        Phone <span className="font-normal text-slate-500">(optional)</span>
        <input type="tel" inputMode="tel" value={phone} onChange={(event) => setPhone(event.target.value)} autoComplete="off" className={cn(inputClass, 'mt-1')} />
      </label>
      <label className="block text-sm font-medium text-slate-700">
        Trade <span className="font-normal text-slate-500">(optional)</span>
        <input value={trade} onChange={(event) => setTrade(event.target.value)} placeholder="e.g. Mason, Fundi, Helper" autoComplete="off" className={cn(inputClass, 'mt-1')} />
      </label>
      <fieldset>
        <legend className="text-sm font-medium text-slate-700">Engagement</legend>
        <div className="mt-1 grid grid-cols-3 gap-1.5">
          {ENGAGEMENTS.map((option) => (
            <button
              key={option}
              type="button"
              aria-pressed={engagementType === option}
              onClick={() => setEngagementType(option)}
              className={cn('h-11 rounded-lg border px-1 text-xs font-medium sm:text-sm', engagementType === option ? 'border-[var(--app-primary)] bg-[var(--app-primary)] text-white' : 'border-slate-200 bg-white text-slate-700')}
            >
              {option}
            </button>
          ))}
        </div>
      </fieldset>
      {!online ? <p className="text-xs text-amber-700">Adding a worker needs a signal. You can still mark the workers already on the roster.</p> : null}
      <button type="submit" disabled={!online || !fullName.trim() || save.isPending} className="inline-flex h-11 w-full items-center justify-center rounded-lg bg-[var(--app-primary)] text-sm font-semibold text-white disabled:opacity-50">
        {save.isPending ? 'Adding…' : 'Add to roster'}
      </button>
    </form>
  )
}
