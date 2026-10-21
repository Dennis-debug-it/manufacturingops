import { useEffect, useState } from 'react'
import toast from 'react-hot-toast'
import { Plus, Trash2 } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { FormDrawer } from '../../shared/components/FormDrawer'
import { SummarySection } from '../../shared/pages/ModulePage'
import { Spinner } from '../../shared/components/Spinner'
import {
  useProjectSettings,
  useProjectStages,
  useProjectTemplates,
  useProjectTypes,
  useSaveProjectSettings,
  useSaveProjectStage,
  useSaveProjectTemplate,
  useSaveProjectType,
} from './api'
import { stageCategories, stageCategoryLabels, type ProjectSettings, type ProjectStage, type ProjectTemplate, type ProjectTemplateItem, type ProjectType, type StageCategory } from './types'
import { fieldClass, labelClass, primaryButtonClass, secondaryButtonClass, smallButtonClass, textAreaClass, textOrNull } from './components/formStyles'
import { StageBadge } from './components/ProjectBadges'

const featureToggles: Array<{ key: keyof Pick<ProjectSettings, 'milestones' | 'tasks' | 'variations' | 'materials' | 'documents'>; label: string; description: string }> = [
  { key: 'milestones', label: 'Milestones', description: 'Weighted stages that drive progress.' },
  { key: 'tasks', label: 'Tasks', description: 'Assigned work items with due dates.' },
  { key: 'variations', label: 'Variations', description: 'Approved additions and omissions to the contract value.' },
  { key: 'materials', label: 'Materials', description: 'Material issues linked to projects (needs Operations).' },
  { key: 'documents', label: 'Documents', description: 'Drawings, approvals and handover files.' },
]

export function ProjectSettingsPage() {
  const { data: settings, isLoading } = useProjectSettings()
  const saveSettings = useSaveProjectSettings()
  const [draft, setDraft] = useState<ProjectSettings | null>(null)
  const [tradesText, setTradesText] = useState('')

  useEffect(() => {
    if (settings) {
      setDraft(settings)
      setTradesText(settings.trades.join('\n'))
    }
  }, [settings])

  if (isLoading || !draft) return <Spinner label="Loading project settings" />

  const save = async () => {
    try {
      await saveSettings.mutateAsync({ ...draft, trades: tradesText.split('\n').map((t) => t.trim()).filter(Boolean) })
      toast.success('Project settings saved')
    } catch {
      // toast from the API client
    }
  }

  return (
    <div className="space-y-4">
      <PageHeader
        title="Project settings"
        subtitle="Choose what Projects tracks for your business, and how projects are classified."
        breadcrumbs={[{ label: 'Projects', to: '/projects' }, { label: 'Settings' }]}
        primaryAction={<button type="button" className={primaryButtonClass} disabled={saveSettings.isPending} onClick={() => void save()}>Save settings</button>}
      />

      <SummarySection title="Features" description="Turning a feature off hides it. Nothing already recorded is deleted.">
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          {featureToggles.map((feature) => (
            <label key={feature.key} className="flex items-start gap-3 rounded-xl border border-slate-200 px-4 py-3">
              <input type="checkbox" checked={draft[feature.key]} onChange={(event) => setDraft({ ...draft, [feature.key]: event.target.checked })} className="mt-1 h-4 w-4 rounded border-slate-300" />
              <span><span className="block text-sm font-medium text-slate-900">{feature.label}</span><span className="text-xs text-slate-500">{feature.description}</span></span>
            </label>
          ))}
        </div>
      </SummarySection>

      <SummarySection title="Progress, visibility and portal">
        <div className="space-y-3">
          <label className="flex items-start gap-3 text-sm text-slate-700">
            <input type="checkbox" checked={draft.progressRequiresVerification} onChange={(event) => setDraft({ ...draft, progressRequiresVerification: event.target.checked })} className="mt-0.5 h-4 w-4 rounded border-slate-300" />
            <span>A milestone counts towards progress only once it is <strong>verified</strong>, not just completed.</span>
          </label>
          <label className="flex items-start gap-3 text-sm text-slate-700">
            <input type="checkbox" checked={draft.allowManualProgressOverride} onChange={(event) => setDraft({ ...draft, allowManualProgressOverride: event.target.checked })} className="mt-0.5 h-4 w-4 rounded border-slate-300" />
            <span>Allow project managers to override the calculated progress (with a recorded reason).</span>
          </label>
          <label className="flex items-start gap-3 text-sm text-slate-700">
            <input type="checkbox" checked={draft.clientPortal} onChange={(event) => setDraft({ ...draft, clientPortal: event.target.checked })} className="mt-0.5 h-4 w-4 rounded border-slate-300" />
            <span>Show a Projects page in the client portal. Clients see progress, milestones, approved variations and shared documents for their own projects.</span>
          </label>
          <div className="max-w-md">
            <label className={labelClass} htmlFor="project-visibility">Who can see projects</label>
            <select id="project-visibility" value={draft.visibility} onChange={(event) => setDraft({ ...draft, visibility: event.target.value as ProjectSettings['visibility'] })} className={fieldClass}>
              <option value="AllProjects">Everyone with Projects access sees every project</option>
              <option value="MembersOnly">People see only projects they are on (managers see all)</option>
            </select>
          </div>
        </div>
      </SummarySection>

      <SummarySection title="Trades" description="One per line. Used when adding people to a project team.">
        <textarea aria-label="Trades" rows={5} value={tradesText} onChange={(event) => setTradesText(event.target.value)} placeholder={'HVAC\nElectrical\nPlumbing'} className={`${textAreaClass} max-w-md`} />
      </SummarySection>

      <TypesSection />
      <StagesSection />
      <TemplatesSection />
    </div>
  )
}

function TypesSection() {
  const { data: types, isLoading } = useProjectTypes()
  const { data: templates } = useProjectTemplates()
  const saveType = useSaveProjectType()
  const [draft, setDraft] = useState<{ id?: string; name: string; description: string; defaultTemplateId: string; isActive: boolean } | null>(null)

  const save = async () => {
    if (!draft?.name.trim()) return
    try {
      await saveType.mutateAsync({ ...(draft.id ? { id: draft.id } : {}), name: draft.name.trim(), description: textOrNull(draft.description), defaultTemplateId: draft.defaultTemplateId || null, isActive: draft.isActive })
      toast.success('Project type saved')
      setDraft(null)
    } catch {
      // toast from the API client
    }
  }

  const columns: DataTableColumn<ProjectType>[] = [
    { key: 'name', header: 'Type', cell: (row) => (<div><div className="font-medium text-slate-900">{row.name}</div>{row.description ? <div className="text-xs text-slate-500">{row.description}</div> : null}</div>) },
    { key: 'defaultTemplateId', header: 'Default template', cell: (row) => templates?.find((t) => t.id === row.defaultTemplateId)?.name ?? '—' },
    { key: 'isActive', header: 'Status', cell: (row) => (row.isActive ? 'Active' : 'Inactive') },
    { key: 'actions', header: '', cell: (row) => <div className="flex justify-end"><button type="button" className={smallButtonClass} onClick={() => setDraft({ id: row.id, name: row.name, description: row.description ?? '', defaultTemplateId: row.defaultTemplateId ?? '', isActive: row.isActive })}>Edit</button></div> },
  ]

  return (
    <SummarySection title="Project types" description="How you classify projects, e.g. New Installation, Service Project, Fit-out.">
      <div className="mb-3 flex justify-end"><button type="button" className={primaryButtonClass} onClick={() => setDraft({ name: '', description: '', defaultTemplateId: '', isActive: true })}><Plus className="h-4 w-4" />Add type</button></div>
      <DataTable columns={columns} data={types ?? []} loading={isLoading} emptyTitle="No types" emptyDescription="A default type is created automatically." />
      <FormDrawer
        open={draft !== null}
        title={draft?.id ? 'Edit project type' : 'Add project type'}
        onClose={() => setDraft(null)}
        footer={<div className="flex justify-end gap-2"><button type="button" className={secondaryButtonClass} onClick={() => setDraft(null)}>Cancel</button><button type="button" className={primaryButtonClass} disabled={!draft?.name.trim() || saveType.isPending} onClick={() => void save()}>Save</button></div>}
      >
        {draft ? (
          <div className="space-y-4">
            <div><label className={labelClass} htmlFor="type-name">Name</label><input id="type-name" value={draft.name} onChange={(event) => setDraft({ ...draft, name: event.target.value })} className={fieldClass} /></div>
            <div><label className={labelClass} htmlFor="type-description">Description</label><input id="type-description" value={draft.description} onChange={(event) => setDraft({ ...draft, description: event.target.value })} className={fieldClass} /></div>
            <div>
              <label className={labelClass} htmlFor="type-template">Default template</label>
              <select id="type-template" value={draft.defaultTemplateId} onChange={(event) => setDraft({ ...draft, defaultTemplateId: event.target.value })} className={fieldClass}>
                <option value="">None</option>
                {(templates ?? []).map((t) => (<option key={t.id} value={t.id}>{t.name}</option>))}
              </select>
            </div>
            <label className="flex items-center gap-2 text-sm text-slate-700"><input type="checkbox" checked={draft.isActive} onChange={(event) => setDraft({ ...draft, isActive: event.target.checked })} className="h-4 w-4 rounded border-slate-300" />Active</label>
          </div>
        ) : null}
      </FormDrawer>
    </SummarySection>
  )
}

function StagesSection() {
  const { data: stages, isLoading } = useProjectStages()
  const saveStage = useSaveProjectStage()
  const [draft, setDraft] = useState<{ id?: string; name: string; category: StageCategory; sortOrder: string; isActive: boolean } | null>(null)

  const save = async () => {
    if (!draft?.name.trim()) return
    try {
      await saveStage.mutateAsync({ ...(draft.id ? { id: draft.id } : {}), name: draft.name.trim(), category: draft.category, sortOrder: draft.sortOrder ? Number(draft.sortOrder) : null, isActive: draft.isActive })
      toast.success('Stage saved')
      setDraft(null)
    } catch {
      // toast from the API client
    }
  }

  const columns: DataTableColumn<ProjectStage>[] = [
    { key: 'name', header: 'Stage', cell: (row) => <StageBadge name={row.name} category={row.category} /> },
    { key: 'category', header: 'Behaves as', cell: (row) => stageCategoryLabels[row.category] },
    { key: 'sortOrder', header: 'Order', cell: (row) => row.sortOrder },
    { key: 'isActive', header: 'Status', cell: (row) => (row.isActive ? 'Active' : 'Inactive') },
    { key: 'actions', header: '', cell: (row) => <div className="flex justify-end"><button type="button" className={smallButtonClass} onClick={() => setDraft({ id: row.id, name: row.name, category: row.category, sortOrder: String(row.sortOrder), isActive: row.isActive })}>Edit</button></div> },
  ]

  return (
    <SummarySection title="Stages" description="Name the stages your way. Each one behaves as a category (for example, Closed locks the project).">
      <div className="mb-3 flex justify-end"><button type="button" className={primaryButtonClass} onClick={() => setDraft({ name: '', category: 'Active', sortOrder: '', isActive: true })}><Plus className="h-4 w-4" />Add stage</button></div>
      <DataTable columns={columns} data={stages ?? []} loading={isLoading} emptyTitle="No stages" emptyDescription="Default stages are created automatically." />
      <FormDrawer
        open={draft !== null}
        title={draft?.id ? 'Edit stage' : 'Add stage'}
        onClose={() => setDraft(null)}
        footer={<div className="flex justify-end gap-2"><button type="button" className={secondaryButtonClass} onClick={() => setDraft(null)}>Cancel</button><button type="button" className={primaryButtonClass} disabled={!draft?.name.trim() || saveStage.isPending} onClick={() => void save()}>Save</button></div>}
      >
        {draft ? (
          <div className="space-y-4">
            <div><label className={labelClass} htmlFor="stage-name">Name</label><input id="stage-name" value={draft.name} onChange={(event) => setDraft({ ...draft, name: event.target.value })} placeholder="e.g. Snagging" className={fieldClass} /></div>
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
              <div>
                <label className={labelClass} htmlFor="stage-category">Behaves as</label>
                <select id="stage-category" value={draft.category} onChange={(event) => setDraft({ ...draft, category: event.target.value as StageCategory })} className={fieldClass}>
                  {stageCategories.map((c) => (<option key={c} value={c}>{stageCategoryLabels[c]}</option>))}
                </select>
              </div>
              <div><label className={labelClass} htmlFor="stage-order">Order</label><input id="stage-order" type="number" value={draft.sortOrder} onChange={(event) => setDraft({ ...draft, sortOrder: event.target.value })} className={fieldClass} /></div>
            </div>
            <label className="flex items-center gap-2 text-sm text-slate-700"><input type="checkbox" checked={draft.isActive} onChange={(event) => setDraft({ ...draft, isActive: event.target.checked })} className="h-4 w-4 rounded border-slate-300" />Active</label>
          </div>
        ) : null}
      </FormDrawer>
    </SummarySection>
  )
}

interface TemplateDraft {
  id?: string
  name: string
  isActive: boolean
  items: Array<{ name: string; weight: string; offsetDays: string; tasks: string }>
}

const toTemplateDraft = (template: ProjectTemplate): TemplateDraft => ({
  id: template.id,
  name: template.name,
  isActive: template.isActive,
  items: template.items.map((item) => ({ name: item.name, weight: String(item.weight), offsetDays: item.offsetDays != null ? String(item.offsetDays) : '', tasks: (item.tasks ?? []).map((t) => t.title).join('\n') })),
})

function TemplatesSection() {
  const { data: templates, isLoading } = useProjectTemplates()
  const saveTemplate = useSaveProjectTemplate()
  const [draft, setDraft] = useState<TemplateDraft | null>(null)

  const save = async () => {
    if (!draft?.name.trim()) return
    const items: ProjectTemplateItem[] = draft.items.filter((item) => item.name.trim()).map((item) => ({
      name: item.name.trim(),
      weight: item.weight === '' ? 1 : Number(item.weight),
      offsetDays: item.offsetDays === '' ? null : Number(item.offsetDays),
      tasks: item.tasks.split('\n').map((t) => t.trim()).filter(Boolean).map((title) => ({ title, offsetDays: null })),
    }))
    try {
      await saveTemplate.mutateAsync({ ...(draft.id ? { id: draft.id } : {}), name: draft.name.trim(), isActive: draft.isActive, items })
      toast.success('Template saved')
      setDraft(null)
    } catch {
      // toast from the API client
    }
  }

  const updateItem = (index: number, patch: Partial<TemplateDraft['items'][number]>) =>
    setDraft((current) => current ? { ...current, items: current.items.map((item, i) => (i === index ? { ...item, ...patch } : item)) } : current)

  const columns: DataTableColumn<ProjectTemplate>[] = [
    { key: 'name', header: 'Template', cell: (row) => <span className="font-medium text-slate-900">{row.name}</span> },
    { key: 'items', header: 'Milestones', cell: (row) => row.items.map((i) => i.name).join(' → ') || '—' },
    { key: 'isActive', header: 'Status', cell: (row) => (row.isActive ? 'Active' : 'Inactive') },
    { key: 'actions', header: '', cell: (row) => <div className="flex justify-end"><button type="button" className={smallButtonClass} onClick={() => setDraft(toTemplateDraft(row))}>Edit</button></div> },
  ]

  return (
    <SummarySection title="Templates" description="Milestones and tasks copied into a new project. Day offsets count from the planned start.">
      <div className="mb-3 flex justify-end"><button type="button" className={primaryButtonClass} onClick={() => setDraft({ name: '', isActive: true, items: [{ name: '', weight: '1', offsetDays: '', tasks: '' }] })}><Plus className="h-4 w-4" />Add template</button></div>
      <DataTable columns={columns} data={templates ?? []} loading={isLoading} emptyTitle="No templates" emptyDescription="Create one for the kinds of project you repeat." />
      <FormDrawer
        open={draft !== null}
        title={draft?.id ? 'Edit template' : 'Add template'}
        onClose={() => setDraft(null)}
        footer={<div className="flex justify-end gap-2"><button type="button" className={secondaryButtonClass} onClick={() => setDraft(null)}>Cancel</button><button type="button" className={primaryButtonClass} disabled={!draft?.name.trim() || saveTemplate.isPending} onClick={() => void save()}>Save</button></div>}
      >
        {draft ? (
          <div className="space-y-4">
            <div><label className={labelClass} htmlFor="template-name">Name</label><input id="template-name" value={draft.name} onChange={(event) => setDraft({ ...draft, name: event.target.value })} placeholder="e.g. HVAC installation" className={fieldClass} /></div>
            {draft.items.map((item, index) => (
              <div key={index} className="space-y-3 rounded-xl border border-slate-200 p-3">
                <div className="grid grid-cols-1 gap-3 sm:grid-cols-[1fr_6rem_7rem_auto] sm:items-end">
                  <div><label className={labelClass} htmlFor={`template-item-${index}`}>Milestone {index + 1}</label><input id={`template-item-${index}`} value={item.name} onChange={(event) => updateItem(index, { name: event.target.value })} className={fieldClass} /></div>
                  <div><label className={labelClass} htmlFor={`template-weight-${index}`}>Weight</label><input id={`template-weight-${index}`} type="number" min="0" step="0.5" value={item.weight} onChange={(event) => updateItem(index, { weight: event.target.value })} className={fieldClass} /></div>
                  <div><label className={labelClass} htmlFor={`template-offset-${index}`}>Day offset</label><input id={`template-offset-${index}`} type="number" value={item.offsetDays} onChange={(event) => updateItem(index, { offsetDays: event.target.value })} className={fieldClass} /></div>
                  <button type="button" aria-label={`Remove milestone ${index + 1}`} className={secondaryButtonClass} onClick={() => setDraft({ ...draft, items: draft.items.filter((_, i) => i !== index) })}><Trash2 className="h-4 w-4" /></button>
                </div>
                <div><label className={labelClass} htmlFor={`template-tasks-${index}`}>Tasks (one per line)</label><textarea id={`template-tasks-${index}`} rows={2} value={item.tasks} onChange={(event) => updateItem(index, { tasks: event.target.value })} className={textAreaClass} /></div>
              </div>
            ))}
            <button type="button" className={secondaryButtonClass} onClick={() => setDraft({ ...draft, items: [...draft.items, { name: '', weight: '1', offsetDays: '', tasks: '' }] })}><Plus className="h-4 w-4" />Add milestone</button>
            <label className="flex items-center gap-2 text-sm text-slate-700"><input type="checkbox" checked={draft.isActive} onChange={(event) => setDraft({ ...draft, isActive: event.target.checked })} className="h-4 w-4 rounded border-slate-300" />Active</label>
          </div>
        ) : null}
      </FormDrawer>
    </SummarySection>
  )
}
