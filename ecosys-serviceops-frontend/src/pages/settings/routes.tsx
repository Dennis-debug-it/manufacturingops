import type { ReactElement } from 'react'
import { Navigate } from 'react-router-dom'
import {
  Bell,
  Boxes,
  Building2,
  ClipboardCheck,
  ListChecks,
  FileDigit,
  FileText,
  History,
  KeyRound,
  Mail,
  MonitorCog,
  Workflow,
  type LucideIcon,
} from 'lucide-react'
import { SettingsBranchesPage } from '../../modules/settings/BranchesPage'
import { ReportTemplateDesignerPage } from '../../modules/settings/ReportTemplateDesignerPage'
import {
  LiveNotificationsSettingsPage,
  LiveOperationsPolicySettingsPage,
  LiveSecuritySettingsPage,
} from '../../modules/settings/AdvancedSettingsPages'
import {
  EmailNotificationsSettingsPage,
  EmailTemplatesSettingsPage,
  MonitoringToolIntakeSettingsPage,
  NumberingRulesAdminPage,
} from '../../modules/settings/AdminSettingsPages'
import { AssetCategoriesSettingsPage } from '../../modules/settings/AssetCategoriesSettingsPage'
import { IntakeProtocolsPage } from '../../modules/settings/intake/IntakeProtocolsPage'
import { SLAManagementPage } from '../../modules/sla/SLAManagementPage'
import {
  AuditLogsSettingsPage,
  CompanyProfileSettingsPage,
} from './SettingsDetailPages'
import { WorkOrderNumberingPage } from '../../modules/settings/NumberingRulesPage'
import { ChecklistTemplatesPage } from '../../modules/settings/ChecklistTemplatesPage'

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

function section(
  segment: string,
  label: string,
  shortLabel: string,
  description: string,
  icon: LucideIcon,
): SettingsSection {
  return {
    label,
    shortLabel,
    description,
    segment,
    path: `/settings/${segment}`,
    icon,
  }
}

export const settingsSections: SettingsSection[] = [
  section('company-profile', 'Company Profile', 'Profile', 'Legal and company identity details.', Building2),
  section('report-template', 'Report Template', 'Reports', 'Design how PDF work order reports look — sections, fonts, footer, watermark.', FileText),
  section('branches', 'Branches / Outlets', 'Branches', 'Branch structure, outlet details, and activation.', Workflow),
  section('asset-categories', 'Asset Categories', 'Categories', 'Define asset categories and custom field schemas.', Boxes),
  section('sla-plans', 'Response Plans', 'SLA', 'Configure response and resolution targets by priority.', Workflow),
  section('numbering-rules', 'Numbering Rules', 'Numbers', 'Prefixes, counters, and document numbering behavior.', FileDigit),
  section('numbering', 'Work Order Numbering', 'WO Numbers', 'Configure prefix, padding, year/month segments, and sequence for work order numbers.', FileDigit),
  section('operations-policy', 'Operations', 'Operations', 'Work order completion policy and signature requirements.', ClipboardCheck),
  section('checklist-templates', 'Checklist Templates', 'Checklists', 'Install starter packs and manage service checklist templates.', ListChecks),
  section('email-notifications', 'Email Notifications', 'Email', 'SMTP and outbound notification sender settings.', Mail),
  section('email-templates', 'Email Templates', 'Templates', 'Edit workspace email templates for invites, onboarding, and operational emails.', Mail),
  section('email-intake', 'Email Intake', 'Email', 'Build intake protocols for automated work order generation from email and monitoring sources.', Mail),
  section('monitoring-intake', 'Monitoring Tool Intake', 'Monitor', 'Webhook intake and automated monitoring work creation.', MonitorCog),
  section('notifications', 'Notifications', 'Alerts', 'Notifications, alerts, and digest preferences.', Bell),
  section('password-rules', 'Password Rules', 'Security', 'Password, MFA, and session security policies.', KeyRound),
  section('audit-logs', 'Audit Logs', 'Audit', 'Configuration history and workspace change tracking.', History),
]

export const defaultSettingsSegment = settingsSections[0].segment

export const settingsPageRoutes: SettingsRouteDefinition[] = [
  { path: 'company-profile', element: <CompanyProfileSettingsPage /> },
  { path: 'report-template', element: <ReportTemplateDesignerPage /> },
  { path: 'users', element: <Navigate to="/team-management?tab=people" replace /> },
  { path: 'assignment-groups', element: <Navigate to="/team-management?tab=groups" replace /> },
  { path: 'branches', element: <SettingsBranchesPage /> },
  { path: 'asset-categories', element: <AssetCategoriesSettingsPage /> },
  { path: 'sla-plans', element: <SLAManagementPage /> },
  { path: 'numbering-rules', element: <NumberingRulesAdminPage /> },
  { path: 'numbering', element: <WorkOrderNumberingPage /> },
  { path: 'operations-policy', element: <LiveOperationsPolicySettingsPage /> },
  { path: 'checklist-templates', element: <ChecklistTemplatesPage /> },
  { path: 'email-notifications', element: <EmailNotificationsSettingsPage /> },
  { path: 'email-templates', element: <EmailTemplatesSettingsPage /> },
  { path: 'email-intake', element: <IntakeProtocolsPage /> },
  { path: 'monitoring-intake', element: <MonitoringToolIntakeSettingsPage /> },
  { path: 'notifications', element: <LiveNotificationsSettingsPage /> },
  { path: 'password-rules', element: <LiveSecuritySettingsPage /> },
  { path: 'audit-logs', element: <AuditLogsSettingsPage /> },
]

export const settingsLegacyRedirects: Array<{ from: string; to: string }> = [
  { from: 'branding', to: 'company-profile' },
  { from: 'users-groups', to: 'users' },
  { from: 'users-roles', to: 'users' },
  { from: 'roles-permissions', to: 'users' },
  { from: 'branches-outlets', to: 'branches' },
  { from: 'sla-contracts', to: 'sla-plans' },
  { from: 'email-settings', to: 'email-notifications' },
  { from: 'monitoring-tool-intake', to: 'monitoring-intake' },
  { from: 'security', to: 'password-rules' },
  { from: 'data-import', to: 'audit-logs' },
]
