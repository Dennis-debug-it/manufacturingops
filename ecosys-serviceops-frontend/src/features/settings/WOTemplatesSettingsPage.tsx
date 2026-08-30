import { useEffect, useState } from 'react'
import toast from 'react-hot-toast'
import { ClipboardList, Plus, Trash2 } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { Spinner } from '../../shared/components/Spinner'
import { EmptyState } from '../../shared/components/EmptyState'
import { FormDrawer } from '../../shared/components/FormDrawer'
import { ConfirmDialog } from '../../shared/components/ConfirmDialog'
import { usePermission } from '../../shared/hooks/usePermission'
import { usePmTemplates } from '../maintenance/api'
import { useMaterialOptions } from '../work-orders/pickerApi'
import {
  useCreateWorkOrderTemplate,
  useDeleteWorkOrderTemplate,
  useUpdateWorkOrderTemplate,
  useWorkOrderTemplates,
  type UpsertWorkOrderTemplatePayload,
} from '../work-orders/api'
import { workTypeOptions, priorityOptions } from '../work-orders/schemas'
import type { WorkOrderTemplate } from '../work-orders/types'

const fieldClass = 'h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none transition focus:border-[var(--app-primary)] disabled:bg-slate-50'
const labelClass = 'mb-1.5 block text-sm font-medium text-slate-700'

interface MaterialDraft {
  key: string
  materialId: string
  quantity: number
}

function TemplateEditor({ template, onClose }: { template: WorkOrderTemplate | null; onClose: () => void }) {
  const canManage = usePermission('settings.manage')
  const { data: checklistTemplates } = usePmTemplates()
  const { data: materialOptions } = useMaterialOptions('')
  const create = useCreateWorkOrderTemplate()
  const update = useUpdateWorkOrderTemplate(template?.id ?? '')

  const [name, setName] = useState(template?.name ?? '')
  const [jobType, setJobType] = useState(template?.jobType ?? 'CorrectiveMaintenance')
  const [description, setDescription] = useState(template?.description ?? '')
  const [estimatedDurationMins, setEstimatedDurationMins] = useState<number | ''>(template?.estimatedDurationMins ?? '')
  const [defaultPriority, setDefaultPriority] = useState(template?.defaultPriority ?? 'Medium')
  const [requiredSkillsText, setRequiredSkillsText] = useState((template?.requiredSkills ?? []).join(', '))
  const [checklistTemplateId, setChecklistTemplateId] = useState(template?.checklistTemplateId ?? '')
  const [isActive, setIsActive] = useState(template?.isActive ?? true)
  const [materials, setMaterials] = useState<MaterialDraft[]>(
    (template?.defaultMaterials ?? []).map((m, index) => ({ key: `${m.materialId}-${index}`, materialId: m.materialId, quantity: m.quantity })),
  )

  useEffect(() => {
    if (!template) return
    setName(template.name)
    setJobType(template.jobType)
    setDescription(template.description ?? '')
    setEstimatedDurationMins(template.estimatedDurationMins ?? '')
    setDefaultPriority(template.defaultPriority)
    setRequiredSkillsText(template.requiredSkills.join(', '))
    setChecklistTemplateId(template.checklistTemplateId ?? '')
    setIsActive(template.isActive)
    setMaterials(template.defaultMaterials.map((m, index) => ({ key: `${m.materialId}-${index}`, materialId: m.materialId, quantity: m.quantity })))
  }, [template])

  const addMaterial = () => {
    setMaterials((prev) => [...prev, { key: `new-${prev.length}-${Date.now()}`, materialId: '', quantity: 1 }])
  }

  const removeMaterial = (key: string) => {
    setMaterials((prev) => prev.filter((m) => m.key !== key))
  }

  const save = async () => {
    if (!name.trim()) {
      toast.error('Template name is required')
      return
    }

    const payload: UpsertWorkOrderTemplatePayload = {
      name: name.trim(),
      jobType,
      description: description.trim() || null,
      estimatedDurationMins: estimatedDurationMins === '' ? null : Number(estimatedDurationMins),
      defaultPriority,
      requiredSkills: requiredSkillsText.split(',').map((s) => s.trim()).filter(Boolean),
      checklistTemplateId: checklistTemplateId || null,
      defaultMaterials: materials.filter((m) => m.materialId).map((m) => ({ materialId: m.materialId, quantity: m.quantity })),
      isActive,
    }

    try {
      if (template) {
        await update.mutateAsync(payload)
      } else {
        await create.mutateAsync(payload)
      }
      toast.success('Work order template saved')
      onClose()
    } catch {
      // interceptor surfaces the error toast
    }
  }

  const isPending = create.isPending || update.isPending

  return (
    <FormDrawer
      open
      title={template ? `Edit ${template.name}` : 'New Work Order Template'}
      onClose={onClose}
      footer={
        canManage ? (
          <div className="flex items-center justify-end gap-2">
            <button type="button" onClick={onClose} className="inline-flex h-11 items-center justify-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">Cancel</button>
            <button type="button" onClick={save} disabled={isPending} className="inline-flex h-11 items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:opacity-60">
              {isPending ? 'Saving…' : 'Save'}
            </button>
          </div>
        ) : null
      }
    >
      <div className="space-y-4">
        <div>
          <label className={labelClass} htmlFor="wot-name">Name</label>
          <input id="wot-name" value={name} disabled={!canManage} onChange={(event) => setName(event.target.value)} className={fieldClass} />
        </div>

        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className={labelClass} htmlFor="wot-job-type">Job Type</label>
            <select id="wot-job-type" value={jobType} disabled={!canManage} onChange={(event) => setJobType(event.target.value)} className={fieldClass}>
              {workTypeOptions.map((option) => (
                <option key={option.value} value={option.value}>{option.label}</option>
              ))}
            </select>
          </div>
          <div>
            <label className={labelClass} htmlFor="wot-priority">Default Priority</label>
            <select id="wot-priority" value={defaultPriority} disabled={!canManage} onChange={(event) => setDefaultPriority(event.target.value)} className={fieldClass}>
              {priorityOptions.map((option) => (
                <option key={option.value} value={option.value}>{option.label}</option>
              ))}
            </select>
          </div>
        </div>

        <div>
          <label className={labelClass} htmlFor="wot-duration">Estimated Duration (minutes)</label>
          <input
            id="wot-duration"
            type="number"
            min={0}
            disabled={!canManage}
            value={estimatedDurationMins}
            onChange={(event) => setEstimatedDurationMins(event.target.value === '' ? '' : Number(event.target.value))}
            className={fieldClass}
          />
        </div>

        <div>
          <label className={labelClass} htmlFor="wot-checklist">Checklist Template</label>
          <select id="wot-checklist" value={checklistTemplateId} disabled={!canManage} onChange={(event) => setChecklistTemplateId(event.target.value)} className={fieldClass}>
            <option value="">No checklist</option>
            {(checklistTemplates ?? []).map((t) => (
              <option key={t.id} value={t.id}>{t.name}</option>
            ))}
          </select>
        </div>

        <div>
          <label className={labelClass} htmlFor="wot-skills">Required Skills (comma separated)</label>
          <input id="wot-skills" value={requiredSkillsText} disabled={!canManage} onChange={(event) => setRequiredSkillsText(event.target.value)} placeholder="Electrical, HVAC, Refrigeration" className={fieldClass} />
        </div>

        <div>
          <label className={labelClass} htmlFor="wot-description">Description</label>
          <textarea id="wot-description" value={description} disabled={!canManage} onChange={(event) => setDescription(event.target.value)} rows={3} className={`${fieldClass} h-auto py-2`} />
        </div>

        <div className="border-t border-slate-200 pt-4">
          <div className="flex items-center justify-between">
            <h4 className="text-sm font-semibold text-slate-900">Default Materials</h4>
            {canManage ? (
              <button type="button" onClick={addMaterial} className="inline-flex items-center gap-1 rounded-lg border border-slate-200 bg-white px-2.5 py-1.5 text-xs font-medium text-slate-700 hover:bg-slate-50">
                <Plus className="h-3.5 w-3.5" />Add
              </button>
            ) : null}
          </div>

          {materials.length === 0 ? (
            <p className="mt-2 text-xs text-slate-500">No default materials configured.</p>
          ) : (
            <div className="mt-3 space-y-2">
              {materials.map((material) => (
                <div key={material.key} className="flex items-center gap-2">
                  <select
                    value={material.materialId}
                    disabled={!canManage}
                    onChange={(event) => setMaterials((prev) => prev.map((m) => (m.key === material.key ? { ...m, materialId: event.target.value } : m)))}
                    className={`${fieldClass} flex-1`}
                  >
                    <option value="">Select material</option>
                    {(materialOptions ?? []).map((option) => (
                      <option key={option.id} value={option.id}>{option.itemName}</option>
                    ))}
                  </select>
                  <input
                    type="number"
                    min={0}
                    step={0.01}
                    value={material.quantity}
                    disabled={!canManage}
                    onChange={(event) => setMaterials((prev) => prev.map((m) => (m.key === material.key ? { ...m, quantity: Number(event.target.value) } : m)))}
                    className="h-11 w-24 rounded-lg border border-slate-300 px-2 text-sm outline-none focus:border-[var(--app-primary)]"
                  />
                  {canManage ? (
                    <button type="button" onClick={() => removeMaterial(material.key)} className="rounded p-2 text-slate-400 hover:bg-rose-50 hover:text-rose-600">
                      <Trash2 className="h-4 w-4" />
                    </button>
                  ) : null}
                </div>
              ))}
            </div>
          )}
        </div>

        <label className="flex items-center gap-2 text-sm text-slate-700">
          <input type="checkbox" checked={isActive} disabled={!canManage} onChange={(event) => setIsActive(event.target.checked)} className="h-4 w-4 rounded border-slate-300" />
          Active (available when creating work orders)
        </label>
      </div>
    </FormDrawer>
  )
}

export function WOTemplatesSettingsPage() {
  const canManage = usePermission('settings.manage')
  const [search, setSearch] = useState('')
  const { data, isLoading, isError } = useWorkOrderTemplates(search ? { search } : undefined)
  const deleteTemplate = useDeleteWorkOrderTemplate()
  const [editing, setEditing] = useState<WorkOrderTemplate | null | 'new'>(null)
  const [confirmDelete, setConfirmDelete] = useState<WorkOrderTemplate | null>(null)

  if (isLoading) {
    return <Spinner label="Loading work order templates" />
  }

  if (isError) {
    return <EmptyState icon={ClipboardList} title="Templates unavailable" description="Work order templates could not be loaded." />
  }

  const templates = data ?? []
  const grouped = new Map<string, WorkOrderTemplate[]>()
  for (const template of templates) {
    const bucket = grouped.get(template.jobType) ?? []
    bucket.push(template)
    grouped.set(template.jobType, bucket)
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title="Work Order Templates"
        subtitle="Reusable job definitions technicians can start from when creating a work order."
        primaryAction={
          canManage ? (
            <button type="button" onClick={() => setEditing('new')} className="inline-flex h-11 items-center gap-2 rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)]">
              <Plus className="h-4 w-4" />New Template
            </button>
          ) : undefined
        }
      />

      <div className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 shadow-sm">
        <input
          value={search}
          onChange={(event) => setSearch(event.target.value)}
          placeholder="Search templates…"
          className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]"
        />
      </div>

      {templates.length === 0 ? (
        <EmptyState icon={ClipboardList} title="No templates yet" description="Create a template to speed up recurring work order creation." />
      ) : (
        Array.from(grouped.entries()).map(([type, items]) => (
          <div key={type} className="space-y-3">
            <h3 className="text-sm font-semibold uppercase tracking-[0.18em] text-slate-500">{workTypeOptions.find((o) => o.value === type)?.label ?? type}</h3>
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-3">
              {items.map((template) => (
                <div key={template.id} className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 shadow-sm">
                  <div className="flex items-start justify-between gap-2">
                    <div>
                      <p className="text-sm font-semibold text-slate-900">{template.name}</p>
                      {!template.isActive ? <span className="mt-1 inline-flex items-center rounded-full bg-slate-100 px-2 py-0.5 text-[11px] font-medium text-slate-500">Inactive</span> : null}
                    </div>
                    {canManage ? (
                      <div className="flex items-center gap-2 text-xs">
                        <button type="button" onClick={() => setEditing(template)} className="font-medium text-slate-600 hover:text-slate-900">Edit</button>
                        <button type="button" onClick={() => setConfirmDelete(template)} className="font-medium text-rose-500 hover:text-rose-700">Delete</button>
                      </div>
                    ) : null}
                  </div>
                  {template.description ? <p className="mt-2 line-clamp-2 text-xs text-slate-600">{template.description}</p> : null}
                  <div className="mt-3 flex flex-wrap gap-x-4 gap-y-1 text-xs text-slate-500">
                    <span>Priority: {template.defaultPriority}</span>
                    {template.estimatedDurationMins ? <span>{template.estimatedDurationMins} mins</span> : null}
                    {template.checklistTemplateName ? <span>Checklist: {template.checklistTemplateName}</span> : null}
                  </div>
                </div>
              ))}
            </div>
          </div>
        ))
      )}

      {editing ? <TemplateEditor template={editing === 'new' ? null : editing} onClose={() => setEditing(null)} /> : null}

      <ConfirmDialog
        open={confirmDelete !== null}
        title="Delete Template"
        message={`This removes the "${confirmDelete?.name ?? ''}" template. Work orders already created from it are unaffected.`}
        confirmLabel="Delete Template"
        danger
        onCancel={() => setConfirmDelete(null)}
        onConfirm={async () => {
          if (!confirmDelete) return
          try {
            await deleteTemplate.mutateAsync(confirmDelete.id)
            toast.success('Template deleted')
          } catch {
            // interceptor surfaces the error toast
          }
          setConfirmDelete(null)
        }}
      />
    </div>
  )
}
