import { useEffect, useMemo, useState, type ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { PageHeader } from '../../components/ui/PageHeader'
import { LoadingState } from '../../components/ui/LoadingState'
import { ErrorState } from '../../components/ui/ErrorState'
import { useToast } from '../../components/ui/ToastProvider'
import { MetricGrid, MetricCard, SectionCard } from '../../components/ui/Workspace'
import { useAsyncData } from '../../hooks/useAsyncData'
import { settingsService, type ReportTemplateConfig } from '../../services/settingsService'
import type { NumberingRuleRecord } from '../../types/api'
import { formatDateOnly } from '../../utils/date'

type LayoutStyle = 'Classic' | 'Modern' | 'Compact' | 'Formal'
type HeaderStyle = 'Logo left, company details right' | 'Logo centered' | 'Letterhead style' | 'Minimal header'
type FooterStyle = 'Contact footer' | 'Bank footer' | 'Legal footer' | 'No footer'
type FontStyle = 'Inter' | 'Arial' | 'Times New Roman' | 'System default'
type Density = 'Comfortable' | 'Compact'
type PreviewDocument =
  | 'Quote / Estimate'
  | 'Proforma Invoice'
  | 'Tax Invoice'
  | 'Receipt'
  | 'Credit Note'
  | 'Debit Note'
  | 'Customer Statement'
  | 'Service Report'
  | 'PM Report'
  | 'Material Issue Note'

type TenantProfile = {
  companyName: string
  legalName: string
  tradingName: string
  postalAddress: string
  physicalAddress: string
  website: string
  vatNumber: string
  registrationNumber: string
  email: string
  phone: string | null
  country: string
  industry: string | null
  address: string | null
  logoUrl: string | null
  primaryColor: string
  secondaryColor: string
  showPoweredByEcosys: boolean
}

type BrandPreviewFields = {
  legalName: string
  tradingName: string
  postalAddress: string
  physicalAddress: string
  website: string
  vatNumber: string
  registrationNumber: string
}

type LayoutPreview = {
  layoutStyle: LayoutStyle
  headerStyle: HeaderStyle
  footerStyle: FooterStyle
  fontStyle: FontStyle
  pageDensity: Density
  watermark: string
  showStampArea: boolean
}

type TemplatePreview = {
  documentType: PreviewDocument
  title: string
  showLogo: boolean
  showTaxBreakdown: boolean
  showBankDetails: boolean
  showSignatureBlock: boolean
  showTerms: boolean
  defaultFooterNote: string
  defaultTermsText: string
}

type TermsPreview = {
  quoteTerms: string
  invoiceTerms: string
  warrantyNote: string
  latePaymentNote: string
  serviceReportDisclaimer: string
  footerLegalText: string
}

type BankPreview = {
  bankName: string
  accountName: string
  accountNumber: string
  branch: string
  swift: string
  paybill: string
  paymentInstructions: string
  defaultCurrency: string
}

const documentNav = [
  { id: 'brand', label: 'Brand identity' },
  { id: 'identity', label: 'Document identity' },
  { id: 'layouts', label: 'Layouts' },
  { id: 'templates', label: 'Templates' },
  { id: 'terms', label: 'Terms & footers' },
  { id: 'bank', label: 'Bank & payment details' },
  { id: 'numbering', label: 'Numbering' },
  { id: 'preview', label: 'Preview' },
]

const documentTypes: PreviewDocument[] = [
  'Quote / Estimate',
  'Proforma Invoice',
  'Tax Invoice',
  'Receipt',
  'Credit Note',
  'Debit Note',
  'Customer Statement',
  'Service Report',
  'PM Report',
  'Material Issue Note',
]

const defaultProfile: TenantProfile = {
  companyName: '',
  legalName: '',
  tradingName: '',
  postalAddress: '',
  physicalAddress: '',
  website: '',
  vatNumber: '',
  registrationNumber: '',
  email: '',
  phone: '',
  country: '',
  industry: '',
  address: '',
  logoUrl: '',
  primaryColor: '#0f4c81',
  secondaryColor: '#f4b942',
  showPoweredByEcosys: true,
}

const defaultLayout: LayoutPreview = {
  layoutStyle: 'Modern',
  headerStyle: 'Logo left, company details right',
  footerStyle: 'Contact footer',
  fontStyle: 'Inter',
  pageDensity: 'Comfortable',
  watermark: 'DRAFT',
  showStampArea: true,
}

const defaultTemplate: TemplatePreview = {
  documentType: 'Quote / Estimate',
  title: 'Quote / Estimate',
  showLogo: true,
  showTaxBreakdown: true,
  showBankDetails: false,
  showSignatureBlock: false,
  showTerms: true,
  defaultFooterNote: 'Thank you for the opportunity to quote.',
  defaultTermsText: 'Standard commercial terms apply unless otherwise stated.',
}

const defaultTerms: TermsPreview = {
  quoteTerms: 'Quote valid for 30 days.',
  invoiceTerms: 'Payment due within 30 days.',
  warrantyNote: 'Standard warranty applies.',
  latePaymentNote: 'Late payments may incur a service charge.',
  serviceReportDisclaimer: 'Service report prepared from site findings and client approval.',
  footerLegalText: 'All prices are subject to applicable taxes.',
}

const defaultBank: BankPreview = {
  bankName: '',
  accountName: '',
  accountNumber: '',
  branch: '',
  swift: '',
  paybill: '',
  paymentInstructions: '',
  defaultCurrency: 'KES',
}

const defaultReportTemplate: ReportTemplateConfig = {
  sections: [],
  layoutStyle: 'Modern',
  headerStyle: 'Logo left, company details right',
  footerStyle: 'Contact footer',
  fontFamily: 'Helvetica',
  reportPrimaryColor: null,
  reportSecondaryColor: null,
  footerText: null,
  watermarkText: null,
  logoAlignment: 'Left',
  showCompanyName: true,
  showCompanyContact: true,
  showLogo: true,
  showTaxBreakdown: true,
  showBankDetails: false,
  showSignatureBlock: false,
  showTerms: true,
  defaultFooterNote: null,
  defaultTermsText: null,
  showStampArea: true,
  pageDensity: 'Comfortable',
  quoteTerms: null,
  invoiceTerms: null,
  warrantyNote: null,
  latePaymentNote: null,
  serviceReportDisclaimer: null,
  footerLegalText: null,
  bankName: null,
  accountName: null,
  accountNumber: null,
  branch: null,
  swift: null,
  paybill: null,
  paymentInstructions: null,
  defaultCurrency: 'KES',
}

function Field({
  label,
  children,
  hint,
}: {
  label: string
  children: ReactNode
  hint?: string
}) {
  return (
    <label className="block space-y-1.5">
      <span className="text-sm font-semibold text-app">{label}</span>
      {children}
      {hint ? <p className="text-xs text-muted">{hint}</p> : null}
    </label>
  )
}

function DocChip({
  active,
  children,
  onClick,
}: {
  active: boolean
  children: string
  onClick: () => void
}) {
  return (
    <button
      type="button"
      className={`rounded-full border px-3 py-1.5 text-xs font-semibold transition ${
        active ? 'border-transparent bg-[color:color-mix(in_srgb,var(--app-primary-soft)_82%,white)] text-app' : 'border-[var(--app-divider)] bg-[var(--app-surface)] text-muted hover:text-app'
      }`}
      onClick={onClick}
    >
      {children}
    </button>
  )
}

function PreviewPaper({
  profile,
  brandFields,
  layout,
  template,
  terms,
  bank,
}: {
  profile: TenantProfile
  brandFields: BrandPreviewFields
  layout: LayoutPreview
  template: TemplatePreview
  terms: TermsPreview
  bank: BankPreview
}) {
  const headerAlign = layout.headerStyle.includes('center') ? 'text-center' : 'text-left'
  const companyLabel = brandFields.tradingName || brandFields.legalName || profile.companyName || 'Company Name'

  return (
    <div className="overflow-hidden rounded-[20px] border border-[var(--app-divider)] bg-[#fcfbf7] p-5 shadow-sm">
      <div className={`space-y-4 ${layout.fontStyle === 'Times New Roman' ? 'font-serif' : 'font-sans'}`}>
        <div className={`flex items-start justify-between gap-4 ${headerAlign}`}>
          <div className="min-w-0">
            <p className="text-[10px] font-semibold uppercase tracking-[0.22em] text-muted">Sample preview</p>
            <h3 className="mt-1 text-xl font-semibold text-app">{template.title}</h3>
            <p className="mt-1 text-sm text-muted">{companyLabel}</p>
            {profile.email ? <p className="mt-1 text-xs text-muted">{profile.email}</p> : null}
          </div>
          {template.showLogo ? (
            <div className="flex h-14 w-14 items-center justify-center rounded-xl border border-dashed border-[var(--app-divider)] bg-white text-xs font-semibold text-muted">
              LOGO
            </div>
          ) : null}
        </div>

        <div className="grid gap-3 sm:grid-cols-2">
          <div className="rounded-2xl bg-white p-3">
            <p className="text-[10px] font-semibold uppercase tracking-[0.2em] text-muted">Client</p>
            <p className="mt-1 text-sm font-semibold text-app">Acme Facilities Ltd</p>
            <p className="mt-1 text-xs text-muted">Nairobi, Kenya</p>
          </div>
          <div className="rounded-2xl bg-white p-3">
            <p className="text-[10px] font-semibold uppercase tracking-[0.2em] text-muted">Document meta</p>
            <p className="mt-1 text-xs text-muted">Layout: {layout.layoutStyle}</p>
            <p className="mt-1 text-xs text-muted">Header: {layout.headerStyle}</p>
            <p className="mt-1 text-xs text-muted">Footer: {layout.footerStyle}</p>
          </div>
        </div>

        <div className="overflow-hidden rounded-2xl border border-[var(--app-divider)] bg-white">
          <table className="w-full text-sm">
            <thead className="bg-[color:color-mix(in_srgb,var(--app-primary-soft)_10%,white)] text-left text-xs uppercase tracking-[0.16em] text-muted">
              <tr>
                <th className="px-4 py-3">Item</th>
                <th className="px-4 py-3 text-right">Qty</th>
                <th className="px-4 py-3 text-right">Rate</th>
                <th className="px-4 py-3 text-right">Total</th>
              </tr>
            </thead>
            <tbody>
              <tr className="border-t border-[var(--app-divider)]">
                <td className="px-4 py-3">Inspection and servicing</td>
                <td className="px-4 py-3 text-right">1</td>
                <td className="px-4 py-3 text-right">KES 25,000</td>
                <td className="px-4 py-3 text-right">KES 25,000</td>
              </tr>
              <tr className="border-t border-[var(--app-divider)]">
                <td className="px-4 py-3">Parts and materials</td>
                <td className="px-4 py-3 text-right">1</td>
                <td className="px-4 py-3 text-right">KES 8,500</td>
                <td className="px-4 py-3 text-right">KES 8,500</td>
              </tr>
            </tbody>
          </table>
        </div>

        <div className="grid gap-3 sm:grid-cols-2">
          <div className="rounded-2xl bg-white p-3">
            <p className="text-[10px] font-semibold uppercase tracking-[0.2em] text-muted">Notes</p>
            <p className="mt-1 text-xs leading-5 text-app">{template.showTerms ? template.defaultTermsText : terms.quoteTerms}</p>
            <p className="mt-2 text-xs leading-5 text-muted">{terms.footerLegalText}</p>
          </div>
          <div className="rounded-2xl bg-white p-3">
            <p className="text-[10px] font-semibold uppercase tracking-[0.2em] text-muted">Payment details</p>
            <p className="mt-1 text-xs text-app">{template.showBankDetails ? bank.bankName || 'Bank details shown here' : 'Bank details hidden for this template.'}</p>
            <p className="mt-1 text-xs text-muted">{bank.paymentInstructions || 'Payment instructions appear here when configured.'}</p>
          </div>
        </div>

        {template.showSignatureBlock ? (
          <div className="grid gap-3 sm:grid-cols-2">
            <div className="h-20 rounded-2xl border border-dashed border-[var(--app-divider)] bg-white p-3 text-xs text-muted">Prepared by signature</div>
            <div className="h-20 rounded-2xl border border-dashed border-[var(--app-divider)] bg-white p-3 text-xs text-muted">Client signature</div>
          </div>
        ) : null}

        {layout.showStampArea ? (
          <div className="rounded-2xl border border-dashed border-[var(--app-divider)] bg-white p-3 text-xs text-muted">Stamp / seal area</div>
        ) : null}

        {layout.watermark ? <p className="text-center text-[11px] font-semibold uppercase tracking-[0.3em] text-muted/70">{layout.watermark}</p> : null}
      </div>
    </div>
  )
}

export function DocumentsBrandingSettingsPage() {
  const { pushToast } = useToast()
  const { data, loading, error, reload } = useAsyncData(
    async () => {
      const [documents, rules] = await Promise.all([
        settingsService.getDocuments(),
        settingsService.listNumberingRules(),
      ])
      return { documents, rules }
    },
    {
      documents: {
        profile: defaultProfile,
        branding: { logoUrl: null, primaryColor: '#0f4c81', secondaryColor: '#f4b942', showPoweredByEcosys: true },
        template: defaultReportTemplate,
      },
      rules: [] as NumberingRuleRecord[],
    },
    [],
  )

  const [profileForm, setProfileForm] = useState<TenantProfile>(defaultProfile)
  const [layout, setLayout] = useState<LayoutPreview>(defaultLayout)
  const [template, setTemplate] = useState<TemplatePreview>(defaultTemplate)
  const [terms, setTerms] = useState<TermsPreview>(defaultTerms)
  const [bank, setBank] = useState<BankPreview>(defaultBank)
  const [saving, setSaving] = useState(false)

  useEffect(() => {
    setProfileForm({
      companyName: data.documents.profile.companyName ?? '',
      legalName: data.documents.profile.legalName ?? '',
      tradingName: data.documents.profile.tradingName ?? '',
      postalAddress: data.documents.profile.postalAddress ?? '',
      physicalAddress: data.documents.profile.physicalAddress ?? '',
      website: data.documents.profile.website ?? '',
      vatNumber: data.documents.profile.vatNumber ?? '',
      registrationNumber: data.documents.profile.registrationNumber ?? '',
      email: data.documents.profile.email ?? '',
      phone: data.documents.profile.phone ?? '',
      country: data.documents.profile.country ?? '',
      industry: data.documents.profile.industry ?? '',
      address: data.documents.profile.address ?? '',
      logoUrl: data.documents.profile.logoUrl ?? data.documents.branding.logoUrl ?? '',
      primaryColor: data.documents.profile.primaryColor || data.documents.branding.primaryColor || '#0f4c81',
      secondaryColor: data.documents.profile.secondaryColor || data.documents.branding.secondaryColor || '#f4b942',
      showPoweredByEcosys: data.documents.profile.showPoweredByEcosys ?? data.documents.branding.showPoweredByEcosys,
    })
    setLayout({
      layoutStyle: (data.documents.template.layoutStyle as LayoutStyle) || defaultLayout.layoutStyle,
      headerStyle: (data.documents.template.headerStyle as HeaderStyle) || defaultLayout.headerStyle,
      footerStyle: (data.documents.template.footerStyle as FooterStyle) || defaultLayout.footerStyle,
      fontStyle: (data.documents.template.fontFamily as FontStyle) || defaultLayout.fontStyle,
      pageDensity: (data.documents.template.pageDensity as Density) || defaultLayout.pageDensity,
      watermark: data.documents.template.watermarkText ?? defaultLayout.watermark,
      showStampArea: data.documents.template.showStampArea ?? defaultLayout.showStampArea,
    })
    setTemplate({
      documentType: 'Quote / Estimate',
      title: 'Quote / Estimate',
      showLogo: data.documents.template.showLogo ?? defaultTemplate.showLogo,
      showTaxBreakdown: data.documents.template.showTaxBreakdown ?? defaultTemplate.showTaxBreakdown,
      showBankDetails: data.documents.template.showBankDetails ?? defaultTemplate.showBankDetails,
      showSignatureBlock: data.documents.template.showSignatureBlock ?? defaultTemplate.showSignatureBlock,
      showTerms: data.documents.template.showTerms ?? defaultTemplate.showTerms,
      defaultFooterNote: data.documents.template.defaultFooterNote ?? defaultTemplate.defaultFooterNote,
      defaultTermsText: data.documents.template.defaultTermsText ?? defaultTemplate.defaultTermsText,
    })
    setTerms({
      quoteTerms: data.documents.template.quoteTerms ?? defaultTerms.quoteTerms,
      invoiceTerms: data.documents.template.invoiceTerms ?? defaultTerms.invoiceTerms,
      warrantyNote: data.documents.template.warrantyNote ?? defaultTerms.warrantyNote,
      latePaymentNote: data.documents.template.latePaymentNote ?? defaultTerms.latePaymentNote,
      serviceReportDisclaimer: data.documents.template.serviceReportDisclaimer ?? defaultTerms.serviceReportDisclaimer,
      footerLegalText: data.documents.template.footerLegalText ?? defaultTerms.footerLegalText,
    })
    setBank({
      bankName: data.documents.template.bankName ?? defaultBank.bankName,
      accountName: data.documents.template.accountName ?? defaultBank.accountName,
      accountNumber: data.documents.template.accountNumber ?? defaultBank.accountNumber,
      branch: data.documents.template.branch ?? defaultBank.branch,
      swift: data.documents.template.swift ?? defaultBank.swift,
      paybill: data.documents.template.paybill ?? defaultBank.paybill,
      paymentInstructions: data.documents.template.paymentInstructions ?? defaultBank.paymentInstructions,
      defaultCurrency: data.documents.template.defaultCurrency ?? defaultBank.defaultCurrency,
    })
  }, [data.documents])

  const previewRule = useMemo(() => data.rules[0] ?? null, [data.rules])

  async function uploadLogo(file: File) {
    try {
      const response = await settingsService.uploadLogo(file)
      setProfileForm((current) => ({ ...current, logoUrl: response.logoUrl }))
      pushToast({ title: 'Logo uploaded', description: 'The logo is ready for document previews and tenant branding.', tone: 'success' })
    } catch (nextError) {
      pushToast({ title: 'Upload failed', description: nextError instanceof Error ? nextError.message : 'Unable to upload the logo.', tone: 'danger' })
    }
  }

  async function saveDocuments() {
    setSaving(true)
    try {
      const templateConfig: ReportTemplateConfig = {
        ...data.documents.template,
        layoutStyle: layout.layoutStyle,
        headerStyle: layout.headerStyle,
        footerStyle: layout.footerStyle,
        fontFamily: layout.fontStyle,
        reportPrimaryColor: profileForm.primaryColor,
        reportSecondaryColor: profileForm.secondaryColor,
        footerText: template.defaultFooterNote || null,
        watermarkText: layout.watermark || null,
        showCompanyName: data.documents.template.showCompanyName,
        showCompanyContact: data.documents.template.showCompanyContact,
        showLogo: template.showLogo,
        showTaxBreakdown: template.showTaxBreakdown,
        showBankDetails: template.showBankDetails,
        showSignatureBlock: template.showSignatureBlock,
        showTerms: template.showTerms,
        defaultFooterNote: template.defaultFooterNote,
        defaultTermsText: template.defaultTermsText,
        showStampArea: layout.showStampArea,
        pageDensity: layout.pageDensity,
        quoteTerms: terms.quoteTerms,
        invoiceTerms: terms.invoiceTerms,
        warrantyNote: terms.warrantyNote,
        latePaymentNote: terms.latePaymentNote,
        serviceReportDisclaimer: terms.serviceReportDisclaimer,
        footerLegalText: terms.footerLegalText,
        bankName: bank.bankName || null,
        accountName: bank.accountName || null,
        accountNumber: bank.accountNumber || null,
        branch: bank.branch || null,
        swift: bank.swift || null,
        paybill: bank.paybill || null,
        paymentInstructions: bank.paymentInstructions || null,
        defaultCurrency: bank.defaultCurrency || 'KES',
      }

      await settingsService.updateDocuments({
        profile: {
          companyName: profileForm.companyName,
          legalName: profileForm.legalName,
          tradingName: profileForm.tradingName,
          postalAddress: profileForm.postalAddress,
          physicalAddress: profileForm.physicalAddress,
          website: profileForm.website,
          vatNumber: profileForm.vatNumber,
          registrationNumber: profileForm.registrationNumber,
          email: profileForm.email,
          phone: profileForm.phone || null,
          country: profileForm.country,
          industry: profileForm.industry || null,
          address: profileForm.physicalAddress || profileForm.postalAddress || null,
          logoUrl: profileForm.logoUrl || null,
          primaryColor: profileForm.primaryColor,
          secondaryColor: profileForm.secondaryColor,
          showPoweredByEcosys: profileForm.showPoweredByEcosys,
        },
        branding: {
          logoUrl: profileForm.logoUrl || null,
          primaryColor: profileForm.primaryColor,
          secondaryColor: profileForm.secondaryColor,
          showPoweredByEcosys: profileForm.showPoweredByEcosys,
        },
        template: templateConfig,
      })
      pushToast({ title: 'Documents saved', description: 'Document branding, preview values, and layout settings have been updated.', tone: 'success' })
      await reload()
    } catch (nextError) {
      pushToast({ title: 'Save failed', description: nextError instanceof Error ? nextError.message : 'Unable to save document settings.', tone: 'danger' })
    } finally {
      setSaving(false)
    }
  }

  if (loading) return <LoadingState label="Loading Documents & Branding" />
  if (error) return <ErrorState title="Unable to load Documents & Branding" description={error} />

  const brandFields: BrandPreviewFields = {
    legalName: profileForm.legalName,
    tradingName: profileForm.tradingName,
    postalAddress: profileForm.postalAddress,
    physicalAddress: profileForm.physicalAddress,
    website: profileForm.website,
    vatNumber: profileForm.vatNumber,
    registrationNumber: profileForm.registrationNumber,
  }

  return (
    <div className="space-y-4">
      <PageHeader
        eyebrow="Settings"
        title="Documents & Branding"
        description="Configure document identity, layout, numbering, terms, and sample previews for tenant-generated finance documents."
        actions={
          <div className="flex items-center gap-3">
            <button type="button" className="button-secondary" onClick={() => void reload()} disabled={loading}>
              Reset
            </button>
            <button type="button" className="button-primary" onClick={() => void saveDocuments()} disabled={saving}>
              {saving ? 'Saving...' : 'Save documents'}
            </button>
          </div>
        }
      />

      <div className="grid gap-4 xl:grid-cols-[280px_minmax(0,1fr)]">
        <aside className="surface-card h-fit space-y-3 border border-[var(--app-divider)] p-4 xl:sticky xl:top-4">
          <div>
            <p className="eyebrow-accent text-xs font-semibold uppercase tracking-[0.2em]">Documents</p>
            <h2 className="mt-2 text-lg font-semibold text-app">Navigate settings</h2>
            <p className="mt-1 text-sm text-muted">Jump between the main document control sections without changing the page.</p>
          </div>
          <div className="space-y-1">
            {documentNav.map((item) => (
              <a
                key={item.id}
                href={`#${item.id}`}
                className="block rounded-xl border border-transparent px-3 py-2 text-sm font-medium text-app transition hover:border-[var(--app-divider)] hover:bg-[var(--app-surface)]"
              >
                {item.label}
              </a>
            ))}
          </div>
          <div className="rounded-2xl border border-[var(--app-divider)] bg-[var(--app-surface)] p-3 text-sm text-muted">
            Branding and report design also stay available as direct pages from the same Documents hub.
          </div>
          <div className="flex flex-wrap gap-2">
            <Link className="button-secondary inline-flex items-center gap-2" to="/settings/branding">
              Branding page
            </Link>
            <Link className="button-secondary inline-flex items-center gap-2" to="/settings/report-template">
              Report design
            </Link>
            <Link className="button-secondary inline-flex items-center gap-2" to="/settings/numbering-rules">
              Numbering rules
            </Link>
          </div>
        </aside>

        <div className="space-y-4">
          <section id="brand">
            <SectionCard title="Tenant profile" description="Persisted tenant identity and shared branding values.">
              <div className="grid gap-3 md:grid-cols-2">
                <Field label="Company name">
                  <input className="field-input" value={profileForm.companyName} onChange={(event) => setProfileForm((current) => ({ ...current, companyName: event.target.value }))} />
                </Field>
                <Field label="Email">
                  <input className="field-input" value={profileForm.email} onChange={(event) => setProfileForm((current) => ({ ...current, email: event.target.value }))} />
                </Field>
                <Field label="Phone">
                  <input className="field-input" value={profileForm.phone ?? ''} onChange={(event) => setProfileForm((current) => ({ ...current, phone: event.target.value }))} />
                </Field>
                <Field label="Country">
                  <input className="field-input" value={profileForm.country} onChange={(event) => setProfileForm((current) => ({ ...current, country: event.target.value }))} />
                </Field>
                <Field label="Industry">
                  <input className="field-input" value={profileForm.industry ?? ''} onChange={(event) => setProfileForm((current) => ({ ...current, industry: event.target.value }))} />
                </Field>
                <Field label="Logo upload" hint="PNG, JPG, JPEG, or WEBP.">
                  <input
                    type="file"
                    className="field-input"
                    accept="image/png,image/jpeg,image/webp"
                    onChange={(event) => {
                      const file = event.target.files?.[0]
                      if (file) void uploadLogo(file)
                    }}
                  />
                </Field>
                <Field label="Primary color">
                  <input type="color" className="field-input h-11 w-full p-1" value={profileForm.primaryColor} onChange={(event) => setProfileForm((current) => ({ ...current, primaryColor: event.target.value }))} />
                </Field>
                <Field label="Secondary color">
                  <input type="color" className="field-input h-11 w-full p-1" value={profileForm.secondaryColor} onChange={(event) => setProfileForm((current) => ({ ...current, secondaryColor: event.target.value }))} />
                </Field>
              </div>
              <div className="mt-4 flex items-center justify-between gap-3 border-t border-[var(--app-divider)] pt-4">
                <label className="flex items-center gap-2 text-sm text-app">
                  <input type="checkbox" checked={profileForm.showPoweredByEcosys} onChange={(event) => setProfileForm((current) => ({ ...current, showPoweredByEcosys: event.target.checked }))} />
                  Show Powered by Ecosys
                </label>
                <p className="text-xs text-muted">Saved with the main Documents action.</p>
              </div>
            </SectionCard>
          </section>

          <section id="identity">
            <SectionCard title="Document identity" description="Persisted tenant identity fields used in document headers, footers, and sample previews.">
              <div className="grid gap-3 md:grid-cols-2">
                <Field label="Legal name">
                  <input className="field-input" value={profileForm.legalName} onChange={(event) => setProfileForm((current) => ({ ...current, legalName: event.target.value }))} />
                </Field>
                <Field label="Trading name">
                  <input className="field-input" value={profileForm.tradingName} onChange={(event) => setProfileForm((current) => ({ ...current, tradingName: event.target.value }))} />
                </Field>
                <Field label="Postal address">
                  <textarea className="field-input min-h-24" value={profileForm.postalAddress} onChange={(event) => setProfileForm((current) => ({ ...current, postalAddress: event.target.value }))} />
                </Field>
                <Field label="Physical address">
                  <textarea className="field-input min-h-24" value={profileForm.physicalAddress} onChange={(event) => setProfileForm((current) => ({ ...current, physicalAddress: event.target.value }))} />
                </Field>
                <Field label="Website">
                  <input className="field-input" value={profileForm.website} onChange={(event) => setProfileForm((current) => ({ ...current, website: event.target.value }))} />
                </Field>
                <Field label="PIN / VAT number">
                  <input className="field-input" value={profileForm.vatNumber} onChange={(event) => setProfileForm((current) => ({ ...current, vatNumber: event.target.value }))} />
                </Field>
                <Field label="Registration number">
                  <input className="field-input" value={profileForm.registrationNumber} onChange={(event) => setProfileForm((current) => ({ ...current, registrationNumber: event.target.value }))} />
                </Field>
              </div>
              <p className="mt-4 text-xs text-muted">These values are stored with the tenant profile and are reused in document previews and exports.</p>
            </SectionCard>
          </section>

          <section id="layouts" className="grid gap-4 xl:grid-cols-[1fr_420px]">
            <SectionCard title="Document layouts" description="Controlled layout presets only. No free-form CSS editing.">
              <div className="grid gap-3 md:grid-cols-2">
                <Field label="Layout style">
                  <select className="field-input" value={layout.layoutStyle} onChange={(event) => setLayout((current) => ({ ...current, layoutStyle: event.target.value as LayoutStyle }))}>
                    <option>Classic</option>
                    <option>Modern</option>
                    <option>Compact</option>
                    <option>Formal</option>
                  </select>
                </Field>
                <Field label="Header style">
                  <select className="field-input" value={layout.headerStyle} onChange={(event) => setLayout((current) => ({ ...current, headerStyle: event.target.value as HeaderStyle }))}>
                    <option>Logo left, company details right</option>
                    <option>Logo centered</option>
                    <option>Letterhead style</option>
                    <option>Minimal header</option>
                  </select>
                </Field>
                <Field label="Footer style">
                  <select className="field-input" value={layout.footerStyle} onChange={(event) => setLayout((current) => ({ ...current, footerStyle: event.target.value as FooterStyle }))}>
                    <option>Contact footer</option>
                    <option>Bank footer</option>
                    <option>Legal footer</option>
                    <option>No footer</option>
                  </select>
                </Field>
                <Field label="Font style">
                  <select className="field-input" value={layout.fontStyle} onChange={(event) => setLayout((current) => ({ ...current, fontStyle: event.target.value as FontStyle }))}>
                    <option>Inter</option>
                    <option>Arial</option>
                    <option>Times New Roman</option>
                    <option>System default</option>
                  </select>
                </Field>
                <Field label="Page density">
                  <select className="field-input" value={layout.pageDensity} onChange={(event) => setLayout((current) => ({ ...current, pageDensity: event.target.value as Density }))}>
                    <option>Comfortable</option>
                    <option>Compact</option>
                  </select>
                </Field>
                <Field label="Watermark">
                  <input className="field-input" value={layout.watermark} onChange={(event) => setLayout((current) => ({ ...current, watermark: event.target.value }))} />
                </Field>
              </div>
              <label className="mt-4 flex items-center gap-2 text-sm text-app">
                <input type="checkbox" checked={layout.showStampArea} onChange={(event) => setLayout((current) => ({ ...current, showStampArea: event.target.checked }))} />
                Reserve stamp / seal area
              </label>
            </SectionCard>

            <SectionCard title="Layout preview" description="Layout changes are reflected in the sample panel.">
              <PreviewPaper profile={profileForm} brandFields={brandFields} layout={layout} template={template} terms={terms} bank={bank} />
            </SectionCard>
          </section>

          <section id="templates" className="grid gap-4 xl:grid-cols-[1fr_420px]">
            <SectionCard title="Document templates" description="Controlled document types with preset toggles and sample preview.">
              <div className="flex flex-wrap gap-2">
                {documentTypes.map((docType) => (
                  <DocChip key={docType} active={template.documentType === docType} onClick={() => setTemplate((current) => ({ ...current, documentType: docType, title: docType }))}>
                    {docType}
                  </DocChip>
                ))}
              </div>
              <div className="mt-4 grid gap-3 md:grid-cols-2">
                <Field label="Document title">
                  <input className="field-input" value={template.title} onChange={(event) => setTemplate((current) => ({ ...current, title: event.target.value }))} />
                </Field>
                <Field label="Footer note">
                  <input className="field-input" value={template.defaultFooterNote} onChange={(event) => setTemplate((current) => ({ ...current, defaultFooterNote: event.target.value }))} />
                </Field>
                <Field label="Default terms text">
                  <textarea className="field-input min-h-24" value={template.defaultTermsText} onChange={(event) => setTemplate((current) => ({ ...current, defaultTermsText: event.target.value }))} />
                </Field>
              </div>
              <div className="mt-4 grid gap-3 md:grid-cols-2">
                <label className="flex items-center gap-2 text-sm text-app"><input type="checkbox" checked={template.showLogo} onChange={(event) => setTemplate((current) => ({ ...current, showLogo: event.target.checked }))} /> Show logo</label>
                <label className="flex items-center gap-2 text-sm text-app"><input type="checkbox" checked={template.showTaxBreakdown} onChange={(event) => setTemplate((current) => ({ ...current, showTaxBreakdown: event.target.checked }))} /> Show tax breakdown</label>
                <label className="flex items-center gap-2 text-sm text-app"><input type="checkbox" checked={template.showBankDetails} onChange={(event) => setTemplate((current) => ({ ...current, showBankDetails: event.target.checked }))} /> Show bank details</label>
                <label className="flex items-center gap-2 text-sm text-app"><input type="checkbox" checked={template.showSignatureBlock} onChange={(event) => setTemplate((current) => ({ ...current, showSignatureBlock: event.target.checked }))} /> Show signature block</label>
                <label className="flex items-center gap-2 text-sm text-app"><input type="checkbox" checked={template.showTerms} onChange={(event) => setTemplate((current) => ({ ...current, showTerms: event.target.checked }))} /> Show terms and conditions</label>
              </div>
            </SectionCard>

            <SectionCard title="Template preview" description="The selected document template uses the current local preview values.">
              <PreviewPaper profile={profileForm} brandFields={brandFields} layout={layout} template={template} terms={terms} bank={bank} />
            </SectionCard>
          </section>

          <section id="numbering" className="space-y-4">
            <SectionCard title="Numbering rules" description="Numbering rules are managed in the shared workspace page and apply across all document types.">
              <MetricGrid className="xl:grid-cols-3">
                <MetricCard label="Loaded rules" value={data.rules.length} meta="Existing document numbering rules" />
                <MetricCard label="Preview sample" value={previewRule?.preview ?? '-'} meta={previewRule ? `${previewRule.documentType} · ${previewRule.prefix}` : 'No rule preview available'} emphasis="accent" />
                <MetricCard label="Last updated" value={previewRule ? formatDateOnly(previewRule.updatedAt ?? previewRule.createdAt) : '-'} meta="Most recently loaded rule" />
              </MetricGrid>
              <div className="mt-4 flex flex-wrap items-center gap-2">
                <Link className="button-primary" to="/settings/numbering-rules">Open shared numbering rules</Link>
              </div>
            </SectionCard>

            <SectionCard title="Document sequence snapshot" description="Current rule set with a compact preview of the next number.">
              <div className="overflow-x-auto">
                <table className="w-full text-sm">
                  <thead>
                    <tr className="border-b border-[var(--app-divider)] text-left text-xs uppercase tracking-[0.16em] text-muted">
                      <th className="py-2 pr-4">Document</th>
                      <th className="py-2 pr-4">Prefix</th>
                      <th className="py-2 pr-4">Next</th>
                      <th className="py-2 pr-4">Preview</th>
                    </tr>
                  </thead>
                  <tbody>
                    {data.rules.map((rule) => (
                      <tr key={rule.id} className="border-b border-[var(--app-divider)]">
                        <td className="py-2 pr-4 font-medium text-app">{rule.documentType}</td>
                        <td className="py-2 pr-4">{rule.prefix}</td>
                        <td className="py-2 pr-4">{rule.nextNumber}</td>
                        <td className="py-2 pr-4 font-mono text-xs text-muted">{rule.preview}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </SectionCard>
          </section>

          <section id="terms">
            <SectionCard title="Terms & footers" description="Reusable text blocks for commercial documents.">
              <div className="grid gap-3 md:grid-cols-2">
                <Field label="Quote terms"><textarea className="field-input min-h-24" value={terms.quoteTerms} onChange={(event) => setTerms((current) => ({ ...current, quoteTerms: event.target.value }))} /></Field>
                <Field label="Invoice payment terms"><textarea className="field-input min-h-24" value={terms.invoiceTerms} onChange={(event) => setTerms((current) => ({ ...current, invoiceTerms: event.target.value }))} /></Field>
                <Field label="Warranty note"><textarea className="field-input min-h-24" value={terms.warrantyNote} onChange={(event) => setTerms((current) => ({ ...current, warrantyNote: event.target.value }))} /></Field>
                <Field label="Late payment note"><textarea className="field-input min-h-24" value={terms.latePaymentNote} onChange={(event) => setTerms((current) => ({ ...current, latePaymentNote: event.target.value }))} /></Field>
                <Field label="Service report disclaimer"><textarea className="field-input min-h-24" value={terms.serviceReportDisclaimer} onChange={(event) => setTerms((current) => ({ ...current, serviceReportDisclaimer: event.target.value }))} /></Field>
                <Field label="Footer legal text"><textarea className="field-input min-h-24" value={terms.footerLegalText} onChange={(event) => setTerms((current) => ({ ...current, footerLegalText: event.target.value }))} /></Field>
              </div>
            </SectionCard>
          </section>

          <section id="bank">
            <SectionCard title="Bank & payment details" description="Visible on invoices, proformas, receipts, and statements when enabled.">
              <div className="grid gap-3 md:grid-cols-2">
                <Field label="Bank name"><input className="field-input" value={bank.bankName} onChange={(event) => setBank((current) => ({ ...current, bankName: event.target.value }))} /></Field>
                <Field label="Account name"><input className="field-input" value={bank.accountName} onChange={(event) => setBank((current) => ({ ...current, accountName: event.target.value }))} /></Field>
                <Field label="Account number"><input className="field-input" value={bank.accountNumber} onChange={(event) => setBank((current) => ({ ...current, accountNumber: event.target.value }))} /></Field>
                <Field label="Branch"><input className="field-input" value={bank.branch} onChange={(event) => setBank((current) => ({ ...current, branch: event.target.value }))} /></Field>
                <Field label="SWIFT"><input className="field-input" value={bank.swift} onChange={(event) => setBank((current) => ({ ...current, swift: event.target.value }))} /></Field>
                <Field label="Paybill / Till"><input className="field-input" value={bank.paybill} onChange={(event) => setBank((current) => ({ ...current, paybill: event.target.value }))} /></Field>
                <Field label="Payment instructions"><textarea className="field-input min-h-24" value={bank.paymentInstructions} onChange={(event) => setBank((current) => ({ ...current, paymentInstructions: event.target.value }))} /></Field>
                <Field label="Default currency"><input className="field-input" value={bank.defaultCurrency} onChange={(event) => setBank((current) => ({ ...current, defaultCurrency: event.target.value }))} /></Field>
              </div>
            </SectionCard>
          </section>

          <section id="preview" className="grid gap-4 xl:grid-cols-[0.95fr_1.05fr]">
            <SectionCard title="Sample controls" description="Choose a document type and inspect the live preview.">
              <div className="flex flex-wrap gap-2">
                {documentTypes.map((docType) => (
                  <DocChip key={docType} active={template.documentType === docType} onClick={() => setTemplate((current) => ({ ...current, documentType: docType, title: docType }))}>
                    {docType}
                  </DocChip>
                ))}
              </div>
              <div className="mt-4 grid gap-3 md:grid-cols-2">
                <Field label="Document title"><input className="field-input" value={template.title} onChange={(event) => setTemplate((current) => ({ ...current, title: event.target.value }))} /></Field>
                <Field label="Watermark"><input className="field-input" value={layout.watermark} onChange={(event) => setLayout((current) => ({ ...current, watermark: event.target.value }))} /></Field>
              </div>
              <p className="mt-3 text-sm text-muted">The preview is sample data only. PDF generation will use the live backend renderer when connected.</p>
            </SectionCard>

            <SectionCard title="Document preview" description="A4-style mockup using the current brand and document settings.">
              <PreviewPaper profile={profileForm} brandFields={brandFields} layout={layout} template={template} terms={terms} bank={bank} />
            </SectionCard>
          </section>
        </div>
      </div>
    </div>
  )
}
