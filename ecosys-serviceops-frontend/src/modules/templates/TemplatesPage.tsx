import { ClipboardList, Copy, PackageCheck, Plus, Power, PowerOff, Trash2 } from 'lucide-react'
import { useMemo, useState } from 'react'
import { Badge } from '../../components/ui/Badge'
import { ConfirmDialog } from '../../components/ui/ConfirmDialog'
import { EmptyState } from '../../components/ui/EmptyState'
import { ErrorState } from '../../components/ui/ErrorState'
import { LoadingState } from '../../components/ui/LoadingState'
import { Modal } from '../../components/ui/Modal'
import { useToast } from '../../components/ui/ToastProvider'
import { MetricCard, MetricGrid, PageScaffold, SectionCard, SegmentedTabs } from '../../components/ui/Workspace'
import { useAsyncData } from '../../hooks/useAsyncData'
import { assetCategoryService, type AssetCategoryFieldRecord, type AssetCategoryRecord, type UpsertAssetCategoryFieldInput } from '../../services/assetCategoryService'
import { pmTemplateService } from '../../services/pmTemplateService'
import { starterPackService, type StarterPackRecord } from '../../services/starterPackService'
import type { PmTemplateRecord, UpsertPmTemplateInput } from '../../types/api'

type TemplatesPayload = {
  starterPacks: StarterPackRecord[]
  assetTemplates: AssetCategoryRecord[]
  serviceChecklists: PmTemplateRecord[]
}

type TemplatesTab = 'packs' | 'asset-templates' | 'service-checklists'

type AssetTemplateFieldDraft = {
  id?: string
  fieldLabel: string
  fieldType: string
  dropdownOptions: string
  unit: string
  helpText: string
  isRequired: boolean
  isActive: boolean
  displayOrder: number
}

type AssetTemplateDraft = {
  name: string
  code: string
  description: string
  defaultServiceIntervalMonths: string
  fields: AssetTemplateFieldDraft[]
}

const checklistAnswerTypes: { value: string; label: string }[] = [
  { value: 'text', label: 'Short text' },
  { value: 'multilinetext', label: 'Multi-line text' },
  { value: 'number', label: 'Number' },
  { value: 'reading', label: 'Meter reading' },
  { value: 'date', label: 'Date' },
  { value: 'yesno', label: 'Yes / No' },
  { value: 'passfail', label: 'Pass / Fail' },
  { value: 'condition', label: 'Condition rating' },
  { value: 'checkbox', label: 'Checkbox' },
  { value: 'dropdown', label: 'Dropdown' },
  { value: 'photo', label: 'Photo capture' },
  { value: 'signature', label: 'Signature' },
]

const assetFieldTypes = ['Text', 'Number', 'Date', 'Dropdown', 'Boolean', 'MultilineText'] as const

const emptyPayload: TemplatesPayload = {
  starterPacks: [],
  assetTemplates: [],
  serviceChecklists: [],
}

const emptyAssetTemplateDraft = (): AssetTemplateDraft => ({
  name: '',
  code: '',
  description: '',
  defaultServiceIntervalMonths: '',
  fields: [
    {
      fieldLabel: '',
      fieldType: 'Text',
      dropdownOptions: '',
      unit: '',
      helpText: '',
      isRequired: false,
      isActive: true,
      displayOrder: 1,
    },
  ],
})

const emptyChecklistDraft = (): UpsertPmTemplateInput => ({
  name: '',
  category: '',
  description: '',
  code: '',
  workType: 'PreventiveMaintenance',
  defaultIntervalMonths: null,
  assetCategoryId: null,
  checklist: [
    {
      sectionName: '',
      sectionOrder: 1,
      question: '',
      helperText: '',
      type: 'text',
      unit: '',
      minValue: null,
      maxValue: null,
      defaultValue: '',
      required: true,
      evidenceRequired: false,
      technicianRemarksAllowed: true,
      requiresNoteOnFail: false,
      order: 1,
      options: [],
    },
  ],
  isActive: true,
})

export function TemplatesPage() {
  const { pushToast } = useToast()
  const [activeTab, setActiveTab] = useState<TemplatesTab>('packs')
  const [installingPackId, setInstallingPackId] = useState<string | null>(null)

  const [assetEditorOpen, setAssetEditorOpen] = useState(false)
  const [editingAssetTemplate, setEditingAssetTemplate] = useState<AssetCategoryRecord | null>(null)
  const [assetDraft, setAssetDraft] = useState<AssetTemplateDraft>(emptyAssetTemplateDraft)
  const [assetSaving, setAssetSaving] = useState(false)
  const [assetSaveError, setAssetSaveError] = useState('')
  const [disableAssetTarget, setDisableAssetTarget] = useState<AssetCategoryRecord | null>(null)

  const [checklistEditorOpen, setChecklistEditorOpen] = useState(false)
  const [editingChecklist, setEditingChecklist] = useState<PmTemplateRecord | null>(null)
  const [checklistDraft, setChecklistDraft] = useState<UpsertPmTemplateInput>(emptyChecklistDraft)
  const [checklistOriginal, setChecklistOriginal] = useState<string>('')
  const [checklistSaving, setChecklistSaving] = useState(false)
  const [checklistSaveError, setChecklistSaveError] = useState('')
  const [deleteChecklistTarget, setDeleteChecklistTarget] = useState<PmTemplateRecord | null>(null)
  const [deletingChecklist, setDeletingChecklist] = useState(false)
  const [discardChecklistOpen, setDiscardChecklistOpen] = useState(false)

  const checklistIsDirty = JSON.stringify(checklistDraft) !== checklistOriginal

  const { data, loading, error, reload } = useAsyncData<TemplatesPayload>(
    async (signal) => {
      const [starterPacks, assetTemplates, serviceChecklists] = await Promise.all([
        starterPackService.list(signal),
        assetCategoryService.list(signal, true),
        pmTemplateService.list(signal),
      ])
      return { starterPacks, assetTemplates, serviceChecklists }
    },
    emptyPayload,
    [],
  )

  const checklistCategories = useMemo(
    () =>
      [...new Set(
        data.serviceChecklists
          .map((item) => item.category?.trim())
          .filter((item): item is string => Boolean(item)),
      )].sort((left, right) => left.localeCompare(right)),
    [data.serviceChecklists],
  )

  const assetTemplateCount = data.assetTemplates.length
  const activeAssetTemplateCount = data.assetTemplates.filter((item) => item.isActive).length
  const checklistCount = data.serviceChecklists.length
  const activeChecklistCount = data.serviceChecklists.filter((item) => item.isActive).length
  const installedPackCount = data.starterPacks.filter((item) => item.isInstalled).length

  const sortedAssetTemplates = useMemo(
    () => [...data.assetTemplates].sort((left, right) => left.displayOrder - right.displayOrder || left.name.localeCompare(right.name)),
    [data.assetTemplates],
  )

  const groupedChecklists = useMemo(() => {
    const groups = new Map<string, PmTemplateRecord[]>()
    for (const template of data.serviceChecklists) {
      const groupKey = (template.workType || 'PreventiveMaintenance') === 'BreakdownCorrective'
        ? 'Breakdown / Corrective Maintenance'
        : 'Preventive Maintenance'
      const current = groups.get(groupKey) ?? []
      current.push(template)
      groups.set(groupKey, current)
    }

    return [...groups.entries()].map(([label, templates]) => ({
      label,
      templates: templates.sort((left, right) => left.category.localeCompare(right.category) || left.name.localeCompare(right.name)),
    }))
  }, [data.serviceChecklists])

  const checklistDraftValid = checklistDraft.name.trim().length > 0
    && checklistDraft.category.trim().length > 0
    && checklistDraft.checklist.length > 0
    && checklistDraft.checklist.every((item) => item.question.trim().length > 0 && (!requiresOptions(item.type) || (item.options?.length ?? 0) > 0))

  function openAssetTemplateEditor(template?: AssetCategoryRecord) {
    setEditingAssetTemplate(template ?? null)
    setAssetSaveError('')
    setAssetDraft(template
      ? {
          name: template.name,
          code: template.code ?? '',
          description: template.description ?? '',
          defaultServiceIntervalMonths: template.defaultServiceIntervalMonths ? String(template.defaultServiceIntervalMonths) : '',
          fields: template.fields
            .slice()
            .sort((left, right) => left.displayOrder - right.displayOrder)
            .map((field) => ({
              id: field.id,
              fieldLabel: field.fieldLabel,
              fieldType: mapAssetFieldType(field),
              dropdownOptions: field.dropdownOptions ?? '',
              unit: field.unit ?? '',
              helpText: field.helpText ?? '',
              isRequired: field.isRequired,
              isActive: field.isActive ?? true,
              displayOrder: field.displayOrder,
            })),
        }
      : emptyAssetTemplateDraft())
    setAssetEditorOpen(true)
  }

  function openChecklistEditor(template?: PmTemplateRecord) {
    setEditingChecklist(template ?? null)
    setChecklistSaveError('')
    setChecklistDraft(template
      ? {
          name: template.name,
          category: template.category,
          description: template.description ?? '',
          code: template.code ?? '',
          workType: template.workType || 'PreventiveMaintenance',
          defaultIntervalMonths: template.defaultIntervalMonths ?? null,
          assetCategoryId: template.assetCategoryId ?? null,
          checklist: template.checklist.map((item) => ({
            sectionName: item.sectionName ?? '',
            sectionOrder: item.sectionOrder ?? 1,
            question: item.question,
            helperText: item.helperText ?? '',
            type: item.type,
            unit: item.unit ?? '',
            minValue: item.minValue ?? null,
            maxValue: item.maxValue ?? null,
            defaultValue: item.defaultValue ?? '',
            required: item.required,
            evidenceRequired: item.evidenceRequired ?? false,
            technicianRemarksAllowed: item.technicianRemarksAllowed ?? true,
            requiresNoteOnFail: item.requiresNoteOnFail ?? false,
            order: item.order,
            options: item.options ?? [],
          })),
          isActive: template.isActive,
        }
      : emptyChecklistDraft())
    const initial = template
      ? {
          name: template.name,
          category: template.category,
          description: template.description ?? '',
          code: template.code ?? '',
          workType: template.workType || 'PreventiveMaintenance',
          defaultIntervalMonths: template.defaultIntervalMonths ?? null,
          assetCategoryId: template.assetCategoryId ?? null,
          checklist: template.checklist.map((item) => ({
            sectionName: item.sectionName ?? '',
            sectionOrder: item.sectionOrder ?? 1,
            question: item.question,
            helperText: item.helperText ?? '',
            type: item.type,
            unit: item.unit ?? '',
            minValue: item.minValue ?? null,
            maxValue: item.maxValue ?? null,
            defaultValue: item.defaultValue ?? '',
            required: item.required,
            evidenceRequired: item.evidenceRequired ?? false,
            technicianRemarksAllowed: item.technicianRemarksAllowed ?? true,
            requiresNoteOnFail: item.requiresNoteOnFail ?? false,
            order: item.order,
            options: item.options ?? [],
          })),
          isActive: template.isActive,
        }
      : emptyChecklistDraft()
    setChecklistOriginal(JSON.stringify(initial))
    setChecklistEditorOpen(true)
  }

  async function installStarterPack(pack: StarterPackRecord) {
    if (installingPackId) return
    setInstallingPackId(pack.id)
    try {
      await starterPackService.install(pack.id)
      pushToast({ title: 'Starter pack installed', description: `${pack.name} is now available in this workspace.`, tone: 'success' })
      await reload()
      setActiveTab('asset-templates')
    } catch (nextError) {
      pushToast({ title: 'Install failed', description: nextError instanceof Error ? nextError.message : 'Unable to install the starter pack.', tone: 'danger' })
    } finally {
      setInstallingPackId(null)
    }
  }

  async function saveAssetTemplate() {
    if (!assetDraft.name.trim()) {
      setAssetSaveError('Template name is required.')
      return
    }

    const remainingFields = assetDraft.fields.filter((field) => field.fieldLabel.trim().length > 0)
    const normalizedKeys = remainingFields.map((field) => normalizeFieldKey(field.fieldLabel))
    if (new Set(normalizedKeys).size !== normalizedKeys.length) {
      setAssetSaveError('Field labels must be unique within the asset template.')
      return
    }

    setAssetSaving(true)
    setAssetSaveError('')
    try {
      const payload = {
        name: assetDraft.name.trim(),
        code: assetDraft.code.trim() || null,
        description: assetDraft.description.trim() || null,
        parentCategoryName: null,
        icon: 'tool',
        displayOrder: editingAssetTemplate?.displayOrder ?? data.assetTemplates.length + 1,
        defaultServiceIntervalMonths: assetDraft.defaultServiceIntervalMonths ? Number(assetDraft.defaultServiceIntervalMonths) : null,
      }

      const savedTemplate = editingAssetTemplate
        ? await assetCategoryService.update(editingAssetTemplate.id, payload)
        : await assetCategoryService.create(payload)

      const existingFieldIds = new Set((editingAssetTemplate?.fields ?? []).map((field) => field.id))
      const keptFieldIds = new Set<string>()

      for (const [index, field] of remainingFields.entries()) {
        const fieldPayload: UpsertAssetCategoryFieldInput = {
          fieldName: normalizeFieldKey(field.fieldLabel),
          fieldLabel: field.fieldLabel.trim(),
          fieldType: field.fieldType,
          dropdownOptions: field.fieldType === 'Dropdown'
            ? field.dropdownOptions.split(',').map((item) => item.trim()).filter(Boolean).join(', ')
            : null,
          unit: field.unit.trim() || null,
          helpText: field.helpText.trim() || null,
          isRequired: field.isRequired,
          isActive: field.isActive,
          displayOrder: index + 1,
        }

        if (field.id) {
          keptFieldIds.add(field.id)
          await assetCategoryService.updateField(savedTemplate.id, field.id, fieldPayload)
        } else {
          await assetCategoryService.addField(savedTemplate.id, fieldPayload)
        }
      }

      for (const fieldId of existingFieldIds) {
        if (!keptFieldIds.has(fieldId)) {
          await assetCategoryService.deleteField(savedTemplate.id, fieldId)
        }
      }

      pushToast({
        title: editingAssetTemplate ? 'Asset template updated' : 'Asset template created',
        description: 'The asset template is ready for asset capture and editing.',
        tone: 'success',
      })
      setAssetEditorOpen(false)
      setEditingAssetTemplate(null)
      setAssetDraft(emptyAssetTemplateDraft())
      await reload()
    } catch (nextError) {
      setAssetSaveError(nextError instanceof Error ? nextError.message : 'Unable to save the asset template.')
    } finally {
      setAssetSaving(false)
    }
  }

  async function disableAssetTemplate() {
    if (!disableAssetTarget) return
    try {
      await assetCategoryService.deactivate(disableAssetTarget.id)
      pushToast({ title: 'Asset template disabled', description: `${disableAssetTarget.name} is no longer available for new asset selection.`, tone: 'success' })
      setDisableAssetTarget(null)
      await reload()
    } catch (nextError) {
      pushToast({ title: 'Disable failed', description: nextError instanceof Error ? nextError.message : 'Unable to disable the asset template.', tone: 'danger' })
    }
  }

  async function saveChecklist() {
    if (!checklistDraftValid || checklistSaving) return
    setChecklistSaving(true)
    setChecklistSaveError('')

    const payload: UpsertPmTemplateInput = {
      ...checklistDraft,
      name: checklistDraft.name.trim(),
      category: checklistDraft.category.trim(),
      description: checklistDraft.description?.trim() || null,
      code: checklistDraft.code?.trim() || null,
      checklist: checklistDraft.checklist.map((item, index) => ({
        sectionName: item.sectionName?.trim() || null,
        sectionOrder: item.sectionOrder ?? index + 1,
        question: item.question.trim(),
        helperText: item.helperText?.trim() || null,
        type: item.type,
        unit: item.unit?.trim() || null,
        minValue: item.minValue ?? null,
        maxValue: item.maxValue ?? null,
        defaultValue: item.defaultValue?.trim() || null,
        required: item.required,
        evidenceRequired: item.evidenceRequired ?? false,
        technicianRemarksAllowed: item.technicianRemarksAllowed ?? true,
        requiresNoteOnFail: item.requiresNoteOnFail ?? false,
        order: index + 1,
        options: requiresOptions(item.type) ? (item.options ?? []).map((option) => option.trim()).filter(Boolean) : [],
      })),
    }

    try {
      if (editingChecklist) {
        await pmTemplateService.update(editingChecklist.id, payload)
        pushToast({ title: 'Service checklist updated', description: 'Checklist changes were saved.', tone: 'success' })
      } else {
        await pmTemplateService.create(payload)
        pushToast({ title: 'Service checklist created', description: 'Checklist is now available for new work orders.', tone: 'success' })
      }

      setChecklistEditorOpen(false)
      setEditingChecklist(null)
      setChecklistDraft(emptyChecklistDraft())
      setChecklistOriginal('')
      await reload()
    } catch (nextError) {
      setChecklistSaveError(nextError instanceof Error ? nextError.message : 'Unable to save the checklist.')
    } finally {
      setChecklistSaving(false)
    }
  }

  async function duplicateChecklist(template: PmTemplateRecord) {
    try {
      await pmTemplateService.duplicate(template.id, `${template.name} Copy`)
      pushToast({ title: 'Checklist duplicated', description: 'A new editable copy was created.', tone: 'success' })
      await reload()
    } catch (nextError) {
      pushToast({ title: 'Duplicate failed', description: nextError instanceof Error ? nextError.message : 'Unable to duplicate the checklist.', tone: 'danger' })
    }
  }

  async function setChecklistActive(template: PmTemplateRecord, isActive: boolean) {
    try {
      if (isActive) {
        await pmTemplateService.activate(template.id)
      } else {
        await pmTemplateService.deactivate(template.id)
      }
      pushToast({ title: isActive ? 'Checklist activated' : 'Checklist disabled', description: 'Checklist status was updated.', tone: 'success' })
      await reload()
    } catch (nextError) {
      pushToast({ title: 'Status update failed', description: nextError instanceof Error ? nextError.message : 'Unable to update the checklist.', tone: 'danger' })
    }
  }

  async function deleteChecklist() {
    if (!deleteChecklistTarget || deletingChecklist) return
    setDeletingChecklist(true)
    try {
      await pmTemplateService.remove(deleteChecklistTarget.id)
      pushToast({ title: 'Checklist removed', description: 'The service checklist has been removed.', tone: 'success' })
      setDeleteChecklistTarget(null)
      await reload()
    } catch (nextError) {
      pushToast({ title: 'Delete failed', description: nextError instanceof Error ? nextError.message : 'Unable to delete the checklist.', tone: 'danger' })
    } finally {
      setDeletingChecklist(false)
    }
  }

  function updateAssetField(index: number, updater: (field: AssetTemplateFieldDraft) => AssetTemplateFieldDraft) {
    setAssetDraft((current) => ({
      ...current,
      fields: current.fields.map((field, fieldIndex) => fieldIndex === index ? updater(field) : field),
    }))
  }

  function moveAssetField(index: number, direction: -1 | 1) {
    setAssetDraft((current) => {
      const nextIndex = index + direction
      if (nextIndex < 0 || nextIndex >= current.fields.length) {
        return current
      }

      const fields = [...current.fields]
      const [moved] = fields.splice(index, 1)
      fields.splice(nextIndex, 0, moved)
      return {
        ...current,
        fields: fields.map((field, fieldIndex) => ({ ...field, displayOrder: fieldIndex + 1 })),
      }
    })
  }

  function updateChecklistItem(index: number, updater: (item: UpsertPmTemplateInput['checklist'][number]) => UpsertPmTemplateInput['checklist'][number]) {
    setChecklistDraft((current) => ({
      ...current,
      checklist: current.checklist.map((item, itemIndex) => itemIndex === index ? updater(item) : item),
    }))
  }

  function moveChecklistItem(index: number, direction: -1 | 1) {
    setChecklistDraft((current) => {
      const nextIndex = index + direction
      if (nextIndex < 0 || nextIndex >= current.checklist.length) {
        return current
      }

      const checklist = [...current.checklist]
      const [moved] = checklist.splice(index, 1)
      checklist.splice(nextIndex, 0, moved)
      return {
        ...current,
        checklist: checklist.map((item, itemIndex) => ({ ...item, order: itemIndex + 1 })),
      }
    })
  }

  function moveChecklistSection(sectionName: string, direction: -1 | 1) {
    setChecklistDraft((current) => {
      const sections = [...new Set(current.checklist.map((item) => item.sectionName?.trim() || 'General'))]
        .map((name) => ({
          name,
          order: Math.min(...current.checklist.filter((item) => (item.sectionName?.trim() || 'General') === name).map((item) => item.sectionOrder ?? item.order)),
        }))
        .sort((left, right) => left.order - right.order || left.name.localeCompare(right.name))

      const index = sections.findIndex((section) => section.name === sectionName)
      const nextIndex = index + direction
      if (index < 0 || nextIndex < 0 || nextIndex >= sections.length) {
        return current
      }

      const reordered = [...sections]
      const [moved] = reordered.splice(index, 1)
      reordered.splice(nextIndex, 0, moved)
      const orderByName = new Map(reordered.map((section, sectionIndex) => [section.name, sectionIndex + 1]))
      return {
        ...current,
        checklist: current.checklist.map((item) => ({
          ...item,
          sectionOrder: orderByName.get(item.sectionName?.trim() || 'General') ?? item.sectionOrder ?? 1,
        })),
      }
    })
  }

  if (loading) {
    return <LoadingState label="Loading starter packs and templates" />
  }

  if (error) {
    return <ErrorState title="Unable to load templates" description={error} />
  }

  return (
    <div className="space-y-5">
      <PageScaffold
        eyebrow="Settings"
        title="Starter Packs and Templates"
        description="Install starter packs, manage asset templates, and tailor service checklists for your workspace."
      >
        <MetricGrid className="xl:grid-cols-4">
          <MetricCard label="Starter packs installed" value={installedPackCount} meta={`${data.starterPacks.length} available`} emphasis="accent" />
          <MetricCard label="Asset templates" value={assetTemplateCount} meta={`${activeAssetTemplateCount} active`} />
          <MetricCard label="Service checklists" value={checklistCount} meta={`${activeChecklistCount} active`} />
          <MetricCard label="Checklist categories" value={checklistCategories.length} meta="Customer-facing service groups" />
        </MetricGrid>

        <SegmentedTabs
          tabs={[
            { id: 'packs', label: 'Starter Packs', meta: data.starterPacks.length },
            { id: 'asset-templates', label: 'Asset Templates', meta: assetTemplateCount },
            { id: 'service-checklists', label: 'Service Checklists', meta: checklistCount },
          ]}
          activeTab={activeTab}
          onChange={setActiveTab}
        />

        {activeTab === 'packs' ? (
          <SectionCard title="Starter Packs" description="Install system-provided contractor starter packs, then edit the copied templates inside your own workspace.">
            {data.starterPacks.length === 0 ? (
              <EmptyState title="No starter packs available" description="Starter packs will appear here once the system pack catalog is ready." />
            ) : (
              <div className="grid gap-4 xl:grid-cols-3">
                {data.starterPacks.map((pack) => (
                  <article key={pack.id} className="rounded-[24px] border border-app bg-[var(--app-surface)] p-5 shadow-[var(--app-shadow-soft)]">
                    <div className="flex flex-wrap items-start justify-between gap-3">
                      <div>
                        <div className="flex flex-wrap gap-2">
                          <Badge tone={pack.isInstalled ? 'success' : 'neutral'}>{pack.isInstalled ? 'Installed' : 'Available'}</Badge>
                          <Badge tone="info">{pack.contractorType}</Badge>
                        </div>
                        <h3 className="mt-3 text-lg font-semibold text-app">{pack.name}</h3>
                        <p className="mt-2 text-sm text-muted">{pack.description || 'System starter pack for contractor operations.'}</p>
                      </div>
                      <button
                        type="button"
                        className="button-primary"
                        disabled={pack.isInstalled || installingPackId === pack.id}
                        onClick={() => void installStarterPack(pack)}
                      >
                        <PackageCheck className="h-4 w-4" />
                        {installingPackId === pack.id ? 'Installing...' : pack.isInstalled ? 'Installed' : 'Install'}
                      </button>
                    </div>

                    <div className="mt-5 space-y-4">
                      <div>
                        <p className="text-sm font-semibold text-app">Included asset templates</p>
                        <div className="mt-2 flex flex-wrap gap-2">
                          {pack.assetCategories.map((category) => (
                            <Badge key={category.id} tone="neutral">{category.name}</Badge>
                          ))}
                        </div>
                      </div>

                      <div>
                        <p className="text-sm font-semibold text-app">Included service checklists</p>
                        <div className="mt-2 space-y-2">
                          {pack.checklistTemplates.map((template) => (
                            <div key={template.id} className="rounded-2xl border border-app bg-[var(--app-subtle)] px-4 py-3">
                              <div className="flex flex-wrap items-center justify-between gap-2">
                                <span className="text-sm font-medium text-app">{template.name}</span>
                                <Badge tone={template.workType === 'BreakdownCorrective' ? 'warning' : 'info'}>
                                  {template.workType === 'BreakdownCorrective' ? 'Breakdown / Corrective' : 'Preventive Maintenance'}
                                </Badge>
                              </div>
                              <p className="mt-1 text-xs text-muted">{template.sectionCount} sections • {template.itemCount} items</p>
                            </div>
                          ))}
                        </div>
                      </div>
                    </div>
                  </article>
                ))}
              </div>
            )}
          </SectionCard>
        ) : null}

        {activeTab === 'asset-templates' ? (
          <SectionCard
            title="Asset Templates"
            description="Define the fields your team captures on assets. Installed starter pack templates can be renamed, adjusted, and extended here."
            action={(
              <button type="button" className="button-primary" onClick={() => openAssetTemplateEditor()}>
                <Plus className="h-4 w-4" />
                New asset template
              </button>
            )}
          >
            {sortedAssetTemplates.length === 0 ? (
              <EmptyState title="No asset templates yet" description="Install a starter pack or create your first asset template." actionLabel="Create asset template" onAction={() => openAssetTemplateEditor()} />
            ) : (
              <div className="grid gap-4 xl:grid-cols-2">
                {sortedAssetTemplates.map((template) => (
                  <article key={template.id} className="rounded-[24px] border border-app bg-[var(--app-surface)] p-5 shadow-[var(--app-shadow-soft)]">
                    <div className="flex flex-wrap items-start justify-between gap-3">
                      <div>
                        <div className="flex flex-wrap gap-2">
                          <Badge tone={template.isActive ? 'success' : 'warning'}>{template.isActive ? 'Active' : 'Disabled'}</Badge>
                          {template.defaultServiceIntervalMonths ? <Badge tone="info">{template.defaultServiceIntervalMonths} month interval</Badge> : null}
                        </div>
                        <h3 className="mt-3 text-lg font-semibold text-app">{template.name}</h3>
                        <p className="mt-2 text-sm text-muted">{template.description || 'Editable asset datasheet template.'}</p>
                      </div>
                      <div className="flex flex-wrap gap-2">
                        <button type="button" className="button-secondary px-3 py-2" onClick={() => openAssetTemplateEditor(template)}>
                          Edit
                        </button>
                        {template.isActive ? (
                          <button type="button" className="button-secondary px-3 py-2" onClick={() => setDisableAssetTarget(template)}>
                            <PowerOff className="h-4 w-4" />
                            Disable
                          </button>
                        ) : null}
                      </div>
                    </div>

                    <div className="mt-4 grid gap-3 sm:grid-cols-2">
                      {template.fields
                        .slice()
                        .sort((left, right) => left.displayOrder - right.displayOrder)
                        .slice(0, 6)
                        .map((field) => (
                          <div key={field.id} className="rounded-2xl border border-app px-4 py-3 text-sm">
                            <div className="flex flex-wrap items-center justify-between gap-2">
                              <span className="font-medium text-app">{field.fieldLabel}</span>
                              <Badge tone={field.isActive ? 'neutral' : 'warning'}>{field.fieldType}</Badge>
                            </div>
                            <p className="mt-1 text-xs text-muted">
                              {field.isRequired ? 'Required' : 'Optional'}
                              {field.unit ? ` • ${field.unit}` : ''}
                              {field.isActive === false ? ' • Disabled' : ''}
                            </p>
                          </div>
                        ))}
                    </div>
                    {template.fields.length > 6 ? <p className="mt-3 text-xs text-muted">+ {template.fields.length - 6} more fields</p> : null}
                  </article>
                ))}
              </div>
            )}
          </SectionCard>
        ) : null}

        {activeTab === 'service-checklists' ? (
          <SectionCard
            title="Service Checklists"
            description="Create reusable preventive maintenance and breakdown checklists. Work orders will use a saved snapshot when a checklist is selected."
            action={(
              <button type="button" className="button-primary" onClick={() => openChecklistEditor()}>
                <Plus className="h-4 w-4" />
                New checklist
              </button>
            )}
          >
            {groupedChecklists.length === 0 ? (
              <EmptyState title="No service checklists yet" description="Install a starter pack or create your first service checklist." actionLabel="Create checklist" onAction={() => openChecklistEditor()} />
            ) : (
              <div className="space-y-5">
                {groupedChecklists.map((group) => (
                  <div key={group.label} className="space-y-3">
                    <div className="flex items-center gap-2">
                      <ClipboardList className="h-4 w-4 text-[var(--app-primary-strong)]" />
                      <h3 className="text-lg font-semibold text-app">{group.label}</h3>
                    </div>
                    <div className="grid gap-4 xl:grid-cols-2">
                      {group.templates.map((template) => (
                        <article key={template.id} className="rounded-[24px] border border-app bg-[var(--app-surface)] p-5 shadow-[var(--app-shadow-soft)]">
                          <div className="flex flex-wrap items-start justify-between gap-3">
                            <div>
                              <div className="flex flex-wrap gap-2">
                                <Badge tone={template.isActive ? 'success' : 'warning'}>{template.isActive ? 'Active' : 'Disabled'}</Badge>
                                <Badge tone="neutral">{template.category}</Badge>
                              </div>
                              <h3 className="mt-3 text-lg font-semibold text-app">{template.name}</h3>
                              <p className="mt-2 text-sm text-muted">{template.description || 'Editable service checklist template.'}</p>
                            </div>
                            <div className="flex flex-wrap gap-2">
                              <button type="button" className="button-secondary px-3 py-2" onClick={() => openChecklistEditor(template)}>
                                Edit
                              </button>
                              <button type="button" className="button-secondary px-3 py-2" onClick={() => void duplicateChecklist(template)}>
                                <Copy className="h-4 w-4" />
                                Duplicate
                              </button>
                              <button type="button" className="button-secondary px-3 py-2" onClick={() => void setChecklistActive(template, !template.isActive)}>
                                {template.isActive ? <PowerOff className="h-4 w-4" /> : <Power className="h-4 w-4" />}
                                {template.isActive ? 'Disable' : 'Enable'}
                              </button>
                              <button type="button" className="button-secondary px-3 py-2 text-rose-200" onClick={() => setDeleteChecklistTarget(template)}>
                                <Trash2 className="h-4 w-4" />
                                Delete
                              </button>
                            </div>
                          </div>

                          <div className="mt-4 flex flex-wrap gap-2">
                            <Badge tone="info">{template.checklist.length} items</Badge>
                            {template.defaultIntervalMonths ? <Badge tone="neutral">{template.defaultIntervalMonths} month interval</Badge> : null}
                            {template.assetCategoryName ? <Badge tone="neutral">{template.assetCategoryName}</Badge> : null}
                          </div>

                          <div className="mt-4 space-y-2">
                            {template.checklist.slice(0, 4).map((item) => (
                              <div key={item.id} className="rounded-2xl border border-app px-4 py-3 text-sm">
                                <div className="flex items-center justify-between gap-3">
                                  <span className="text-app">{item.order}. {item.question}</span>
                                  <Badge tone="neutral">{checklistAnswerTypes.find((t) => t.value === item.type)?.label ?? item.type}</Badge>
                                </div>
                              </div>
                            ))}
                            {template.checklist.length > 4 ? <p className="text-xs text-muted">+ {template.checklist.length - 4} more items</p> : null}
                          </div>
                        </article>
                      ))}
                    </div>
                  </div>
                ))}
              </div>
            )}
          </SectionCard>
        ) : null}
      </PageScaffold>

      <Modal
        open={assetEditorOpen}
        title={editingAssetTemplate ? 'Edit asset template' : 'Create asset template'}
        description="Build a reusable asset datasheet template for your workspace."
        onClose={() => {
          if (assetSaving) return
          setAssetEditorOpen(false)
          setAssetSaveError('')
        }}
        maxWidth="max-w-5xl"
      >
        <div className="space-y-4">
          <div className="grid gap-4 md:grid-cols-2">
            <label className="block space-y-2">
              <span className="text-sm font-medium text-app">Template name</span>
              <input value={assetDraft.name} onChange={(event) => setAssetDraft((current) => ({ ...current, name: event.target.value }))} className="field-input" />
            </label>
            <label className="block space-y-2">
              <span className="text-sm font-medium text-app">Template code</span>
              <input value={assetDraft.code} onChange={(event) => setAssetDraft((current) => ({ ...current, code: event.target.value }))} className="field-input" placeholder="generator-main, split-ac..." />
            </label>
            <label className="block space-y-2 md:col-span-2">
              <span className="text-sm font-medium text-app">Description</span>
              <textarea value={assetDraft.description} onChange={(event) => setAssetDraft((current) => ({ ...current, description: event.target.value }))} className="field-input min-h-[110px]" />
            </label>
            <label className="block space-y-2">
              <span className="text-sm font-medium text-app">Default service interval</span>
              <select
                value={assetDraft.defaultServiceIntervalMonths}
                onChange={(event) => setAssetDraft((current) => ({ ...current, defaultServiceIntervalMonths: event.target.value }))}
                className="field-input"
              >
                <option value="">Not set</option>
                <option value="1">Monthly</option>
                <option value="3">Quarterly</option>
                <option value="6">Semi-Annual</option>
                <option value="12">Annual</option>
              </select>
            </label>
          </div>

          <div className="space-y-3">
            <div className="flex items-center justify-between gap-3">
              <div>
                <p className="text-lg font-semibold text-app">Datasheet fields</p>
                <p className="text-sm text-muted">Add, disable, reorder, or remove fields that should appear on assets.</p>
              </div>
              <button
                type="button"
                className="button-secondary"
                onClick={() =>
                  setAssetDraft((current) => ({
                    ...current,
                    fields: [
                      ...current.fields,
                      {
                        fieldLabel: '',
                        fieldType: 'Text',
                        dropdownOptions: '',
                        unit: '',
                        helpText: '',
                        isRequired: false,
                        isActive: true,
                        displayOrder: current.fields.length + 1,
                      },
                    ],
                  }))}
              >
                <Plus className="h-4 w-4" />
                Add field
              </button>
            </div>

            {assetDraft.fields.map((field, index) => (
              <div key={`${field.id ?? 'new'}-${index}`} className="rounded-[24px] border border-app p-4">
                <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_180px_120px_auto]">
                  <label className="block space-y-2">
                    <span className="text-sm font-medium text-app">Field label</span>
                    <input value={field.fieldLabel} onChange={(event) => updateAssetField(index, (current) => ({ ...current, fieldLabel: event.target.value }))} className="field-input" />
                  </label>
                  <label className="block space-y-2">
                    <span className="text-sm font-medium text-app">Field type</span>
                    <select value={field.fieldType} onChange={(event) => updateAssetField(index, (current) => ({ ...current, fieldType: event.target.value }))} className="field-input">
                      {assetFieldTypes.map((type) => (
                        <option key={type} value={type}>{type}</option>
                      ))}
                    </select>
                  </label>
                  <label className="panel-subtle mt-7 flex items-center justify-between rounded-2xl px-4 py-3">
                    <span className="text-sm text-app">Required</span>
                    <input type="checkbox" checked={field.isRequired} onChange={(event) => updateAssetField(index, (current) => ({ ...current, isRequired: event.target.checked }))} />
                  </label>
                  <div className="mt-7 flex flex-wrap justify-end gap-2">
                    <button type="button" className="button-secondary px-3 py-2" onClick={() => moveAssetField(index, -1)} disabled={index === 0}>Up</button>
                    <button type="button" className="button-secondary px-3 py-2" onClick={() => moveAssetField(index, 1)} disabled={index === assetDraft.fields.length - 1}>Down</button>
                    <button type="button" className="button-secondary px-3 py-2 text-rose-200" onClick={() => setAssetDraft((current) => ({ ...current, fields: current.fields.filter((_, fieldIndex) => fieldIndex !== index).map((item, itemIndex) => ({ ...item, displayOrder: itemIndex + 1 })) }))} disabled={assetDraft.fields.length === 1}>
                      Delete
                    </button>
                  </div>
                </div>
                <div className="mt-4 grid gap-3 md:grid-cols-2">
                  <label className="block space-y-2">
                    <span className="text-sm font-medium text-app">Unit</span>
                    <input value={field.unit} onChange={(event) => updateAssetField(index, (current) => ({ ...current, unit: event.target.value }))} className="field-input" placeholder="V, Hz, kVA..." />
                  </label>
                  <label className="block space-y-2">
                    <span className="text-sm font-medium text-app">Help text</span>
                    <input value={field.helpText} onChange={(event) => updateAssetField(index, (current) => ({ ...current, helpText: event.target.value }))} className="field-input" />
                  </label>
                  {field.fieldType === 'Dropdown' ? (
                    <label className="block space-y-2 md:col-span-2">
                      <span className="text-sm font-medium text-app">Options</span>
                      <input value={field.dropdownOptions} onChange={(event) => updateAssetField(index, (current) => ({ ...current, dropdownOptions: event.target.value }))} className="field-input" placeholder="Option A, Option B, Option C" />
                    </label>
                  ) : null}
                  <label className="panel-subtle flex items-center justify-between rounded-2xl px-4 py-3">
                    <span className="text-sm text-app">Field is active</span>
                    <input type="checkbox" checked={field.isActive} onChange={(event) => updateAssetField(index, (current) => ({ ...current, isActive: event.target.checked }))} />
                  </label>
                </div>
              </div>
            ))}
          </div>

          {assetSaveError ? <div className="rounded-2xl border border-rose-400/30 bg-rose-500/10 px-4 py-3 text-sm text-rose-200">{assetSaveError}</div> : null}

          <div className="flex justify-end gap-3">
            <button type="button" className="button-secondary" onClick={() => setAssetEditorOpen(false)} disabled={assetSaving}>Cancel</button>
            <button type="button" className="button-primary" onClick={() => void saveAssetTemplate()} disabled={assetSaving}>
              {assetSaving ? 'Saving template...' : editingAssetTemplate ? 'Save changes' : 'Create template'}
            </button>
          </div>
        </div>
      </Modal>

      <Modal
        open={checklistEditorOpen}
        title={editingChecklist ? 'Edit service checklist' : 'Create service checklist'}
        description="Build a reusable service checklist. Work orders will use a saved copy when this checklist is selected."
        onClose={() => {
          if (checklistSaving) return
          if (checklistIsDirty) { setDiscardChecklistOpen(true); return }
          setChecklistEditorOpen(false)
          setChecklistSaveError('')
        }}
        maxWidth="max-w-5xl"
      >
        <div className="space-y-4">
          <div className="grid gap-4 md:grid-cols-2">
            <label className="block space-y-2">
              <span className="text-sm font-medium text-app">Checklist name</span>
              <input value={checklistDraft.name} onChange={(event) => setChecklistDraft((current) => ({ ...current, name: event.target.value }))} className="field-input" />
            </label>
            <label className="block space-y-2">
              <span className="text-sm font-medium text-app">Service category</span>
              <input
                value={checklistDraft.category}
                onChange={(event) => setChecklistDraft((current) => ({ ...current, category: event.target.value }))}
                className="field-input"
                list="service-checklist-categories"
                placeholder="Generator, HVAC, Solar..."
              />
              <datalist id="service-checklist-categories">
                {checklistCategories.map((category) => (
                  <option key={category} value={category} />
                ))}
              </datalist>
            </label>
            <label className="block space-y-2">
              <span className="text-sm font-medium text-app">Checklist code</span>
              <input value={checklistDraft.code || ''} onChange={(event) => setChecklistDraft((current) => ({ ...current, code: event.target.value }))} className="field-input" />
            </label>
            <label className="block space-y-2">
              <span className="text-sm font-medium text-app">Work type</span>
              <select value={checklistDraft.workType || 'PreventiveMaintenance'} onChange={(event) => setChecklistDraft((current) => ({ ...current, workType: event.target.value as UpsertPmTemplateInput['workType'] }))} className="field-input">
                <option value="PreventiveMaintenance">Preventive Maintenance</option>
                <option value="BreakdownCorrective">Breakdown / Corrective Maintenance</option>
              </select>
            </label>
            <label className="block space-y-2">
              <span className="text-sm font-medium text-app">Default service interval</span>
              <select
                value={checklistDraft.defaultIntervalMonths?.toString() ?? ''}
                onChange={(event) => setChecklistDraft((current) => ({ ...current, defaultIntervalMonths: event.target.value ? Number(event.target.value) : null }))}
                className="field-input"
              >
                <option value="">Not set</option>
                <option value="1">Monthly</option>
                <option value="3">Quarterly</option>
                <option value="6">Semi-Annual</option>
                <option value="12">Annual</option>
              </select>
            </label>
            <label className="block space-y-2">
              <span className="text-sm font-medium text-app">Linked asset template</span>
              <select
                value={checklistDraft.assetCategoryId ?? ''}
                onChange={(event) => setChecklistDraft((current) => ({ ...current, assetCategoryId: event.target.value || null }))}
                className="field-input"
              >
                <option value="">Any asset template</option>
                {data.assetTemplates.filter((item) => item.isActive).map((template) => (
                  <option key={template.id} value={template.id}>{template.name}</option>
                ))}
              </select>
            </label>
            <label className="block space-y-2 md:col-span-2">
              <span className="text-sm font-medium text-app">Description</span>
              <textarea value={checklistDraft.description || ''} onChange={(event) => setChecklistDraft((current) => ({ ...current, description: event.target.value }))} className="field-input min-h-[110px]" />
            </label>
          </div>

          <label className="panel-subtle flex items-center justify-between rounded-2xl px-4 py-3">
            <span className="text-sm text-app">Checklist is active</span>
            <input type="checkbox" checked={checklistDraft.isActive} onChange={(event) => setChecklistDraft((current) => ({ ...current, isActive: event.target.checked }))} />
          </label>

          <div className="space-y-3">
            <div className="flex items-center justify-between gap-3">
              <div>
                <p className="text-lg font-semibold text-app">Checklist items</p>
                <p className="text-sm text-muted">Add sections and questions in the order technicians should complete them.</p>
              </div>
              <button
                type="button"
                className="button-secondary"
                onClick={() =>
                  setChecklistDraft((current) => ({
                    ...current,
                    checklist: [
                      ...current.checklist,
                      {
                        sectionName: '',
                        sectionOrder: current.checklist.length + 1,
                        question: '',
                        helperText: '',
                        type: 'text',
                        unit: '',
                        minValue: null,
                        maxValue: null,
                        defaultValue: '',
                        required: true,
                        evidenceRequired: false,
                        technicianRemarksAllowed: true,
                        requiresNoteOnFail: false,
                        order: current.checklist.length + 1,
                        options: [],
                      },
                    ],
                  }))}
              >
                <Plus className="h-4 w-4" />
                Add item
              </button>
            </div>

            {checklistDraft.checklist.map((item, index) => (
              <div key={`${item.order}-${index}`} className="rounded-[24px] border border-app p-4">
                <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_180px_120px_auto]">
                  <div className="block space-y-2 lg:col-span-4">
                    <span className="text-sm font-medium text-app">Section details</span>
                    <div className="grid gap-3 md:grid-cols-[minmax(0,1fr)_120px_auto]">
                      <label className="block space-y-2">
                        <span className="text-sm font-medium text-app">Section name</span>
                        <input value={item.sectionName || ''} onChange={(event) => updateChecklistItem(index, (current) => ({ ...current, sectionName: event.target.value }))} className="field-input" placeholder="Safety checks" />
                      </label>
                      <label className="block space-y-2">
                        <span className="text-sm font-medium text-app">Section order</span>
                        <input type="number" min={1} value={item.sectionOrder ?? 1} onChange={(event) => updateChecklistItem(index, (current) => ({ ...current, sectionOrder: Number(event.target.value) || 1 }))} className="field-input" />
                      </label>
                      <div className="flex gap-2">
                        <button type="button" className="button-secondary px-3 py-2" onClick={() => moveChecklistSection(item.sectionName?.trim() || 'General', -1)}>Section up</button>
                        <button type="button" className="button-secondary px-3 py-2" onClick={() => moveChecklistSection(item.sectionName?.trim() || 'General', 1)}>Section down</button>
                      </div>
                    </div>
                  </div>

                  <label className="block space-y-2">
                    <span className="text-sm font-medium text-app">Question label</span>
                    <input value={item.question} onChange={(event) => updateChecklistItem(index, (current) => ({ ...current, question: event.target.value }))} className="field-input" />
                  </label>
                  <label className="block space-y-2">
                    <span className="text-sm font-medium text-app">Answer type</span>
                    <select
                      value={item.type}
                      onChange={(event) =>
                        updateChecklistItem(index, (current) => {
                          const nextType = event.target.value as UpsertPmTemplateInput['checklist'][number]['type']
                          const defaultOptions = nextType === 'passfail'
                            ? ['Pass', 'Fail']
                            : nextType === 'condition'
                              ? ['Good', 'Fair', 'Poor', 'Critical']
                              : current.options ?? ['']

                          return {
                            ...current,
                            type: nextType,
                            options: requiresOptions(nextType) ? defaultOptions : [],
                          }
                        })}
                      className="field-input"
                    >
                      {checklistAnswerTypes.map(({ value, label }) => (
                        <option key={value} value={value}>{label}</option>
                      ))}
                    </select>
                  </label>
                  <label className="panel-subtle mt-7 flex items-center justify-between rounded-2xl px-4 py-3">
                    <span className="text-sm text-app">Required</span>
                    <input type="checkbox" checked={item.required} onChange={(event) => updateChecklistItem(index, (current) => ({ ...current, required: event.target.checked }))} />
                  </label>
                  <div className="mt-7 flex flex-wrap justify-end gap-2">
                    <button type="button" className="button-secondary px-3 py-2" onClick={() => moveChecklistItem(index, -1)} disabled={index === 0}>Up</button>
                    <button type="button" className="button-secondary px-3 py-2" onClick={() => moveChecklistItem(index, 1)} disabled={index === checklistDraft.checklist.length - 1}>Down</button>
                    <button type="button" className="button-secondary px-3 py-2 text-rose-200" onClick={() => setChecklistDraft((current) => ({ ...current, checklist: current.checklist.filter((_, itemIndex) => itemIndex !== index).map((entry, entryIndex) => ({ ...entry, order: entryIndex + 1 })) }))} disabled={checklistDraft.checklist.length === 1}>
                      Delete
                    </button>
                  </div>
                </div>

                <div className="mt-4 grid gap-3 md:grid-cols-2">
                  <label className="block space-y-2">
                    <span className="text-sm font-medium text-app">Helper text</span>
                    <input value={item.helperText || ''} onChange={(event) => updateChecklistItem(index, (current) => ({ ...current, helperText: event.target.value }))} className="field-input" />
                  </label>
                  <label className="block space-y-2">
                    <span className="text-sm font-medium text-app">Unit of measure</span>
                    <input value={item.unit || ''} onChange={(event) => updateChecklistItem(index, (current) => ({ ...current, unit: event.target.value }))} className="field-input" placeholder="V, A, deg C..." />
                  </label>
                  <label className="block space-y-2">
                    <span className="text-sm font-medium text-app">Min value</span>
                    <input type="number" value={item.minValue ?? ''} onChange={(event) => updateChecklistItem(index, (current) => ({ ...current, minValue: event.target.value === '' ? null : Number(event.target.value) }))} className="field-input" />
                  </label>
                  <label className="block space-y-2">
                    <span className="text-sm font-medium text-app">Max value</span>
                    <input type="number" value={item.maxValue ?? ''} onChange={(event) => updateChecklistItem(index, (current) => ({ ...current, maxValue: event.target.value === '' ? null : Number(event.target.value) }))} className="field-input" />
                  </label>
                  <label className="block space-y-2 md:col-span-2">
                    <span className="text-sm font-medium text-app">Default value</span>
                    <input value={item.defaultValue || ''} onChange={(event) => updateChecklistItem(index, (current) => ({ ...current, defaultValue: event.target.value }))} className="field-input" />
                  </label>
                  <div className="grid gap-2 md:col-span-2 sm:grid-cols-3">
                    <label className="panel-subtle flex items-center justify-between rounded-2xl px-4 py-3">
                      <span className="text-sm text-app">Evidence required</span>
                      <input type="checkbox" checked={item.evidenceRequired ?? false} onChange={(event) => updateChecklistItem(index, (current) => ({ ...current, evidenceRequired: event.target.checked }))} />
                    </label>
                    <label className="panel-subtle flex items-center justify-between rounded-2xl px-4 py-3">
                      <span className="text-sm text-app">Remarks allowed</span>
                      <input type="checkbox" checked={item.technicianRemarksAllowed ?? true} onChange={(event) => updateChecklistItem(index, (current) => ({ ...current, technicianRemarksAllowed: event.target.checked }))} />
                    </label>
                    <label className="panel-subtle flex items-center justify-between rounded-2xl px-4 py-3">
                      <span className="text-sm text-app">Note on fail</span>
                      <input type="checkbox" checked={item.requiresNoteOnFail ?? false} onChange={(event) => updateChecklistItem(index, (current) => ({ ...current, requiresNoteOnFail: event.target.checked }))} />
                    </label>
                  </div>
                </div>

                {requiresOptions(item.type) ? (
                  <label className="mt-4 block space-y-2">
                    <span className="text-sm font-medium text-app">Options</span>
                    <input
                      value={(item.options ?? []).join(', ')}
                      onChange={(event) => updateChecklistItem(index, (current) => ({ ...current, options: event.target.value.split(',').map((option) => option.trim()).filter(Boolean) }))}
                      className="field-input"
                      placeholder="Option A, Option B, Option C"
                    />
                  </label>
                ) : null}
              </div>
            ))}
          </div>

          {checklistSaveError ? <div className="rounded-2xl border border-rose-400/30 bg-rose-500/10 px-4 py-3 text-sm text-rose-200">{checklistSaveError}</div> : null}

          <div className="flex justify-end gap-3">
            <button
              type="button"
              className="button-secondary"
              onClick={() => {
                if (checklistIsDirty) { setDiscardChecklistOpen(true); return }
                setChecklistEditorOpen(false)
              }}
              disabled={checklistSaving}
            >
              Cancel
            </button>
            <button type="button" className="button-primary" onClick={() => void saveChecklist()} disabled={!checklistDraftValid || checklistSaving}>
              {checklistSaving ? 'Saving checklist...' : editingChecklist ? 'Save changes' : 'Create checklist'}
            </button>
          </div>
        </div>
      </Modal>

      <ConfirmDialog
        open={Boolean(disableAssetTarget)}
        title="Disable asset template"
        description={`Disable ${disableAssetTarget?.name ?? 'this asset template'}? It will stop appearing for new asset selection.`}
        confirmLabel="Disable template"
        onCancel={() => setDisableAssetTarget(null)}
        onConfirm={() => void disableAssetTemplate()}
      />

      <ConfirmDialog
        open={discardChecklistOpen}
        title="Discard unsaved changes?"
        description="You have unsaved changes to this checklist. Discard them and close?"
        confirmLabel="Discard changes"
        onCancel={() => setDiscardChecklistOpen(false)}
        onConfirm={() => {
          setDiscardChecklistOpen(false)
          setChecklistEditorOpen(false)
          setChecklistSaveError('')
          setChecklistOriginal('')
        }}
      />

      <ConfirmDialog
        open={Boolean(deleteChecklistTarget)}
        title="Delete service checklist"
        description={`Delete ${deleteChecklistTarget?.name ?? 'this checklist'}? Existing work orders will keep their saved checklist snapshot.`}
        confirmLabel={deletingChecklist ? 'Deleting...' : 'Delete checklist'}
        onCancel={() => {
          if (!deletingChecklist) {
            setDeleteChecklistTarget(null)
          }
        }}
        onConfirm={() => void deleteChecklist()}
      />
    </div>
  )
}

function requiresOptions(type: string) {
  return ['dropdown', 'passfail', 'condition'].includes(type)
}

function normalizeFieldKey(label: string) {
  return label
    .trim()
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '_')
    .replace(/^_+|_+$/g, '')
}

function mapAssetFieldType(field: AssetCategoryFieldRecord) {
  if (field.fieldType === 'TextArea') {
    return 'MultilineText'
  }

  return field.fieldType
}
