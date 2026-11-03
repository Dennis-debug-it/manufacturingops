import type { AssetCategoryField } from '../assetCategoriesApi'

const fieldClass = 'h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none transition focus:border-[var(--app-primary)]'

/** The category's datasheet fields (e.g. a generator's kVA and fuel type), filled in per asset. */
export function AssetDatasheetFields({
  categoryName,
  fields,
  values,
  missing,
  onChange,
}: {
  categoryName: string
  fields: AssetCategoryField[]
  values: Record<string, string>
  /** Required fields left empty on the last save attempt. */
  missing: string[]
  onChange: (fieldId: string, value: string) => void
}) {
  if (fields.length === 0) return null
  return (
    <fieldset data-testid="asset-custom-fields-panel" className="space-y-3 rounded-xl border border-slate-200 bg-slate-50/60 p-4">
      <legend className="px-1 text-sm font-semibold text-slate-800">{categoryName} datasheet</legend>
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
        {fields.map((field) => {
          const id = `asset-field-${field.id}`
          const value = values[field.id] ?? ''
          const label = (
            <label htmlFor={id} className="mb-1.5 block text-sm font-medium text-slate-700">
              {field.fieldLabel}{field.isRequired ? ' *' : ''}{field.unit ? ' ' : ''}
              {field.unit ? <span className="ml-1 text-xs font-normal text-slate-500">Unit: {field.unit}</span> : null}
            </label>
          )
          const options = (field.dropdownOptions ?? '').split(',').map((option) => option.trim()).filter(Boolean)
          const type = field.fieldType.toLowerCase()
          return (
            <div key={field.id} className={type === 'textarea' ? 'sm:col-span-2' : undefined}>
              {label}
              {type === 'dropdown' ? (
                <select id={id} value={value} onChange={(event) => onChange(field.id, event.target.value)} className={fieldClass}>
                  <option value="">Select…</option>
                  {options.map((option) => <option key={option} value={option}>{option}</option>)}
                </select>
              ) : type === 'textarea' ? (
                <textarea id={id} rows={3} value={value} onChange={(event) => onChange(field.id, event.target.value)} className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[var(--app-primary)]" />
              ) : (
                <input
                  id={id}
                  type={type === 'number' ? 'number' : type === 'date' ? 'date' : 'text'}
                  step={type === 'number' ? 'any' : undefined}
                  value={value}
                  onChange={(event) => onChange(field.id, event.target.value)}
                  className={fieldClass}
                />
              )}
              {field.helpText ? <p className="mt-1 text-xs text-slate-500">{field.helpText}</p> : null}
              {missing.includes(field.id) ? <p className="mt-1 text-xs text-rose-600">{field.fieldLabel} is required.</p> : null}
            </div>
          )
        })}
      </div>
    </fieldset>
  )
}
