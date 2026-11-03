import { useEffect, useMemo, useState } from 'react'
import toast from 'react-hot-toast'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ChevronDown, ChevronUp, Download, RotateCcw } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { Spinner } from '../../shared/components/Spinner'
import { ErrorState } from '../../shared/components/ErrorState'
import { Collapsible } from '../../shared/components/Collapsible'
import { usePermission } from '../../shared/hooks/usePermission'
import { api, apiClient } from '../../shared/api/apiClient'
import { DocumentPreview, type DocType } from './components/DocumentPreview'

const supportedFonts = ['Helvetica', 'Arial', 'Times New Roman'] as const

interface ReportSection {
  id: string
  label: string
  enabled: boolean
  order: number
}

interface ReportTemplateConfig {
  layoutStyle: string
  headerStyle: string
  footerStyle: string
  sections: ReportSection[]
  fontFamily: string
  reportPrimaryColor: string | null
  reportSecondaryColor: string | null
  footerText: string | null
  watermarkText: string | null
  logoAlignment: 'Left' | 'Center' | 'Right'
  showCompanyName: boolean
  showCompanyContact: boolean
  showLogo: boolean
  showTaxBreakdown: boolean
  showBankDetails: boolean
  showSignatureBlock: boolean
  showTerms: boolean
  defaultFooterNote: string | null
  defaultTermsText: string | null
  showStampArea: boolean
  pageDensity: string
  quoteTerms: string | null
  invoiceTerms: string | null
  warrantyNote: string | null
  latePaymentNote: string | null
  serviceReportDisclaimer: string | null
  footerLegalText: string | null
  bankName: string | null
  accountName: string | null
  accountNumber: string | null
  branch: string | null
  swift: string | null
  paybill: string | null
  paymentInstructions: string | null
  defaultCurrency: string
  // Design-system fields
  logoWidth: number
  companyNameFontSize: number
  headerBackgroundColor: string | null
  bodyFontSize: number
  lineSpacing: number
  headerLayout: string
  introText: string | null
  watermarkPosition: string
  watermarkOpacity: number
  watermarkColor: string | null
  showPageNumbers: boolean
  pageNumberPosition: string
  dateFormat: string
  currencyDisplay: string
  signatureFields: string[]
  signaturePosition: string
  includePhotos: boolean
  photoLayout: string
  maxPhotosPerPage: number
  showPoweredByEcosys: boolean
}

interface DocumentDesignEntry {
  isCustomised: boolean
  config: ReportTemplateConfig
}

interface DocumentDesignsResponse {
  shared: ReportTemplateConfig
  documents: Record<string, DocumentDesignEntry>
}

const docTypeList: Array<{ id: DocType; label: string }> = [
  { id: 'invoice', label: 'Invoice' },
  { id: 'quotation', label: 'Quotation' },
  { id: 'receipt', label: 'Receipt' },
  { id: 'statement', label: 'Statement' },
  { id: 'service-report', label: 'Service Report' },
  { id: 'purchase-order', label: 'Purchase Order' },
]

const toggles: Array<{ key: keyof ReportTemplateConfig; label: string }> = [
  { key: 'showLogo', label: 'Show Logo' },
  { key: 'showCompanyName', label: 'Show Company Name' },
  { key: 'showCompanyContact', label: 'Show Company Contact Info' },
  { key: 'showTaxBreakdown', label: 'Show Tax Breakdown' },
  { key: 'showBankDetails', label: 'Show Bank Details' },
  { key: 'showSignatureBlock', label: 'Show Signature Block' },
  { key: 'showTerms', label: 'Show Terms & Conditions' },
  { key: 'showStampArea', label: 'Reserve Stamp/Seal Area' },
]

const textFields: Array<{ key: keyof ReportTemplateConfig; label: string; area?: boolean }> = [
  { key: 'footerText', label: 'Footer Message' },
  { key: 'defaultFooterNote', label: 'Default Footer Note', area: true },
  { key: 'defaultTermsText', label: 'Default Terms Text', area: true },
  { key: 'quoteTerms', label: 'Quote Terms', area: true },
  { key: 'invoiceTerms', label: 'Invoice Payment Terms', area: true },
  { key: 'warrantyNote', label: 'Warranty Note', area: true },
  { key: 'latePaymentNote', label: 'Late Payment Note', area: true },
  { key: 'serviceReportDisclaimer', label: 'Service Report Disclaimer', area: true },
  { key: 'footerLegalText', label: 'Footer Legal Text', area: true },
]

const bankFields: Array<{ key: keyof ReportTemplateConfig; label: string }> = [
  { key: 'bankName', label: 'Bank Name' },
  { key: 'accountName', label: 'Account Name' },
  { key: 'accountNumber', label: 'Account Number' },
  { key: 'branch', label: 'Branch' },
  { key: 'swift', label: 'SWIFT / BIC' },
  { key: 'paybill', label: 'Paybill / Till Number' },
]

const inputClass = 'h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)] disabled:bg-slate-50'
const labelClass = 'mb-1.5 block text-sm font-medium text-slate-700'

export function DocumentTemplatesSettingsPage() {
  const canManage = usePermission('settings.manage')
  const queryClient = useQueryClient()
  const { data, isLoading, isError, refetch } = useQuery({
    queryKey: ['settings', 'document-designs'],
    queryFn: () => api.get<DocumentDesignsResponse>('/api/settings/document-designs'),
    staleTime: 30_000,
  })

  const [selectedType, setSelectedType] = useState<DocType>('invoice')
  const [config, setConfig] = useState<ReportTemplateConfig | null>(null)
  const [dirty, setDirty] = useState(false)
  const [resetConfirm, setResetConfirm] = useState('')
  const [showResetModal, setShowResetModal] = useState(false)
  const [downloading, setDownloading] = useState(false)

  // Load the selected type's config whenever data or the selected type changes (unless the user has unsaved edits).
  useEffect(() => {
    if (!data) return
    const entry = data.documents[selectedType]
    if (entry) {
      setConfig(structuredClone(entry.config))
      setDirty(false)
    }
  }, [data, selectedType])

  const save = useMutation({
    mutationFn: () => api.put<DocumentDesignEntry>(`/api/settings/document-designs/${selectedType}`, config),
    onSuccess: (result) => {
      toast.success(`${docTypeList.find((d) => d.id === selectedType)?.label} design saved`)
      setConfig(structuredClone(result.config))
      setDirty(false)
      void queryClient.invalidateQueries({ queryKey: ['settings', 'document-designs'] })
    },
  })

  const reset = useMutation({
    mutationFn: () => api.post<DocumentDesignEntry>(`/api/settings/document-designs/${selectedType}/reset`, {}),
    onSuccess: (result) => {
      toast.success('Reset to shared defaults')
      setConfig(structuredClone(result.config))
      setDirty(false)
      setShowResetModal(false)
      setResetConfirm('')
      void queryClient.invalidateQueries({ queryKey: ['settings', 'document-designs'] })
    },
  })

  const currentEntry = data?.documents[selectedType]

  const downloadSample = async () => {
    setDownloading(true)
    try {
      // Persist the current edits first so the sample reflects exactly what's on screen.
      if (dirty && canManage) await save.mutateAsync()
      const response = await apiClient.get(`/api/settings/document-designs/${selectedType}/sample-pdf`, { responseType: 'blob', suppressGlobalErrorToast: true })
      const url = URL.createObjectURL(response.data as Blob)
      window.open(url, '_blank', 'noopener,noreferrer')
      setTimeout(() => URL.revokeObjectURL(url), 60_000)
    } catch {
      toast.error('Could not generate the sample PDF')
    } finally {
      setDownloading(false)
    }
  }

  const set = <K extends keyof ReportTemplateConfig>(key: K, value: ReportTemplateConfig[K]) => {
    setConfig((current) => (current ? { ...current, [key]: value } : current))
    setDirty(true)
  }

  const toggleSection = (id: string) => {
    setConfig((current) =>
      current ? { ...current, sections: current.sections.map((section) => (section.id === id ? { ...section, enabled: !section.enabled } : section)) } : current,
    )
    setDirty(true)
  }

  const moveSection = (id: string, delta: number) => {
    setConfig((current) => {
      if (!current) return current
      const ordered = [...current.sections].sort((a, b) => a.order - b.order)
      const index = ordered.findIndex((section) => section.id === id)
      const target = index + delta
      if (index < 0 || target < 0 || target >= ordered.length) return current
      const [moved] = ordered.splice(index, 1)
      ordered.splice(target, 0, moved!)
      return { ...current, sections: ordered.map((section, order) => ({ ...section, order })) }
    })
    setDirty(true)
  }

  const setSignatureField = (index: number, value: string) => {
    setConfig((current) => {
      if (!current) return current
      const fields = [...(current.signatureFields ?? [])]
      fields[index] = value
      return { ...current, signatureFields: fields }
    })
    setDirty(true)
  }

  const addSignatureField = () => {
    setConfig((current) => (current && (current.signatureFields?.length ?? 0) < 4 ? { ...current, signatureFields: [...(current.signatureFields ?? []), 'Signature'] } : current))
    setDirty(true)
  }

  const removeSignatureField = (index: number) => {
    setConfig((current) => (current ? { ...current, signatureFields: (current.signatureFields ?? []).filter((_, i) => i !== index) } : current))
    setDirty(true)
  }

  const previewConfig = useMemo(() => config, [config])

  if (isLoading) {
    return <Spinner label="Loading document designer" />
  }

  // `config` is seeded from the query, so a failed load must not fall through to a blank designer.
  if (isError || !config) {
    return (
      <ErrorState
        title="Document design settings unavailable"
        description="We couldn’t load the document designer configuration. Nothing has been changed — try again."
        onRetry={() => void refetch()}
      />
    )
  }

  return (
    <div className="space-y-4">
      <PageHeader
        title="Document Designer"
        subtitle="Design every generated document independently — branding, colours, fonts, watermark, sections, and boilerplate — with a live preview and real sample PDFs."
        primaryAction={
          canManage ? (
            <button type="button" onClick={() => save.mutate()} disabled={save.isPending || !dirty} className="inline-flex h-11 items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white hover:bg-[var(--app-secondary)] disabled:opacity-50">
              {save.isPending ? 'Saving…' : dirty ? 'Save Changes' : 'Saved'}
            </button>
          ) : undefined
        }
      />

      {/* Document type selector */}
      <div className="flex flex-wrap gap-2">
        {docTypeList.map((doc) => {
          const customised = data?.documents[doc.id]?.isCustomised
          return (
            <button
              key={doc.id}
              type="button"
              onClick={() => setSelectedType(doc.id)}
              className={`inline-flex items-center gap-1.5 rounded-lg border px-3 py-2 text-sm font-medium transition-colors ${
                selectedType === doc.id ? 'border-[var(--app-primary)] bg-[var(--app-primary)] text-white' : 'border-slate-200 bg-white text-slate-600 hover:border-slate-300'
              }`}
            >
              {doc.label}
              {customised ? <span className={`h-1.5 w-1.5 rounded-full ${selectedType === doc.id ? 'bg-emerald-300' : 'bg-emerald-500'}`} title="Customised" /> : null}
            </button>
          )
        })}
      </div>

      <div className="flex flex-wrap items-center justify-between gap-2 rounded-xl border border-slate-200 bg-slate-50 px-4 py-2.5 text-sm">
        <span className="text-slate-600">
          {currentEntry?.isCustomised ? (
            <><span className="font-semibold text-slate-800">{docTypeList.find((d) => d.id === selectedType)?.label}</span> has its own design.</>
          ) : (
            <><span className="font-semibold text-slate-800">{docTypeList.find((d) => d.id === selectedType)?.label}</span> inherits the shared base design. Saving creates an independent copy.</>
          )}
        </span>
        <div className="flex gap-2">
          <button type="button" onClick={downloadSample} disabled={downloading} className="inline-flex h-9 items-center gap-1.5 rounded-lg border border-slate-300 bg-white px-3 text-xs font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-50">
            <Download className="h-3.5 w-3.5" />
            {downloading ? 'Generating…' : 'Preview with sample data'}
          </button>
          {canManage && currentEntry?.isCustomised ? (
            <button type="button" onClick={() => setShowResetModal(true)} className="inline-flex h-9 items-center gap-1.5 rounded-lg border border-red-200 bg-white px-3 text-xs font-medium text-red-600 hover:bg-red-50">
              <RotateCcw className="h-3.5 w-3.5" />
              Reset to Default
            </button>
          ) : null}
        </div>
      </div>

      <div className="grid grid-cols-1 items-start gap-4 xl:grid-cols-[minmax(0,1fr)_420px]">
        <div className="min-w-0 space-y-4">
          <Collapsible title="Branding & Typography" defaultOpen>
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
              <div>
                <label className={labelClass}>Font Family</label>
                <select value={supportedFonts.includes(config.fontFamily as (typeof supportedFonts)[number]) ? config.fontFamily : 'Helvetica'} disabled={!canManage} onChange={(e) => set('fontFamily', e.target.value)} className={inputClass}>
                  {supportedFonts.map((font) => <option key={font} value={font}>{font}</option>)}
                </select>
                <p className="mt-1 text-xs text-slate-500">Fonts supported by the PDF renderer.</p>
              </div>
              <div>
                <label className={labelClass}>Header Layout</label>
                <select value={config.headerLayout} disabled={!canManage} onChange={(e) => set('headerLayout', e.target.value)} className={inputClass}>
                  {['Minimal', 'Standard', 'Corporate', 'Custom'].map((o) => <option key={o} value={o}>{o}</option>)}
                </select>
              </div>
              <div>
                <label className={labelClass}>Body Font Size ({config.bodyFontSize}pt)</label>
                <input type="range" min={7} max={14} value={config.bodyFontSize} disabled={!canManage} onChange={(e) => set('bodyFontSize', Number(e.target.value))} className="w-full accent-slate-900" />
              </div>
              <div>
                <label className={labelClass}>Line Spacing ({config.lineSpacing.toFixed(2)}×)</label>
                <input type="range" min={1} max={2} step={0.05} value={config.lineSpacing} disabled={!canManage} onChange={(e) => set('lineSpacing', Number(e.target.value))} className="w-full accent-slate-900" />
              </div>
              <div>
                <label className={labelClass}>Company Name Size ({config.companyNameFontSize}pt)</label>
                <input type="range" min={10} max={32} value={config.companyNameFontSize} disabled={!canManage} onChange={(e) => set('companyNameFontSize', Number(e.target.value))} className="w-full accent-slate-900" />
              </div>
              <div>
                <label className={labelClass}>Logo Width ({config.logoWidth}px)</label>
                <input type="range" min={24} max={160} value={config.logoWidth} disabled={!canManage} onChange={(e) => set('logoWidth', Number(e.target.value))} className="w-full accent-slate-900" />
              </div>
              <div>
                <label className={labelClass}>Logo Alignment</label>
                <select value={config.logoAlignment} disabled={!canManage} onChange={(e) => set('logoAlignment', e.target.value as ReportTemplateConfig['logoAlignment'])} className={inputClass}>
                  {['Left', 'Center', 'Right'].map((o) => <option key={o} value={o}>{o}</option>)}
                </select>
              </div>
              <div>
                <label className={labelClass}>Page Density</label>
                <select value={config.pageDensity} disabled={!canManage} onChange={(e) => set('pageDensity', e.target.value)} className={inputClass}>
                  {['Comfortable', 'Compact'].map((o) => <option key={o} value={o}>{o}</option>)}
                </select>
              </div>
              <ColorField label="Primary Colour" value={config.reportPrimaryColor} fallback="#0F4C81" canManage={canManage} onChange={(v) => set('reportPrimaryColor', v)} />
              <ColorField label="Secondary / Accent Colour" value={config.reportSecondaryColor} fallback="#64748B" canManage={canManage} onChange={(v) => set('reportSecondaryColor', v)} />
              <ColorField label="Header Background" value={config.headerBackgroundColor} fallback="#F5F3FF" canManage={canManage} onChange={(v) => set('headerBackgroundColor', v)} />
              <div>
                <label className={labelClass}>Default Currency</label>
                <input value={config.defaultCurrency} disabled={!canManage} onChange={(e) => set('defaultCurrency', e.target.value.toUpperCase())} className={inputClass} />
              </div>
              <div>
                <label className={labelClass}>Currency Display</label>
                <select value={config.currencyDisplay} disabled={!canManage} onChange={(e) => set('currencyDisplay', e.target.value)} className={inputClass}>
                  <option value="Code">Code (KES)</option>
                  <option value="Symbol">Symbol (KSh)</option>
                </select>
              </div>
              <div>
                <label className={labelClass}>Date Format</label>
                <select value={config.dateFormat} disabled={!canManage} onChange={(e) => set('dateFormat', e.target.value)} className={inputClass}>
                  {['dd MMM yyyy', 'dd/MM/yyyy', 'MM/dd/yyyy', 'yyyy-MM-dd', 'd MMMM yyyy'].map((o) => <option key={o} value={o}>{o}</option>)}
                </select>
              </div>
            </div>
          </Collapsible>

          <Collapsible title="Introduction & Header Text">
            <label className={labelClass}>Introduction Text</label>
            <textarea value={config.introText ?? ''} disabled={!canManage} onChange={(e) => set('introText', e.target.value || null)} rows={2} placeholder="Optional paragraph shown above the document body" className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[var(--app-primary)] disabled:bg-slate-50" />
          </Collapsible>

          <Collapsible title="Sections (order & visibility)">
            <div className="space-y-1.5">
              {config.sections.slice().sort((a, b) => a.order - b.order).map((section, index, ordered) => (
                <div key={section.id} className="flex items-center justify-between gap-2 rounded-lg border border-slate-200 px-3 py-2">
                  <label className="flex flex-1 items-center gap-2 text-sm text-slate-700">
                    <input type="checkbox" checked={section.enabled} disabled={!canManage} onChange={() => toggleSection(section.id)} className="h-4 w-4 rounded border-slate-300" />
                    {section.label}
                  </label>
                  <div className="flex gap-1">
                    <button type="button" disabled={!canManage || index === 0} onClick={() => moveSection(section.id, -1)} aria-label={`Move ${section.label} up`} className="inline-flex h-8 w-8 items-center justify-center rounded-lg border border-slate-200 bg-white text-slate-500 hover:bg-slate-50 disabled:opacity-30">
                      <ChevronUp className="h-4 w-4" />
                    </button>
                    <button type="button" disabled={!canManage || index === ordered.length - 1} onClick={() => moveSection(section.id, 1)} aria-label={`Move ${section.label} down`} className="inline-flex h-8 w-8 items-center justify-center rounded-lg border border-slate-200 bg-white text-slate-500 hover:bg-slate-50 disabled:opacity-30">
                      <ChevronDown className="h-4 w-4" />
                    </button>
                  </div>
                </div>
              ))}
            </div>
            <p className="mt-2 text-xs text-slate-500">Section ordering primarily affects the service report; other documents use their fixed sections but respect the visibility toggles below.</p>
          </Collapsible>

          <Collapsible title="Display Toggles">
            <div className="grid grid-cols-1 gap-2 sm:grid-cols-2">
              {toggles.map((toggle) => (
                <label key={toggle.key} className="flex items-center gap-2 text-sm text-slate-700">
                  <input type="checkbox" checked={Boolean(config[toggle.key])} disabled={!canManage} onChange={(e) => set(toggle.key, e.target.checked as never)} className="h-4 w-4 rounded border-slate-300" />
                  {toggle.label}
                </label>
              ))}
            </div>
          </Collapsible>

          <Collapsible title="Watermark">
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
              <div>
                <label className={labelClass}>Watermark Text</label>
                <input value={config.watermarkText ?? ''} disabled={!canManage} onChange={(e) => set('watermarkText', e.target.value || null)} placeholder="e.g. DRAFT, CONFIDENTIAL, VOID" className={inputClass} />
              </div>
              <div>
                <label className={labelClass}>Position</label>
                <select value={config.watermarkPosition} disabled={!canManage} onChange={(e) => set('watermarkPosition', e.target.value)} className={inputClass}>
                  {['Diagonal', 'Top', 'Bottom'].map((o) => <option key={o} value={o}>{o}</option>)}
                </select>
              </div>
              <div>
                <label className={labelClass}>Opacity ({config.watermarkOpacity}%)</label>
                <input type="range" min={5} max={60} value={config.watermarkOpacity} disabled={!canManage} onChange={(e) => set('watermarkOpacity', Number(e.target.value))} className="w-full accent-slate-900" />
              </div>
              <ColorField label="Watermark Colour" value={config.watermarkColor} fallback="#9CA3AF" canManage={canManage} onChange={(v) => set('watermarkColor', v)} />
            </div>
          </Collapsible>

          <Collapsible title="Page Numbering & Attribution">
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
              <label className="flex items-center gap-2 text-sm text-slate-700">
                <input type="checkbox" checked={config.showPageNumbers} disabled={!canManage} onChange={(e) => set('showPageNumbers', e.target.checked)} className="h-4 w-4 rounded border-slate-300" />
                Show Page Numbers
              </label>
              <div>
                <label className={labelClass}>Page Number Position</label>
                <select value={config.pageNumberPosition} disabled={!canManage || !config.showPageNumbers} onChange={(e) => set('pageNumberPosition', e.target.value)} className={inputClass}>
                  {['Left', 'Center', 'Right'].map((o) => <option key={o} value={o}>{o}</option>)}
                </select>
              </div>
              <label className="flex items-center gap-2 text-sm text-slate-700 sm:col-span-2">
                <input type="checkbox" checked={config.showPoweredByEcosys} disabled={!canManage} onChange={(e) => set('showPoweredByEcosys', e.target.checked)} className="h-4 w-4 rounded border-slate-300" />
                Show "Powered by Ecosys" attribution
              </label>
            </div>
          </Collapsible>

          <Collapsible title="Signature Block">
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
              <div>
                <label className={labelClass}>Alignment</label>
                <select value={config.signaturePosition} disabled={!canManage} onChange={(e) => set('signaturePosition', e.target.value)} className={inputClass}>
                  {['Left', 'Center', 'Right', 'SpaceBetween'].map((o) => <option key={o} value={o}>{o === 'SpaceBetween' ? 'Space Between' : o}</option>)}
                </select>
              </div>
            </div>
            <div className="mt-3 space-y-2">
              <label className={labelClass}>Signature Fields ({(config.signatureFields ?? []).length}/4)</label>
              {(config.signatureFields ?? []).map((field, i) => (
                <div key={i} className="flex items-center gap-2">
                  <input value={field} disabled={!canManage} onChange={(e) => setSignatureField(i, e.target.value)} className={inputClass} />
                  <button type="button" disabled={!canManage} onClick={() => removeSignatureField(i)} className="inline-flex h-9 shrink-0 items-center rounded-lg border border-slate-200 px-2 text-xs text-slate-500 hover:bg-slate-50">Remove</button>
                </div>
              ))}
              {(config.signatureFields?.length ?? 0) < 4 ? (
                <button type="button" disabled={!canManage} onClick={addSignatureField} className="text-xs font-medium text-slate-600 underline underline-offset-2 hover:text-slate-900">+ Add signature field</button>
              ) : null}
            </div>
          </Collapsible>

          <Collapsible title="Evidence Photos (service report)">
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
              <label className="flex items-center gap-2 text-sm text-slate-700">
                <input type="checkbox" checked={config.includePhotos} disabled={!canManage} onChange={(e) => set('includePhotos', e.target.checked)} className="h-4 w-4 rounded border-slate-300" />
                Include Evidence Photos
              </label>
              <div>
                <label className={labelClass}>Photo Layout</label>
                <select value={config.photoLayout} disabled={!canManage || !config.includePhotos} onChange={(e) => set('photoLayout', e.target.value)} className={inputClass}>
                  {['Inline', 'Appendix'].map((o) => <option key={o} value={o}>{o}</option>)}
                </select>
              </div>
              <div>
                <label className={labelClass}>Max Photos Per Page ({config.maxPhotosPerPage})</label>
                <input type="range" min={1} max={12} value={config.maxPhotosPerPage} disabled={!canManage || !config.includePhotos} onChange={(e) => set('maxPhotosPerPage', Number(e.target.value))} className="w-full accent-slate-900" />
              </div>
            </div>
          </Collapsible>

          <Collapsible title="Boilerplate Text">
            <div className="grid grid-cols-1 gap-4">
              {textFields.map((field) => (
                <div key={field.key}>
                  <label className={labelClass}>{field.label}</label>
                  {field.area ? (
                    <textarea value={(config[field.key] as string) ?? ''} disabled={!canManage} onChange={(e) => set(field.key, (e.target.value || null) as never)} rows={2} className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[var(--app-primary)] disabled:bg-slate-50" />
                  ) : (
                    <input value={(config[field.key] as string) ?? ''} disabled={!canManage} onChange={(e) => set(field.key, (e.target.value || null) as never)} className={inputClass} />
                  )}
                </div>
              ))}
            </div>
          </Collapsible>

          <Collapsible title="Payment / Bank Details">
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
              {bankFields.map((field) => (
                <div key={field.key}>
                  <label className={labelClass}>{field.label}</label>
                  <input value={(config[field.key] as string) ?? ''} disabled={!canManage} onChange={(e) => set(field.key, (e.target.value || null) as never)} className={inputClass} />
                </div>
              ))}
              <div className="sm:col-span-2">
                <label className={labelClass}>Payment Instructions</label>
                <textarea value={config.paymentInstructions ?? ''} disabled={!canManage} onChange={(e) => set('paymentInstructions', e.target.value || null)} rows={2} className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[var(--app-primary)] disabled:bg-slate-50" />
              </div>
            </div>
          </Collapsible>
        </div>

        <div className="min-w-0 xl:sticky xl:top-20">
          <div className="mb-2 flex items-center justify-between">
            <h2 className="text-sm font-semibold uppercase tracking-[0.16em] text-slate-500">Live Preview</h2>
            <span className="text-xs text-slate-500">{docTypeList.find((d) => d.id === selectedType)?.label} · updates as you edit</span>
          </div>
          {previewConfig ? <DocumentPreview config={previewConfig} docType={selectedType} /> : null}
        </div>
      </div>

      {showResetModal ? (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-[var(--app-primary)]/50 p-4">
          <div className="w-full max-w-md rounded-2xl bg-white p-6 shadow-xl">
            <h3 className="text-lg font-semibold text-slate-900">Reset {docTypeList.find((d) => d.id === selectedType)?.label} design?</h3>
            <p className="mt-2 text-sm text-slate-600">This removes this document's independent design and reverts it to the shared base design. Type <span className="font-mono font-semibold">RESET</span> to confirm.</p>
            <input value={resetConfirm} onChange={(e) => setResetConfirm(e.target.value)} placeholder="RESET" className="mt-3 h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]" autoFocus />
            <div className="mt-4 flex justify-end gap-2">
              <button type="button" onClick={() => { setShowResetModal(false); setResetConfirm('') }} className="inline-flex h-10 items-center rounded-lg border border-slate-300 px-4 text-sm font-medium text-slate-700 hover:bg-slate-50">Cancel</button>
              <button type="button" disabled={resetConfirm !== 'RESET' || reset.isPending} onClick={() => reset.mutate()} className="inline-flex h-10 items-center rounded-lg bg-red-600 px-4 text-sm font-medium text-white hover:bg-red-700 disabled:opacity-50">
                {reset.isPending ? 'Resetting…' : 'Reset to Default'}
              </button>
            </div>
          </div>
        </div>
      ) : null}
    </div>
  )
}

function ColorField({ label, value, fallback, canManage, onChange }: { label: string; value: string | null; fallback: string; canManage: boolean; onChange: (value: string | null) => void }) {
  return (
    <div>
      <label className={labelClass}>{label}</label>
      <div className="flex items-center gap-2">
        <input type="color" value={value ?? fallback} disabled={!canManage} onChange={(e) => onChange(e.target.value)} className="h-11 w-14 cursor-pointer rounded-lg border border-slate-300 bg-white p-1" aria-label={label} />
        <span className="font-mono text-xs text-slate-600">{value ?? 'Default'}</span>
        {value ? (
          <button type="button" disabled={!canManage} onClick={() => onChange(null)} className="text-xs font-medium text-slate-500 underline underline-offset-2 hover:text-slate-900">Clear</button>
        ) : null}
      </div>
    </div>
  )
}
