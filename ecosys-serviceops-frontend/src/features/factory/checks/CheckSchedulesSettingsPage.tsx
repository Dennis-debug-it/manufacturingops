import { useEffect, useState } from 'react'
import toast from 'react-hot-toast'
import { Plus, Route as RouteIcon } from 'lucide-react'
import { PageHeader } from '../../../shared/components/PageHeader'
import { FormDrawer } from '../../../shared/components/FormDrawer'
import { Spinner } from '../../../shared/components/Spinner'
import { EmptyState } from '../../../shared/components/EmptyState'
import { Badge } from '../../../shared/components/Badge'
import { Field, LocationSelect, inputClass, primaryButton, secondaryButton } from '../components/ui'
import { useFactoryAssets } from '../api'
import { FREQUENCY_LABELS, useCheckSchedules, useCheckTemplates, useSaveCheckSchedule, type CheckSchedule } from './api'

/** Settings › Running checks: which checklist is done on which machines, and how often. */
export function CheckSchedulesSettingsPage() {
  const [includeInactive, setIncludeInactive] = useState(false)
  const schedules = useCheckSchedules(includeInactive)
  const [editing, setEditing] = useState<CheckSchedule | 'new' | null>(null)

  return (
    <div className="space-y-4">
      <PageHeader
        title="Running checks"
        subtitle="Shift and daily checklists. Each machine gets its own check every period; a route is walked in order."
        primaryAction={<button type="button" className={primaryButton} onClick={() => setEditing('new')}><Plus className="h-4 w-4" />New schedule</button>}
      />
      <label className="inline-flex items-center gap-2 text-sm text-slate-600"><input type="checkbox" checked={includeInactive} onChange={(event) => setIncludeInactive(event.target.checked)} />Show paused</label>

      {schedules.isLoading ? <Spinner /> : (schedules.data ?? []).length === 0 ? (
        <EmptyState title="No check schedules" description="Pick a checklist, a section and its machines to start." actionLabel="New schedule" onAction={() => setEditing('new')} />
      ) : (
        <div className="overflow-x-auto rounded-[var(--radius-card)] border border-slate-200 bg-white shadow-sm">
          <table className="w-full text-sm">
            <thead className="bg-slate-50 text-left text-xs font-medium text-slate-500">
              <tr><th className="px-4 py-2">Schedule</th><th className="px-4 py-2">Checklist</th><th className="px-4 py-2">How often</th><th className="px-4 py-2">Where</th><th className="px-4 py-2 text-right">Machines</th></tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {(schedules.data ?? []).map((row) => (
                <tr key={row.id} className="cursor-pointer hover:bg-slate-50" onClick={() => setEditing(row)}>
                  <td className="px-4 py-2 font-medium text-slate-900">
                    <span className="inline-flex items-center gap-1.5">{row.isRoute ? <RouteIcon className="h-4 w-4 text-slate-400" aria-label="Route" /> : null}{row.name}</span>
                    {!row.isActive ? <Badge size="sm" className="ml-2">Paused</Badge> : null}
                  </td>
                  <td className="px-4 py-2 text-slate-600">{row.templateName}</td>
                  <td className="px-4 py-2 text-slate-600">{FREQUENCY_LABELS[row.frequency] ?? row.frequency}</td>
                  <td className="px-4 py-2 text-slate-600">{row.orgNodeName ?? '—'}</td>
                  <td className="px-4 py-2 text-right tabular-nums">{row.assets.length}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <ScheduleDrawer schedule={editing} onClose={() => setEditing(null)} />
    </div>
  )
}

function ScheduleDrawer({ schedule, onClose }: { schedule: CheckSchedule | 'new' | null; onClose: () => void }) {
  const templates = useCheckTemplates()
  const save = useSaveCheckSchedule()
  const existing = schedule && schedule !== 'new' ? schedule : null
  const [form, setForm] = useState({ name: '', templateId: '', frequency: 'PerShift', orgNodeId: '', isRoute: false, isActive: true })
  const [assetIds, setAssetIds] = useState<string[]>([])
  const assets = useFactoryAssets({ orgNodeId: form.orgNodeId || undefined, status: 'active' }, 1, 200)

  useEffect(() => {
    if (!schedule) return
    setForm(existing
      ? { name: existing.name, templateId: existing.templateId, frequency: existing.frequency, orgNodeId: existing.orgNodeId, isRoute: existing.isRoute, isActive: existing.isActive }
      : { name: '', templateId: '', frequency: 'PerShift', orgNodeId: '', isRoute: false, isActive: true })
    setAssetIds(existing ? existing.assets.map((x) => x.assetId) : [])
  }, [schedule, existing])

  const template = (templates.data ?? []).find((x) => x.id === form.templateId)
  const candidates = (assets.data?.items ?? []).filter((x) => !template?.assetCategoryId || x.assetCategoryId === template.assetCategoryId)

  const chooseTemplate = (id: string) => {
    const chosen = (templates.data ?? []).find((x) => x.id === id)
    setForm((current) => ({ ...current, templateId: id, name: current.name || chosen?.name || '', frequency: chosen?.suggestedFrequency ?? current.frequency }))
  }

  const submit = async () => {
    try {
      await save.mutateAsync({ id: existing?.id, ...form, assetIds, isActive: existing ? form.isActive : undefined })
      toast.success('Schedule saved')
      onClose()
    } catch {
      // The API client shows the error.
    }
  }

  const move = (index: number, by: number) => setAssetIds((current) => {
    const next = [...current]
    const [item] = next.splice(index, 1)
    if (item) next.splice(index + by, 0, item)
    return next
  })

  const label = (id: string) => {
    const fromList = (assets.data?.items ?? []).find((x) => x.id === id)
    if (fromList) return `${fromList.assetCode} · ${fromList.assetName}`
    const fromSchedule = existing?.assets.find((x) => x.assetId === id)
    return fromSchedule ? `${fromSchedule.assetCode} · ${fromSchedule.assetName}` : id
  }

  return (
    <FormDrawer open={schedule !== null} title={existing ? existing.name : 'New check schedule'} onClose={onClose} footer={
      <div className="flex justify-end gap-2">
        <button type="button" className={secondaryButton} onClick={onClose}>Cancel</button>
        <button type="button" className={primaryButton} disabled={save.isPending || !form.name.trim() || !form.templateId || !form.orgNodeId || assetIds.length === 0} onClick={() => void submit()}>Save</button>
      </div>
    }>
      <div className="grid gap-4">
        <Field label="Checklist" required hint="Edit the questions in Settings › Checklists.">
          <select className={inputClass} value={form.templateId} onChange={(event) => chooseTemplate(event.target.value)}>
            <option value="">Choose a checklist</option>
            {(templates.data ?? []).map((x) => <option key={x.id} value={x.id}>{x.name} ({x.questionCount} questions)</option>)}
          </select>
        </Field>
        <Field label="Name" required><input className={inputClass} value={form.name} onChange={(event) => setForm({ ...form, name: event.target.value })} /></Field>
        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="How often">
            <select className={inputClass} value={form.frequency} onChange={(event) => setForm({ ...form, frequency: event.target.value })}>
              {Object.entries(FREQUENCY_LABELS).map(([value, text]) => <option key={value} value={value}>{text}</option>)}
            </select>
          </Field>
          <Field label="Section" required><LocationSelect value={form.orgNodeId} onChange={(value) => setForm({ ...form, orgNodeId: value })} includeGroups /></Field>
        </div>
        <label className="flex items-center gap-2 text-sm text-slate-700"><input type="checkbox" checked={form.isRoute} onChange={(event) => setForm({ ...form, isRoute: event.target.checked })} />A route: the machines are checked in this order</label>
        {existing ? <label className="flex items-center gap-2 text-sm text-slate-700"><input type="checkbox" checked={form.isActive} onChange={(event) => setForm({ ...form, isActive: event.target.checked })} />Active</label> : null}

        <Field label="Machines" required hint={template?.assetCategoryName ? `Only ${template.assetCategoryName} assets in this section.` : undefined}>
          {form.orgNodeId ? (
            <div className="max-h-56 space-y-1 overflow-y-auto rounded-lg border border-slate-200 p-2">
              {candidates.length === 0 ? <p className="text-sm text-slate-500">No matching assets here.</p> : candidates.map((asset) => (
                <label key={asset.id} className="flex items-center gap-2 text-sm">
                  <input type="checkbox" checked={assetIds.includes(asset.id)} onChange={(event) => setAssetIds(event.target.checked ? [...assetIds, asset.id] : assetIds.filter((x) => x !== asset.id))} />
                  <span className="font-mono text-xs text-slate-500">{asset.assetCode}</span>{asset.assetName}
                </label>
              ))}
            </div>
          ) : <p className="text-sm text-slate-500">Choose a section first.</p>}
        </Field>

        {form.isRoute && assetIds.length > 1 ? (
          <Field label="Route order">
            <ol className="space-y-1 rounded-lg border border-slate-200 p-2 text-sm">
              {assetIds.map((id, index) => (
                <li key={id} className="flex items-center justify-between gap-2">
                  <span><span className="mr-2 tabular-nums text-slate-400">{index + 1}.</span>{label(id)}</span>
                  <span className="flex gap-1">
                    <button type="button" className="rounded px-2 text-slate-500 hover:bg-slate-100 disabled:opacity-30" disabled={index === 0} onClick={() => move(index, -1)} aria-label="Move up">↑</button>
                    <button type="button" className="rounded px-2 text-slate-500 hover:bg-slate-100 disabled:opacity-30" disabled={index === assetIds.length - 1} onClick={() => move(index, 1)} aria-label="Move down">↓</button>
                  </span>
                </li>
              ))}
            </ol>
          </Field>
        ) : null}
      </div>
    </FormDrawer>
  )
}
