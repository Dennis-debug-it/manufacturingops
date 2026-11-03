import { useEffect, useState } from 'react'
import toast from 'react-hot-toast'
import { useNavigate } from 'react-router-dom'
import { useQueryClient } from '@tanstack/react-query'
import { FormDrawer } from '../../../shared/components/FormDrawer'
import { useProjectPeople } from '../../projects/api'
import { useAwardQuotation } from '../../projects/siteApi'

const inputClass = 'h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]'
const labelClass = 'mb-1.5 block text-sm font-medium text-slate-700'

export interface AwardProjectDrawerProps {
  open: boolean
  quotationId: string
  defaultName: string
  onClose: () => void
}

/** Turns an accepted quotation into a project. The backend marks the quotation Converted. */
export function AwardProjectDrawer({ open, quotationId, defaultName, onClose }: AwardProjectDrawerProps) {
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const award = useAwardQuotation()
  const { data: people } = useProjectPeople(open)
  const [name, setName] = useState(defaultName)
  const [projectManagerUserId, setProjectManagerUserId] = useState('')
  const [plannedStartDate, setPlannedStartDate] = useState('')
  const [plannedEndDate, setPlannedEndDate] = useState('')

  useEffect(() => {
    if (open) {
      setName(defaultName)
      setProjectManagerUserId('')
      setPlannedStartDate('')
      setPlannedEndDate('')
    }
  }, [open, defaultName])

  const submit = async () => {
    if (plannedStartDate && plannedEndDate && plannedEndDate < plannedStartDate) {
      toast.error('Planned end must be on or after the planned start')
      return
    }
    try {
      const project = await award.mutateAsync({
        quotationId,
        name: name.trim() || null,
        projectTypeId: null,
        projectManagerUserId: projectManagerUserId || null,
        branchId: null,
        plannedStartDate: plannedStartDate || null,
        plannedEndDate: plannedEndDate || null,
        templateId: null,
      })
      void queryClient.invalidateQueries({ queryKey: ['quotations'] })
      toast.success('Project created from quotation')
      onClose()
      navigate(`/projects/${project.summary.id}`)
    } catch {
      // interceptor surfaces the error toast
    }
  }

  return (
    <FormDrawer
      open={open}
      title="Award as project"
      onClose={onClose}
      footer={
        <div className="flex items-center justify-end gap-2">
          <button type="button" onClick={onClose} className="inline-flex h-11 items-center justify-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">
            Cancel
          </button>
          <button type="button" onClick={() => void submit()} disabled={award.isPending} className="inline-flex h-11 items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:opacity-60">
            {award.isPending ? 'Creating...' : 'Create project'}
          </button>
        </div>
      }
    >
      <div className="space-y-4">
        <p className="text-sm text-slate-600">The project takes the quotation's client, site, value (before tax) and currency. The quotation is then marked Converted.</p>
        <div>
          <label className={labelClass} htmlFor="award-name">Project name</label>
          <input id="award-name" value={name} onChange={(event) => setName(event.target.value)} placeholder="Defaults to the quotation subject" className={inputClass} />
        </div>
        <div>
          <label className={labelClass} htmlFor="award-manager">Project manager</label>
          <select id="award-manager" value={projectManagerUserId} onChange={(event) => setProjectManagerUserId(event.target.value)} className={inputClass}>
            <option value="">Not assigned</option>
            {(people?.users ?? []).map((user) => (
              <option key={user.id} value={user.id}>
                {user.fullName}
              </option>
            ))}
          </select>
        </div>
        <div className="grid gap-4 sm:grid-cols-2">
          <div>
            <label className={labelClass} htmlFor="award-start">Planned start</label>
            <input id="award-start" type="date" value={plannedStartDate} onChange={(event) => setPlannedStartDate(event.target.value)} className={inputClass} />
          </div>
          <div>
            <label className={labelClass} htmlFor="award-end">Planned end</label>
            <input id="award-end" type="date" value={plannedEndDate} onChange={(event) => setPlannedEndDate(event.target.value)} className={inputClass} />
          </div>
        </div>
      </div>
    </FormDrawer>
  )
}
