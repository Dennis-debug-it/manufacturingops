import { useEffect, useState } from 'react'
import { EcosysLogo, PoweredByEcosys } from '../../../../components/brand'
import { brand } from '../../../../config/brand'
import { InfoAlert } from '../../../../components/ui/InfoAlert'
import { LoadingState } from '../../../../components/ui/LoadingState'
import { useToast } from '../../../../components/ui/ToastProvider'
import { FormSection, SettingsCard, StickyActionFooter } from '../../../../components/ui/Workspace'
import { useAsyncData } from '../../../../hooks/useAsyncData'
import { Field, SectionTitle } from '../../../../modules/platform-v2/PlatformCommon'
import { platformSettingsService, type PlatformBrandingSettings } from '../../../../services/platformSettingsService'
import { toServiceError } from '../../../../services/platformService'

export function BrandingSettingsPanel() {
  const { pushToast } = useToast()
  const { data, loading, error, reload } = useAsyncData(
    () => platformSettingsService.getBranding(),
    {
      platformLogoUrl: '/ecosys-logo-horizontal-dark.svg',
      faviconUrl: '/ecosys-favicon.svg',
      primaryColor: brand.colors.darkTeal,
      secondaryColor: brand.colors.teal,
      accentColor: brand.colors.lime,
      showPoweredByEcosys: true,
      loginPageBrandingPreview: true,
      documentBrandingPreview: true,
    },
    [],
  )
  const [form, setForm] = useState<PlatformBrandingSettings>(data)
  const [saving, setSaving] = useState(false)

  useEffect(() => {
    setForm(data)
  }, [data])

  async function save() {
    setSaving(true)
    try {
      await platformSettingsService.updateBranding(form)
      pushToast({ title: 'Branding saved', description: 'Branding settings were updated.', tone: 'success' })
      await reload()
    } catch (saveError) {
      pushToast({ title: 'Save failed', description: toServiceError(saveError, 'Unable to save branding settings.'), tone: 'danger' })
    } finally {
      setSaving(false)
    }
  }

  if (loading) return <LoadingState label="Loading branding settings" />
  if (error) return <InfoAlert title="Unable to load branding settings" description={error} tone="danger" />

  return (
    <SettingsCard title="Branding Settings" description="Platform brand assets, palette, and visual previews.">
      <SectionTitle title="Branding Settings" description="Platform brand assets, palette, and visual previews." />
      <FormSection title="Brand Assets" description="Core URLs and colors used across platform touchpoints.">
        <Field label="Platform Logo URL"><input value={form.platformLogoUrl} onChange={(event) => setForm((current) => ({ ...current, platformLogoUrl: event.target.value }))} className="field-input" /></Field>
        <Field label="Favicon URL"><input value={form.faviconUrl} onChange={(event) => setForm((current) => ({ ...current, faviconUrl: event.target.value }))} className="field-input" /></Field>
        <Field label="Primary Color"><input value={form.primaryColor} onChange={(event) => setForm((current) => ({ ...current, primaryColor: event.target.value }))} className="field-input" /></Field>
        <Field label="Secondary Color"><input value={form.secondaryColor} onChange={(event) => setForm((current) => ({ ...current, secondaryColor: event.target.value }))} className="field-input" /></Field>
        <Field label="Accent Color"><input value={form.accentColor} onChange={(event) => setForm((current) => ({ ...current, accentColor: event.target.value }))} className="field-input" /></Field>
      </FormSection>
      <div className="grid gap-3 md:grid-cols-3">
        <label className="panel-subtle flex items-center justify-between rounded-2xl px-4 py-3">
          <span className="text-sm text-app">Show &ldquo;Powered by Ecosys&rdquo;</span>
          <input type="checkbox" checked={form.showPoweredByEcosys} onChange={(event) => setForm((current) => ({ ...current, showPoweredByEcosys: event.target.checked }))} />
        </label>
        <label className="panel-subtle flex items-center justify-between rounded-2xl px-4 py-3">
          <span className="text-sm text-app">Login page branding preview</span>
          <input type="checkbox" checked={form.loginPageBrandingPreview} onChange={(event) => setForm((current) => ({ ...current, loginPageBrandingPreview: event.target.checked }))} />
        </label>
        <label className="panel-subtle flex items-center justify-between rounded-2xl px-4 py-3">
          <span className="text-sm text-app">Document branding preview</span>
          <input type="checkbox" checked={form.documentBrandingPreview} onChange={(event) => setForm((current) => ({ ...current, documentBrandingPreview: event.target.checked }))} />
        </label>
      </div>
      <div className="grid gap-3 md:grid-cols-2">
        <article className="panel-subtle rounded-2xl p-4">
          <p className="text-xs font-semibold uppercase tracking-[0.16em] text-muted">Login Preview</p>
          <div className="mt-3 rounded-2xl border border-app p-4" style={{ borderColor: form.primaryColor, background: 'linear-gradient(135deg, rgba(14, 124, 102, 0.08), rgba(245, 197, 66, 0.08))' }}>
            <EcosysLogo variant="dark" size="md" subtitle="ServiceOps Workspace" />
            <p className="mt-4 text-sm font-semibold text-app">Welcome back</p>
            <p className="mt-1 text-xs text-muted">Brand preview: {form.loginPageBrandingPreview ? 'Enabled' : 'Disabled'}</p>
          </div>
        </article>
        <article className="panel-subtle rounded-2xl p-4">
          <p className="text-xs font-semibold uppercase tracking-[0.16em] text-muted">Document Preview</p>
          <div className="mt-3 rounded-2xl border border-app p-4" style={{ borderColor: form.secondaryColor }}>
            <div className="flex flex-wrap items-center justify-between gap-3">
              <EcosysLogo variant="dark" size="md" subtitle="Enterprise Document" />
              {form.showPoweredByEcosys ? <PoweredByEcosys tone="light" /> : null}
            </div>
            <p className="mt-4 text-sm font-semibold text-app">INV-000001</p>
            <p className="mt-1 text-xs text-muted">Brand preview: {form.documentBrandingPreview ? 'Enabled' : 'Disabled'}</p>
          </div>
        </article>
      </div>
      <StickyActionFooter>
        <button type="button" className="button-primary" onClick={() => void save()} disabled={saving}>{saving ? 'Saving...' : 'Save Branding Settings'}</button>
      </StickyActionFooter>
    </SettingsCard>
  )
}
