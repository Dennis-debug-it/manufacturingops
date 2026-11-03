import { SettingsForm } from '../../features/settings/components/SettingsForm'

export function BrandingSettingsPage() {
  return (
    <SettingsForm
      title="Platform Branding"
      subtitle="Logo, favicon, and colour palette applied across the platform admin console and outbound documents."
      queryKey="platform-branding"
      fetchUrl="/api/platform/settings/branding"
      saveUrl="/api/platform/settings/branding"
      permissionAction="platform.settings.manage"
      fields={[
        { key: 'platformLogoUrl', label: 'Platform Logo URL', type: 'text' },
        { key: 'faviconUrl', label: 'Favicon URL', type: 'text' },
        { key: 'primaryColor', label: 'Primary Colour', type: 'color' },
        { key: 'secondaryColor', label: 'Secondary Colour', type: 'color' },
        { key: 'accentColor', label: 'Accent Colour', type: 'color' },
        { key: 'showPoweredByEcosys', label: 'Show "Powered by Ecosys" on tenant workspaces', type: 'checkbox' },
        { key: 'loginPageBrandingPreview', label: 'Show Branding Preview on Login Page', type: 'checkbox' },
        { key: 'documentBrandingPreview', label: 'Show Branding Preview on Generated Documents', type: 'checkbox' },
      ]}
    />
  )
}
