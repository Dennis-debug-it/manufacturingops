import { useEffect, useMemo, useState } from 'react'
import toast from 'react-hot-toast'
import { ChevronRight, Factory, FolderTree, Layers, Plus, Sparkles } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { FormDrawer } from '../../shared/components/FormDrawer'
import { EmptyState } from '../../shared/components/EmptyState'
import { Badge } from '../../shared/components/Badge'
import { usePermission } from '../../shared/hooks/usePermission'
import { cn } from '../../shared/utils/cn'
import {
  useApplySectionTemplate,
  useFactorySettings,
  useIndustryTemplates,
  useOrgNodes,
  useSaveOrgNode,
  useSetOrgNodeActive,
  useUpdateFactorySettings,
} from './api'
import { buildTree, childLevels, type TreeNode } from './tree'
import { Field, inputClass, primaryButton, secondaryButton, textareaClass } from './components/ui'
import { LEVEL_LABELS, type FactorySettings, type OrgNode, type OrgNodeLevel, type UpdateFactorySettingsPayload } from './types'

const DAYS = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday']
const MONTHS = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December']

const LEVEL_ICON: Record<OrgNodeLevel, typeof Factory> = { Group: FolderTree, Factory, Section: Layers, SubSection: ChevronRight }

interface EditorState {
  node?: OrgNode
  parent: OrgNode | null
}

/** The group → factory → section → sub-section tree, plus the tenant's factory settings. */
export function OrganisationSettingsPage() {
  const canManage = usePermission('settings.manage')
  const [showInactive, setShowInactive] = useState(false)
  const { data: nodes, isLoading } = useOrgNodes(showInactive)
  const { data: settings } = useFactorySettings()
  const [editor, setEditor] = useState<EditorState | null>(null)
  const tree = useMemo(() => buildTree(nodes ?? []), [nodes])

  return (
    <div className="space-y-6">
      <PageHeader
        title="Organisation"
        subtitle="Groups, factories, sections and sub-sections. Assets, people and reports are organised by this tree."
        breadcrumbs={[{ label: 'Settings', to: '/settings' }, { label: 'Organisation' }]}
        primaryAction={canManage ? (
          <button type="button" className={primaryButton} onClick={() => setEditor({ parent: null })}>
            <Plus className="h-4 w-4" />{settings?.useGroupLevel ? 'Add group or factory' : 'Add factory'}
          </button>
        ) : undefined}
      />

      <section className="rounded-[var(--radius-card)] border border-slate-200 bg-white p-4 shadow-sm">
        <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
          <h2 className="text-base font-semibold text-slate-900">Structure</h2>
          <label className="inline-flex items-center gap-2 text-sm text-slate-600">
            <input type="checkbox" checked={showInactive} onChange={(event) => setShowInactive(event.target.checked)} />
            Show inactive
          </label>
        </div>
        {isLoading ? <p className="text-sm text-slate-500">Loading…</p> : tree.length === 0 ? (
          <EmptyState
            icon={Factory}
            title="No factories yet"
            description="Add your first factory, then add its sections from your industry's template or by hand."
            actionLabel={canManage ? 'Add factory' : undefined}
            onAction={canManage ? () => setEditor({ parent: null }) : undefined}
          />
        ) : (
          <ul className="divide-y divide-slate-100">
            {tree.map((node) => (
              <TreeRow key={node.id} node={node} canManage={canManage} settings={settings} onEdit={(target, parent) => setEditor({ node: target, parent })} onAdd={(parent) => setEditor({ parent })} />
            ))}
          </ul>
        )}
      </section>

      {settings ? <SettingsForm settings={settings} canManage={canManage} /> : null}

      {editor ? (
        <NodeEditor
          state={editor}
          settings={settings}
          onClose={() => setEditor(null)}
        />
      ) : null}
    </div>
  )
}

function TreeRow({ node, canManage, settings, onEdit, onAdd }: {
  node: TreeNode
  canManage: boolean
  settings: FactorySettings | undefined
  onEdit: (node: OrgNode, parent: OrgNode | null) => void
  onAdd: (parent: OrgNode) => void
}) {
  const applyTemplate = useApplySectionTemplate()
  const Icon = LEVEL_ICON[node.level]
  const nextLevels = settings ? childLevels(node, settings) : []
  const nextLevel = nextLevels[nextLevels.length - 1]
  const canAddChild = canManage && node.canAccess && node.isActive && nextLevel !== undefined

  const addDefaults = async () => {
    try {
      const result = await applyTemplate.mutateAsync({ factoryId: node.id })
      toast.success(result.created === 0 ? 'All the default sections are already there.' : `Added ${result.created} section${result.created === 1 ? '' : 's'}.`)
    } catch {
      // The API client shows the error.
    }
  }

  return (
    <li>
      <div className={cn('flex flex-wrap items-center gap-2 py-2', !node.isActive && 'opacity-60')} style={{ paddingLeft: `${node.depth * 1.5}rem` }}>
        <Icon className="h-4 w-4 shrink-0 text-slate-400" aria-hidden />
        <span className="font-medium text-slate-900">{node.name}</span>
        <span className="font-mono text-xs text-slate-500">{node.code}</span>
        <Badge size="sm">{LEVEL_LABELS[node.level]}</Badge>
        {node.level === 'Factory' && node.templateKey && node.templateKey !== settings?.industryTemplate ? <Badge size="sm" tone="info"><IndustryName industryKey={node.templateKey} /></Badge> : null}
        {!node.isActive ? <Badge size="sm" tone="warning">Inactive</Badge> : null}
        {node.assetCount > 0 ? <span className="text-xs text-slate-500">{node.assetCount} asset{node.assetCount === 1 ? '' : 's'}</span> : null}
        <span className="ml-auto flex flex-wrap gap-1">
          {canManage && node.level === 'Factory' && node.canAccess && node.isActive ? (
            <button type="button" className="inline-flex h-8 items-center gap-1 rounded-md px-2 text-xs font-medium text-slate-600 hover:bg-slate-100" onClick={() => void addDefaults()} disabled={applyTemplate.isPending}>
              <Sparkles className="h-3.5 w-3.5" />Add default sections
            </button>
          ) : null}
          {canAddChild ? (
            <button type="button" className="inline-flex h-8 items-center gap-1 rounded-md px-2 text-xs font-medium text-slate-600 hover:bg-slate-100" onClick={() => onAdd(node)}>
              <Plus className="h-3.5 w-3.5" />Add {nextLevel ? LEVEL_LABELS[nextLevel].toLowerCase() : ''}
            </button>
          ) : null}
          {canManage && node.canAccess ? (
            <button type="button" className="inline-flex h-8 items-center rounded-md px-2 text-xs font-medium text-[var(--app-primary)] hover:bg-slate-100" onClick={() => onEdit(node, null)}>
              Edit
            </button>
          ) : null}
        </span>
      </div>
      {node.children.length > 0 ? (
        <ul>
          {node.children.map((child) => (
            <TreeRow key={child.id} node={child} canManage={canManage} settings={settings} onEdit={(target) => onEdit(target, node)} onAdd={onAdd} />
          ))}
        </ul>
      ) : null}
    </li>
  )
}

function NodeEditor({ state, settings, onClose }: { state: EditorState; settings: FactorySettings | undefined; onClose: () => void }) {
  const { node, parent } = state
  const save = useSaveOrgNode()
  const setActive = useSetOrgNodeActive()
  const levels = node ? [node.level] : childLevels(parent, settings ?? { useGroupLevel: true, useSubSectionLevel: true })
  const [form, setForm] = useState({
    level: node?.level ?? levels[levels.length - 1] ?? 'Factory',
    name: node?.name ?? '',
    code: node?.code ?? (parent && levels.includes('Section') ? `${parent.code}-` : ''),
    description: node?.description ?? '',
    location: node?.location ?? '',
    displayOrder: node?.displayOrder ?? 0,
    externalId: node?.externalId ?? '',
    externalSystem: node?.externalSystem ?? '',
    industry: node?.templateKey && node.templateKey !== settings?.industryTemplate ? node.templateKey : '',
  })
  const { data: templates } = useIndustryTemplates()
  const workspaceIndustry = templates?.find((x) => x.key === settings?.industryTemplate)
  const set = <K extends keyof typeof form>(key: K, value: (typeof form)[K]) => setForm((current) => ({ ...current, [key]: value }))

  const submit = async () => {
    try {
      await save.mutateAsync({
        id: node?.id,
        payload: {
          parentId: node ? node.parentId : parent?.id ?? null,
          level: form.level,
          name: form.name,
          code: form.code,
          description: form.description || null,
          location: form.location || null,
          displayOrder: form.displayOrder,
          externalId: form.externalId || null,
          externalSystem: form.externalSystem || null,
          // Blank means the workspace's industry.
          industry: form.level === 'Factory' ? form.industry : null,
        },
      })
      toast.success(node ? 'Saved' : `${LEVEL_LABELS[form.level]} added`)
      onClose()
    } catch {
      // The API client shows the error.
    }
  }

  const toggleActive = async () => {
    if (!node) return
    try {
      await setActive.mutateAsync({ id: node.id, active: !node.isActive })
      toast.success(node.isActive ? 'Deactivated' : 'Reactivated')
      onClose()
    } catch {
      // The API client shows the error (for example, active assets still located here).
    }
  }

  return (
    <FormDrawer
      open
      title={node ? `Edit ${LEVEL_LABELS[node.level].toLowerCase()}` : parent ? `Add to ${parent.name}` : 'Add to the organisation'}
      onClose={onClose}
      footer={
        <div className="flex flex-wrap justify-between gap-2">
          {node ? (
            <button type="button" className={secondaryButton} onClick={() => void toggleActive()} disabled={setActive.isPending}>
              {node.isActive ? 'Deactivate' : 'Reactivate'}
            </button>
          ) : <span />}
          <div className="flex gap-2">
            <button type="button" className={secondaryButton} onClick={onClose}>Cancel</button>
            <button type="button" className={primaryButton} onClick={() => void submit()} disabled={save.isPending || !form.name.trim() || !form.code.trim()}>Save</button>
          </div>
        </div>
      }
    >
      <div className="grid gap-4 sm:grid-cols-2">
        {levels.length > 1 ? (
          <Field label="Level" required>
            <select className={inputClass} value={form.level} onChange={(event) => set('level', event.target.value as OrgNodeLevel)}>
              {levels.map((level) => <option key={level} value={level}>{LEVEL_LABELS[level]}</option>)}
            </select>
          </Field>
        ) : null}
        <Field label="Name" required><input className={inputClass} value={form.name} onChange={(event) => set('name', event.target.value)} /></Field>
        <Field label="Code" required hint="Short and unique, e.g. CHL or CHL-DRY. Used in imports and on labels.">
          <input className={cn(inputClass, 'font-mono uppercase')} value={form.code} onChange={(event) => set('code', event.target.value.toUpperCase())} />
        </Field>
        <Field label="Display order"><input type="number" className={inputClass} value={form.displayOrder} onChange={(event) => set('displayOrder', Number(event.target.value))} /></Field>
        {form.level === 'Factory' ? (
          <Field label="Industry" className="sm:col-span-2" hint="For a group with different kinds of factory. Sets its default sections, checks, statutory items, zones and checkpoints.">
            <select className={inputClass} value={form.industry} onChange={(event) => set('industry', event.target.value)}>
              <option value="">Same as the workspace{workspaceIndustry ? ` (${workspaceIndustry.name})` : ''}</option>
              {(templates ?? []).filter((x) => x.key !== settings?.industryTemplate).map((x) => <option key={x.key} value={x.key}>{x.name}</option>)}
            </select>
          </Field>
        ) : null}
        <Field label="Location" className="sm:col-span-2" hint="Town or address, for factories."><input className={inputClass} value={form.location} onChange={(event) => set('location', event.target.value)} /></Field>
        <Field label="Description" className="sm:col-span-2"><textarea className={textareaClass} value={form.description} onChange={(event) => set('description', event.target.value)} /></Field>
        <Field label="ERP ID" hint="This place's ID in your ERP, if any."><input className={inputClass} value={form.externalId} onChange={(event) => set('externalId', event.target.value)} /></Field>
        <Field label="ERP system"><input className={inputClass} value={form.externalSystem} onChange={(event) => set('externalSystem', event.target.value)} /></Field>
      </div>
    </FormDrawer>
  )
}

function SettingsForm({ settings, canManage }: { settings: FactorySettings; canManage: boolean }) {
  const update = useUpdateFactorySettings()
  const { data: templates } = useIndustryTemplates()
  const [form, setForm] = useState<UpdateFactorySettingsPayload>(settings)
  useEffect(() => setForm(settings), [settings])
  const set = <K extends keyof UpdateFactorySettingsPayload>(key: K, value: UpdateFactorySettingsPayload[K]) => setForm((current) => ({ ...current, [key]: value }))
  const chosen = templates?.find((x) => x.key === form.industryTemplate)
  const vocabulary = chosen?.vocabulary ?? settings.industry?.vocabulary
  // Choosing another industry brings its production unit and quiet season; they stay editable.
  const chooseIndustry = (key: string) => {
    const pack = templates?.find((x) => x.key === key)
    setForm((current) => pack && key !== current.industryTemplate ? {
      ...current, industryTemplate: key, productionUnit: pack.productionUnit, productionUnitShort: pack.productionUnitShort,
      lowCropStartMonth: pack.lowSeasonStartMonth ?? current.lowCropStartMonth, lowCropEndMonth: pack.lowSeasonEndMonth ?? current.lowCropEndMonth,
    } : { ...current, industryTemplate: key })
  }

  const submit = async () => {
    try {
      await update.mutateAsync(form)
      toast.success('Factory settings saved')
    } catch {
      // The API client shows the error.
    }
  }

  return (
    <section className="space-y-4 rounded-[var(--radius-card)] border border-slate-200 bg-white p-4 shadow-sm">
      <div>
        <h2 className="text-base font-semibold text-slate-900">Factory settings</h2>
        <p className="text-sm text-slate-500">Apply to every factory unless a factory sets its own.</p>
      </div>
      <fieldset disabled={!canManage} className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        <Field label="Industry" hint="A factory can have its own (Edit the factory). Existing data is not changed.">
          <select className={inputClass} value={form.industryTemplate} onChange={(event) => chooseIndustry(event.target.value)}>
            {(templates ?? []).map((template) => <option key={template.key} value={template.key}>{template.name}</option>)}
          </select>
        </Field>
        {chosen ? (
          <div className="rounded-lg bg-slate-50 p-3 text-xs text-slate-600 sm:col-span-2 lg:col-span-3">
            <p className="text-sm text-slate-700">{chosen.description}</p>
            <p className="mt-1">
              Its template brings {chosen.sections.length} sections, {chosen.assetClasses} asset classes, {chosen.runningChecks} shift checks, {chosen.windowRoutines} weekly-window routines,
              {' '}{chosen.statutoryItems} statutory items, {chosen.zones} visitor zones and {chosen.checkpoints} patrol checkpoints. Each is added when you use it (Add default sections,
              the asset class pack, compliance, zones, checkpoints) and can be changed after.
            </p>
          </div>
        ) : null}
        <Field label="Production unit" hint={`Used for downtime cost and KPIs, e.g. ${chosen?.productionUnit ?? 'kg made tea'}.`}>
          <input className={inputClass} value={form.productionUnit} onChange={(event) => set('productionUnit', event.target.value)} />
        </Field>
        <Field label="Short form"><input className={inputClass} value={form.productionUnitShort} onChange={(event) => set('productionUnitShort', event.target.value)} /></Field>
        <Field label="Weekly maintenance window">
          <select className={inputClass} value={form.weeklyWindowEnabled ? form.weeklyWindowDay : ''} onChange={(event) => event.target.value ? setForm((current) => ({ ...current, weeklyWindowEnabled: true, weeklyWindowDay: event.target.value })) : set('weeklyWindowEnabled', false)}>
            <option value="">No weekly window</option>
            {DAYS.map((day) => <option key={day} value={day}>{day}</option>)}
          </select>
        </Field>
        <Field label="Window starts"><input type="time" className={inputClass} value={form.weeklyWindowStart} disabled={!form.weeklyWindowEnabled} onChange={(event) => set('weeklyWindowStart', event.target.value)} /></Field>
        <Field label="Window ends"><input type="time" className={inputClass} value={form.weeklyWindowEnd} disabled={!form.weeklyWindowEnabled} onChange={(event) => set('weeklyWindowEnd', event.target.value)} /></Field>
        <Field label={`${vocabulary?.lowSeason ?? 'Low-crop season'} starts`} hint="When the annual overhaul usually happens.">
          <select className={inputClass} value={form.lowCropStartMonth ?? ''} onChange={(event) => set('lowCropStartMonth', event.target.value ? Number(event.target.value) : null)}>
            <option value="">Not set</option>
            {MONTHS.map((month, index) => <option key={month} value={index + 1}>{month}</option>)}
          </select>
        </Field>
        <Field label={`${vocabulary?.lowSeason ?? 'Low-crop season'} ends`}>
          <select className={inputClass} value={form.lowCropEndMonth ?? ''} onChange={(event) => set('lowCropEndMonth', event.target.value ? Number(event.target.value) : null)}>
            <option value="">Not set</option>
            {MONTHS.map((month, index) => <option key={month} value={index + 1}>{month}</option>)}
          </select>
        </Field>
        <Field label="Tag number prefix" hint={`Next tag: ${form.tagPrefix}-${String(settings.nextTagNumber).padStart(6, '0')}`}>
          <input className={cn(inputClass, 'font-mono uppercase')} maxLength={10} value={form.tagPrefix} onChange={(event) => set('tagPrefix', event.target.value.toUpperCase())} />
        </Field>
        <fieldset className="space-y-2 rounded-lg border border-slate-200 p-3 sm:col-span-2 lg:col-span-3">
          <legend className="px-1 text-sm font-semibold text-slate-900">Jobs</legend>
          <label className="flex items-center gap-2 text-sm text-slate-700">
            <input type="checkbox" checked={form.requireWorkOrderApproval} onChange={(event) => set('requireWorkOrderApproval', event.target.checked)} />
            New jobs need a supervisor's approval before they are planned
          </label>
          <label className="flex items-center gap-2 text-sm text-slate-700">
            <input type="checkbox" checked={form.autoApproveBreakdowns} disabled={!form.requireWorkOrderApproval} onChange={(event) => set('autoApproveBreakdowns', event.target.checked)} />
            Breakdowns skip approval (the machine is already down)
          </label>
          <label className="flex items-center gap-2 text-sm text-slate-700">
            <input type="checkbox" checked={form.closeJobsOnVerify} onChange={(event) => set('closeJobsOnVerify', event.target.checked)} />
            Verifying a job also closes it (one step instead of two)
          </label>
        </fieldset>
        <label className="inline-flex items-center gap-2 text-sm text-slate-700">
          <input type="checkbox" checked={form.useGroupLevel} onChange={(event) => set('useGroupLevel', event.target.checked)} />
          Use groups (several factories under one group)
        </label>
        <label className="inline-flex items-center gap-2 text-sm text-slate-700">
          <input type="checkbox" checked={form.useSubSectionLevel} onChange={(event) => set('useSubSectionLevel', event.target.checked)} />
          Use sub-sections
        </label>
      </fieldset>
      {canManage ? (
        <div className="flex justify-end">
          <button type="button" className={primaryButton} onClick={() => void submit()} disabled={update.isPending}>Save settings</button>
        </div>
      ) : null}
    </section>
  )
}

function IndustryName({ industryKey }: { industryKey: string }) {
  const { data: templates } = useIndustryTemplates()
  return <>{templates?.find((x) => x.key === industryKey)?.name ?? industryKey}</>
}
