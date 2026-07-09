import { ChevronDown, ChevronRight, Plus, Trash2 } from 'lucide-react'
import { useState, type ReactNode } from 'react'
import { Badge } from '../../components/ui/Badge'
import { ErrorState } from '../../components/ui/ErrorState'
import { LoadingState } from '../../components/ui/LoadingState'
import { PageHeader } from '../../components/ui/PageHeader'
import { useToast } from '../../components/ui/ToastProvider'
import { useAsyncData } from '../../hooks/useAsyncData'
import type { AssetCategoryFieldRecord, AssetCategoryRecord, UpsertAssetCategoryFieldInput, UpsertAssetCategoryInput } from '../../services/assetCategoryService'
import { assetCategoryService } from '../../services/assetCategoryService'

const FIELD_TYPES = ['Text', 'Number', 'Date', 'Boolean', 'Dropdown', 'TextArea']

const emptyCategoryForm: UpsertAssetCategoryInput = {
  name: '',
  parentCategoryName: '',
  icon: '',
  isDefault: false,
  isActive: true,
  displayOrder: 0,
}

const emptyFieldForm: UpsertAssetCategoryFieldInput = {
  fieldName: '',
  fieldLabel: '',
  fieldType: 'Text',
  dropdownOptions: '',
  unit: '',
  isRequired: false,
  displayOrder: 0,
}

export function AssetCategoriesSettingsPage() {
  const { pushToast } = useToast()
  const { data: categories, loading, error, reload } = useAsyncData<AssetCategoryRecord[]>(
    (signal) => assetCategoryService.list(signal),
    [],
    [],
  )

  const [expanded, setExpanded] = useState<Set<string>>(new Set())
  const [showCategoryForm, setShowCategoryForm] = useState(false)
  const [editingCategory, setEditingCategory] = useState<AssetCategoryRecord | null>(null)
  const [categoryForm, setCategoryForm] = useState<UpsertAssetCategoryInput>(emptyCategoryForm)

  const [editingField, setEditingField] = useState<{ categoryId: string; field: AssetCategoryFieldRecord | null } | null>(null)
  const [fieldForm, setFieldForm] = useState<UpsertAssetCategoryFieldInput>(emptyFieldForm)

  function toggleExpand(id: string) {
    setExpanded((prev) => {
      const next = new Set(prev)
      if (next.has(id)) {
        next.delete(id)
      } else {
        next.add(id)
      }
      return next
    })
  }

  function openCategoryEditor(category?: AssetCategoryRecord) {
    setEditingCategory(category ?? null)
    setCategoryForm(category ? {
      name: category.name,
      parentCategoryName: category.parentCategoryName ?? '',
      icon: category.icon ?? '',
      isDefault: category.isDefault,
      isActive: category.isActive,
      displayOrder: category.displayOrder,
    } : emptyCategoryForm)
    setShowCategoryForm(true)
  }

  async function saveCategory() {
    try {
      if (editingCategory) {
        await assetCategoryService.update(editingCategory.id, categoryForm)
        pushToast({ title: 'Category updated', tone: 'success' })
      } else {
        await assetCategoryService.create(categoryForm)
        pushToast({ title: 'Category created', tone: 'success' })
      }
      setShowCategoryForm(false)
      await reload()
    } catch (err) {
      pushToast({ title: 'Save failed', description: err instanceof Error ? err.message : 'Unable to save.', tone: 'danger' })
    }
  }

  async function deactivateCategory(category: AssetCategoryRecord) {
    if (!window.confirm(`Deactivate category "${category.name}"?`)) return
    try {
      await assetCategoryService.deactivate(category.id)
      pushToast({ title: 'Category deactivated', tone: 'success' })
      await reload()
    } catch (err) {
      pushToast({ title: 'Failed', description: err instanceof Error ? err.message : 'Unable to deactivate.', tone: 'danger' })
    }
  }

  function openFieldEditor(categoryId: string, field?: AssetCategoryFieldRecord) {
    setEditingField({ categoryId, field: field ?? null })
    setFieldForm(field ? {
      fieldName: field.fieldName,
      fieldLabel: field.fieldLabel,
      fieldType: field.fieldType,
      dropdownOptions: field.dropdownOptions ?? '',
      unit: field.unit ?? '',
      isRequired: field.isRequired,
      displayOrder: field.displayOrder,
    } : emptyFieldForm)
  }

  async function saveField() {
    if (!editingField) return
    const { categoryId, field } = editingField
    try {
      if (field) {
        await assetCategoryService.updateField(categoryId, field.id, fieldForm)
        pushToast({ title: 'Field updated', tone: 'success' })
      } else {
        await assetCategoryService.addField(categoryId, fieldForm)
        pushToast({ title: 'Field added', tone: 'success' })
      }
      setEditingField(null)
      await reload()
    } catch (err) {
      pushToast({ title: 'Save failed', description: err instanceof Error ? err.message : 'Unable to save.', tone: 'danger' })
    }
  }

  async function deleteField(categoryId: string, fieldId: string) {
    if (!window.confirm('Remove this field?')) return
    try {
      await assetCategoryService.deleteField(categoryId, fieldId)
      pushToast({ title: 'Field removed', tone: 'success' })
      await reload()
    } catch (err) {
      pushToast({ title: 'Failed', description: err instanceof Error ? err.message : 'Unable to remove field.', tone: 'danger' })
    }
  }

  return (
    <div className="space-y-6">
      <PageHeader
        eyebrow="Settings"
        title="Asset Categories"
        description="Define asset categories and their custom field schemas for structured data capture."
      />

      {loading && <LoadingState />}
      {!loading && error && <ErrorState message={error} onRetry={reload} />}

      {!loading && !error && (
        <div className="space-y-4">
          <div className="flex justify-end">
            <button type="button" className="button-primary" onClick={() => openCategoryEditor()}>
              <Plus className="h-4 w-4" />
              Add category
            </button>
          </div>

          {categories.length === 0 && (
            <div className="surface-card py-12 text-center">
              <p className="text-muted">No asset categories yet. Create the first one.</p>
            </div>
          )}

          {categories.map((cat) => {
            const isExpanded = expanded.has(cat.id)
            const isEditingFieldHere = editingField?.categoryId === cat.id
            return (
              <div key={cat.id} className="surface-card overflow-hidden">
                <div className="flex items-center gap-3 p-4">
                  <button
                    type="button"
                    className="flex-1 flex items-center gap-3 text-left"
                    onClick={() => toggleExpand(cat.id)}
                  >
                    {isExpanded ? <ChevronDown className="h-4 w-4 text-muted flex-shrink-0" /> : <ChevronRight className="h-4 w-4 text-muted flex-shrink-0" />}
                    <div className="min-w-0">
                      <p className="font-semibold text-app">{cat.icon && <span className="mr-2">{cat.icon}</span>}{cat.name}</p>
                      {cat.parentCategoryName && <p className="text-xs text-muted">Parent: {cat.parentCategoryName}</p>}
                    </div>
                    <Badge tone={cat.isActive ? 'success' : 'neutral'} className="flex-shrink-0">{cat.isActive ? 'Active' : 'Inactive'}</Badge>
                    {cat.isDefault && <Badge tone="info" className="flex-shrink-0">Default</Badge>}
                    <span className="ml-auto text-xs text-muted flex-shrink-0">{cat.fields.length} field{cat.fields.length !== 1 ? 's' : ''}</span>
                  </button>
                  <div className="flex gap-2 flex-shrink-0">
                    <button type="button" className="button-secondary px-3 py-1.5 text-xs" onClick={() => openCategoryEditor(cat)}>Edit</button>
                    {cat.isActive && (
                      <button type="button" className="button-secondary px-3 py-1.5 text-xs" onClick={() => void deactivateCategory(cat)}>Deactivate</button>
                    )}
                  </div>
                </div>

                {isExpanded && (
                  <div className="border-t border-app px-4 pb-4">
                    <div className="mt-4 flex items-center justify-between">
                      <h4 className="text-xs font-semibold uppercase tracking-[0.18em] text-muted">Custom Fields</h4>
                      <button
                        type="button"
                        className="button-secondary px-3 py-1.5 text-xs"
                        onClick={() => openFieldEditor(cat.id)}
                      >
                        <Plus className="h-3.5 w-3.5" />
                        Add field
                      </button>
                    </div>

                    {cat.fields.length === 0 && (
                      <p className="mt-3 text-sm text-muted">No custom fields. Add one to capture structured data for assets in this category.</p>
                    )}

                    <div className="mt-3 space-y-2">
                      {cat.fields
                        .slice()
                        .sort((a, b) => a.displayOrder - b.displayOrder)
                        .map((field) => (
                          <div key={field.id} className="flex items-center gap-3 rounded-xl border border-app bg-subtle px-3 py-2.5">
                            <div className="flex-1 min-w-0">
                              <p className="text-sm font-medium text-app">{field.fieldLabel}</p>
                              <p className="text-xs text-muted">{field.fieldName} · {field.fieldType}{field.unit ? ` · ${field.unit}` : ''}{field.isRequired ? ' · Required' : ''}</p>
                            </div>
                            <div className="flex gap-2 flex-shrink-0">
                              <button type="button" className="button-secondary px-2 py-1 text-xs" onClick={() => openFieldEditor(cat.id, field)}>Edit</button>
                              <button type="button" className="button-secondary px-2 py-1 text-xs" onClick={() => void deleteField(cat.id, field.id)}>
                                <Trash2 className="h-3.5 w-3.5" />
                              </button>
                            </div>
                          </div>
                        ))}
                    </div>

                    {isEditingFieldHere && (
                      <div className="mt-4 rounded-2xl border border-app bg-subtle p-4">
                        <h5 className="mb-3 text-sm font-semibold text-app">{editingField?.field ? 'Edit field' : 'New field'}</h5>
                        <div className="grid gap-3 sm:grid-cols-2">
                          <FieldLabel label="Field name (key)">
                            <input value={fieldForm.fieldName} onChange={(e) => setFieldForm((f) => ({ ...f, fieldName: e.target.value }))} className="field-input" placeholder="e.g. capacity_kva" />
                          </FieldLabel>
                          <FieldLabel label="Display label">
                            <input value={fieldForm.fieldLabel} onChange={(e) => setFieldForm((f) => ({ ...f, fieldLabel: e.target.value }))} className="field-input" placeholder="e.g. Capacity (kVA)" />
                          </FieldLabel>
                          <FieldLabel label="Field type">
                            <select value={fieldForm.fieldType} onChange={(e) => setFieldForm((f) => ({ ...f, fieldType: e.target.value }))} className="field-input">
                              {FIELD_TYPES.map((t) => <option key={t} value={t}>{t}</option>)}
                            </select>
                          </FieldLabel>
                          <FieldLabel label="Unit (optional)">
                            <input value={fieldForm.unit ?? ''} onChange={(e) => setFieldForm((f) => ({ ...f, unit: e.target.value }))} className="field-input" placeholder="e.g. kVA, °C, m²" />
                          </FieldLabel>
                          <FieldLabel label="Display order">
                            <input type="number" value={fieldForm.displayOrder} onChange={(e) => setFieldForm((f) => ({ ...f, displayOrder: Number(e.target.value) }))} className="field-input" />
                          </FieldLabel>
                          <FieldLabel label="Required">
                            <label className="flex items-center gap-2 mt-2">
                              <input type="checkbox" checked={fieldForm.isRequired} onChange={(e) => setFieldForm((f) => ({ ...f, isRequired: e.target.checked }))} className="h-4 w-4" />
                              <span className="text-sm text-app">Required field</span>
                            </label>
                          </FieldLabel>
                        </div>
                        {fieldForm.fieldType === 'Dropdown' && (
                          <FieldLabel label="Options (comma-separated)">
                            <input value={fieldForm.dropdownOptions ?? ''} onChange={(e) => setFieldForm((f) => ({ ...f, dropdownOptions: e.target.value }))} className="field-input mt-2" placeholder="Option A, Option B, Option C" />
                          </FieldLabel>
                        )}
                        <div className="mt-3 flex gap-2">
                          <button type="button" className="button-secondary" onClick={() => setEditingField(null)}>Cancel</button>
                          <button type="button" className="button-primary" onClick={() => void saveField()}>Save field</button>
                        </div>
                      </div>
                    )}
                  </div>
                )}
              </div>
            )
          })}
        </div>
      )}

      {showCategoryForm && (
        <div className="fixed inset-0 z-50 flex items-end justify-center sm:items-center bg-black/40 p-4" onClick={() => setShowCategoryForm(false)}>
          <div className="surface-card w-full max-w-lg space-y-4 rounded-2xl p-6" onClick={(e) => e.stopPropagation()}>
            <h3 className="text-lg font-semibold text-app">{editingCategory ? 'Edit category' : 'New category'}</h3>
            <div className="grid gap-3 sm:grid-cols-2">
              <FieldLabel label="Category name">
                <input value={categoryForm.name} onChange={(e) => setCategoryForm((f) => ({ ...f, name: e.target.value }))} className="field-input" />
              </FieldLabel>
              <FieldLabel label="Parent category (optional)">
                <input value={categoryForm.parentCategoryName ?? ''} onChange={(e) => setCategoryForm((f) => ({ ...f, parentCategoryName: e.target.value }))} className="field-input" placeholder="e.g. Electrical" />
              </FieldLabel>
              <FieldLabel label="Icon (emoji or text)">
                <input value={categoryForm.icon ?? ''} onChange={(e) => setCategoryForm((f) => ({ ...f, icon: e.target.value }))} className="field-input" placeholder="e.g. ⚡" />
              </FieldLabel>
              <FieldLabel label="Display order">
                <input type="number" value={categoryForm.displayOrder} onChange={(e) => setCategoryForm((f) => ({ ...f, displayOrder: Number(e.target.value) }))} className="field-input" />
              </FieldLabel>
            </div>
            <div className="flex items-center gap-4">
              <label className="flex items-center gap-2">
                <input type="checkbox" checked={categoryForm.isDefault} onChange={(e) => setCategoryForm((f) => ({ ...f, isDefault: e.target.checked }))} className="h-4 w-4" />
                <span className="text-sm text-app">Default category</span>
              </label>
              <label className="flex items-center gap-2">
                <input type="checkbox" checked={categoryForm.isActive} onChange={(e) => setCategoryForm((f) => ({ ...f, isActive: e.target.checked }))} className="h-4 w-4" />
                <span className="text-sm text-app">Active</span>
              </label>
            </div>
            <div className="flex justify-end gap-2">
              <button type="button" className="button-secondary" onClick={() => setShowCategoryForm(false)}>Cancel</button>
              <button type="button" className="button-primary" onClick={() => void saveCategory()}>Save category</button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}

function FieldLabel({ label, children }: { label: string; children: ReactNode }) {
  return (
    <label className="block space-y-1.5">
      <span className="text-xs font-semibold uppercase tracking-[0.18em] text-muted">{label}</span>
      {children}
    </label>
  )
}
