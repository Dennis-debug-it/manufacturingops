import { useEffect, useState } from 'react'
import toast from 'react-hot-toast'
import { useNavigate } from 'react-router-dom'
import { FormDrawer } from '../../../shared/components/FormDrawer'
import { useSites } from '../../clients/api'
import { priorityOptions, workTypeOptions } from '../../work-orders/schemas'
import { useConvertToClient, useCreateWorkOrderFromOpportunity } from '../api'
import { opportunityParty, type SalesOpportunityDetail } from '../types'

const fieldClass = 'h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none transition focus:border-[var(--app-primary)]'
const labelClass = 'mb-1.5 block text-sm font-medium text-slate-700'

/**
 * The sales → operations hand-off. For a deal that is still a prospect this first creates the
 * client from the prospect details, then raises the work order against it; the new job keeps a
 * link back to the deal.
 */
export function ConvertToWorkOrderDrawer({ open, onClose, opportunity }: { open: boolean; onClose: () => void; opportunity: SalesOpportunityDetail }) {
  const navigate = useNavigate()
  const convertToClient = useConvertToClient(opportunity.id)
  const createWorkOrder = useCreateWorkOrderFromOpportunity()
  const { data: sites } = useSites(opportunity.clientId ?? undefined)
  const [title, setTitle] = useState(opportunity.title)
  const [description, setDescription] = useState(opportunity.description ?? '')
  const [workType, setWorkType] = useState(opportunity.serviceType ?? 'NewInstallation')
  const [priority, setPriority] = useState('Medium')
  const [dueDate, setDueDate] = useState('')
  const [siteId, setSiteId] = useState(opportunity.siteId ?? '')
  const busy = convertToClient.isPending || createWorkOrder.isPending
  const needsClient = !opportunity.clientId

  useEffect(() => {
    if (!open) return
    setTitle(opportunity.title)
    setDescription(opportunity.description ?? '')
    setWorkType(opportunity.serviceType ?? 'NewInstallation')
    setPriority('Medium')
    setDueDate('')
    setSiteId(opportunity.siteId ?? '')
  }, [open, opportunity])

  const submit = async () => {
    if (!title.trim()) {
      toast.error('Work order title is required')
      return
    }
    try {
      if (needsClient) {
        await convertToClient.mutateAsync()
      }
      const workOrder = await createWorkOrder.mutateAsync({
        opportunityId: opportunity.id,
        title: title.trim(),
        description: description.trim() || null,
        priority,
        workType,
        dueDate: dueDate ? new Date(dueDate).toISOString() : null,
        siteId: siteId || null,
      })
      toast.success(`Work order ${workOrder.workOrderNumber} created`)
      onClose()
      navigate(`/work-orders/${workOrder.id}`)
    } catch {
      // interceptor surfaces the error toast
    }
  }

  return (
    <FormDrawer
      open={open}
      title="Create Work Order from Deal"
      onClose={onClose}
      footer={
        <div className="flex items-center justify-end gap-2">
          <button type="button" onClick={onClose} className="inline-flex h-11 items-center justify-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">Cancel</button>
          <button type="button" onClick={() => void submit()} disabled={busy} className="inline-flex h-11 items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:opacity-60">
            {busy ? 'Creating…' : 'Create Work Order'}
          </button>
        </div>
      }
    >
      <div className="space-y-4">
        {needsClient ? (
          <div className="rounded-xl border border-sky-200 bg-sky-50 px-4 py-3 text-sm text-sky-900">
            <span className="font-medium">{opportunityParty(opportunity)}</span> is not a client yet. A client record will be created from the deal&apos;s contact details first.
          </div>
        ) : null}
        <div>
          <label className={labelClass} htmlFor="wo-title">Work order title</label>
          <input id="wo-title" value={title} onChange={(event) => setTitle(event.target.value)} className={fieldClass} maxLength={200} />
        </div>
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          <div>
            <label className={labelClass} htmlFor="wo-type">Work type</label>
            <select id="wo-type" value={workType} onChange={(event) => setWorkType(event.target.value)} className={fieldClass}>
              {workTypeOptions.map((option) => (
                <option key={option.value} value={option.value}>{option.label}</option>
              ))}
            </select>
          </div>
          <div>
            <label className={labelClass} htmlFor="wo-priority">Priority</label>
            <select id="wo-priority" value={priority} onChange={(event) => setPriority(event.target.value)} className={fieldClass}>
              {priorityOptions.map((option) => (
                <option key={option.value} value={option.value}>{option.label}</option>
              ))}
            </select>
          </div>
          <div>
            <label className={labelClass} htmlFor="wo-due">Due date (optional)</label>
            <input id="wo-due" type="date" value={dueDate} onChange={(event) => setDueDate(event.target.value)} className={fieldClass} />
          </div>
          {!needsClient ? (
            <div>
              <label className={labelClass} htmlFor="wo-site">Site (optional)</label>
              <select id="wo-site" value={siteId} onChange={(event) => setSiteId(event.target.value)} className={fieldClass}>
                <option value="">No specific site</option>
                {(sites ?? []).map((site) => (
                  <option key={site.id} value={site.id}>{site.siteName}</option>
                ))}
              </select>
            </div>
          ) : null}
        </div>
        <div>
          <label className={labelClass} htmlFor="wo-description">Description</label>
          <textarea id="wo-description" value={description} onChange={(event) => setDescription(event.target.value)} rows={5} className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[var(--app-primary)]" />
        </div>
        <p className="text-sm text-slate-500">
          Technicians can be assigned from the work order once it is created. The deal can produce more than one work order.
        </p>
      </div>
    </FormDrawer>
  )
}
