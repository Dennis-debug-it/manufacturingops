import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import toast from 'react-hot-toast'
import { FormDrawer } from '../../../shared/components/FormDrawer'
import { useDebounce } from '../../../shared/hooks/useDebounce'
import { useAssetSearch } from '../api'
import { Field, LocationSelect, inputClass, primaryButton, secondaryButton, textareaClass } from '../components/ui'
import { useFactoryLinks } from '../links'
import { useCreateJob, useJobPlans } from './api'
import { PRIORITIES, WORK_TYPES } from './types'

interface AssetChoice { id: string; label: string; categoryId: string | null }

/** Raise a job or report a breakdown. A breakdown puts the asset down and skips approval. */
export function NewJobDrawer({ open, onClose, asset, breakdown = false }: {
  open: boolean
  onClose: () => void
  asset?: AssetChoice | undefined
  breakdown?: boolean | undefined
}) {
  const navigate = useNavigate()
  const links = useFactoryLinks()
  const create = useCreateJob()
  const [form, setForm] = useState({ title: '', description: '', workType: 'Corrective', priority: 'Medium', isBreakdown: breakdown, needsLineStop: false, orgNodeId: '', dueDate: '', jobPlanId: '' })
  const [chosen, setChosen] = useState<AssetChoice | null>(asset ?? null)
  const [search, setSearch] = useState('')
  const debounced = useDebounce(search, 250)
  const results = useAssetSearch(debounced, debounced.trim().length >= 2)
  const plans = useJobPlans(chosen?.categoryId)

  useEffect(() => {
    if (open) {
      setForm((current) => ({ ...current, title: '', description: '', isBreakdown: breakdown, jobPlanId: '' }))
      setChosen(asset ?? null)
    }
  }, [open, asset, breakdown])

  const set = <K extends keyof typeof form>(key: K, value: (typeof form)[K]) => setForm((current) => ({ ...current, [key]: value }))

  const submit = async () => {
    try {
      const job = await create.mutateAsync({
        title: form.title,
        description: form.description || null,
        workType: form.workType,
        priority: form.priority,
        isBreakdown: form.isBreakdown,
        needsLineStop: form.needsLineStop,
        assetId: chosen?.id ?? null,
        orgNodeId: chosen ? null : form.orgNodeId || null,
        dueDate: form.dueDate || null,
        jobPlanId: form.jobPlanId || null,
      })
      toast.success(`${job.workOrderNumber} raised`)
      onClose()
      navigate(links.job(job.id))
    } catch {
      // The API client shows the error.
    }
  }

  return (
    <FormDrawer open={open} title={form.isBreakdown ? 'Report a breakdown' : 'Raise a job'} onClose={onClose} footer={
      <div className="flex justify-end gap-2">
        <button type="button" className={secondaryButton} onClick={onClose}>Cancel</button>
        <button type="button" className={primaryButton} disabled={create.isPending || !form.title.trim() || (!chosen && !form.orgNodeId) || (form.isBreakdown && !chosen)} onClick={() => void submit()}>
          {form.isBreakdown ? 'Report breakdown' : 'Raise job'}
        </button>
      </div>
    }>
      <div className="grid gap-4 sm:grid-cols-2">
        <label className="flex items-center gap-2 rounded-lg border border-rose-200 bg-rose-50 px-3 py-2 text-sm text-rose-900 sm:col-span-2">
          <input type="checkbox" checked={form.isBreakdown} onChange={(event) => set('isBreakdown', event.target.checked)} />
          The machine has stopped (breakdown)
        </label>
        <Field label="Asset" required={form.isBreakdown} className="sm:col-span-2" hint={chosen ? undefined : 'Search by name or number, or leave it and choose a location.'}>
          {chosen ? (
            <div className="flex h-10 items-center justify-between rounded-lg border border-slate-200 bg-slate-50 px-3 text-sm">
              <span>{chosen.label}</span>
              {!asset ? <button type="button" className="text-slate-500" onClick={() => setChosen(null)}>Change</button> : null}
            </div>
          ) : (
            <div className="relative">
              <input className={inputClass} value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Search assets" />
              {debounced.trim().length >= 2 ? (
                <ul className="absolute z-10 mt-1 max-h-56 w-full overflow-y-auto rounded-lg border border-slate-200 bg-white shadow-lg">
                  {(results.data?.items ?? []).map((item) => (
                    <li key={item.id}>
                      <button type="button" className="w-full px-3 py-2 text-left text-sm hover:bg-slate-50" onClick={() => { setChosen({ id: item.id, label: `${item.assetCode} · ${item.assetName}`, categoryId: item.assetCategoryId }); setSearch('') }}>
                        <span className="font-mono text-xs text-slate-500">{item.assetCode}</span> {item.assetName}
                      </button>
                    </li>
                  ))}
                  {results.data && results.data.items.length === 0 ? <li className="px-3 py-2 text-sm text-slate-500">No matching assets</li> : null}
                </ul>
              ) : null}
            </div>
          )}
        </Field>
        {!chosen ? <Field label="Location" required className="sm:col-span-2"><LocationSelect value={form.orgNodeId} onChange={(value) => set('orgNodeId', value)} /></Field> : null}
        <Field label="What is wrong, or what needs doing" required className="sm:col-span-2">
          <input className={inputClass} value={form.title} onChange={(event) => set('title', event.target.value)} />
        </Field>
        <Field label="Details" className="sm:col-span-2"><textarea className={textareaClass} value={form.description} onChange={(event) => set('description', event.target.value)} /></Field>
        <Field label="Type">
          <select className={inputClass} value={form.workType} onChange={(event) => set('workType', event.target.value)}>
            {WORK_TYPES.map((type) => <option key={type}>{type}</option>)}
          </select>
        </Field>
        <Field label="Priority" hint={form.isBreakdown ? 'Breakdowns are at least High.' : undefined}>
          <select className={inputClass} value={form.priority} onChange={(event) => set('priority', event.target.value)}>
            {PRIORITIES.map((priority) => <option key={priority}>{priority}</option>)}
          </select>
        </Field>
        <Field label="Job plan" hint="Copies its steps, hours and tools onto the job.">
          <select className={inputClass} value={form.jobPlanId} onChange={(event) => set('jobPlanId', event.target.value)}>
            <option value="">None</option>
            {(plans.data ?? []).map((plan) => <option key={plan.id} value={plan.id}>{plan.name}</option>)}
          </select>
        </Field>
        <Field label="Needed by" hint="Blank uses the response and repair targets."><input type="date" className={inputClass} value={form.dueDate} onChange={(event) => set('dueDate', event.target.value)} /></Field>
        <label className="flex items-center gap-2 text-sm text-slate-700 sm:col-span-2">
          <input type="checkbox" checked={form.needsLineStop} onChange={(event) => set('needsLineStop', event.target.checked)} />
          Needs the line stopped (do it in the weekly window)
        </label>
      </div>
    </FormDrawer>
  )
}
