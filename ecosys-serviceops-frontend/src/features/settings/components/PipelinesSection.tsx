import { useState } from 'react'
import toast from 'react-hot-toast'
import { ArrowDown, ArrowUp, Plus, X } from 'lucide-react'
import { ConfirmDialog } from '../../../shared/components/ConfirmDialog'
import { useDealFields, useDeletePipeline, usePipelines, useSavePipeline } from '../../crm/api'
import type { SalesPipeline, StageRequirement, UpsertPipelinePayload } from '../../crm/types'

const fieldClass = 'h-10 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]'

const moduleOptions = [
  { value: 'operations', label: 'Operations' },
  { value: 'finance', label: 'Finance' },
  { value: 'maintenance', label: 'Maintenance' },
  { value: 'inventory', label: 'Inventory' },
  { value: 'projects', label: 'Projects' },
  { value: 'advanced', label: 'Advanced' },
  { value: 'enterprise_tools', label: 'Enterprise tools' },
] as const

type EditStage = { key: string | null; name: string; probability: string; dealCount: number; requiredFields: string[] }
type Editing = Omit<UpsertPipelinePayload, 'stages' | 'wonRequiredFields'> & { id?: string; stages: EditStage[]; wonRequiredFields: string[] }

const newPipeline = (): Editing => ({
  name: '',
  module: null,
  isActive: true,
  isDefault: false,
  stages: [
    { key: null, name: 'Lead', probability: '10', dealCount: 0, requiredFields: [] },
    { key: null, name: 'Proposal', probability: '50', dealCount: 0, requiredFields: [] },
    { key: null, name: 'Negotiation', probability: '75', dealCount: 0, requiredFields: [] },
  ],
  wonRequiredFields: [],
})

const toEditing = (pipeline: SalesPipeline): Editing => ({
  id: pipeline.id,
  name: pipeline.name,
  module: pipeline.module,
  isActive: pipeline.isActive,
  isDefault: pipeline.isDefault,
  stages: pipeline.stages.filter((s) => !s.isClosed).map((s) => ({ key: s.key, name: s.name, probability: String(s.probability), dealCount: s.dealCount, requiredFields: s.requiredFields ?? [] })),
  wonRequiredFields: pipeline.stages.find((s) => s.key === 'Won')?.requiredFields ?? [],
})

const dealTotal = (pipeline: SalesPipeline) => pipeline.stages.reduce((sum, s) => sum + s.dealCount, 0)

/**
 * The workspace's sales pipelines. Each has its own open stages and ends in Won and Lost; a pipeline
 * tied to a module only shows when the plan includes that module. Renaming a stage keeps its deals.
 */
export function PipelinesSection() {
  const { data: pipelines } = usePipelines()
  const save = useSavePipeline()
  const remove = useDeletePipeline()
  const { data: dealFields } = useDealFields()
  const requirements = dealFields?.requirements ?? []
  const [editing, setEditing] = useState<Editing | null>(null)
  const [deleting, setDeleting] = useState<SalesPipeline | null>(null)

  const setStage = (index: number, patch: Partial<EditStage>) =>
    editing && setEditing({ ...editing, stages: editing.stages.map((s, i) => (i === index ? { ...s, ...patch } : s)) })
  const moveStage = (index: number, direction: -1 | 1) => {
    if (!editing) return
    const stages = [...editing.stages]
    const [item] = stages.splice(index, 1)
    if (!item) return
    stages.splice(index + direction, 0, item)
    setEditing({ ...editing, stages })
  }

  const submit = async () => {
    if (!editing) return
    if (!editing.name.trim()) return void toast.error('Give the pipeline a name')
    if (editing.stages.length === 0) return void toast.error('Add at least one stage before Won and Lost')
    if (editing.stages.some((s) => !s.name.trim())) return void toast.error('Every stage needs a name')
    const { id, stages, ...rest } = editing
    const payload: UpsertPipelinePayload = {
      ...rest,
      name: rest.name.trim(),
      stages: stages.map((s) => ({ key: s.key, name: s.name.trim(), probability: Math.min(100, Math.max(0, Number(s.probability) || 0)), requiredFields: s.requiredFields })),
    }
    try {
      await save.mutateAsync({ id, payload })
      toast.success('Pipeline saved')
      setEditing(null)
    } catch {
      // interceptor surfaces the error toast
    }
  }

  return (
    <section className="space-y-3 rounded-[var(--radius-card)] border border-slate-200 bg-white p-4">
      <div className="flex items-start justify-between gap-3">
        <div>
          <h2 className="text-sm font-semibold text-slate-900">Pipelines</h2>
          <p className="mt-1 text-sm text-slate-500">
            Separate pipelines for different kinds of sale, each with its own stages and win chance per stage. Every pipeline ends in Won and Lost.
          </p>
        </div>
        {!editing ? (
          <button type="button" onClick={() => setEditing(newPipeline())} className="inline-flex h-9 shrink-0 items-center gap-1.5 rounded-lg border border-slate-200 px-3 text-sm font-medium text-slate-700 hover:bg-slate-50">
            <Plus className="h-4 w-4" />Add pipeline
          </button>
        ) : null}
      </div>

      {editing ? (
        <div className="space-y-3 rounded-xl border border-slate-200 p-3">
          <div className="grid gap-3 md:grid-cols-2">
            <input aria-label="Pipeline name" placeholder="Pipeline name, e.g. Maintenance contracts" value={editing.name} onChange={(event) => setEditing({ ...editing, name: event.target.value })} className={fieldClass} />
            <select aria-label="Module" value={editing.module ?? ''} onChange={(event) => setEditing({ ...editing, module: event.target.value || null })} className={fieldClass}>
              <option value="">Every plan</option>
              {moduleOptions.map((option) => <option key={option.value} value={option.value}>Only with {option.label}</option>)}
            </select>
            <label className="flex items-center gap-2 text-sm text-slate-700">
              <input type="checkbox" checked={editing.isActive} disabled={editing.isDefault && Boolean(editing.id)} onChange={(event) => setEditing({ ...editing, isActive: event.target.checked })} />
              Pipeline is on
            </label>
            <label className="flex items-center gap-2 text-sm text-slate-700">
              <input type="checkbox" checked={editing.isDefault} onChange={(event) => setEditing({ ...editing, isDefault: event.target.checked, isActive: event.target.checked || editing.isActive })} />
              Default (new deals, renewals and imports land here unless told otherwise)
            </label>
          </div>

          <div>
            <div className="mb-1 grid grid-cols-[1fr_6rem_5.5rem] gap-2 text-xs font-medium text-slate-500">
              <span>Stage</span>
              <span>Win chance %</span>
              <span />
            </div>
            <ol className="space-y-2">
              {editing.stages.map((stage, index) => (
                <li key={stage.key ?? `new-${index}`} className="space-y-1">
                  <div className="grid grid-cols-[1fr_6rem_5.5rem] items-center gap-2">
                    <input aria-label={`Stage ${index + 1} name`} value={stage.name} onChange={(event) => setStage(index, { name: event.target.value })} className={fieldClass} />
                    <input aria-label={`Stage ${index + 1} win chance`} type="number" min="0" max="100" value={stage.probability} onChange={(event) => setStage(index, { probability: event.target.value })} className={fieldClass} />
                    <div className="flex items-center">
                      <button type="button" aria-label="Move stage up" disabled={index === 0} onClick={() => moveStage(index, -1)} className="rounded p-1 text-slate-500 hover:bg-slate-100 disabled:opacity-30"><ArrowUp className="h-4 w-4" /></button>
                      <button type="button" aria-label="Move stage down" disabled={index === editing.stages.length - 1} onClick={() => moveStage(index, 1)} className="rounded p-1 text-slate-500 hover:bg-slate-100 disabled:opacity-30"><ArrowDown className="h-4 w-4" /></button>
                      <button
                        type="button"
                        aria-label="Remove stage"
                        disabled={stage.dealCount > 0}
                        title={stage.dealCount > 0 ? `${stage.dealCount} deal${stage.dealCount === 1 ? ' is' : 's are'} here; move ${stage.dealCount === 1 ? 'it' : 'them'} first` : undefined}
                        onClick={() => setEditing({ ...editing, stages: editing.stages.filter((_, i) => i !== index) })}
                        className="rounded p-1 text-rose-500 hover:bg-rose-50 disabled:opacity-30"
                      >
                        <X className="h-4 w-4" />
                      </button>
                    </div>
                  </div>
                  <RequirementPicker stageName={stage.name || `stage ${index + 1}`} options={requirements} value={stage.requiredFields} onChange={(requiredFields) => setStage(index, { requiredFields })} />
                </li>
              ))}
            </ol>
            <button type="button" onClick={() => setEditing({ ...editing, stages: [...editing.stages, { key: null, name: '', probability: '50', dealCount: 0, requiredFields: [] }] })} className="mt-2 inline-flex h-9 items-center gap-1.5 rounded-lg border border-dashed border-slate-300 px-3 text-sm font-medium text-slate-600 hover:bg-slate-50">
              <Plus className="h-4 w-4" />Add stage
            </button>
            <div className="mt-3 space-y-1 rounded-lg bg-slate-50 px-3 py-2">
              <div className="text-sm font-medium text-slate-700">Won <span className="font-normal text-slate-500">(100%)</span></div>
              <RequirementPicker stageName="Won" options={requirements} value={editing.wonRequiredFields} onChange={(wonRequiredFields) => setEditing({ ...editing, wonRequiredFields })} />
              <div className="text-sm font-medium text-slate-700">Lost <span className="font-normal text-slate-500">(0%, always open to move to, with a reason)</span></div>
            </div>
            <p className="mt-2 text-xs text-slate-500">
              What a stage needs also applies to every stage after it, so skipping ahead does not skip the check. A stage that still has deals cannot be removed.
            </p>
          </div>

          <div className="flex gap-2">
            <button type="button" disabled={save.isPending} onClick={() => void submit()} className="inline-flex h-10 items-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white disabled:opacity-60">Save pipeline</button>
            <button type="button" onClick={() => setEditing(null)} className="inline-flex h-10 items-center rounded-lg border border-slate-200 px-4 text-sm font-medium text-slate-700">Cancel</button>
          </div>
        </div>
      ) : null}

      <ul className="divide-y divide-slate-100">
        {(pipelines ?? []).map((pipeline) => (
          <li key={pipeline.id} className="flex flex-col gap-2 py-3 sm:flex-row sm:items-center sm:justify-between">
            <div className="min-w-0 text-sm">
              <div className="font-medium text-slate-900">
                {pipeline.name}
                {pipeline.isDefault ? <span className="ml-2 rounded-full bg-slate-100 px-2 py-0.5 text-xs font-normal text-slate-600">Default</span> : null}
                {!pipeline.isActive ? <span className="ml-2 text-xs font-normal text-slate-400">(off)</span> : null}
                {pipeline.module ? (
                  <span className={pipeline.available ? 'ml-2 text-xs font-normal text-slate-500' : 'ml-2 text-xs font-normal text-amber-700'}>
                    {pipeline.available ? `with ${moduleOptions.find((m) => m.value === pipeline.module)?.label ?? pipeline.module}` : `hidden: plan has no ${moduleOptions.find((m) => m.value === pipeline.module)?.label ?? pipeline.module}`}
                  </span>
                ) : null}
              </div>
              <div className="text-slate-500">
                {pipeline.stages.map((s) => s.name).join(' → ')} · {dealTotal(pipeline)} deal{dealTotal(pipeline) === 1 ? '' : 's'}
              </div>
            </div>
            <div className="flex shrink-0 items-center gap-3">
              <button type="button" onClick={() => setEditing(toEditing(pipeline))} className="text-xs font-medium text-slate-600 hover:underline">Edit</button>
              {!pipeline.isDefault ? (
                <button type="button" disabled={remove.isPending} onClick={() => setDeleting(pipeline)} className="text-xs font-medium text-rose-600 hover:underline">Delete</button>
              ) : null}
            </div>
          </li>
        ))}
      </ul>

      <ConfirmDialog
        open={Boolean(deleting)}
        title="Delete pipeline"
        message={deleting ? (dealTotal(deleting) > 0 ? `${deleting.name} still has deals. Move them to another pipeline first, or switch it off instead.` : `Delete the ${deleting.name} pipeline?`) : ''}
        confirmLabel="Delete"
        danger
        onCancel={() => setDeleting(null)}
        onConfirm={() => {
          if (!deleting) return
          remove.mutate(deleting.id, { onSuccess: () => toast.success('Pipeline deleted') })
          setDeleting(null)
        }}
      />
    </section>
  )
}

/** Picks what a deal needs before entering a stage; shows the choice as a sentence. */
function RequirementPicker({ stageName, options, value, onChange }: { stageName: string; options: StageRequirement[]; value: string[]; onChange: (value: string[]) => void }) {
  const labelOf = (key: string) => options.find((x) => x.key === key)?.label ?? key.replace(/^cf:/, '')
  return (
    <details className="text-xs">
      <summary className="cursor-pointer list-none text-slate-500 hover:text-slate-700">
        {value.length ? <>Needs before {stageName}: <span className="text-slate-700">{value.map(labelOf).join(', ')}</span></> : <>Nothing required to enter {stageName} · <span className="underline">add a requirement</span></>}
      </summary>
      <div className="mt-1 flex flex-wrap gap-x-4 gap-y-1 rounded-lg border border-slate-200 bg-white p-2">
        {options.map((option) => (
          <label key={option.key} className="flex items-center gap-1.5 text-sm text-slate-700">
            <input
              type="checkbox"
              checked={value.includes(option.key)}
              onChange={(event) => onChange(event.target.checked ? [...value, option.key] : value.filter((x) => x !== option.key))}
            />
            {option.label}
          </label>
        ))}
      </div>
    </details>
  )
}
