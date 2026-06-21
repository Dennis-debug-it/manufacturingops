import { useEffect, useState } from 'react'
import { InfoAlert } from '../../../../components/ui/InfoAlert'
import { LoadingState } from '../../../../components/ui/LoadingState'
import { useToast } from '../../../../components/ui/ToastProvider'
import { useAsyncData } from '../../../../hooks/useAsyncData'
import { platformSettingsService, type PlatformIntegrationsSettings } from '../../../../services/platformSettingsService'
import { toServiceError } from '../../../../services/platformService'
import { Field, SectionTitle } from '../../../../modules/platform-v2/PlatformCommon'

export function IntegrationsSettingsPanel() {
  const { pushToast } = useToast()
  const { data, loading, error, reload } = useAsyncData(() => platformSettingsService.getIntegrations(), {
    mpesaDarajaEnabled: false,
    mpesaConsumerKeyMasked: '',
    mpesaConsumerSecretMasked: '',
    mpesaConsumerKey: '',
    mpesaConsumerSecret: '',
    webhooksEnabled: true,
    apiKeysEnabled: true,
    futureMonitoringIntegrationsNotes: 'Future monitoring integrations can be configured here.',
    emailSmtpEnabled: true,
  }, [])
  const [form, setForm] = useState<PlatformIntegrationsSettings>(data)
  const [saving, setSaving] = useState(false)

  useEffect(() => {
    setForm(data)
  }, [data])

  async function save() {
    setSaving(true)
    try {
      await platformSettingsService.updateIntegrations(form)
      pushToast({ title: 'Integrations settings saved', description: 'Integration settings were updated.', tone: 'success' })
      await reload()
    } catch (saveError) {
      pushToast({ title: 'Save failed', description: toServiceError(saveError, 'Unable to save integrations settings.'), tone: 'danger' })
    } finally {
      setSaving(false)
    }
  }

  if (loading) return <LoadingState label="Loading integrations settings" />
  if (error) return <InfoAlert title="Unable to load integrations settings" description={error} tone="danger" />

  return (
    <section className="surface-card space-y-4">
      <SectionTitle title="Integrations Settings" description="M-Pesa Daraja, SMTP linkage, webhooks, API keys, and future integration notes." />
      <div className="grid gap-3 md:grid-cols-2">
        <Field label="M-Pesa Consumer Key (masked on save)"><input type="password" placeholder={form.mpesaConsumerKeyMasked || 'Not configured'} value={form.mpesaConsumerKey || ''} onChange={(event) => setForm((current) => ({ ...current, mpesaConsumerKey: event.target.value }))} className="field-input" /></Field>
        <Field label="M-Pesa Consumer Secret (masked on save)"><input type="password" placeholder={form.mpesaConsumerSecretMasked || 'Not configured'} value={form.mpesaConsumerSecret || ''} onChange={(event) => setForm((current) => ({ ...current, mpesaConsumerSecret: event.target.value }))} className="field-input" /></Field>
      </div>
      <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
        <label className="panel-subtle flex items-center justify-between rounded-2xl px-4 py-3"><span className="text-sm text-app">M-Pesa Daraja</span><input type="checkbox" checked={form.mpesaDarajaEnabled} onChange={(event) => setForm((current) => ({ ...current, mpesaDarajaEnabled: event.target.checked }))} /></label>
        <label className="panel-subtle flex items-center justify-between rounded-2xl px-4 py-3"><span className="text-sm text-app">Email SMTP integration</span><input type="checkbox" checked={form.emailSmtpEnabled} onChange={(event) => setForm((current) => ({ ...current, emailSmtpEnabled: event.target.checked }))} /></label>
        <label className="panel-subtle flex items-center justify-between rounded-2xl px-4 py-3"><span className="text-sm text-app">Webhooks</span><input type="checkbox" checked={form.webhooksEnabled} onChange={(event) => setForm((current) => ({ ...current, webhooksEnabled: event.target.checked }))} /></label>
        <label className="panel-subtle flex items-center justify-between rounded-2xl px-4 py-3"><span className="text-sm text-app">API Keys</span><input type="checkbox" checked={form.apiKeysEnabled} onChange={(event) => setForm((current) => ({ ...current, apiKeysEnabled: event.target.checked }))} /></label>
      </div>
      <Field label="Future Monitoring Integrations">
        <textarea value={form.futureMonitoringIntegrationsNotes} onChange={(event) => setForm((current) => ({ ...current, futureMonitoringIntegrationsNotes: event.target.value }))} className="field-input min-h-[120px]" />
      </Field>
      <div className="flex justify-end">
        <button type="button" className="button-primary" onClick={() => void save()} disabled={saving}>{saving ? 'Saving...' : 'Save Integrations Settings'}</button>
      </div>
    </section>
  )
}
