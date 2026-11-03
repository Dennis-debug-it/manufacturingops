import type { SettingsFieldOption } from '../../features/settings/components/SettingsForm'

/**
 * Zones offered in the settings pickers. East African zones lead because that is the platform's
 * home region; the rest cover the markets tenants operate in. The backend accepts any IANA id, so
 * this list is a convenience, not a limit.
 */
export const TIME_ZONE_OPTIONS: SettingsFieldOption[] = [
  { value: 'Africa/Nairobi', label: 'East Africa Time — Nairobi (UTC+3)' },
  { value: 'Africa/Kampala', label: 'East Africa Time — Kampala (UTC+3)' },
  { value: 'Africa/Dar_es_Salaam', label: 'East Africa Time — Dar es Salaam (UTC+3)' },
  { value: 'Africa/Kigali', label: 'Central Africa Time — Kigali (UTC+2)' },
  { value: 'Africa/Addis_Ababa', label: 'East Africa Time — Addis Ababa (UTC+3)' },
  { value: 'Africa/Mogadishu', label: 'East Africa Time — Mogadishu (UTC+3)' },
  { value: 'Africa/Juba', label: 'Central Africa Time — Juba (UTC+2)' },
  { value: 'Africa/Lagos', label: 'West Africa Time — Lagos (UTC+1)' },
  { value: 'Africa/Accra', label: 'Greenwich Mean Time — Accra (UTC+0)' },
  { value: 'Africa/Johannesburg', label: 'South Africa Standard Time — Johannesburg (UTC+2)' },
  { value: 'Africa/Cairo', label: 'Eastern European Time — Cairo (UTC+2)' },
  { value: 'Europe/London', label: 'United Kingdom — London (UTC+0/+1)' },
  { value: 'Europe/Paris', label: 'Central European Time — Paris (UTC+1/+2)' },
  { value: 'Asia/Dubai', label: 'Gulf Standard Time — Dubai (UTC+4)' },
  { value: 'Asia/Kolkata', label: 'India Standard Time — Kolkata (UTC+5:30)' },
  { value: 'America/New_York', label: 'Eastern Time — New York (UTC-5/-4)' },
  { value: 'UTC', label: 'Coordinated Universal Time (UTC)' },
]
