import type { ReactElement } from 'react'
import { Navigate } from 'react-router-dom'
import type { LucideIcon } from 'lucide-react'
import {
  defaultSettingsGroupPath,
  settingsGroups,
  settingsLegacyRedirects,
  settingsPageRoutes as enterpriseSettingsPageRoutes,
  type SettingsGroup,
} from './enterpriseSettings'

export type SettingsSection = {
  label: string
  shortLabel: string
  description: string
  segment: string
  path: string
  icon: LucideIcon
}

export type SettingsRouteDefinition = {
  path: string
  element: ReactElement
}

export type SettingsPathMatcher = {
  groupPath: string
  matchers: string[]
}

export const settingsSections: SettingsSection[] = settingsGroups.map((group) => ({
  label: group.label,
  shortLabel: group.label,
  description: group.description,
  segment: group.id,
  path: group.path,
  icon: group.icon,
}))

export const defaultSettingsSegment = settingsGroups[0]?.id ?? 'workspace'

export { settingsGroups, settingsLegacyRedirects, defaultSettingsGroupPath }

export const settingsDefaultRoute: ReactElement = <Navigate to={defaultSettingsGroupPath} replace />

const legacyRedirectRoutes: SettingsRouteDefinition[] = settingsLegacyRedirects.map((redirect) => ({
  path: redirect.from,
  element: <Navigate to={`/settings/${redirect.to}`} replace />,
}))

export const settingsPageRouteDefinitions: SettingsRouteDefinition[] = [...enterpriseSettingsPageRoutes, ...legacyRedirectRoutes]

export const settingsPageRoutes = settingsPageRouteDefinitions

export const settingsPathMatchers: SettingsPathMatcher[] = [
  ...settingsGroups.map((group: SettingsGroup) => ({
    groupPath: group.path,
    matchers: [group.path],
  })),
  ...settingsLegacyRedirects.map((redirect) => ({
    groupPath: `/settings/${redirect.to}`,
    matchers: [`/settings/${redirect.from}`],
  })),
]
