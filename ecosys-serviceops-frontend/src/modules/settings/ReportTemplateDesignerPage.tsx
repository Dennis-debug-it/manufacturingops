import { useEffect, useState } from 'react'
import { GripVertical, Eye, RefreshCw, ToggleLeft, ToggleRight } from 'lucide-react'
import { PageHeader } from '../../components/ui/PageHeader'
import { LoadingState } from '../../components/ui/LoadingState'
import { ErrorState } from '../../components/ui/ErrorState'
import { useToast } from '../../components/ui/ToastProvider'
import { useAsyncData } from '../../hooks/useAsyncData'
import { useUnsavedChangesGuard } from '../../hooks/useUnsavedChangesGuard'
import { api } from '../../lib/api'
import { useAuth } from '../../auth/AuthContext'

// ── Types ──────────────────────────────────────────────────────────────────────

interface ReportSection {
  id: string
  label: string
  enabled: boolean
  order: number
}

interface ReportTemplateConfig {
  sections: ReportSection[]
  fontFamily: string
  reportPrimaryColor: string | null
  reportSecondaryColor: string | null
  footerText: string | null
  watermarkText: string | null
  logoAlignment: 'Left' | 'Center' | 'Right'
  showCompanyName: boolean
  showCompanyContact: boolean
}

const DEFAULT_SECTIONS: ReportSection[] = [
  { id: 'service-details', label: 'Service Details',       enabled: true, order: 0 },
  { id: 'problem',         label: 'Problem Reported',      enabled: true, order: 1 },
  { id: 'findings',        label: 'Findings',              enabled: true, order: 2 },
  { id: 'work-done',       label: 'Work Done',             enabled: true, order: 3 },
  { id: 'timestamps',      label: 'Timestamps',            enabled: true, order: 4 },
  { id: 'checklist',       label: 'Checklist Responses',   enabled: true, order: 5 },
  { id: 'materials',       label: 'Materials Used',        enabled: true, order: 6 },
  { id: 'photos',          label: 'Photo Evidence',        enabled: true, order: 7 },
  { id: 'signatures',      label: 'Signatures / Sign-off', enabled: true, order: 8 },
]

const DEFAULT_CONFIG: ReportTemplateConfig = {
  sections: DEFAULT_SECTIONS,
  fontFamily: 'Helvetica',
  reportPrimaryColor: null,
  reportSecondaryColor: null,
  footerText: null,
  watermarkText: null,
  logoAlignment: 'Left',
  showCompanyName: true,
  showCompanyContact: true,
}

const FONT_OPTIONS = ['Helvetica', 'Arial', 'Times New Roman']

// ── Presets ───────────────────────────────────────────────────────────────────

/** Sections enabled for the Minimal preset — essentials only. */
const MINIMAL_SECTIONS: ReportSection[] = DEFAULT_SECTIONS.map((s) => ({
  ...s,
  enabled: ['service-details', 'problem', 'work-done', 'signatures'].includes(s.id),
}))

type PresetId = 'standard' | 'minimal' | 'detailed'

const PRESETS: Array<{
  id: PresetId
  label: string
  description: string
  font: string
  config: Partial<ReportTemplateConfig>
}> = [
  {
    id: 'standard',
    label: 'Standard',
    description: 'All sections, clean layout',
    font: 'Helvetica',
    config: {
      sections: DEFAULT_SECTIONS,
      fontFamily: 'Helvetica',
      logoAlignment: 'Left',
      showCompanyName: true,
      showCompanyContact: true,
      footerText: 'Thank you for choosing [Company Name]',
      watermarkText: null,
      reportPrimaryColor: null,
      reportSecondaryColor: null,
    },
  },
  {
    id: 'minimal',
    label: 'Minimal',
    description: 'Essentials only',
    font: 'Arial',
    config: {
      sections: MINIMAL_SECTIONS,
      fontFamily: 'Arial',
      logoAlignment: 'Left',
      showCompanyName: true,
      showCompanyContact: false,
      footerText: null,
      watermarkText: null,
      reportPrimaryColor: '#64748b',
      reportSecondaryColor: '#94a3b8',
    },
  },
  {
    id: 'detailed',
    label: 'Detailed',
    description: 'Full audit trail',
    font: 'Times New Roman',
    config: {
      sections: DEFAULT_SECTIONS.map((s) => ({ ...s, enabled: true })),
      fontFamily: 'Times New Roman',
      logoAlignment: 'Center',
      showCompanyName: true,
      showCompanyContact: true,
      footerText: 'This report is a certified record of work completed.',
      watermarkText: 'VERIFIED',
      reportPrimaryColor: '#1e3a5f',
      reportSecondaryColor: '#2563eb',
    },
  },
]

// ── Component ──────────────────────────────────────────────────────────────────

export function ReportTemplateDesignerPage() {
  const { session } = useAuth()
  const { pushToast } = useToast()

  const { data, loading, error, reload } = useAsyncData(
    (signal) => api.get<ReportTemplateConfig>('/api/settings/report-template', { signal }),
    DEFAULT_CONFIG,
    [],
  )

  const [form, setForm] = useState<ReportTemplateConfig>(DEFAULT_CONFIG)
  const [saving, setSaving] = useState(false)
  const [dragIndex, setDragIndex] = useState<number | null>(null)
  const [activePreset, setActivePreset] = useState<PresetId | null>(null)
  const isDirty = JSON.stringify(form) !== JSON.stringify(data)
  useUnsavedChangesGuard(isDirty)

  useEffect(() => {
    if (data) setForm({ ...DEFAULT_CONFIG, ...data, sections: data.sections?.length ? data.sections : DEFAULT_SECTIONS })
  }, [data])

  /**
   * Patch form state from a manual user edit.
   * Clears the active preset highlight so the UI reflects that the form is now customised.
   */
  function patchForm(updater: Partial<ReportTemplateConfig> | ((prev: ReportTemplateConfig) => ReportTemplateConfig)) {
    setActivePreset(null)
    if (typeof updater === 'function') {
      setForm(updater)
    } else {
      setForm((prev) => ({ ...prev, ...updater }))
    }
  }

  // Section drag-to-reorder helpers
  function onDragStart(index: number) { setDragIndex(index) }
  function onDragOver(event: React.DragEvent, index: number) {
    event.preventDefault()
    if (dragIndex === null || dragIndex === index) return
    patchForm((current) => {
      const next = [...current.sections]
      const [moved] = next.splice(dragIndex, 1)
      next.splice(index, 0, moved)
      return { ...current, sections: next.map((s, i) => ({ ...s, order: i })) }
    })
    setDragIndex(index)
  }
  function onDragEnd() { setDragIndex(null) }

  function toggleSection(id: string) {
    patchForm((current) => ({
      ...current,
      sections: current.sections.map((s) => s.id === id ? { ...s, enabled: !s.enabled } : s),
    }))
  }

  function updateSectionLabel(id: string, label: string) {
    patchForm((current) => ({
      ...current,
      sections: current.sections.map((section) => (
        section.id === id
          ? { ...section, label }
          : section
      )),
    }))
  }

  function applyPreset(preset: typeof PRESETS[number]) {
    setForm((prev) => ({ ...prev, ...preset.config }))
    setActivePreset(preset.id)
  }

  async function save() {
    setSaving(true)
    try {
      await api.put<ReportTemplateConfig>('/api/settings/report-template', form)
      pushToast({ title: 'Template saved', description: 'Your report template is now active for all new PDF reports.', tone: 'success' })
      await reload()
    } catch (saveError) {
      pushToast({ title: 'Save failed', description: saveError instanceof Error ? saveError.message : 'Unable to save template.', tone: 'danger' })
    } finally {
      setSaving(false)
    }
  }

  function resetToDefaults() {
    setForm(DEFAULT_CONFIG)
    setActivePreset(null)
    pushToast({ title: 'Reset to defaults', description: 'All sections re-enabled with default order.', tone: 'success' })
  }

  if (loading) return <LoadingState label="Loading report template" />
  if (error) return <ErrorState title="Unable to load report template" description={error} />

  const primaryColor   = form.reportPrimaryColor  ?? session?.primaryColor  ?? '#0F4C81'
  const secondaryColor = form.reportSecondaryColor ?? session?.secondaryColor ?? '#F4B942'
  const companyName    = session?.tenantName ?? 'Your Company'

  return (
    <div className="space-y-6">
      <PageHeader
        eyebrow="Settings"
        title="Report Template Designer"
        description="Control which sections appear on PDF work order reports, their order, font, colors, and custom footer messaging."
        actions={
          <div className="flex items-center gap-3">
            {isDirty ? <span className="text-xs font-semibold text-amber-400">Unsaved changes</span> : null}
            <button type="button" className="button-secondary" onClick={resetToDefaults}>
              <RefreshCw className="h-4 w-4" />
              Reset defaults
            </button>
            <button type="button" className="button-primary" onClick={() => void save()} disabled={saving}>
              {saving ? 'Saving…' : 'Save template'}
            </button>
          </div>
        }
      />

      <div className="grid gap-6 xl:grid-cols-[1fr_420px]">
        {/* ── Left: configuration ───────────────────────────────────── */}
        <div className="space-y-5">

          {/* ── Quick Start Presets ──────────────────────────────────── */}
          <section className="surface-card space-y-3">
            <div>
              <h2 className="text-lg font-semibold text-app">Quick Start Presets</h2>
              <p className="mt-1 text-sm text-muted">Click a preset to populate the form, then customise.</p>
            </div>
            <div className="flex gap-3">
              {PRESETS.map((preset) => {
                const isActive = activePreset === preset.id
                return (
                  <button
                    key={preset.id}
                    type="button"
                    onClick={() => applyPreset(preset)}
                    className={`flex flex-1 flex-col gap-1 rounded-2xl border px-4 py-3 text-left transition hover:bg-[var(--app-subtle)] ${
                      isActive
                        ? 'border-[var(--app-primary-strong)] bg-[var(--app-subtle)] ring-1 ring-[var(--app-primary-strong)]'
                        : 'border-app'
                    }`}
                  >
                    <span className="text-sm font-semibold text-app">{preset.label}</span>
                    <span className="text-xs text-muted">{preset.description}</span>
                    <span className="mt-1 text-[10px] text-muted/70">{preset.font}</span>
                  </button>
                )
              })}
            </div>
          </section>

          {/* Section toggles + drag reorder */}
          <section className="surface-card space-y-4">
            <div>
              <h2 className="text-lg font-semibold text-app">Sections</h2>
              <p className="mt-1 text-sm text-muted">
                Drag to reorder. Toggle each section on or off. Disabled sections are omitted from the PDF.
              </p>
            </div>
            <div className="space-y-2">
              {form.sections.map((section, index) => (
                <div
                  key={section.id}
                  draggable
                  onDragStart={() => onDragStart(index)}
                  onDragOver={(event) => onDragOver(event, index)}
                  onDragEnd={onDragEnd}
                  className={`flex items-center gap-3 rounded-2xl border px-4 py-3 transition ${
                    dragIndex === index ? 'opacity-50' : 'opacity-100'
                  } ${section.enabled ? 'border-app bg-[var(--app-surface)]' : 'border-app/40 bg-[var(--app-subtle)] opacity-60'}`}
                >
                  <GripVertical className="h-4 w-4 shrink-0 cursor-grab text-muted" />
                  <div className="flex-1 space-y-2">
                    <input
                      type="text"
                      value={section.label}
                      onChange={(event) => updateSectionLabel(section.id, event.target.value)}
                      className="field-input py-2 text-sm font-medium"
                      placeholder="Section title"
                    />
                    <p className="text-xs text-muted">Shown in the final report exactly as entered here.</p>
                  </div>
                  <button
                    type="button"
                    aria-label={section.enabled ? `Disable ${section.label}` : `Enable ${section.label}`}
                    onClick={() => toggleSection(section.id)}
                    className="shrink-0 text-muted transition hover:text-app"
                  >
                    {section.enabled
                      ? <ToggleRight className="h-6 w-6 text-[var(--app-primary-strong)]" />
                      : <ToggleLeft className="h-6 w-6" />}
                  </button>
                </div>
              ))}
            </div>
          </section>

          {/* Style options */}
          <section className="surface-card space-y-4">
            <h2 className="text-lg font-semibold text-app">Style</h2>
            <div className="grid gap-4 sm:grid-cols-2">
              <label className="space-y-2">
                <span className="text-sm font-medium text-app">Font</span>
                <select
                  value={form.fontFamily}
                  onChange={(event) => patchForm({ fontFamily: event.target.value })}
                  className="field-input"
                >
                  {FONT_OPTIONS.map((f) => <option key={f} value={f}>{f}</option>)}
                </select>
              </label>
              <label className="space-y-2">
                <span className="text-sm font-medium text-app">Logo alignment</span>
                <select
                  value={form.logoAlignment}
                  onChange={(event) => patchForm({ logoAlignment: event.target.value as ReportTemplateConfig['logoAlignment'] })}
                  className="field-input"
                >
                  <option value="Left">Left</option>
                  <option value="Center">Center</option>
                  <option value="Right">Right</option>
                </select>
              </label>
              <label className="space-y-2">
                <span className="text-sm font-medium text-app">Report primary color</span>
                <div className="flex items-center gap-3">
                  <input
                    type="color"
                    value={form.reportPrimaryColor ?? primaryColor}
                    onChange={(event) => patchForm({ reportPrimaryColor: event.target.value })}
                    className="h-10 w-10 cursor-pointer rounded-xl border border-app bg-transparent p-0.5"
                  />
                  <input
                    type="text"
                    value={form.reportPrimaryColor ?? ''}
                    onChange={(event) => patchForm({ reportPrimaryColor: event.target.value || null })}
                    placeholder={`Tenant default: ${primaryColor}`}
                    className="field-input font-mono text-sm"
                    maxLength={7}
                  />
                </div>
              </label>
              <label className="space-y-2">
                <span className="text-sm font-medium text-app">Report border/accent color</span>
                <div className="flex items-center gap-3">
                  <input
                    type="color"
                    value={form.reportSecondaryColor ?? secondaryColor}
                    onChange={(event) => patchForm({ reportSecondaryColor: event.target.value })}
                    className="h-10 w-10 cursor-pointer rounded-xl border border-app bg-transparent p-0.5"
                  />
                  <input
                    type="text"
                    value={form.reportSecondaryColor ?? ''}
                    onChange={(event) => patchForm({ reportSecondaryColor: event.target.value || null })}
                    placeholder={`Tenant default: ${secondaryColor}`}
                    className="field-input font-mono text-sm"
                    maxLength={7}
                  />
                </div>
              </label>
            </div>
          </section>

          {/* Header toggles */}
          <section className="surface-card space-y-3">
            <h2 className="text-lg font-semibold text-app">Header</h2>
            <label className="panel-subtle flex items-center justify-between rounded-2xl px-4 py-3">
              <div>
                <p className="text-sm font-medium text-app">Show company name</p>
                <p className="text-xs text-muted">Displays your company name in bold in the report header.</p>
              </div>
              <input type="checkbox" checked={form.showCompanyName}
                onChange={(event) => patchForm({ showCompanyName: event.target.checked })} />
            </label>
            <label className="panel-subtle flex items-center justify-between rounded-2xl px-4 py-3">
              <div>
                <p className="text-sm font-medium text-app">Show contact details</p>
                <p className="text-xs text-muted">Phone, email, and address line below company name.</p>
              </div>
              <input type="checkbox" checked={form.showCompanyContact}
                onChange={(event) => patchForm({ showCompanyContact: event.target.checked })} />
            </label>
          </section>

          {/* Footer & watermark */}
          <section className="surface-card space-y-4">
            <h2 className="text-lg font-semibold text-app">Footer & watermark</h2>
            <label className="space-y-2">
              <span className="text-sm font-medium text-app">Custom footer message</span>
              <input
                type="text"
                value={form.footerText ?? ''}
                onChange={(event) => patchForm({ footerText: event.target.value || null })}
                placeholder='e.g. "Thank you for choosing Ecosys Field Services"'
                className="field-input"
                maxLength={200}
              />
            </label>
            <label className="space-y-2">
              <span className="text-sm font-medium text-app">Watermark text</span>
              <input
                type="text"
                value={form.watermarkText ?? ''}
                onChange={(event) => patchForm({ watermarkText: event.target.value || null })}
                placeholder='e.g. "CONFIDENTIAL" or company name'
                className="field-input"
                maxLength={40}
              />
              <p className="text-xs text-muted">Rendered diagonally in light grey across each page. Leave empty to disable.</p>
            </label>
          </section>
        </div>

        {/* ── Right: live HTML preview ──────────────────────────────── */}
        <div className="space-y-4">
          <section className="surface-card space-y-4">
            <div className="flex items-center gap-2">
              <Eye className="h-4 w-4 text-muted" />
              <h2 className="text-base font-semibold text-app">Report preview</h2>
              <span className="rounded-full bg-amber-500/20 px-2 py-0.5 text-[10px] font-semibold uppercase tracking-[0.12em] text-amber-300">HTML mockup</span>
            </div>

            {/* Preview container */}
            <div
              className="overflow-hidden rounded-2xl border border-app bg-white text-gray-800 shadow-sm"
              style={{ fontFamily: form.fontFamily === 'Times New Roman' ? 'serif' : 'sans-serif', fontSize: 11 }}
            >
              {/* Header */}
              <div className="p-4">
                <div className={`flex items-start gap-3 ${form.logoAlignment === 'Center' ? 'justify-center' : form.logoAlignment === 'Right' ? 'flex-row-reverse' : ''}`}>
                  <div className="h-10 w-10 shrink-0 rounded-xl" style={{ background: form.reportPrimaryColor ?? primaryColor }} />
                  <div>
                    {form.showCompanyName ? (
                      <p className="text-base font-bold" style={{ color: form.reportPrimaryColor ?? primaryColor }}>{companyName}</p>
                    ) : null}
                    {form.showCompanyContact ? (
                      <p className="text-[10px] text-gray-500">support@company.com  ·  +254 700 000</p>
                    ) : null}
                  </div>
                </div>
                <div className="mt-3 border-b-2 pb-2 text-sm font-semibold text-gray-800"
                  style={{ borderColor: form.reportSecondaryColor ?? secondaryColor }}>
                  WO-2024-001  |  HVAC Preventive Maintenance
                </div>
              </div>

              {/* Sections */}
              <div className="space-y-4 px-4 pb-4">
                {form.sections.filter((s) => s.enabled).map((section) => (
                  <div
                    key={section.id}
                    className="text-[10px]"
                  >
                    <div
                      className="pb-1"
                      style={{ borderBottom: `1px solid ${form.reportSecondaryColor ?? secondaryColor}` }}
                    >
                      <p className="font-bold text-[11px]" style={{ color: form.reportPrimaryColor ?? primaryColor }}>
                        {section.label}
                      </p>
                    </div>
                    <p className="mt-2 leading-5 text-gray-600">{sectionPlaceholder(section.id)}</p>
                  </div>
                ))}
                {form.sections.every((s) => !s.enabled) ? (
                  <p className="py-4 text-center text-xs text-gray-400">All sections are disabled — the PDF will only contain the header and footer.</p>
                ) : null}
              </div>

              {/* Footer */}
              <div className="border-t-2 px-4 py-2" style={{ borderColor: form.reportSecondaryColor ?? secondaryColor }}>
                <div className="flex items-start justify-between gap-2 text-[9px] text-gray-500">
                  <div>
                    <p className="font-semibold text-gray-700">{companyName}</p>
                    {form.footerText ? <p className="italic">{form.footerText}</p> : null}
                  </div>
                  <p>Page 1 of 1</p>
                </div>
              </div>
            </div>

            {/* Watermark badge */}
            {form.watermarkText ? (
              <div className="rounded-2xl bg-amber-500/10 px-4 py-3 text-sm text-amber-200">
                Watermark active: <span className="font-semibold">"{form.watermarkText}"</span> — displayed diagonally on each PDF page.
              </div>
            ) : null}

            {/* Summary chips */}
            <div className="flex flex-wrap gap-2">
              {[
                `${form.sections.filter((s) => s.enabled).length}/${form.sections.length} sections`,
                form.fontFamily,
                `Logo: ${form.logoAlignment}`,
                form.watermarkText ? 'Watermark on' : null,
                form.footerText ? 'Custom footer' : null,
              ].filter(Boolean).map((chip) => (
                <span key={chip} className="rounded-full border border-app px-3 py-1 text-xs text-muted">{chip}</span>
              ))}
            </div>
          </section>
        </div>
      </div>
    </div>
  )
}

function sectionPlaceholder(id: string): string {
  const map: Record<string, string> = {
    'service-details': 'Client: Acme Corp  ·  Site: Main Campus  ·  Asset: HVAC Unit A',
    'problem':         'Scheduled preventive maintenance cycle per PM plan.',
    'findings':        'All filters replaced, refrigerant levels checked, coils cleaned.',
    'work-done':       'Full PM service completed. Unit operating within spec.',
    'timestamps':      'Arrived: 09:00  ·  Started: 09:15  ·  Completed: 11:45',
    'checklist':       '✓ Filter check  ✓ Coil cleaning  ✓ Refrigerant level  …',
    'materials':       'Air filter ×2  ·  Refrigerant R-410A — 0.5 kg',
    'photos':          '3 photos attached (Before, During, After)',
    'signatures':      'Technician: J. Doe  ·  Client: M. Smith  ·  Date: Today',
  }
  return map[id] ?? 'Section content'
}
