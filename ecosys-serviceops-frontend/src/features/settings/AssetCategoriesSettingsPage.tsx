import { useEffect, useState } from 'react'
import toast from 'react-hot-toast'
import { Plus } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { Spinner } from '../../shared/components/Spinner'
import { usePermission } from '../../shared/hooks/usePermission'
import { cn } from '../../shared/utils/cn'
import {
  ASSET_FIELD_TYPES,
  useAssetCategories,
  useDeleteAssetCategory,
  useSaveAssetCategory,
  useSaveAssetCategoryField,
  type AssetCategory,
  type AssetCategoryField,
} from '../clients/assetCategoriesApi'
import { StarterPacks } from './components/StarterPacks'

const inputClass = 'h-10 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]'
const labelClass = 'mb-1 block text-sm font-medium text-slate-700'
const fieldTypeLabels: Record<string, string> = { Text: 'Text', Number: 'Number', Date: 'Date', Dropdown: 'Dropdown (pick from a list)', TextArea: 'Long text' }

function CategoryForm({ category, onDone }: { category?: AssetCategory; onDone: (saved: AssetCategory) => void }) {
  const save = useSaveAssetCategory()
  const [name, setName] = useState(category?.name ?? '')
  const [parent, setParent] = useState(category?.parentCategoryName ?? '')
  const [interval, setInterval] = useState(category?.defaultServiceIntervalMonths ? String(category.defaultServiceIntervalMonths) : '')

  const submit = async (event: React.FormEvent) => {
    event.preventDefault()
    if (!name.trim()) return toast.error('Enter a category name')
    try {
      const saved = await save.mutateAsync({
        ...(category ? { id: category.id } : {}),
        payload: {
          name: name.trim(),
          parentCategoryName: parent.trim() || null,
          defaultServiceIntervalMonths: Number(interval) > 0 ? Number(interval) : null,
          description: category?.description ?? null,
          icon: null,
          displayOrder: category?.displayOrder ?? 0,
        },
      })
      toast.success(category ? 'Category saved' : `Added ${saved.name}`)
      onDone(saved)
    } catch {
      // interceptor surfaces the error toast
    }
  }

  return (
    <form onSubmit={submit} className="grid grid-cols-1 gap-3 rounded-xl border border-slate-200 bg-white p-4 sm:grid-cols-3">
      <div>
        <label className={labelClass} htmlFor="category-name">Category name</label>
        <input id="category-name" value={name} onChange={(event) => setName(event.target.value)} placeholder="e.g. Pump" className={inputClass} />
      </div>
      <div>
        <label className={labelClass} htmlFor="category-parent">Parent category (optional)</label>
        <input id="category-parent" value={parent} onChange={(event) => setParent(event.target.value)} placeholder="e.g. Mechanical" className={inputClass} />
      </div>
      <div>
        <label className={labelClass} htmlFor="category-interval">Service every (months, optional)</label>
        <input id="category-interval" type="number" min={1} value={interval} onChange={(event) => setInterval(event.target.value)} className={inputClass} />
      </div>
      <div className="sm:col-span-3">
        <button type="submit" disabled={save.isPending} className="inline-flex h-10 items-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white disabled:opacity-60">
          {save.isPending ? 'Saving…' : 'Save category'}
        </button>
      </div>
    </form>
  )
}

function FieldForm({ categoryId, field, nextOrder, onDone }: { categoryId: string; field?: AssetCategoryField; nextOrder: number; onDone: () => void }) {
  const save = useSaveAssetCategoryField(categoryId)
  const [label, setLabel] = useState(field?.fieldLabel ?? '')
  const [type, setType] = useState(field?.fieldType ?? 'Text')
  const [unit, setUnit] = useState(field?.unit ?? '')
  const [options, setOptions] = useState(field?.dropdownOptions ?? '')
  const [helpText, setHelpText] = useState(field?.helpText ?? '')
  const [required, setRequired] = useState(field?.isRequired ?? false)

  const submit = async (event: React.FormEvent) => {
    event.preventDefault()
    if (!label.trim()) return toast.error('Enter a label for the field')
    if (type === 'Dropdown' && !options.trim()) return toast.error('List the choices for the dropdown, separated by commas')
    try {
      await save.mutateAsync({
        ...(field ? { fieldId: field.id } : {}),
        payload: {
          fieldLabel: label.trim(),
          fieldType: type,
          unit: unit.trim() || null,
          dropdownOptions: type === 'Dropdown' ? options.split(',').map((option) => option.trim()).filter(Boolean).join(',') : null,
          helpText: helpText.trim() || null,
          isRequired: required,
          displayOrder: field?.displayOrder ?? nextOrder,
          isActive: field?.isActive ?? true,
        },
      })
      toast.success(field ? 'Field saved' : `Added ${label.trim()}`)
      onDone()
    } catch {
      // interceptor surfaces the error toast
    }
  }

  return (
    <form onSubmit={submit} className="grid grid-cols-1 gap-3 rounded-xl border border-slate-200 bg-slate-50 p-4 sm:grid-cols-2">
      <div>
        <label className={labelClass} htmlFor="field-label">Display label</label>
        <input id="field-label" value={label} onChange={(event) => setLabel(event.target.value)} placeholder="e.g. Flow Rate" className={inputClass} />
      </div>
      <div>
        <label className={labelClass} htmlFor="field-type">Field type</label>
        <select id="field-type" value={type} onChange={(event) => setType(event.target.value)} className={inputClass}>
          {ASSET_FIELD_TYPES.map((option) => <option key={option} value={option}>{fieldTypeLabels[option]}</option>)}
        </select>
      </div>
      <div>
        <label className={labelClass} htmlFor="field-unit">Unit (optional)</label>
        <input id="field-unit" value={unit} onChange={(event) => setUnit(event.target.value)} placeholder="e.g. kVA, L/min" className={inputClass} />
      </div>
      {type === 'Dropdown' ? (
        <div>
          <label className={labelClass} htmlFor="field-options">Choices (comma-separated)</label>
          <input id="field-options" value={options} onChange={(event) => setOptions(event.target.value)} placeholder="Diesel, Petrol, Gas" className={inputClass} />
        </div>
      ) : (
        <div>
          <label className={labelClass} htmlFor="field-help">Hint (optional)</label>
          <input id="field-help" value={helpText} onChange={(event) => setHelpText(event.target.value)} className={inputClass} />
        </div>
      )}
      <label className="flex items-center gap-2 text-sm text-slate-700 sm:col-span-2">
        <input type="checkbox" checked={required} onChange={(event) => setRequired(event.target.checked)} />
        Required when saving an asset
      </label>
      <div className="flex gap-2 sm:col-span-2">
        <button type="submit" disabled={save.isPending} className="inline-flex h-10 items-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white disabled:opacity-60">
          {save.isPending ? 'Saving…' : 'Save field'}
        </button>
        <button type="button" onClick={onDone} className="inline-flex h-10 items-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700">Cancel</button>
      </div>
    </form>
  )
}

/**
 * Asset categories and their datasheet fields — what an asset of that kind records (a generator's kVA,
 * fuel type, engine make…). Starter packs add ready-made ones; anything can be edited or added here.
 */
export function AssetCategoriesSettingsPage() {
  const canManage = usePermission('settings.manage')
  const { data, isLoading, isError } = useAssetCategories()
  const deleteCategory = useDeleteAssetCategory()
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [adding, setAdding] = useState(false)
  const [editingCategory, setEditingCategory] = useState(false)
  const [fieldForm, setFieldForm] = useState<'new' | string | null>(null)

  const categories = (data ?? []).filter((category) => category.isActive)
  const selected = categories.find((category) => category.id === selectedId) ?? null
  useEffect(() => {
    if (!selectedId && categories[0]) setSelectedId(categories[0].id)
  }, [categories, selectedId])
  useEffect(() => { setFieldForm(null); setEditingCategory(false) }, [selectedId])

  const toggleField = useSaveAssetCategoryField(selected?.id ?? '')

  const setFieldActive = async (field: AssetCategoryField, isActive: boolean) => {
    try {
      await toggleField.mutateAsync({
        fieldId: field.id,
        payload: {
          fieldLabel: field.fieldLabel, fieldType: field.fieldType, unit: field.unit ?? null, dropdownOptions: field.dropdownOptions ?? null,
          helpText: field.helpText ?? null, isRequired: field.isRequired, displayOrder: field.displayOrder, isActive,
        },
      })
    } catch {
      // interceptor surfaces the error toast
    }
  }

  const removeCategory = async (category: AssetCategory) => {
    if (!window.confirm(`Remove the "${category.name}" category? Assets already using it keep their details.`)) return
    try {
      await deleteCategory.mutateAsync(category.id)
      toast.success(`${category.name} removed`)
      setSelectedId(null)
    } catch {
      // interceptor surfaces the error toast (e.g. assets still use it)
    }
  }

  const fields = [...(selected?.fields ?? [])].sort((a, b) => a.displayOrder - b.displayOrder)

  return (
    <div className="space-y-4">
      <PageHeader
        title="Asset Categories"
        subtitle="The kinds of equipment you maintain, and the datasheet details recorded for each."
        primaryAction={canManage ? (
          <button type="button" onClick={() => { setAdding(true); setSelectedId(null) }} className="inline-flex h-11 items-center gap-2 rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white">
            <Plus className="h-4 w-4" /> Add category
          </button>
        ) : undefined}
      />

      {adding ? <CategoryForm onDone={(saved) => { setAdding(false); setSelectedId(saved.id) }} /> : null}

      {isLoading ? <Spinner label="Loading categories" /> : isError ? (
        <p className="text-sm text-rose-600">Asset categories could not be loaded.</p>
      ) : (
        <div className="grid grid-cols-1 gap-4 lg:grid-cols-[260px_minmax(0,1fr)]">
          <nav aria-label="Categories" className="space-y-1 rounded-xl border border-slate-200 bg-white p-2">
            {categories.length === 0 ? <p className="p-2 text-sm text-slate-500">No categories yet.</p> : null}
            {categories.map((category) => (
              <button
                key={category.id}
                type="button"
                onClick={() => { setSelectedId(category.id); setAdding(false) }}
                aria-current={category.id === selected?.id}
                className={cn('flex w-full items-center justify-between rounded-lg px-3 py-2 text-left text-sm', category.id === selected?.id ? 'bg-[var(--app-primary)] text-white' : 'text-slate-700 hover:bg-slate-50')}
              >
                <span>{category.parentCategoryName ? <span className="opacity-70">{category.parentCategoryName} › </span> : null}{category.name}</span>
                <span className="text-xs opacity-70">{category.fields.filter((field) => field.isActive !== false).length}</span>
              </button>
            ))}
          </nav>

          {selected ? (
            <section className="space-y-3 rounded-xl border border-slate-200 bg-white p-4" aria-label={`${selected.name} fields`}>
              <div className="flex flex-wrap items-start justify-between gap-2">
                <div>
                  <h2 className="text-base font-semibold text-slate-900">{selected.name}</h2>
                  <p className="text-xs text-slate-500">
                    {selected.parentCategoryName ? `${selected.parentCategoryName} · ` : ''}
                    {selected.defaultServiceIntervalMonths ? (selected.defaultServiceIntervalMonths === 1 ? 'Serviced monthly' : `Service every ${selected.defaultServiceIntervalMonths} months`) : 'No default service interval'}
                  </p>
                </div>
                {canManage ? (
                  <div className="flex gap-2">
                    <button type="button" onClick={() => setEditingCategory((value) => !value)} className="h-9 rounded-lg border border-slate-200 px-3 text-sm text-slate-700">Edit</button>
                    <button type="button" onClick={() => void removeCategory(selected)} className="h-9 rounded-lg border border-rose-200 px-3 text-sm text-rose-700">Remove</button>
                  </div>
                ) : null}
              </div>
              {editingCategory ? <CategoryForm category={selected} onDone={() => setEditingCategory(false)} /> : null}

              <div className="flex items-center justify-between">
                <h3 className="text-sm font-semibold text-slate-800">Datasheet fields</h3>
                {canManage && fieldForm === null ? (
                  <button type="button" onClick={() => setFieldForm('new')} className="inline-flex h-9 items-center gap-1 rounded-lg border border-slate-200 px-3 text-sm text-slate-700">
                    <Plus className="h-4 w-4" /> Add field
                  </button>
                ) : null}
              </div>
              {fieldForm === 'new' ? <FieldForm categoryId={selected.id} nextOrder={fields.length} onDone={() => setFieldForm(null)} /> : null}

              {fields.length === 0 ? (
                <p className="text-sm text-slate-500">No fields yet. Add the details you record for this kind of equipment, like capacity, make or serial number.</p>
              ) : (
                <ul className="divide-y divide-slate-100">
                  {fields.map((field) => (
                    <li key={field.id} className="py-2">
                      {fieldForm === field.id ? (
                        <FieldForm categoryId={selected.id} field={field} nextOrder={fields.length} onDone={() => setFieldForm(null)} />
                      ) : (
                        <div className={cn('flex flex-wrap items-center justify-between gap-2', field.isActive === false && 'opacity-50')}>
                          <div>
                            <span className="text-sm font-medium text-slate-900">{field.fieldLabel}</span>
                            <span className="ml-2 text-xs text-slate-500">
                              {fieldTypeLabels[field.fieldType] ?? field.fieldType}{field.unit ? ` · ${field.unit}` : ''}{field.isRequired ? ' · required' : ''}{field.isActive === false ? ' · switched off' : ''}
                            </span>
                            {field.dropdownOptions ? <p className="text-xs text-slate-500">{field.dropdownOptions.split(',').join(', ')}</p> : null}
                          </div>
                          {canManage ? (
                            <div className="flex gap-2">
                              <button type="button" onClick={() => setFieldForm(field.id)} className="h-8 rounded-lg border border-slate-200 px-2 text-xs text-slate-700">Edit</button>
                              <button type="button" onClick={() => void setFieldActive(field, field.isActive === false)} className="h-8 rounded-lg border border-slate-200 px-2 text-xs text-slate-700">
                                {field.isActive === false ? 'Switch on' : 'Switch off'}
                              </button>
                            </div>
                          ) : null}
                        </div>
                      )}
                    </li>
                  ))}
                </ul>
              )}
            </section>
          ) : !adding ? (
            <p className="rounded-xl border border-dashed border-slate-300 p-6 text-sm text-slate-500">Pick a category to see its datasheet fields.</p>
          ) : null}
        </div>
      )}

      <div className="rounded-2xl border border-slate-200 bg-slate-50/60 p-4">
        <StarterPacks />
      </div>
    </div>
  )
}
