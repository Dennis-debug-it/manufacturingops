import { useState } from 'react'
import toast from 'react-hot-toast'
import { ArrowDown, ArrowUp, Plus } from 'lucide-react'
import { ConfirmDialog } from '../../../shared/components/ConfirmDialog'
import { useDealFields, useDeleteDealField, usePipelines, useSaveDealField } from '../../crm/api'
import { DEAL_FIELD_TYPES, type DealField, type DealFieldType, type UpsertDealFieldPayload } from '../../crm/types'

const fieldClass = 'h-10 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]'

const typeLabels: Record<DealFieldType, string> = {
  Text: 'Text',
  Number: 'Number',
  Date: 'Date',
  Dropdown: 'Dropdown (pick one)',
  Checkbox: 'Yes / no',
}

type Editing = Omit<UpsertDealFieldPayload, 'options'> & { id?: string; options: string; valueCount: number }

const newField = (): Editing => ({ label: '', fieldType: 'Text', options: '', helpText: null, pipelineId: null, isActive: true, valueCount: 0 })

const toPayload = (field: DealField, patch: Partial<UpsertDealFieldPayload> = {}): UpsertDealFieldPayload => ({
  label: field.label, fieldType: field.fieldType, options: field.options, helpText: field.helpText, pipelineId: field.pipelineId, isActive: field.isActive, ...patch,
})

/**
 * Fields the workspace adds to its deals. They show on the deal form and page, in the deals export and
 * import (a column per field), and stages can require them.
 */
export function DealFieldsSection() {
  const { data } = useDealFields()
  const { data: pipelines } = usePipelines()
  const save = useSaveDealField()
  const remove = useDeleteDealField()
  const [editing, setEditing] = useState<Editing | null>(null)
  const [deleting, setDeleting] = useState<DealField | null>(null)
  const fields = data?.fields ?? []

  const submit = async () => {
    if (!editing) return
    if (!editing.label.trim()) return void toast.error('Give the field a label')
    const options = editing.options.split('\n').map((x) => x.trim()).filter(Boolean)
    if (editing.fieldType === 'Dropdown' && options.length === 0) return void toast.error('List the choices, one per line')
    const { id, valueCount: _valueCount, ...rest } = editing
    try {
      await save.mutateAsync({ id, payload: { ...rest, label: rest.label.trim(), helpText: rest.helpText?.trim() || null, options: editing.fieldType === 'Dropdown' ? options : null } })
      toast.success('Field saved')
      setEditing(null)
    } catch {
      // interceptor surfaces the error toast
    }
  }

  const move = (field: DealField, direction: -1 | 1) => {
    const index = fields.findIndex((x) => x.id === field.id)
    const other = fields[index + direction]
    if (!other) return
    void save.mutateAsync({ id: field.id, payload: toPayload(field, { sortOrder: other.sortOrder }) })
      .then(() => save.mutateAsync({ id: other.id, payload: toPayload(other, { sortOrder: field.sortOrder }) }))
  }

  return (
    <section className="space-y-3 rounded-[var(--radius-card)] border border-slate-200 bg-white p-4">
      <div className="flex items-start justify-between gap-3">
        <div>
          <h2 className="text-sm font-semibold text-slate-900">Deal fields</h2>
          <p className="mt-1 text-sm text-slate-500">Extra details your team records on deals, e.g. building size or decision maker. They also appear in exports and imports, and stages can require them.</p>
        </div>
        {!editing ? (
          <button type="button" onClick={() => setEditing(newField())} className="inline-flex h-9 shrink-0 items-center gap-1.5 rounded-lg border border-slate-200 px-3 text-sm font-medium text-slate-700 hover:bg-slate-50">
            <Plus className="h-4 w-4" />Add field
          </button>
        ) : null}
      </div>

      {editing ? (
        <div className="grid gap-3 rounded-xl border border-slate-200 p-3 md:grid-cols-2">
          <input aria-label="Field label" placeholder="Label, e.g. Building size (m²)" value={editing.label} onChange={(event) => setEditing({ ...editing, label: event.target.value })} className={fieldClass} />
          <select
            aria-label="Field type"
            value={editing.fieldType}
            disabled={editing.valueCount > 0}
            title={editing.valueCount > 0 ? 'Deals already have values for this field, so its type is fixed' : undefined}
            onChange={(event) => setEditing({ ...editing, fieldType: event.target.value as DealFieldType })}
            className={fieldClass}
          >
            {DEAL_FIELD_TYPES.map((type) => <option key={type} value={type}>{typeLabels[type]}</option>)}
          </select>
          {editing.fieldType === 'Dropdown' ? (
            <textarea aria-label="Choices" placeholder={'Choices, one per line\nFlat\nPitched'} rows={4} value={editing.options} onChange={(event) => setEditing({ ...editing, options: event.target.value })} className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[var(--app-primary)] md:col-span-2" />
          ) : null}
          <input aria-label="Help text" placeholder="Help text shown under the field (optional)" value={editing.helpText ?? ''} onChange={(event) => setEditing({ ...editing, helpText: event.target.value })} className={fieldClass} />
          <select aria-label="Pipeline" value={editing.pipelineId ?? ''} onChange={(event) => setEditing({ ...editing, pipelineId: event.target.value || null })} className={fieldClass}>
            <option value="">On deals in every pipeline</option>
            {(pipelines ?? []).map((p) => <option key={p.id} value={p.id}>Only in {p.name}</option>)}
          </select>
          <label className="flex items-center gap-2 text-sm text-slate-700">
            <input type="checkbox" checked={editing.isActive} onChange={(event) => setEditing({ ...editing, isActive: event.target.checked })} />
            Field is on
          </label>
          <div className="flex gap-2 md:col-span-2">
            <button type="button" disabled={save.isPending} onClick={() => void submit()} className="inline-flex h-10 items-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white disabled:opacity-60">Save field</button>
            <button type="button" onClick={() => setEditing(null)} className="inline-flex h-10 items-center rounded-lg border border-slate-200 px-4 text-sm font-medium text-slate-700">Cancel</button>
          </div>
        </div>
      ) : null}

      {data && fields.length === 0 && !editing ? <p className="text-sm text-slate-500">No extra fields yet.</p> : null}
      <ul className="divide-y divide-slate-100">
        {fields.map((field, index) => (
          <li key={field.id} className="flex flex-col gap-2 py-3 sm:flex-row sm:items-center sm:justify-between">
            <div className="min-w-0 text-sm">
              <div className="font-medium text-slate-900">
                {field.label} {field.isActive ? null : <span className="text-xs font-normal text-slate-400">(off)</span>}
              </div>
              <div className="text-slate-500">
                {typeLabels[field.fieldType]}
                {field.fieldType === 'Dropdown' ? `: ${field.options.join(', ')}` : ''}
                {field.pipelineId ? ` · only in ${pipelines?.find((p) => p.id === field.pipelineId)?.name ?? 'one pipeline'}` : ''}
                {` · ${field.valueCount} deal${field.valueCount === 1 ? '' : 's'}`}
              </div>
            </div>
            <div className="flex shrink-0 items-center gap-2">
              <button type="button" aria-label="Move up" disabled={index === 0 || save.isPending} onClick={() => move(field, -1)} className="rounded p-1 text-slate-500 hover:bg-slate-100 disabled:opacity-30"><ArrowUp className="h-4 w-4" /></button>
              <button type="button" aria-label="Move down" disabled={index === fields.length - 1 || save.isPending} onClick={() => move(field, 1)} className="rounded p-1 text-slate-500 hover:bg-slate-100 disabled:opacity-30"><ArrowDown className="h-4 w-4" /></button>
              <button type="button" onClick={() => setEditing({ ...toPayload(field), id: field.id, options: field.options.join('\n'), valueCount: field.valueCount })} className="text-xs font-medium text-slate-600 hover:underline">Edit</button>
              {field.valueCount === 0 ? (
                <button type="button" disabled={remove.isPending} onClick={() => setDeleting(field)} className="text-xs font-medium text-rose-600 hover:underline">Delete</button>
              ) : null}
            </div>
          </li>
        ))}
      </ul>

      <ConfirmDialog
        open={Boolean(deleting)}
        title="Delete field"
        message={deleting ? `Delete ${deleting.label}? Stages that require it stop doing so.` : ''}
        confirmLabel="Delete"
        danger
        onCancel={() => setDeleting(null)}
        onConfirm={() => {
          if (!deleting) return
          remove.mutate(deleting.id, { onSuccess: () => toast.success('Field deleted') })
          setDeleting(null)
        }}
      />
    </section>
  )
}
