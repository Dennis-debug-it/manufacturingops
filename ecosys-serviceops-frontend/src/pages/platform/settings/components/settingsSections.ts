import type { PlatformSettingsSection } from '../../../../services/platformSettingsService'

export type SettingsNavSection = {
  id: PlatformSettingsSection
  label: string
  shortLabel: string
  description: string
}

export const settingsSections: SettingsNavSection[] = [
  { id: 'general', label: 'General', shortLabel: 'General', description: 'Platform identity and defaults.' },
  { id: 'branding', label: 'Branding', shortLabel: 'Branding', description: 'Visual identity and logo controls.' },
  { id: 'email', label: 'Email & Notifications', shortLabel: 'Email', description: 'SMTP setup, outbound content controls, notification rules, and delivery logs.' },
  { id: 'numbering', label: 'Numbering', shortLabel: 'Numbers', description: 'Prefixes, counters, and generated previews.' },
  { id: 'security', label: 'Security', shortLabel: 'Security', description: 'Password and session policies.' },
  { id: 'system-preferences', label: 'System Preferences', shortLabel: 'System', description: 'Date/time, pagination, and platform toggles.' },
]
