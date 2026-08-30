import { SettingsForm } from '../../features/settings/components/SettingsForm'
import { TIME_ZONE_OPTIONS } from '../../shared/utils/timeZoneOptions'

export function IdentitySettingsPage() {
  return (
    <SettingsForm
      title="Platform Identity"
      subtitle="Platform name, support contact, locale, and legal company details shown to platform users."
      queryKey="platform-general"
      fetchUrl="/api/platform/settings/general"
      saveUrl="/api/platform/settings/general"
      permissionAction="platform.settings.manage"
      fields={[
        { key: 'platformName', label: 'Platform Name', type: 'text' },
        { key: 'supportEmail', label: 'Support Email', type: 'email' },
        { key: 'defaultCountry', label: 'Default Country', type: 'text' },
        { key: 'defaultCurrency', label: 'Default Currency', type: 'text' },
        {
          key: 'timezone',
          label: 'Default Timezone',
          type: 'select',
          options: TIME_ZONE_OPTIONS,
          helperText: 'Inherited by every tenant that has not set its own timezone.',
        },
        { key: 'defaultLanguage', label: 'Default Language', type: 'text' },
        { key: 'companyLegalName', label: 'Company Legal Name', type: 'text' },
        { key: 'companyRegistrationNumber', label: 'Company Registration Number', type: 'text' },
        { key: 'companyPinTaxNumber', label: 'Company PIN / Tax Number', type: 'text' },
      ]}
    />
  )
}
