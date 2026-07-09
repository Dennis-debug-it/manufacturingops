import { SettingsForm } from './components/SettingsForm'

export function CompanySettingsPage() {
  return (
    <SettingsForm
      title="Company Profile"
      subtitle="Legal details, addresses, timezone, and currency."
      queryKey="company"
      fetchUrl="/api/settings/company"
      saveUrl="/api/settings/company"
      fields={[
        { key: 'companyName', label: 'Company Name', type: 'text' },
        { key: 'legalName', label: 'Legal Name', type: 'text' },
        { key: 'tradingName', label: 'Trading Name', type: 'text' },
        { key: 'email', label: 'Email', type: 'email' },
        { key: 'phone', label: 'Phone', type: 'text' },
        { key: 'website', label: 'Website', type: 'text' },
        { key: 'vatNumber', label: 'VAT Number', type: 'text' },
        { key: 'registrationNumber', label: 'Registration Number', type: 'text' },
        { key: 'country', label: 'Country', type: 'text' },
        { key: 'industry', label: 'Industry', type: 'text' },
        { key: 'physicalAddress', label: 'Physical Address', type: 'textarea' },
        { key: 'postalAddress', label: 'Postal Address', type: 'textarea' },
      ]}
    />
  )
}
