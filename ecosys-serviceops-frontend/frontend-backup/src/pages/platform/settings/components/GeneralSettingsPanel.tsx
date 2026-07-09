import { useEffect, useState } from 'react'
import { InfoAlert } from '../../../../components/ui/InfoAlert'
import { LoadingState } from '../../../../components/ui/LoadingState'
import { useToast } from '../../../../components/ui/ToastProvider'
import { FormSection, SettingsCard, StickyActionFooter } from '../../../../components/ui/Workspace'
import { useAsyncData } from '../../../../hooks/useAsyncData'
import { platformSettingsService, type PlatformGeneralSettings } from '../../../../services/platformSettingsService'
import { toServiceError } from '../../../../services/platformService'
import { Field, SectionTitle } from '../../../../modules/platform-v2/PlatformCommon'

export function GeneralSettingsPanel() {
  const { pushToast } = useToast()
  const { data, loading, error, reload } = useAsyncData(() => platformSettingsService.getGeneral(), {
    platformName: '',
    supportEmail: '',
    defaultCountry: 'Kenya',
    defaultCurrency: 'KES',
    timezone: 'Africa/Nairobi',
    companyLegalName: '',
    companyRegistrationNumber: '',
    companyPinTaxNumber: '',
    defaultLanguage: 'en',
  }, [])
  const [form, setForm] = useState<PlatformGeneralSettings>(data)
  const [saving, setSaving] = useState(false)

  useEffect(() => {
    setForm(data)
  }, [data])

  async function save() {
    if (!form.platformName.trim()) {
      pushToast({ title: 'Platform name required', description: 'Enter a platform name before saving.', tone: 'warning' })
      return
    }
    if (!form.supportEmail.trim() || !form.supportEmail.includes('@')) {
      pushToast({ title: 'Support email required', description: 'Provide a valid support email address.', tone: 'warning' })
      return
    }

    setSaving(true)
    try {
      await platformSettingsService.updateGeneral(form)
      pushToast({ title: 'General settings saved', description: 'Platform general settings were updated.', tone: 'success' })
      await reload()
    } catch (saveError) {
      pushToast({ title: 'Save failed', description: toServiceError(saveError, 'Unable to save general settings.'), tone: 'danger' })
    } finally {
      setSaving(false)
    }
  }

  if (loading) return <LoadingState label="Loading general settings" />
  if (error) return <InfoAlert title="Unable to load general settings" description={error} tone="danger" />

  return (
    <SettingsCard title="General Settings" description="Core platform identity, location, and default operating values.">
      <SectionTitle title="General Settings" description="Core platform identity, location, and default operating values." />
      <FormSection title="Platform Identity" description="Fundamental metadata for your service organization.">
        <Field label="Platform Name"><input value={form.platformName} onChange={(event) => setForm((current) => ({ ...current, platformName: event.target.value }))} className="field-input" /></Field>
        <Field label="Support Email"><input type="email" value={form.supportEmail} onChange={(event) => setForm((current) => ({ ...current, supportEmail: event.target.value }))} className="field-input" /></Field>
        <Field label="Company Legal Name"><input value={form.companyLegalName} onChange={(event) => setForm((current) => ({ ...current, companyLegalName: event.target.value }))} className="field-input" /></Field>
        <Field label="Company Registration Number"><input value={form.companyRegistrationNumber} onChange={(event) => setForm((current) => ({ ...current, companyRegistrationNumber: event.target.value }))} className="field-input" /></Field>
        <Field label="Company PIN / Tax Number"><input value={form.companyPinTaxNumber} onChange={(event) => setForm((current) => ({ ...current, companyPinTaxNumber: event.target.value }))} className="field-input" /></Field>
      </FormSection>
      <FormSection title="Regional Defaults" description="Set global standards for date, language, and localization.">
        <Field label="Default Country"><input value={form.defaultCountry} onChange={(event) => setForm((current) => ({ ...current, defaultCountry: event.target.value }))} className="field-input" /></Field>
        <Field label="Default Currency"><input value={form.defaultCurrency} onChange={(event) => setForm((current) => ({ ...current, defaultCurrency: event.target.value.toUpperCase() }))} className="field-input" /></Field>
        <Field label="Timezone"><input value={form.timezone} onChange={(event) => setForm((current) => ({ ...current, timezone: event.target.value }))} className="field-input" /></Field>
        <Field label="Default Language"><input value={form.defaultLanguage} onChange={(event) => setForm((current) => ({ ...current, defaultLanguage: event.target.value }))} className="field-input" /></Field>
      </FormSection>
      <StickyActionFooter>
        <button type="button" className="button-primary" onClick={() => void save()} disabled={saving}>{saving ? 'Saving...' : 'Save General Settings'}</button>
      </StickyActionFooter>
    </SettingsCard>
  )
}
