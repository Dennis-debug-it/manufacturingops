import { Copy, Eye, Plus, Trash2 } from 'lucide-react'
import { useEffect, useMemo, useState } from 'react'
import { InfoAlert } from '../../../../components/ui/InfoAlert'
import { LoadingState } from '../../../../components/ui/LoadingState'
import { useToast } from '../../../../components/ui/ToastProvider'
import { useAsyncData } from '../../../../hooks/useAsyncData'
import {
  createEmptyTemplateDraft,
  normalizeTemplate,
  platformSettingsService,
  toTemplatePayload,
  type PlatformTemplate,
  type PlatformTemplatePreview,
} from '../../../../services/platformSettingsService'
import { toServiceError } from '../../../../services/platformService'
import { SectionTitle } from '../../../../modules/platform-v2/PlatformCommon'
import { TemplateEditor } from './TemplateEditor'
import { TemplatePreview } from './TemplatePreview'

export function TemplatesSettingsPanel() {
  const { pushToast } = useToast()
  const { data, loading, error, reload } = useAsyncData(() => platformSettingsService.listTemplates(), [] as PlatformTemplate[], [])
  const [templates, setTemplates] = useState<PlatformTemplate[]>([])
  const [editorOpen, setEditorOpen] = useState(false)
  const [previewOpen, setPreviewOpen] = useState(false)
  const [saving, setSaving] = useState(false)
  const [activeTemplate, setActiveTemplate] = useState<PlatformTemplate | null>(null)
  const [preview, setPreview] = useState<PlatformTemplatePreview | null>(null)

  useEffect(() => {
    setTemplates(data.map(normalizeTemplate))
  }, [data])

  const grouped = useMemo(() => {
    const map = new Map<string, PlatformTemplate[]>()
    for (const item of templates) {
      const current = map.get(item.type) || []
      current.push(item)
      map.set(item.type, current)
    }
    return Array.from(map.entries()).sort((a, b) => a[0].localeCompare(b[0]))
  }, [templates])

  async function saveTemplate() {
    if (!activeTemplate) return
    if (!activeTemplate.name.trim()) {
      pushToast({ title: 'Template name required', description: 'Enter a template name before saving.', tone: 'warning' })
      return
    }

    setSaving(true)
    try {
      if (activeTemplate.id) {
        await platformSettingsService.updateTemplate(activeTemplate.id, toTemplatePayload(activeTemplate))
      } else {
        await platformSettingsService.createTemplate(toTemplatePayload(activeTemplate))
      }
      pushToast({ title: 'Template saved', description: 'Template changes were saved successfully.', tone: 'success' })
      setEditorOpen(false)
      await reload()
    } catch (saveError) {
      pushToast({ title: 'Save failed', description: toServiceError(saveError, 'Unable to save template.'), tone: 'danger' })
    } finally {
      setSaving(false)
    }
  }

  async function openPreview(template: PlatformTemplate) {
    try {
      const response = await platformSettingsService.previewTemplate(template.id)
      setPreview(response)
      setPreviewOpen(true)
    } catch (previewError) {
      pushToast({ title: 'Preview failed', description: toServiceError(previewError, 'Unable to preview template.'), tone: 'danger' })
    }
  }

  async function duplicateTemplate(template: PlatformTemplate) {
    try {
      await platformSettingsService.duplicateTemplate(template.id)
      pushToast({ title: 'Template duplicated', description: 'A copy of the template was created.', tone: 'success' })
      await reload()
    } catch (duplicateError) {
      pushToast({ title: 'Duplicate failed', description: toServiceError(duplicateError, 'Unable to duplicate template.'), tone: 'danger' })
    }
  }

  async function toggleTemplate(template: PlatformTemplate) {
    try {
      if (template.isActive) {
        await platformSettingsService.deactivateTemplate(template.id)
      } else {
        await platformSettingsService.activateTemplate(template.id)
      }
      pushToast({ title: 'Template updated', description: `${template.name} is now ${template.isActive ? 'inactive' : 'active'}.`, tone: 'success' })
      await reload()
    } catch (toggleError) {
      pushToast({ title: 'Update failed', description: toServiceError(toggleError, 'Unable to update template state.'), tone: 'danger' })
    }
  }

  async function setDefault(template: PlatformTemplate) {
    try {
      await platformSettingsService.makeDefaultTemplate(template.id)
      pushToast({ title: 'Default updated', description: `${template.name} is now default for ${template.type}.`, tone: 'success' })
      await reload()
    } catch (defaultError) {
      pushToast({ title: 'Update failed', description: toServiceError(defaultError, 'Unable to set template default.'), tone: 'danger' })
    }
  }

  async function deleteTemplate(template: PlatformTemplate) {
    try {
      await platformSettingsService.deleteTemplate(template.id)
      pushToast({ title: 'Template deleted', description: `${template.name} was deleted.`, tone: 'warning' })
      await reload()
    } catch (deleteError) {
      pushToast({ title: 'Delete failed', description: toServiceError(deleteError, 'Unable to delete template.'), tone: 'danger' })
    }
  }

  if (loading) return <LoadingState label="Loading templates settings" />
  if (error) return <InfoAlert title="Unable to load templates settings" description={error} tone="danger" />

  return (
    <section data-testid="templates-page" className="surface-card space-y-4">
      <SectionTitle
        title="Templates Settings"
        description="Create, edit, preview, duplicate, activate, and manage default document/email templates."
        action={<button data-testid="create-template-button" type="button" className="button-primary" onClick={() => { setActiveTemplate(createEmptyTemplateDraft()); setEditorOpen(true) }}><Plus className="h-4 w-4" />Create Template</button>}
      />

      {grouped.length === 0 ? <InfoAlert title="No templates yet" description="Create your first template to start generating platform documents and emails." tone="info" /> : null}
      <div data-testid="template-list" className="space-y-4">
        {grouped.map(([type, items]) => (
          <article key={type} className="panel-subtle rounded-2xl p-4">
            <p className="text-xs font-semibold uppercase tracking-[0.16em] text-muted">{type}</p>
            <div className="mt-3 grid gap-3 lg:grid-cols-2">
              {items.map((template) => (
                <div key={template.id} className="rounded-2xl border border-app p-4">
                  <div className="flex items-start justify-between gap-3">
                    <div>
                      <p className="font-semibold text-app">{template.name}</p>
                      <p className="mt-1 text-xs text-muted">{template.previewText || 'No preview text yet.'}</p>
                    </div>
                    <div className="text-xs text-muted">{template.isDefault ? 'Default' : template.isActive ? 'Active' : 'Inactive'}</div>
                  </div>
                  <div className="mt-4 flex flex-wrap gap-2">
                    <button type="button" className="button-secondary px-3 py-2" onClick={() => { setActiveTemplate(template); setEditorOpen(true) }}>Edit</button>
                    <button type="button" className="button-secondary px-3 py-2" onClick={() => void duplicateTemplate(template)}><Copy className="h-4 w-4" />Duplicate</button>
                    <button data-testid="template-preview-button" type="button" className="button-secondary px-3 py-2" onClick={() => void openPreview(template)}><Eye className="h-4 w-4" />Preview</button>
                    <button type="button" className="button-secondary px-3 py-2" onClick={() => void toggleTemplate(template)}>{template.isActive ? 'Deactivate' : 'Activate'}</button>
                    <button type="button" className="button-secondary px-3 py-2" onClick={() => void setDefault(template)}>{template.isDefault ? 'Default' : 'Make Default'}</button>
                    <button type="button" className="button-secondary px-3 py-2 text-rose-200" onClick={() => void deleteTemplate(template)}><Trash2 className="h-4 w-4" />Delete</button>
                  </div>
                </div>
              ))}
            </div>
          </article>
        ))}
      </div>

      <TemplateEditor open={editorOpen} value={activeTemplate} saving={saving} onClose={() => setEditorOpen(false)} onChange={setActiveTemplate} onSave={() => void saveTemplate()} />
      <TemplatePreview open={previewOpen} preview={preview} onClose={() => setPreviewOpen(false)} />
    </section>
  )
}
