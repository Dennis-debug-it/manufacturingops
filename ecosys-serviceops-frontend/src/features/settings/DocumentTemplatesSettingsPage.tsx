import { useEffect, useState } from 'react'
import toast from 'react-hot-toast'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ChevronDown, ChevronUp } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { Spinner } from '../../shared/components/Spinner'
import { Collapsible } from '../../shared/components/Collapsible'
import { usePermission } from '../../shared/hooks/usePermission'
import { api } from '../../shared/api/apiClient'
import { DocumentPreview } from './components/DocumentPreview'

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
}

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
  { key: 'watermarkText', label: 'Watermark Text' },
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

export function DocumentTemplatesSettingsPage() {
  const canManage = usePermission('settings.manage')
  const queryClient = useQueryClient()
  const { data, isLoading, isError } = useQuery({
    queryKey: ['settings', 'report-template'],
    queryFn: () => api.get<ReportTemplateConfig>('/api/settings/report-template'),
    staleTime: 30_000,
  })

  const [config, setConfig] = useState<ReportTemplateConfig | null>(null)

  useEffect(() => {
    if (data) setConfig(data)
  }, [data])

  const save = useMutation({
    mutationFn: () => api.put<ReportTemplateConfig>('/api/settings/report-template', config),
    onSuccess: (result) => {
      toast.success('Document template saved')
      setConfig(result)
      void queryClient.invalidateQueries({ queryKey: ['settings', 'report-template'] })
    },
  })

  if (isLoading || !config) {
    return isLoading ? <Spinner label="Loading document template" /> : (
      <div className="rounded-2xl border border-dashed border-slate-300 bg-white p-8 text-center text-sm text-slate-600">Document template settings could not be loaded.</div>
    )
  }

  if (isError) {
    return <div className="rounded-2xl border border-dashed border-slate-300 bg-white p-8 text-center text-sm text-slate-600">Document template settings could not be loaded.</div>
  }

  const set = <K extends keyof ReportTemplateConfig>(key: K, value: ReportTemplateConfig[K]) =>
    setConfig((current) => (current ? { ...current, [key]: value } : current))

  const toggleSection = (id: string) =>
    setConfig((current) =>
      current ? { ...current, sections: current.sections.map((section) => (section.id === id ? { ...section, enabled: !section.enabled } : section)) } : current,
    )

  const moveSection = (id: string, delta: number) =>
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

  return (
    <div className="space-y-6">
      <PageHeader
        title="Document Templates"
        subtitle="Layout, branding, and boilerplate text used on quotes, invoices, receipts, and service reports — with a live preview of your changes."
        primaryAction={
          canManage ? (
            <button type="button" onClick={() => save.mutate()} disabled={save.isPending} className="inline-flex h-11 items-center justify-center rounded-lg bg-slate-900 px-4 text-sm font-medium text-white hover:bg-slate-800 disabled:opacity-60">
              {save.isPending ? 'Saving…' : 'Save Changes'}
            </button>
          ) : undefined
        }
      />

      <div className="grid grid-cols-1 items-start gap-6 xl:grid-cols-[minmax(0,1fr)_420px]">
      <div className="min-w-0 space-y-6">
      <Collapsible title="Layout & Branding" defaultOpen>
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Layout Style</label>
            <select value={config.layoutStyle} disabled={!canManage} onChange={(event) => set('layoutStyle', event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900 disabled:bg-slate-50">
              {['Modern', 'Classic', 'Minimal'].map((option) => <option key={option} value={option}>{option}</option>)}
            </select>
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Page Density</label>
            <select value={config.pageDensity} disabled={!canManage} onChange={(event) => set('pageDensity', event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900 disabled:bg-slate-50">
              {['Comfortable', 'Compact'].map((option) => <option key={option} value={option}>{option}</option>)}
            </select>
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Font Family</label>
            <select value={supportedFonts.includes(config.fontFamily as (typeof supportedFonts)[number]) ? config.fontFamily : 'Helvetica'} disabled={!canManage} onChange={(event) => set('fontFamily', event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900 disabled:bg-slate-50">
              {supportedFonts.map((font) => <option key={font} value={font}>{font}</option>)}
            </select>
            <p className="mt-1 text-xs text-slate-500">Fonts supported by the PDF renderer.</p>
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Primary Colour</label>
            <div className="flex items-center gap-2">
              <input type="color" value={config.reportPrimaryColor ?? '#0F4C81'} disabled={!canManage} onChange={(event) => set('reportPrimaryColor', event.target.value)} className="h-11 w-14 cursor-pointer rounded-lg border border-slate-300 bg-white p-1" aria-label="Primary colour" />
              <span className="font-mono text-xs text-slate-600">{config.reportPrimaryColor ?? 'Brand default'}</span>
              {config.reportPrimaryColor ? (
                <button type="button" disabled={!canManage} onClick={() => set('reportPrimaryColor', null)} className="text-xs font-medium text-slate-500 underline underline-offset-2 hover:text-slate-900">Use brand default</button>
              ) : null}
            </div>
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Secondary Colour</label>
            <div className="flex items-center gap-2">
              <input type="color" value={config.reportSecondaryColor ?? '#64748B'} disabled={!canManage} onChange={(event) => set('reportSecondaryColor', event.target.value)} className="h-11 w-14 cursor-pointer rounded-lg border border-slate-300 bg-white p-1" aria-label="Secondary colour" />
              <span className="font-mono text-xs text-slate-600">{config.reportSecondaryColor ?? 'Brand default'}</span>
              {config.reportSecondaryColor ? (
                <button type="button" disabled={!canManage} onClick={() => set('reportSecondaryColor', null)} className="text-xs font-medium text-slate-500 underline underline-offset-2 hover:text-slate-900">Use brand default</button>
              ) : null}
            </div>
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Default Currency</label>
            <input value={config.defaultCurrency} disabled={!canManage} onChange={(event) => set('defaultCurrency', event.target.value.toUpperCase())} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900 disabled:bg-slate-50" />
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Logo Alignment</label>
            <select value={config.logoAlignment} disabled={!canManage} onChange={(event) => set('logoAlignment', event.target.value as ReportTemplateConfig['logoAlignment'])} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900 disabled:bg-slate-50">
              {['Left', 'Center', 'Right'].map((option) => <option key={option} value={option}>{option}</option>)}
            </select>
          </div>
        </div>
      </Collapsible>

      <Collapsible title="Report Sections (order & visibility)">
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
      </Collapsible>

      <Collapsible title="Display Toggles">
        <div className="grid grid-cols-1 gap-2 sm:grid-cols-2">
          {toggles.map((toggle) => (
            <label key={toggle.key} className="flex items-center gap-2 text-sm text-slate-700">
              <input type="checkbox" checked={Boolean(config[toggle.key])} disabled={!canManage} onChange={(event) => set(toggle.key, event.target.checked as never)} className="h-4 w-4 rounded border-slate-300" />
              {toggle.label}
            </label>
          ))}
        </div>
      </Collapsible>

      <Collapsible title="Boilerplate Text">
        <div className="grid grid-cols-1 gap-4">
          {textFields.map((field) => (
            <div key={field.key}>
              <label className="mb-1.5 block text-sm font-medium text-slate-700">{field.label}</label>
              {field.area ? (
                <textarea value={(config[field.key] as string) ?? ''} disabled={!canManage} onChange={(event) => set(field.key, event.target.value as never)} rows={2} className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-slate-900 disabled:bg-slate-50" />
              ) : (
                <input value={(config[field.key] as string) ?? ''} disabled={!canManage} onChange={(event) => set(field.key, event.target.value as never)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900 disabled:bg-slate-50" />
              )}
            </div>
          ))}
        </div>
      </Collapsible>

      <Collapsible title="Payment / Bank Details">
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          {bankFields.map((field) => (
            <div key={field.key}>
              <label className="mb-1.5 block text-sm font-medium text-slate-700">{field.label}</label>
              <input value={(config[field.key] as string) ?? ''} disabled={!canManage} onChange={(event) => set(field.key, event.target.value as never)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900 disabled:bg-slate-50" />
            </div>
          ))}
          <div className="sm:col-span-2">
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Payment Instructions</label>
            <textarea value={config.paymentInstructions ?? ''} disabled={!canManage} onChange={(event) => set('paymentInstructions', event.target.value)} rows={2} className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-slate-900 disabled:bg-slate-50" />
          </div>
        </div>
      </Collapsible>
      </div>

      <div className="min-w-0 xl:sticky xl:top-20">
        <div className="mb-2 flex items-center justify-between">
          <h2 className="text-sm font-semibold uppercase tracking-[0.16em] text-slate-500">Live Preview</h2>
          <span className="text-xs text-slate-500">Sample invoice · updates as you edit</span>
        </div>
        <DocumentPreview config={config} />
      </div>
      </div>
    </div>
  )
}
