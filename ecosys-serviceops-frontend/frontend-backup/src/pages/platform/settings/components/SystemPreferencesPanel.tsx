import { useEffect, useState } from 'react'
import { InfoAlert } from '../../../../components/ui/InfoAlert'
import { LoadingState } from '../../../../components/ui/LoadingState'
import { useToast } from '../../../../components/ui/ToastProvider'
import { FormSection, SettingsCard, StickyActionFooter } from '../../../../components/ui/Workspace'
import { useAsyncData } from '../../../../hooks/useAsyncData'
import { platformSettingsService, type PlatformSystemPreferences } from '../../../../services/platformSettingsService'
import { toServiceError } from '../../../../services/platformService'
import { Field, SectionTitle } from '../../../../modules/platform-v2/PlatformCommon'

export function SystemPreferencesPanel() {
  const { pushToast } = useToast()
  const { data, loading, error, reload } = useAsyncData(() => platformSettingsService.getSystemPreferences(), {
    dateFormat: 'yyyy-MM-dd',
    timeFormat: 'HH:mm',
    defaultPaginationSize: 25,
    enableDarkModeDefault: false,
    maintenanceMode: false,
    showBetaModules: false,
    allowTenantSelfRegistration: false,
  }, [])
  const [form, setForm] = useState<PlatformSystemPreferences>(data)
  const [saving, setSaving] = useState(false)

  useEffect(() => {
    setForm(data)
  }, [data])

  async function save() {
    setSaving(true)
    try {
      await platformSettingsService.updateSystemPreferences(form)
      pushToast({ title: 'System preferences saved', description: 'System preferences were updated.', tone: 'success' })
      await reload()
    } catch (saveError) {
      pushToast({ title: 'Save failed', description: toServiceError(saveError, 'Unable to save system preferences.'), tone: 'danger' })
    } finally {
      setSaving(false)
    }
  }

  if (loading) return <LoadingState label="Loading system preferences" />
  if (error) return <InfoAlert title="Unable to load system preferences" description={error} tone="danger" />

  return (
    <SettingsCard title="System Preferences" description="Date/time format, pagination defaults, maintenance and module visibility toggles.">
      <SectionTitle title="System Preferences" description="Date/time format, pagination defaults, maintenance and module visibility toggles." />
      <FormSection title="Workspace Defaults" description="Set rendering and behavior defaults for the platform workspace." columns={3}>
        <Field label="Date Format"><input value={form.dateFormat} onChange={(event) => setForm((current) => ({ ...current, dateFormat: event.target.value }))} className="field-input" /></Field>
        <Field label="Time Format"><input value={form.timeFormat} onChange={(event) => setForm((current) => ({ ...current, timeFormat: event.target.value }))} className="field-input" /></Field>
        <Field label="Default Pagination Size"><input type="number" min={10} max={200} value={form.defaultPaginationSize} onChange={(event) => setForm((current) => ({ ...current, defaultPaginationSize: Number(event.target.value) || 25 }))} className="field-input" /></Field>
      </FormSection>
      <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
        <label className="panel-subtle flex items-center justify-between rounded-2xl px-4 py-3"><span className="text-sm text-app">Enable dark mode by default</span><input type="checkbox" checked={form.enableDarkModeDefault} onChange={(event) => setForm((current) => ({ ...current, enableDarkModeDefault: event.target.checked }))} /></label>
        <label className="panel-subtle flex items-center justify-between rounded-2xl px-4 py-3"><span className="text-sm text-app">Maintenance mode</span><input type="checkbox" checked={form.maintenanceMode} onChange={(event) => setForm((current) => ({ ...current, maintenanceMode: event.target.checked }))} /></label>
        <label className="panel-subtle flex items-center justify-between rounded-2xl px-4 py-3"><span className="text-sm text-app">Show beta modules</span><input type="checkbox" checked={form.showBetaModules} onChange={(event) => setForm((current) => ({ ...current, showBetaModules: event.target.checked }))} /></label>
        <label className="panel-subtle flex items-center justify-between rounded-2xl px-4 py-3"><span className="text-sm text-app">Allow tenant self-registration</span><input type="checkbox" checked={form.allowTenantSelfRegistration} onChange={(event) => setForm((current) => ({ ...current, allowTenantSelfRegistration: event.target.checked }))} /></label>
      </div>
      <StickyActionFooter>
        <button type="button" className="button-primary" onClick={() => void save()} disabled={saving}>{saving ? 'Saving...' : 'Save System Preferences'}</button>
      </StickyActionFooter>
    </SettingsCard>
  )
}
