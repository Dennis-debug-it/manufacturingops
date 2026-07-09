import { useEffect, useState } from 'react'
import toast from 'react-hot-toast'
import { ChevronDown, ChevronUp, Plus, Trash2 } from 'lucide-react'
import { FormDrawer } from '../../../shared/components/FormDrawer'
import { Spinner } from '../../../shared/components/Spinner'
import { useCreatePmTemplate, usePmTemplate, useUpdatePmTemplate, type UpsertPmTemplatePayload } from '../../maintenance/api'

const fieldClass = 'h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none transition focus:border-slate-900'
const smallFieldClass = 'h-10 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none transition focus:border-slate-900'
const labelClass = 'mb-1.5 block text-sm font-medium text-slate-700'

const questionTypes = [
  { value: 'checkbox', label: 'Checkbox' },
  { value: 'yesno', label: 'Yes / No' },
  { value: 'passfail', label: 'Pass / Fail' },
  { value: 'condition', label: 'Condition Rating' },
  { value: 'number', label: 'Number' },
  { value: 'reading', label: 'Meter Reading' },
  { value: 'text', label: 'Short Text' },
  { value: 'multilinetext', label: 'Long Text' },
  { value: 'date', label: 'Date' },
  { value: 'dropdown', label: 'Dropdown' },
  { value: 'photo', label: 'Photo' },
  { value: 'signature', label: 'Signature' },
] as const

const numericTypes = new Set(['number', 'reading'])
// Backend requires at least one option for dropdown, pass/fail, and condition questions.
const optionTypes = new Set(['dropdown', 'passfail', 'condition'])
const defaultOptionsByType: Record<string, string> = {
  passfail: 'Pass, Fail',
  condition: 'Good, Fair, Poor',
}

interface EditorItem {
  question: string
  helperText: string
  type: string
  unit: string
  minValue: string
  maxValue: string
  required: boolean
  evidenceRequired: boolean
  requiresNoteOnFail: boolean
  optionsText: string
}

interface EditorSection {
  name: string
  items: EditorItem[]
}

const emptyItem = (): EditorItem => ({
  question: '',
  helperText: '',
  type: 'checkbox',
  unit: '',
  minValue: '',
  maxValue: '',
  required: true,
  evidenceRequired: false,
  requiresNoteOnFail: false,
  optionsText: '',
})

const emptySection = (name = 'General'): EditorSection => ({ name, items: [emptyItem()] })

export interface PmTemplateEditorDrawerProps {
  open: boolean
  templateId: string | null
  onClose: () => void
}

export function PmTemplateEditorDrawer({ open, templateId, onClose }: PmTemplateEditorDrawerProps) {
  const isEdit = Boolean(templateId)
  const { data: existing, isLoading: loadingExisting } = usePmTemplate(open && templateId ? templateId : undefined)
  const createTemplate = useCreatePmTemplate()
  const updateTemplate = useUpdatePmTemplate(templateId ?? '')

  const [name, setName] = useState('')
  const [code, setCode] = useState('')
  const [category, setCategory] = useState('General')
  const [workType, setWorkType] = useState('PreventiveMaintenance')
  const [intervalMonths, setIntervalMonths] = useState('')
  const [description, setDescription] = useState('')
  const [isActive, setIsActive] = useState(true)
  const [sections, setSections] = useState<EditorSection[]>([emptySection()])

  useEffect(() => {
    if (!open) return
    if (!templateId) {
      setName('')
      setCode('')
      setCategory('General')
      setWorkType('PreventiveMaintenance')
      setIntervalMonths('')
      setDescription('')
      setIsActive(true)
      setSections([emptySection()])
    }
  }, [open, templateId])

  useEffect(() => {
    if (!open || !existing) return
    setName(existing.name)
    setCode(existing.code ?? '')
    setCategory(existing.category)
    setWorkType(existing.workType)
    setIntervalMonths(existing.defaultIntervalMonths != null ? String(existing.defaultIntervalMonths) : '')
    setDescription(existing.description ?? '')
    setIsActive(existing.isActive)

    const grouped = new Map<string, EditorItem[]>()
    const orderedNames: string[] = []
    for (const item of [...existing.checklist].sort((a, b) => a.sectionOrder - b.sectionOrder || a.order - b.order)) {
      const sectionName = item.sectionName?.trim() || 'General'
      if (!grouped.has(sectionName)) {
        grouped.set(sectionName, [])
        orderedNames.push(sectionName)
      }
      grouped.get(sectionName)!.push({
        question: item.question,
        helperText: item.helperText ?? '',
        type: item.type.toLowerCase(),
        unit: item.unit ?? '',
        minValue: item.minValue != null ? String(item.minValue) : '',
        maxValue: item.maxValue != null ? String(item.maxValue) : '',
        required: item.required,
        evidenceRequired: item.evidenceRequired,
        requiresNoteOnFail: item.requiresNoteOnFail,
        optionsText: (item.options ?? []).join(', '),
      })
    }
    setSections(orderedNames.length > 0 ? orderedNames.map((sectionName) => ({ name: sectionName, items: grouped.get(sectionName)! })) : [emptySection()])
  }, [open, existing])

  const updateSection = (index: number, patch: Partial<EditorSection>) =>
    setSections((current) => current.map((section, i) => (i === index ? { ...section, ...patch } : section)))

  const moveSection = (index: number, delta: number) =>
    setSections((current) => {
      const target = index + delta
      if (target < 0 || target >= current.length) return current
      const next = [...current]
      const [moved] = next.splice(index, 1)
      next.splice(target, 0, moved!)
      return next
    })

  const removeSection = (index: number) =>
    setSections((current) => (current.length > 1 ? current.filter((_, i) => i !== index) : current))

  const updateItem = (sectionIndex: number, itemIndex: number, patch: Partial<EditorItem>) =>
    setSections((current) =>
      current.map((section, si) =>
        si === sectionIndex
          ? { ...section, items: section.items.map((item, ii) => (ii === itemIndex ? { ...item, ...patch } : item)) }
          : section,
      ),
    )

  const addItem = (sectionIndex: number) =>
    setSections((current) => current.map((section, si) => (si === sectionIndex ? { ...section, items: [...section.items, emptyItem()] } : section)))

  const removeItem = (sectionIndex: number, itemIndex: number) =>
    setSections((current) =>
      current.map((section, si) =>
        si === sectionIndex && section.items.length > 1
          ? { ...section, items: section.items.filter((_, ii) => ii !== itemIndex) }
          : section,
      ),
    )

  const moveItem = (sectionIndex: number, itemIndex: number, delta: number) =>
    setSections((current) =>
      current.map((section, si) => {
        if (si !== sectionIndex) return section
        const target = itemIndex + delta
        if (target < 0 || target >= section.items.length) return section
        const items = [...section.items]
        const [moved] = items.splice(itemIndex, 1)
        items.splice(target, 0, moved!)
        return { ...section, items }
      }),
    )

  const submit = async () => {
    if (!name.trim()) {
      toast.error('Template name is required')
      return
    }
    const checklist: UpsertPmTemplatePayload['checklist'] = []
    let hasQuestion = false
    sections.forEach((section, sectionIndex) => {
      section.items.forEach((item, itemIndex) => {
        if (!item.question.trim()) return
        hasQuestion = true
        checklist.push({
          sectionName: section.name.trim() || 'General',
          sectionOrder: sectionIndex,
          question: item.question.trim(),
          helperText: item.helperText.trim() || null,
          type: item.type,
          unit: numericTypes.has(item.type) && item.unit.trim() ? item.unit.trim() : null,
          minValue: numericTypes.has(item.type) && item.minValue.trim() ? Number(item.minValue) : null,
          maxValue: numericTypes.has(item.type) && item.maxValue.trim() ? Number(item.maxValue) : null,
          defaultValue: null,
          required: item.required,
          evidenceRequired: item.evidenceRequired,
          requiresNoteOnFail: item.requiresNoteOnFail,
          order: itemIndex,
          options: optionTypes.has(item.type)
            ? item.optionsText.split(',').map((option) => option.trim()).filter(Boolean)
            : null,
        })
      })
    })
    if (!hasQuestion) {
      toast.error('Add at least one checklist question')
      return
    }

    const payload: UpsertPmTemplatePayload = {
      name: name.trim(),
      category: category.trim() || 'General',
      description: description.trim() || null,
      assetCategoryId: existing?.assetCategoryId ?? null,
      checklist,
      isActive,
      code: code.trim() || null,
      workType,
      defaultIntervalMonths: intervalMonths.trim() ? Number(intervalMonths) : null,
    }

    try {
      if (isEdit) {
        await updateTemplate.mutateAsync(payload)
        toast.success('Template updated')
      } else {
        await createTemplate.mutateAsync(payload)
        toast.success('Template created')
      }
      onClose()
    } catch {
      // interceptor surfaces the error toast
    }
  }

  const saving = createTemplate.isPending || updateTemplate.isPending

  return (
    <FormDrawer
      open={open}
      title={isEdit ? `Edit Template${existing ? ` — ${existing.name}` : ''}` : 'New PM Template'}
      onClose={onClose}
      footer={
        <div className="flex items-center justify-end gap-2">
          <button type="button" onClick={onClose} className="inline-flex h-11 items-center justify-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">Cancel</button>
          <button type="button" onClick={() => void submit()} disabled={saving || (isEdit && loadingExisting)} className="inline-flex h-11 items-center justify-center rounded-lg bg-slate-900 px-4 text-sm font-medium text-white transition hover:bg-slate-800 disabled:opacity-60">
            {saving ? 'Saving…' : isEdit ? 'Save Changes' : 'Create Template'}
          </button>
        </div>
      }
    >
      {isEdit && loadingExisting ? (
        <Spinner label="Loading template" />
      ) : (
        <div className="space-y-5">
          <div className="grid grid-cols-2 gap-3">
            <div className="col-span-2">
              <label className={labelClass} htmlFor="tpl-name">Template Name</label>
              <input id="tpl-name" value={name} onChange={(event) => setName(event.target.value)} className={fieldClass} />
            </div>
            <div>
              <label className={labelClass} htmlFor="tpl-code">Code</label>
              <input id="tpl-code" value={code} onChange={(event) => setCode(event.target.value)} placeholder="Optional" className={fieldClass} />
            </div>
            <div>
              <label className={labelClass} htmlFor="tpl-category">Category</label>
              <input id="tpl-category" value={category} onChange={(event) => setCategory(event.target.value)} className={fieldClass} />
            </div>
            <div>
              <label className={labelClass} htmlFor="tpl-worktype">Work Type</label>
              <select id="tpl-worktype" value={workType} onChange={(event) => setWorkType(event.target.value)} className={fieldClass}>
                <option value="PreventiveMaintenance">Preventive Maintenance</option>
                <option value="BreakdownCorrective">Breakdown / Corrective</option>
              </select>
            </div>
            <div>
              <label className={labelClass} htmlFor="tpl-interval">Default Interval (months)</label>
              <input id="tpl-interval" type="number" min="1" value={intervalMonths} onChange={(event) => setIntervalMonths(event.target.value)} placeholder="e.g. 3" className={fieldClass} />
            </div>
            <div className="col-span-2">
              <label className={labelClass} htmlFor="tpl-description">Description</label>
              <textarea id="tpl-description" value={description} onChange={(event) => setDescription(event.target.value)} rows={2} className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-slate-900" />
            </div>
            <label className="col-span-2 flex items-center gap-2 text-sm text-slate-700">
              <input type="checkbox" checked={isActive} onChange={(event) => setIsActive(event.target.checked)} className="h-4 w-4 rounded border-slate-300" />
              Active (available when scheduling PM plans)
            </label>
          </div>

          <div className="space-y-4">
            <div className="flex items-center justify-between">
              <h3 className="text-sm font-semibold uppercase tracking-[0.16em] text-slate-500">Checklist Sections</h3>
              <button type="button" onClick={() => setSections((current) => [...current, emptySection(`Section ${current.length + 1}`)])} className="inline-flex h-9 items-center gap-1.5 rounded-lg border border-slate-200 bg-white px-3 text-xs font-medium text-slate-700 hover:bg-slate-50">
                <Plus className="h-3.5 w-3.5" />Add Section
              </button>
            </div>

            {sections.map((section, sectionIndex) => (
              <div key={sectionIndex} className="space-y-3 rounded-xl border border-slate-200 p-3">
                <div className="flex items-center gap-2">
                  <input
                    value={section.name}
                    onChange={(event) => updateSection(sectionIndex, { name: event.target.value })}
                    placeholder="Section name"
                    className={cnSection}
                  />
                  <button type="button" onClick={() => moveSection(sectionIndex, -1)} disabled={sectionIndex === 0} aria-label="Move section up" className={iconBtn}>
                    <ChevronUp className="h-4 w-4" />
                  </button>
                  <button type="button" onClick={() => moveSection(sectionIndex, 1)} disabled={sectionIndex === sections.length - 1} aria-label="Move section down" className={iconBtn}>
                    <ChevronDown className="h-4 w-4" />
                  </button>
                  <button type="button" onClick={() => removeSection(sectionIndex)} disabled={sections.length === 1} aria-label="Remove section" className={`${iconBtn} hover:bg-rose-50 hover:text-rose-600`}>
                    <Trash2 className="h-4 w-4" />
                  </button>
                </div>

                {section.items.map((item, itemIndex) => (
                  <div key={itemIndex} className="space-y-2 rounded-lg border border-slate-100 bg-slate-50/60 p-3">
                    <div className="flex items-center gap-2">
                      <input
                        value={item.question}
                        onChange={(event) => updateItem(sectionIndex, itemIndex, { question: event.target.value })}
                        placeholder="Question / check item"
                        className={smallFieldClass}
                      />
                      <button type="button" onClick={() => moveItem(sectionIndex, itemIndex, -1)} disabled={itemIndex === 0} aria-label="Move up" className={iconBtnSm}>
                        <ChevronUp className="h-3.5 w-3.5" />
                      </button>
                      <button type="button" onClick={() => moveItem(sectionIndex, itemIndex, 1)} disabled={itemIndex === section.items.length - 1} aria-label="Move down" className={iconBtnSm}>
                        <ChevronDown className="h-3.5 w-3.5" />
                      </button>
                      <button type="button" onClick={() => removeItem(sectionIndex, itemIndex)} disabled={section.items.length === 1} aria-label="Remove question" className={`${iconBtnSm} hover:bg-rose-50 hover:text-rose-600`}>
                        <Trash2 className="h-3.5 w-3.5" />
                      </button>
                    </div>
                    <div className="grid grid-cols-2 gap-2">
                      <select
                        value={item.type}
                        onChange={(event) => {
                          const nextType = event.target.value
                          updateItem(sectionIndex, itemIndex, {
                            type: nextType,
                            ...(optionTypes.has(nextType) && !item.optionsText.trim()
                              ? { optionsText: defaultOptionsByType[nextType] ?? '' }
                              : {}),
                          })
                        }}
                        className={smallFieldClass}
                        aria-label="Response type"
                      >
                        {questionTypes.map((option) => (
                          <option key={option.value} value={option.value}>{option.label}</option>
                        ))}
                      </select>
                      <input
                        value={item.helperText}
                        onChange={(event) => updateItem(sectionIndex, itemIndex, { helperText: event.target.value })}
                        placeholder="Helper text (optional)"
                        className={smallFieldClass}
                      />
                    </div>
                    {numericTypes.has(item.type) ? (
                      <div className="grid grid-cols-3 gap-2">
                        <input value={item.unit} onChange={(event) => updateItem(sectionIndex, itemIndex, { unit: event.target.value })} placeholder="Unit (e.g. °C)" className={smallFieldClass} />
                        <input type="number" value={item.minValue} onChange={(event) => updateItem(sectionIndex, itemIndex, { minValue: event.target.value })} placeholder="Min" className={smallFieldClass} />
                        <input type="number" value={item.maxValue} onChange={(event) => updateItem(sectionIndex, itemIndex, { maxValue: event.target.value })} placeholder="Max" className={smallFieldClass} />
                      </div>
                    ) : null}
                    {optionTypes.has(item.type) ? (
                      <input
                        value={item.optionsText}
                        onChange={(event) => updateItem(sectionIndex, itemIndex, { optionsText: event.target.value })}
                        placeholder="Options, comma-separated (e.g. Good, Fair, Poor)"
                        className={smallFieldClass}
                      />
                    ) : null}
                    <div className="flex flex-wrap gap-4 text-xs text-slate-700">
                      <label className="flex items-center gap-1.5">
                        <input type="checkbox" checked={item.required} onChange={(event) => updateItem(sectionIndex, itemIndex, { required: event.target.checked })} className="h-3.5 w-3.5 rounded border-slate-300" />
                        Required
                      </label>
                      <label className="flex items-center gap-1.5">
                        <input type="checkbox" checked={item.evidenceRequired} onChange={(event) => updateItem(sectionIndex, itemIndex, { evidenceRequired: event.target.checked })} className="h-3.5 w-3.5 rounded border-slate-300" />
                        Photo evidence required
                      </label>
                      <label className="flex items-center gap-1.5">
                        <input type="checkbox" checked={item.requiresNoteOnFail} onChange={(event) => updateItem(sectionIndex, itemIndex, { requiresNoteOnFail: event.target.checked })} className="h-3.5 w-3.5 rounded border-slate-300" />
                        Note required on fail
                      </label>
                    </div>
                  </div>
                ))}

                <button type="button" onClick={() => addItem(sectionIndex)} className="text-xs font-medium text-slate-600 underline decoration-slate-300 underline-offset-4 hover:text-slate-900">
                  + Add question
                </button>
              </div>
            ))}
          </div>
        </div>
      )}
    </FormDrawer>
  )
}

const cnSection = 'h-10 w-full rounded-lg border border-slate-300 px-3 text-sm font-medium outline-none transition focus:border-slate-900'
const iconBtn = 'inline-flex h-10 w-10 shrink-0 items-center justify-center rounded-lg border border-slate-200 bg-white text-slate-500 transition hover:bg-slate-50 disabled:opacity-40'
const iconBtnSm = 'inline-flex h-9 w-9 shrink-0 items-center justify-center rounded-lg border border-slate-200 bg-white text-slate-500 transition hover:bg-slate-50 disabled:opacity-40'
