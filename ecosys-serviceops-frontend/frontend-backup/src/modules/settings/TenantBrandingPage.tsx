import { useEffect, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import { Upload, Palette, Eye, RefreshCw } from 'lucide-react'
import { PageHeader } from '../../components/ui/PageHeader'
import { LoadingState } from '../../components/ui/LoadingState'
import { ErrorState } from '../../components/ui/ErrorState'
import { useToast } from '../../components/ui/ToastProvider'
import { useAsyncData } from '../../hooks/useAsyncData'
import { useUnsavedChangesGuard } from '../../hooks/useUnsavedChangesGuard'
import { settingsService, type TenantBrandingSettings } from '../../services/settingsService'
import { useAuth } from '../../auth/AuthContext'

const DEFAULT_PRIMARY = '#0F4C81'
const DEFAULT_SECONDARY = '#F4B942'
const DEFAULT_BRANDING: TenantBrandingSettings = {
  logoUrl: null,
  primaryColor: DEFAULT_PRIMARY,
  secondaryColor: DEFAULT_SECONDARY,
  showPoweredByEcosys: true,
}

function isHexColor(value: string | null | undefined): value is string {
  return typeof value === 'string' && /^#([0-9a-fA-F]{3}|[0-9a-fA-F]{6})$/.test(value)
}

function normalizeBrandingSettings(input?: Partial<TenantBrandingSettings> | null): TenantBrandingSettings {
  const primaryColor = input?.primaryColor
  const secondaryColor = input?.secondaryColor

  return {
    logoUrl: input?.logoUrl?.trim() ? input.logoUrl.trim() : null,
    primaryColor: isHexColor(primaryColor) ? primaryColor : DEFAULT_PRIMARY,
    secondaryColor: isHexColor(secondaryColor) ? secondaryColor : DEFAULT_SECONDARY,
    showPoweredByEcosys: typeof input?.showPoweredByEcosys === 'boolean' ? input.showPoweredByEcosys : true,
  }
}

export function TenantBrandingPage() {
  const { session, updateSession } = useAuth()
  const { pushToast } = useToast()
  const { data, loading, error, reload } = useAsyncData(
    async (signal) => normalizeBrandingSettings(await settingsService.getBranding(signal)),
    DEFAULT_BRANDING,
    [],
  )

  const [form, setForm] = useState<TenantBrandingSettings>(DEFAULT_BRANDING)
  const [saving, setSaving] = useState(false)
  const [logoUploading, setLogoUploading] = useState(false)
  const logoInputRef = useRef<HTMLInputElement>(null)
  const isDirty = JSON.stringify(form) !== JSON.stringify(data)
  useUnsavedChangesGuard(isDirty)

  useEffect(() => { setForm(normalizeBrandingSettings(data)) }, [data])

  // Live preview of CSS vars as user adjusts colors
  useEffect(() => {
    document.documentElement.style.setProperty('--brand-primary-preview', form.primaryColor)
    document.documentElement.style.setProperty('--brand-secondary-preview', form.secondaryColor)
  }, [form.primaryColor, form.secondaryColor])

  async function handleLogoUpload(file: File) {
    if (!file.type.match(/^image\/(png|jpeg|jpg|webp|svg\+xml)$/)) {
      pushToast({ title: 'Invalid file type', description: 'Upload a PNG, JPG, WebP, or SVG logo.', tone: 'warning' })
      return
    }
    setLogoUploading(true)
    try {
      const result = await settingsService.uploadLogo(file)
      setForm((current) => ({ ...current, logoUrl: result.logoUrl }))
      pushToast({ title: 'Logo uploaded', description: 'Your logo has been saved. Click "Save branding" to apply.', tone: 'success' })
    } catch (uploadError) {
      pushToast({ title: 'Upload failed', description: uploadError instanceof Error ? uploadError.message : 'Unable to upload logo.', tone: 'danger' })
    } finally {
      setLogoUploading(false)
      if (logoInputRef.current) logoInputRef.current.value = ''
    }
  }

  async function save() {
    setSaving(true)
    try {
      const saved = normalizeBrandingSettings(await settingsService.updateBranding({
        logoUrl: form.logoUrl,
        primaryColor: form.primaryColor,
        secondaryColor: form.secondaryColor,
        showPoweredByEcosys: form.showPoweredByEcosys,
      }))
      setForm(saved)
      updateSession({
        primaryColor: saved.primaryColor,
        secondaryColor: saved.secondaryColor,
        logoUrl: saved.logoUrl ?? undefined,
      })
      pushToast({ title: 'Branding saved', description: 'Your brand settings are now active across the workspace.', tone: 'success' })
      await reload()
    } catch (saveError) {
      pushToast({ title: 'Save failed', description: saveError instanceof Error ? saveError.message : 'Unable to save branding.', tone: 'danger' })
    } finally {
      setSaving(false)
    }
  }

  if (loading) return <LoadingState label="Loading branding settings" />
  if (error) return <ErrorState title="Unable to load branding settings" description={error} />

  const companyName = session?.tenantName ?? 'Your Company'

  return (
    <div className="space-y-4">
      <PageHeader
        eyebrow="Settings"
        title="Tenant Branding"
        description="Customize your logo, brand colors, and workspace identity. Changes apply to the sidebar, PDF reports, and email notifications."
        actions={
          <div className="flex items-center gap-3">
            <Link className="button-secondary" to="/settings/documents">
              Back to Documents
            </Link>
            {isDirty ? <span className="text-xs font-semibold text-amber-400">Unsaved changes</span> : null}
            <button type="button" className="button-secondary" onClick={() => void reload()} disabled={loading}>
              <RefreshCw className="h-4 w-4" />
              Reset
            </button>
            <button type="button" className="button-primary" onClick={() => void save()} disabled={saving || logoUploading}>
              {saving ? 'Saving…' : 'Save branding'}
            </button>
          </div>
        }
      />

      <div className="grid gap-4 xl:grid-cols-[1fr_360px]">
        {/* ── Left: form ─────────────────────────────────────────────── */}
        <div className="space-y-4">

          {/* Logo */}
          <section className="surface-card space-y-3.5">
            <div className="flex items-center gap-3">
              <div className="icon-accent rounded-2xl p-3"><Upload className="h-5 w-5" /></div>
              <div>
                <h2 className="text-lg font-semibold text-app">Logo</h2>
                <p className="text-sm text-muted">Shown in the sidebar, PDF headers, and email footers.</p>
              </div>
            </div>
            <div className="grid gap-4 sm:grid-cols-[auto_1fr]">
              <div className="flex h-24 w-40 items-center justify-center overflow-hidden rounded-2xl border border-dashed border-app bg-[var(--app-subtle)]">
                {form.logoUrl ? (
                  <img src={form.logoUrl} alt="Brand logo" className="max-h-full max-w-full object-contain p-2" />
                ) : (
                  <p className="text-center text-xs text-muted">No logo</p>
                )}
              </div>
              <div className="space-y-3">
                <label className="button-secondary inline-flex cursor-pointer items-center gap-2">
                  <Upload className="h-4 w-4" />
                  {logoUploading ? 'Uploading…' : 'Upload logo'}
                  <input
                    ref={logoInputRef}
                    type="file"
                    accept="image/png,image/jpeg,image/webp,image/svg+xml"
                    className="hidden"
                    disabled={logoUploading}
                    onChange={(event) => {
                      const file = event.target.files?.[0]
                      if (file) void handleLogoUpload(file)
                    }}
                  />
                </label>
                <p className="text-xs text-muted">PNG, JPG, WebP, or SVG. Recommended size: 300×80 px, transparent background.</p>
                {form.logoUrl ? (
                  <div className="flex items-center gap-2">
                    <input
                      type="text"
                      value={form.logoUrl}
                      onChange={(event) => setForm((current) => ({ ...current, logoUrl: event.target.value }))}
                      className="field-input text-xs"
                      placeholder="Logo URL"
                    />
                    <button
                      type="button"
                      className="button-secondary shrink-0 text-xs"
                      onClick={() => setForm((current) => ({ ...current, logoUrl: null }))}
                    >
                      Remove
                    </button>
                  </div>
                ) : null}
              </div>
            </div>
          </section>

          {/* Colors */}
          <section className="surface-card space-y-3.5">
            <div className="flex items-center gap-3">
              <div className="icon-accent rounded-2xl p-3"><Palette className="h-5 w-5" /></div>
              <div>
                <h2 className="text-lg font-semibold text-app">Brand colors</h2>
                <p className="text-sm text-muted">Primary color is used for report headers and buttons. Secondary for borders and accents.</p>
              </div>
            </div>
            <div className="grid gap-4 sm:grid-cols-2">
              <label className="space-y-2">
                <span className="text-sm font-medium text-app">Primary color</span>
                <div className="flex items-center gap-3">
                  <input
                    type="color"
                    value={form.primaryColor}
                    onChange={(event) => setForm((current) => ({ ...current, primaryColor: event.target.value }))}
                    className="h-10 w-10 cursor-pointer rounded-xl border border-app bg-transparent p-0.5"
                  />
                  <input
                    type="text"
                    value={form.primaryColor}
                    onChange={(event) => {
                      const v = event.target.value
                      if (/^#[0-9a-fA-F]{0,6}$/.test(v)) setForm((current) => ({ ...current, primaryColor: v }))
                    }}
                    className="field-input font-mono text-sm"
                    maxLength={7}
                    placeholder="#0F4C81"
                  />
                </div>
              </label>
              <label className="space-y-2">
                <span className="text-sm font-medium text-app">Secondary / accent color</span>
                <div className="flex items-center gap-3">
                  <input
                    type="color"
                    value={form.secondaryColor}
                    onChange={(event) => setForm((current) => ({ ...current, secondaryColor: event.target.value }))}
                    className="h-10 w-10 cursor-pointer rounded-xl border border-app bg-transparent p-0.5"
                  />
                  <input
                    type="text"
                    value={form.secondaryColor}
                    onChange={(event) => {
                      const v = event.target.value
                      if (/^#[0-9a-fA-F]{0,6}$/.test(v)) setForm((current) => ({ ...current, secondaryColor: v }))
                    }}
                    className="field-input font-mono text-sm"
                    maxLength={7}
                    placeholder="#F4B942"
                  />
                </div>
              </label>
            </div>
            <div className="flex gap-2 pt-1 text-xs text-muted">
              <button type="button" className="underline" onClick={() => setForm((current) => ({ ...current, primaryColor: DEFAULT_PRIMARY, secondaryColor: DEFAULT_SECONDARY }))}>
                Reset to defaults
              </button>
            </div>
          </section>

          {/* Options */}
          <section className="surface-card space-y-2.5">
            <h2 className="text-lg font-semibold text-app">Options</h2>
            <label className="panel-subtle flex items-center justify-between rounded-2xl px-4 py-3">
              <div>
                <p className="text-sm font-medium text-app">Show "Powered by Ecosys"</p>
                <p className="text-xs text-muted">Appears on PDF footers and email signatures.</p>
              </div>
              <input
                type="checkbox"
                checked={form.showPoweredByEcosys}
                onChange={(event) => setForm((current) => ({ ...current, showPoweredByEcosys: event.target.checked }))}
              />
            </label>
          </section>
        </div>

        {/* ── Right: live preview ────────────────────────────────────── */}
        <div className="space-y-4">
          <section className="surface-card space-y-3.5">
            <div className="flex items-center gap-2">
              <Eye className="h-4 w-4 text-muted" />
              <h2 className="text-base font-semibold text-app">Live preview</h2>
            </div>

            {/* Sidebar strip preview */}
            <div>
              <p className="mb-2 text-xs font-semibold uppercase tracking-[0.14em] text-muted">Sidebar / nav</p>
              <div className="overflow-hidden rounded-2xl border border-app" style={{ background: '#1A1F23' }}>
                <div className="flex items-center gap-3 px-4 py-3">
                  {form.logoUrl ? (
                    <img src={form.logoUrl} alt="logo" className="h-8 max-w-[120px] object-contain" />
                  ) : (
                    <div className="flex items-center gap-2">
                      <div className="h-8 w-8 rounded-xl" style={{ background: form.primaryColor }} />
                      <span className="text-sm font-semibold text-white">{companyName}</span>
                    </div>
                  )}
                </div>
                {['Dashboard', 'Work Orders', 'Assets', 'Reports'].map((item, index) => (
                  <div
                    key={item}
                    className="flex items-center gap-3 px-4 py-2 text-sm"
                    style={{
                      background: index === 0 ? form.primaryColor + '33' : 'transparent',
                      color: index === 0 ? form.primaryColor : '#9CA3AF',
                      borderLeft: index === 0 ? `3px solid ${form.primaryColor}` : '3px solid transparent',
                    }}
                  >
                    {item}
                  </div>
                ))}
              </div>
            </div>

            {/* PDF header preview */}
            <div>
              <p className="mb-2 text-xs font-semibold uppercase tracking-[0.14em] text-muted">PDF report header</p>
              <div className="overflow-hidden rounded-2xl border border-app bg-white p-4">
                <div className="flex items-start justify-between gap-3">
                  <div className="flex items-center gap-3">
                    {form.logoUrl ? (
                      <img src={form.logoUrl} alt="logo" className="h-10 max-w-[80px] object-contain" />
                    ) : (
                      <div className="h-10 w-10 rounded-xl" style={{ background: form.primaryColor }} />
                    )}
                    <div>
                      <p className="text-base font-bold" style={{ color: form.primaryColor }}>{companyName}</p>
                      <p className="text-xs text-gray-500">support@yourcompany.com  ·  +1 555 000</p>
                    </div>
                  </div>
                  <div className="text-right">
                    <p className="text-xs text-gray-400">Generated: Today</p>
                  </div>
                </div>
                <div
                  className="mt-3 border-b-2 pb-2 text-sm font-semibold text-gray-800"
                  style={{ borderColor: form.secondaryColor }}
                >
                  WO-2024-001  |  HVAC Maintenance - Building A
                </div>
                <div className="mt-3 grid grid-cols-2 gap-2 text-xs text-gray-600">
                  {[['Client', 'Acme Corp'], ['Site', 'Main Campus'], ['Technician', 'John Doe'], ['Status', 'Completed']].map(([label, value]) => (
                    <div key={label}>
                      <span className="font-semibold">{label}: </span>{value}
                    </div>
                  ))}
                </div>
                <div
                  className="mt-3 rounded p-2 text-xs font-semibold text-white"
                  style={{ background: form.primaryColor }}
                >
                  Service details
                </div>
              </div>
            </div>

            {/* Color swatches */}
            <div className="flex gap-3">
              <div className="flex-1 space-y-1 text-center">
                <div className="h-10 rounded-xl" style={{ background: form.primaryColor }} />
                <p className="text-[10px] text-muted">Primary</p>
                <p className="font-mono text-[10px] text-app">{form.primaryColor}</p>
              </div>
              <div className="flex-1 space-y-1 text-center">
                <div className="h-10 rounded-xl" style={{ background: form.secondaryColor }} />
                <p className="text-[10px] text-muted">Secondary</p>
                <p className="font-mono text-[10px] text-app">{form.secondaryColor}</p>
              </div>
            </div>
          </section>
        </div>
      </div>
    </div>
  )
}
