import { SettingsForm } from './components/SettingsForm'

export function BrandingSettingsPage() {
  return (
    <SettingsForm
      title="Branding"
      subtitle="Logo, primary colour, document, and email branding."
      queryKey="branding"
      fetchUrl="/api/settings/branding"
      saveUrl="/api/settings/branding"
      fields={[
        { key: 'logoUrl', label: 'Logo URL', type: 'text' },
        { key: 'primaryColor', label: 'Primary Colour', type: 'color' },
        { key: 'secondaryColor', label: 'Secondary Colour', type: 'color' },
        { key: 'showPoweredByEcosys', label: 'Show "Powered by Ecosys"', type: 'checkbox' },
      ]}
    />
  )
}
