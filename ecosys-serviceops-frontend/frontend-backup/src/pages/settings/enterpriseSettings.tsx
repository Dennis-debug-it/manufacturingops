// @ts-nocheck
import {
  Building2,
  CalendarDays,
  CircleSlash2,
  Download,
  FileText,
  Filter,
  Mail,
  Receipt,
  Search,
  ServerCog,
  ShieldCheck,
  Table2,
  Users2,
  Workflow,
  type LucideIcon,
} from 'lucide-react'
import { useEffect, useMemo, useState, type ReactNode } from 'react'
import { Badge } from '../../components/ui/Badge'
import { DataTable } from '../../components/ui/DataTable'
import { Drawer } from '../../components/ui/Drawer'
import { ErrorState } from '../../components/ui/ErrorState'
import { LoadingState } from '../../components/ui/LoadingState'
import { PageHeader } from '../../components/ui/PageHeader'
import { SettingsSection } from '../../components/ui/Workspace'
import { useAsyncData } from '../../hooks/useAsyncData'
import { branchService } from '../../services/branchService'
import { settingsService, type DocumentsSettingsBundle, type UpdateDocumentsSettingsInput } from '../../services/settingsService'
import { userService } from '../../services/userService'
import { useToast } from '../../components/ui/ToastProvider'
import { api } from '../../lib/api'
import type {
  AssignmentGroupRecord,
  BranchRecord,
  EmailIntakeSettings,
  EmailNotificationSettings,
  EmailTemplateRecord,
  MonitoringWebhookIntegrationRecord,
  NumberingRuleRecord,
  NotificationSettings,
  TenantSecuritySettings,
  UpdateEmailIntakeSettingsInput,
  UpdateEmailNotificationSettingsInput,
  UserRecord,
} from '../../types/api'

export type SettingsGroupId =
  | 'workspace'
  | 'access'
  | 'service'
  | 'dispatch'
  | 'documents'
  | 'finance'
  | 'intake'
  | 'notifications'
  | 'integrations'
  | 'logs'
  | 'security'

export type SettingsGroup = {
  id: SettingsGroupId
  label: string
  description: string
  path: string
  icon: LucideIcon
}

export const settingsGroups: SettingsGroup[] = [
  { id: 'workspace', label: 'System Settings', description: 'Workspace profile, branches, and numbering rules.', path: '/settings/workspace', icon: Building2 },
  { id: 'access', label: 'User & Access Management', description: 'Users, roles, permissions, and assignment groups.', path: '/settings/access', icon: Users2 },
  { id: 'service', label: 'Service Management', description: 'Work order controls and completion policy.', path: '/settings/service', icon: Workflow },
  { id: 'dispatch', label: 'Dispatch Management', description: 'Working hours, routing, and availability rules.', path: '/settings/dispatch', icon: CalendarDays },
  { id: 'documents', label: 'Document Management', description: 'Templates, appearance, branding, and document defaults.', path: '/settings/documents', icon: FileText },
  { id: 'finance', label: 'Finance Settings', description: 'Currency, tax, billing, and payment rules.', path: '/settings/finance', icon: Receipt },
  { id: 'intake', label: 'Intake Management', description: 'Email intake, monitoring intake, and matching rules.', path: '/settings/intake', icon: Mail },
  { id: 'notifications', label: 'Notification Management', description: 'Work order alerts, SLA alerts, and delivery rules.', path: '/settings/notifications', icon: Filter },
  { id: 'integrations', label: 'Integration Management', description: 'Email providers, monitoring systems, APIs, and webhooks.', path: '/settings/integrations', icon: ServerCog },
  { id: 'logs', label: 'Log Management', description: 'Audit and system tables for operational review.', path: '/settings/logs', icon: Table2 },
  { id: 'security', label: 'Security & Account', description: 'Password policy, session policy, export, and ownership controls.', path: '/settings/security', icon: ShieldCheck },
]

export const defaultSettingsGroupPath = settingsGroups[0]?.path ?? '/settings/workspace'

export const settingsLegacyRedirects: Array<{ from: string; to: string }> = [
  { from: 'company-profile', to: 'workspace' },
  { from: 'branches', to: 'workspace' },
  { from: 'branches-outlets', to: 'workspace' },
  { from: 'numbering-rules', to: 'workspace' },
  { from: 'numbering', to: 'workspace' },
  { from: 'users', to: 'access' },
  { from: 'roles-permissions', to: 'access' },
  { from: 'assignment-groups', to: 'access' },
  { from: 'teams', to: 'access' },
  { from: 'asset-categories', to: 'service' },
  { from: 'checklist-templates', to: 'service' },
  { from: 'operations-policy', to: 'service' },
  { from: 'sla-plans', to: 'service' },
  { from: 'documents-branding', to: 'documents' },
  { from: 'branding', to: 'documents' },
  { from: 'report-template', to: 'documents' },
  { from: 'email-intake', to: 'intake' },
  { from: 'monitoring-intake', to: 'integrations' },
  { from: 'email-notifications', to: 'notifications' },
  { from: 'email-templates', to: 'notifications' },
  { from: 'notifications', to: 'notifications' },
  { from: 'audit-logs', to: 'logs' },
  { from: 'account-management', to: 'security' },
  { from: 'danger-zone', to: 'security' },
  { from: 'password-rules', to: 'security' },
  { from: 'data-import', to: 'security' },
]

export const settingsPageRoutes = [
  { path: 'workspace', element: <WorkspaceSettingsPage /> },
  { path: 'access', element: <AccessSettingsPage /> },
  { path: 'service', element: <ServiceSettingsPage /> },
  { path: 'dispatch', element: <DispatchSettingsPage /> },
  { path: 'documents', element: <DocumentsSettingsPage /> },
  { path: 'finance', element: <FinanceSettingsPage /> },
  { path: 'intake', element: <IntakeSettingsPage /> },
  { path: 'notifications', element: <NotificationSettingsPage /> },
  { path: 'integrations', element: <IntegrationSettingsPage /> },
  { path: 'logs', element: <LogSettingsPage /> },
  { path: 'security', element: <SecuritySettingsPage /> },
]

function SettingsPageFrame({
  title,
  description,
  actions,
  children,
}: {
  title: string
  description: string
  actions?: ReactNode
  children: ReactNode
}) {
  return (
    <div className="space-y-4">
      <PageHeader eyebrow="Settings" title={title} description={description} actions={actions} />
      {children}
    </div>
  )
}

function ShellSection({
  title,
  description,
  actions,
  children,
}: {
  title: string
  description?: string
  actions?: ReactNode
  children: ReactNode
}) {
  return (
    <SettingsSection title={title} description={description} actions={actions}>
      {children}
    </SettingsSection>
  )
}

function SectionNote({ title, detail }: { title: string; detail: string }) {
  return (
    <div className="border-t border-[var(--app-divider)] pt-4 first:border-t-0 first:pt-0">
      <p className="text-sm font-semibold text-app">{title}</p>
      <p className="mt-1 text-sm leading-6 text-muted">{detail}</p>
    </div>
  )
}

function Field({
  label,
  description,
  children,
}: {
  label: string
  description?: string
  children: ReactNode
}) {
  return (
    <label className="space-y-1.5">
      <span className="text-xs font-semibold uppercase tracking-[0.18em] text-muted">{label}</span>
      {children}
      {description ? <span className="block text-xs leading-5 text-muted">{description}</span> : null}
    </label>
  )
}

function inputClass() {
  return 'field-input w-full'
}

function emptySettingCopy(title: string, detail: string) {
  return (
    <div className="border-t border-[var(--app-divider)] pt-4">
      <p className="text-sm font-semibold text-app">{title}</p>
      <p className="mt-1 text-sm leading-6 text-muted">{detail}</p>
    </div>
  )
}

type DocumentsFormState = UpdateDocumentsSettingsInput

function toDocumentsFormState(bundle: DocumentsSettingsBundle): DocumentsFormState {
  return {
    profile: {
      companyName: bundle.profile.companyName,
      legalName: bundle.profile.legalName,
      tradingName: bundle.profile.tradingName,
      postalAddress: bundle.profile.postalAddress,
      physicalAddress: bundle.profile.physicalAddress,
      website: bundle.profile.website,
      vatNumber: bundle.profile.vatNumber,
      registrationNumber: bundle.profile.registrationNumber,
      email: bundle.profile.email,
      phone: bundle.profile.phone,
      country: bundle.profile.country,
      industry: bundle.profile.industry,
      address: bundle.profile.address,
      logoUrl: bundle.profile.logoUrl,
      primaryColor: bundle.profile.primaryColor,
      secondaryColor: bundle.profile.secondaryColor,
      showPoweredByEcosys: bundle.profile.showPoweredByEcosys,
    },
    branding: {
      logoUrl: bundle.branding.logoUrl,
      primaryColor: bundle.branding.primaryColor,
      secondaryColor: bundle.branding.secondaryColor,
      showPoweredByEcosys: bundle.branding.showPoweredByEcosys,
    },
    template: bundle.template,
  }
}

function toEmailIntakeForm(settings: EmailIntakeSettings): UpdateEmailIntakeSettingsInput {
  return {
    isEnabled: settings.isEnabled,
    intakeEmailAddress: settings.intakeEmailAddress ?? null,
    mailboxProvider: settings.mailboxProvider,
    host: settings.host,
    port: settings.port,
    useSsl: settings.useSsl,
    username: settings.username ?? null,
    password: undefined,
    defaultClientId: settings.defaultClientId ?? null,
    defaultBranchId: settings.defaultBranchId ?? null,
    defaultAssignmentGroupId: settings.defaultAssignmentGroupId ?? null,
    defaultPriority: settings.defaultPriority,
    createWorkOrderFromUnknownSender: settings.createWorkOrderFromUnknownSender,
    subjectParsingRules: settings.subjectParsingRules ?? null,
    allowedSenderDomains: settings.allowedSenderDomains ?? null,
  }
}

function toNotificationForm(settings: NotificationSettings): NotificationSettings {
  return {
    ...settings,
    id: settings.id,
    tenantId: settings.tenantId,
    emailAlertsEnabled: settings.emailAlertsEnabled,
    smsAlertsEnabled: settings.smsAlertsEnabled,
    workOrderAssignmentAlerts: settings.workOrderAssignmentAlerts,
    licenseExpiryAlerts: settings.licenseExpiryAlerts,
    dailyDigestEnabled: settings.dailyDigestEnabled,
  }
}

function toEmailNotificationForm(settings: EmailNotificationSettings): UpdateEmailNotificationSettingsInput {
  return {
    isEnabled: settings.isEnabled,
    deliveryMode: settings.deliveryMode,
    fromName: settings.fromName,
    fromEmail: settings.fromEmail,
    replyToEmail: settings.replyToEmail ?? null,
    smtpHost: settings.smtpHost,
    smtpPort: settings.smtpPort,
    enableSslTls: settings.enableSslTls,
    secureMode: settings.secureMode,
    smtpUsername: settings.smtpUsername ?? null,
    smtpPasswordSecret: undefined,
    apiEndpoint: settings.apiEndpoint ?? null,
    apiKeySecret: undefined,
    apiProviderName: settings.apiProviderName ?? null,
    timeoutSeconds: settings.timeoutSeconds,
    maxRetries: settings.maxRetries,
  }
}

function saveButton({ onClick, busy }: { onClick: () => void; busy?: boolean }) {
  return (
    <button type="button" className="button-primary" disabled={busy} onClick={onClick}>
      Save
    </button>
  )
}

export function WorkspaceSettingsPage() {
  const { pushToast } = useToast()
  const { data, loading, error, reload } = useAsyncData(async (signal) => settingsService.getDocuments(signal), null as DocumentsSettingsBundle | null, [])
  const [form, setForm] = useState<DocumentsFormState | null>(null)
  const [saving, setSaving] = useState(false)
  const [branch, setBranch] = useState<BranchRecord | null>(null)
  const [rule, setRule] = useState<NumberingRuleRecord | null>(null)
  const [seeding, setSeeding] = useState(false)

  useEffect(() => {
    if (data) setForm(toDocumentsFormState(data))
  }, [data])

  const branches = useAsyncData(async (signal) => branchService.list(signal), [] as BranchRecord[], [])
  const numbering = useAsyncData(async (signal) => settingsService.listNumberingRules(signal), [] as NumberingRuleRecord[], [])

  const profile = form?.profile
  const template = form?.template

  async function saveDocuments() {
    if (!form) return
    setSaving(true)
    try {
      const next = await settingsService.updateDocuments(form)
      setForm(toDocumentsFormState(next))
      pushToast({ title: 'Documents updated', description: 'Company profile and appearance settings were saved.', tone: 'success' })
      await reload()
    } catch (saveError) {
      pushToast({ title: 'Save failed', description: saveError instanceof Error ? saveError.message : 'Unable to save document settings.', tone: 'danger' })
    } finally {
      setSaving(false)
    }
  }

  async function seedNumberingDefaults() {
    setSeeding(true)
    try {
      await settingsService.seedDefaultNumberingRules()
      pushToast({ title: 'Numbering defaults seeded', description: 'Default numbering rules are now available.', tone: 'success' })
      await numbering.reload()
    } catch (seedError) {
      pushToast({ title: 'Seed failed', description: seedError instanceof Error ? seedError.message : 'Unable to seed numbering defaults.', tone: 'danger' })
    } finally {
      setSeeding(false)
    }
  }

  if (loading && !data) {
    return <SettingsPageFrame title="System Settings" description="Workspace profile, branches, and numbering rules."><LoadingState label="Loading system settings" /></SettingsPageFrame>
  }

  if (error) {
    return <SettingsPageFrame title="System Settings" description="Workspace profile, branches, and numbering rules."><ErrorState title="Unable to load system settings" description={error} /></SettingsPageFrame>
  }

  const branchRows = branches.data ?? []
  const ruleRows = numbering.data ?? []

  return (
    <SettingsPageFrame
      title="System Settings"
      description="Company identity, branch records, and numbering rules are the real system-level configuration points."
      actions={saveButton({ onClick: saveDocuments, busy: saving })}
    >
      <ShellSection
        title="Company profile"
        description="This is the actual tenant profile data stored by the backend."
        actions={saveButton({ onClick: saveDocuments, busy: saving })}
      >
        {!form ? (
          <LoadingState label="Preparing company profile" />
        ) : (
          <div className="grid gap-3 md:grid-cols-2">
            <Field label="Company name"><input className={inputClass()} value={profile?.companyName ?? ''} onChange={(event) => setForm((current) => current ? { ...current, profile: { ...current.profile, companyName: event.target.value } } : current)} /></Field>
            <Field label="Legal name"><input className={inputClass()} value={profile?.legalName ?? ''} onChange={(event) => setForm((current) => current ? { ...current, profile: { ...current.profile, legalName: event.target.value } } : current)} /></Field>
            <Field label="Trading name"><input className={inputClass()} value={profile?.tradingName ?? ''} onChange={(event) => setForm((current) => current ? { ...current, profile: { ...current.profile, tradingName: event.target.value } } : current)} /></Field>
            <Field label="Email"><input className={inputClass()} value={profile?.email ?? ''} onChange={(event) => setForm((current) => current ? { ...current, profile: { ...current.profile, email: event.target.value } } : current)} /></Field>
            <Field label="Phone"><input className={inputClass()} value={profile?.phone ?? ''} onChange={(event) => setForm((current) => current ? { ...current, profile: { ...current.profile, phone: event.target.value } } : current)} /></Field>
            <Field label="Country"><input className={inputClass()} value={profile?.country ?? ''} onChange={(event) => setForm((current) => current ? { ...current, profile: { ...current.profile, country: event.target.value } } : current)} /></Field>
            <Field label="VAT number"><input className={inputClass()} value={profile?.vatNumber ?? ''} onChange={(event) => setForm((current) => current ? { ...current, profile: { ...current.profile, vatNumber: event.target.value } } : current)} /></Field>
            <Field label="Registration number"><input className={inputClass()} value={profile?.registrationNumber ?? ''} onChange={(event) => setForm((current) => current ? { ...current, profile: { ...current.profile, registrationNumber: event.target.value } } : current)} /></Field>
            <Field label="Website"><input className={inputClass()} value={profile?.website ?? ''} onChange={(event) => setForm((current) => current ? { ...current, profile: { ...current.profile, website: event.target.value } } : current)} /></Field>
            <Field label="Physical address" description="Plain tenant profile fields only."><input className={inputClass()} value={profile?.physicalAddress ?? ''} onChange={(event) => setForm((current) => current ? { ...current, profile: { ...current.profile, physicalAddress: event.target.value } } : current)} /></Field>
            <Field label="Postal address"><input className={inputClass()} value={profile?.postalAddress ?? ''} onChange={(event) => setForm((current) => current ? { ...current, profile: { ...current.profile, postalAddress: event.target.value } } : current)} /></Field>
            <Field label="Logo URL"><input className={inputClass()} value={profile?.logoUrl ?? ''} onChange={(event) => setForm((current) => current ? { ...current, profile: { ...current.profile, logoUrl: event.target.value } } : current)} /></Field>
            <Field label="Primary color"><input className={inputClass()} value={profile?.primaryColor ?? ''} onChange={(event) => setForm((current) => current ? { ...current, profile: { ...current.profile, primaryColor: event.target.value } } : current)} /></Field>
            <Field label="Secondary color"><input className={inputClass()} value={profile?.secondaryColor ?? ''} onChange={(event) => setForm((current) => current ? { ...current, profile: { ...current.profile, secondaryColor: event.target.value } } : current)} /></Field>
            <label className="flex items-center gap-2 text-sm text-app">
              <input type="checkbox" checked={Boolean(profile?.showPoweredByEcosys)} onChange={(event) => setForm((current) => current ? { ...current, profile: { ...current.profile, showPoweredByEcosys: event.target.checked } } : current)} />
              Show powered by Ecosys
            </label>
          </div>
        )}
      </ShellSection>

      <ShellSection title="Branches" description="Branch records stay in a table, with details available from the drawer.">
        <DataTable
          rows={branchRows}
          rowKey={(row) => row.id}
          pageSize={8}
          onRowClick={(row) => setBranch(row)}
          columns={[
            { key: 'name', header: 'Branch', cell: (row) => row.name },
            { key: 'code', header: 'Code', cell: (row) => row.code },
            { key: 'location', header: 'Location', cell: (row) => row.location || '—' },
            { key: 'status', header: 'Status', cell: (row) => <Badge tone={row.isActive ? 'success' : 'neutral'}>{row.isActive ? 'Active' : 'Inactive'}</Badge> },
          ]}
          emptyTitle="No branches yet"
          emptyDescription="Branch records will appear here once the workspace has locations."
        />
      </ShellSection>

      <ShellSection title="Numbering rules" description="Document numbering is live and can be managed from this table." actions={<button type="button" className="button-secondary" onClick={() => void seedNumberingDefaults()} disabled={seeding}>{seeding ? 'Seeding...' : 'Seed defaults'}</button>}>
        <DataTable
          rows={ruleRows}
          rowKey={(row) => row.id}
          pageSize={8}
          onRowClick={(row) => setRule(row)}
          columns={[
            { key: 'type', header: 'Document type', cell: (row) => row.documentType },
            { key: 'prefix', header: 'Prefix', cell: (row) => row.prefix },
            { key: 'next', header: 'Next number', cell: (row) => row.nextNumber },
            { key: 'preview', header: 'Preview', cell: (row) => row.preview },
            { key: 'active', header: 'State', cell: (row) => <Badge tone={row.isActive ? 'success' : 'neutral'}>{row.isActive ? 'Active' : 'Inactive'}</Badge> },
          ]}
          emptyTitle="No numbering rules"
          emptyDescription="Use the seed button to load default numbering rows."
        />
      </ShellSection>

      <ShellSection title="Document appearance" description="These are the real document appearance fields persisted by the backend.">
        {!template ? (
          <LoadingState label="Loading document appearance" />
        ) : (
          <div className="grid gap-3 md:grid-cols-2">
            <Field label="Layout style"><input className={inputClass()} value={template.layoutStyle} onChange={(event) => setForm((current) => current ? { ...current, template: { ...current.template, layoutStyle: event.target.value } } : current)} /></Field>
            <Field label="Header style"><input className={inputClass()} value={template.headerStyle} onChange={(event) => setForm((current) => current ? { ...current, template: { ...current.template, headerStyle: event.target.value } } : current)} /></Field>
            <Field label="Footer style"><input className={inputClass()} value={template.footerStyle} onChange={(event) => setForm((current) => current ? { ...current, template: { ...current.template, footerStyle: event.target.value } } : current)} /></Field>
            <Field label="Font style"><input className={inputClass()} value={template.fontFamily} onChange={(event) => setForm((current) => current ? { ...current, template: { ...current.template, fontFamily: event.target.value } } : current)} /></Field>
            <Field label="Layout density"><input className={inputClass()} value={template.pageDensity} onChange={(event) => setForm((current) => current ? { ...current, template: { ...current.template, pageDensity: event.target.value } } : current)} /></Field>
            <Field label="Default currency"><input className={inputClass()} value={template.defaultCurrency} onChange={(event) => setForm((current) => current ? { ...current, template: { ...current.template, defaultCurrency: event.target.value } } : current)} /></Field>
            <Field label="Terms and conditions"><textarea className={`${inputClass()} min-h-24`} value={template.defaultTermsText ?? ''} onChange={(event) => setForm((current) => current ? { ...current, template: { ...current.template, defaultTermsText: event.target.value || null } } : current)} /></Field>
            <Field label="Signature blocks"><textarea className={`${inputClass()} min-h-24`} value={template.showSignatureBlock ? 'Enabled' : 'Disabled'} readOnly /></Field>
            <Field label="Footer text"><textarea className={`${inputClass()} min-h-24`} value={template.footerText ?? ''} onChange={(event) => setForm((current) => current ? { ...current, template: { ...current.template, footerText: event.target.value || null } } : current)} /></Field>
            <Field label="Report disclaimer"><textarea className={`${inputClass()} min-h-24`} value={template.serviceReportDisclaimer ?? ''} onChange={(event) => setForm((current) => current ? { ...current, template: { ...current.template, serviceReportDisclaimer: event.target.value || null } } : current)} /></Field>
          </div>
        )}
      </ShellSection>

      <Drawer open={Boolean(branch)} title="Branch details" description="Read-only branch record." onClose={() => setBranch(null)}>
        {branch ? (
          <div className="space-y-3 text-sm">
            <Detail label="Name" value={branch.name} />
            <Detail label="Code" value={branch.code} />
            <Detail label="Location" value={branch.location || '—'} />
            <Detail label="Address" value={branch.address || '—'} />
            <Detail label="Contact person" value={branch.contactPerson || '—'} />
            <Detail label="Phone" value={branch.phone || '—'} />
            <Detail label="Email" value={branch.email || '—'} />
            <Detail label="Status" value={branch.isActive ? 'Active' : 'Inactive'} />
          </div>
        ) : null}
      </Drawer>

      <Drawer open={Boolean(rule)} title="Numbering rule details" description="Read-only numbering record." onClose={() => setRule(null)}>
        {rule ? (
          <div className="space-y-3 text-sm">
            <Detail label="Document type" value={rule.documentType} />
            <Detail label="Prefix" value={rule.prefix} />
            <Detail label="Suffix" value={rule.suffix || '—'} />
            <Detail label="Next number" value={String(rule.nextNumber)} />
            <Detail label="Padding length" value={String(rule.paddingLength)} />
            <Detail label="Reset period" value={rule.resetPeriod} />
            <Detail label="Preview" value={rule.preview} />
            <Detail label="Status" value={rule.isActive ? 'Active' : 'Inactive'} />
          </div>
        ) : null}
      </Drawer>
    </SettingsPageFrame>
  )
}

export function AccessSettingsPage() {
  const { data, loading, error } = useAsyncData(async (signal) => ({
    users: await userService.list(signal),
    groups: await settingsService.listAssignmentGroups(signal),
    branches: await branchService.list(signal),
  }), { users: [] as UserRecord[], groups: [] as AssignmentGroupRecord[], branches: [] as BranchRecord[] }, [])
  const [selectedUser, setSelectedUser] = useState<UserRecord | null>(null)
  const [selectedGroup, setSelectedGroup] = useState<AssignmentGroupRecord | null>(null)

  const roles = useMemo(() => {
    const counts = new Map<string, number>()
    for (const user of data.users) {
      counts.set(user.role, (counts.get(user.role) ?? 0) + 1)
    }
    return ['SuperAdmin', 'Admin', 'User', 'Technician'].map((role) => ({
      role,
      users: counts.get(role) ?? 0,
      permissions: role === 'SuperAdmin' ? 'Platform access' : role === 'Admin' ? 'Operations access' : role === 'User' ? 'Operational access' : 'Field access',
    }))
  }, [data.users])

  if (loading && !data.users.length) {
    return <SettingsPageFrame title="User & Access Management" description="Users, roles, permissions, and assignment groups."><LoadingState label="Loading access data" /></SettingsPageFrame>
  }

  if (error) {
    return <SettingsPageFrame title="User & Access Management" description="Users, roles, permissions, and assignment groups."><ErrorState title="Unable to load access settings" description={error} /></SettingsPageFrame>
  }

  return (
    <SettingsPageFrame title="User & Access Management" description="Users, roles, permissions, and assignment groups are shown as records rather than decorative cards.">
      <ShellSection title="Users" description="User records are table-first. Create and edit actions remain in the users module.">
        <DataTable
          rows={data.users}
          rowKey={(row) => row.id}
          pageSize={10}
          onRowClick={(row) => setSelectedUser(row)}
          columns={[
            { key: 'name', header: 'User', cell: (row) => row.fullName },
            { key: 'email', header: 'Email', cell: (row) => row.email },
            { key: 'role', header: 'Role', cell: (row) => row.role },
            { key: 'branch', header: 'Branch', cell: (row) => row.defaultBranchName || '—' },
            { key: 'status', header: 'Status', cell: (row) => <Badge tone={row.isActive ? 'success' : 'neutral'}>{row.isActive ? 'Active' : 'Inactive'}</Badge> },
          ]}
          emptyTitle="No users available"
          emptyDescription="Users will appear here once the tenant has active accounts."
        />
      </ShellSection>

      <ShellSection title="Roles" description="Roles are shown as a compact record table because no separate role endpoint is exposed.">
        <DataTable
          rows={roles}
          rowKey={(row) => row.role}
          pageSize={10}
          columns={[
            { key: 'role', header: 'Role', cell: (row) => row.role },
            { key: 'users', header: 'Users', cell: (row) => row.users },
            { key: 'permissions', header: 'Permissions', cell: (row) => row.permissions },
          ]}
          emptyTitle="No roles found"
          emptyDescription="Roles are derived from the current user records."
        />
      </ShellSection>

      <ShellSection title="Assignment groups" description="Routing groups remain record-based and open in a drawer for inspection.">
        <DataTable
          rows={data.groups}
          rowKey={(row) => row.id}
          pageSize={10}
          onRowClick={(row) => setSelectedGroup(row)}
          columns={[
            { key: 'name', header: 'Group', cell: (row) => row.name },
            { key: 'branch', header: 'Branch', cell: (row) => row.branchName || '—' },
            { key: 'skill', header: 'Skill area', cell: (row) => row.skillArea || '—' },
            { key: 'members', header: 'Members', cell: (row) => row.members?.length ?? row.technicianIds.length },
            { key: 'status', header: 'Status', cell: (row) => <Badge tone={row.isActive ? 'success' : 'neutral'}>{row.isActive ? 'Active' : 'Inactive'}</Badge> },
          ]}
          emptyTitle="No assignment groups"
          emptyDescription="Assignment groups will appear once routing teams are created."
        />
      </ShellSection>

      <Drawer open={Boolean(selectedUser)} title="User details" description="Read-only user record." onClose={() => setSelectedUser(null)}>
        {selectedUser ? (
          <div className="space-y-3 text-sm">
            <Detail label="Name" value={selectedUser.fullName} />
            <Detail label="Email" value={selectedUser.email} />
            <Detail label="Role" value={selectedUser.role} />
            <Detail label="Job title" value={selectedUser.jobTitle || '—'} />
            <Detail label="Department" value={selectedUser.department || '—'} />
            <Detail label="Status" value={selectedUser.isActive ? 'Active' : 'Inactive'} />
            <Detail label="Branch access" value={selectedUser.hasAllBranchAccess ? 'All branches' : selectedUser.branchIds.join(', ') || 'Limited'} />
          </div>
        ) : null}
      </Drawer>

      <Drawer open={Boolean(selectedGroup)} title="Assignment group details" description="Read-only assignment group record." onClose={() => setSelectedGroup(null)}>
        {selectedGroup ? (
          <div className="space-y-3 text-sm">
            <Detail label="Name" value={selectedGroup.name} />
            <Detail label="Description" value={selectedGroup.description || '—'} />
            <Detail label="Branch" value={selectedGroup.branchName || '—'} />
            <Detail label="Skill area" value={selectedGroup.skillArea || '—'} />
            <Detail label="Members" value={String(selectedGroup.members?.length ?? selectedGroup.technicianIds.length)} />
            <Detail label="Status" value={selectedGroup.isActive ? 'Active' : 'Inactive'} />
          </div>
        ) : null}
      </Drawer>
    </SettingsPageFrame>
  )
}

export function ServiceSettingsPage() {
  return (
    <SettingsPageFrame title="Service Management" description="Work order settings are not wired yet.">
      <ShellSection title="Work order settings are not wired yet." description="Do not map this page to unrelated security or completion endpoints.">
        <div className="space-y-3">
          {[
            'Work order types',
            'Required technician signature',
            'Required customer signature',
            'Evidence required',
            'Multiday updates',
            'On-hold reasons',
            'Completion checklist',
          ].map((item) => (
            <SectionNote key={item} title={item} detail="Not configured yet." />
          ))}
        </div>
      </ShellSection>
    </SettingsPageFrame>
  )
}

export function DispatchSettingsPage() {
  return (
    <SettingsPageFrame title="Dispatch Management" description="Dispatch settings are not wired yet.">
      <ShellSection title="Dispatch settings are not wired yet." description="Avoid fake toggles until the backend exposes real dispatch configuration.">
        <div className="space-y-3">
          {[
            'Working hours',
            'Dispatch rules',
            'Technician availability',
            'Job duration',
            'Buffer rules',
            'Service area rules',
          ].map((item) => (
            <SectionNote key={item} title={item} detail="Not configured yet." />
          ))}
        </div>
      </ShellSection>
    </SettingsPageFrame>
  )
}

export function DocumentsSettingsPage() {
  const { pushToast } = useToast()
  const { data, loading, error, reload } = useAsyncData(async (signal) => ({
    documents: await settingsService.getDocuments(signal),
    templates: await settingsService.listEmailTemplates(signal).catch(() => [] as EmailTemplateRecord[]),
  }), { documents: null as DocumentsSettingsBundle | null, templates: [] as EmailTemplateRecord[] }, [])
  const [form, setForm] = useState<DocumentsFormState | null>(null)
  const [saving, setSaving] = useState(false)

  useEffect(() => {
    if (data.documents) setForm(toDocumentsFormState(data.documents))
  }, [data.documents])

  async function save() {
    if (!form) return
    setSaving(true)
    try {
      const next = await settingsService.updateDocuments(form)
      setForm(toDocumentsFormState(next))
      pushToast({ title: 'Document settings saved', description: 'Document appearance and profile fields were updated.', tone: 'success' })
      await reload()
    } catch (saveError) {
      pushToast({ title: 'Save failed', description: saveError instanceof Error ? saveError.message : 'Unable to save document settings.', tone: 'danger' })
    } finally {
      setSaving(false)
    }
  }

  if (loading && !data.documents) {
    return <SettingsPageFrame title="Document Management" description="Templates, appearance, branding, and document defaults."><LoadingState label="Loading documents" /></SettingsPageFrame>
  }

  if (error) {
    return <SettingsPageFrame title="Document Management" description="Templates, appearance, branding, and document defaults."><ErrorState title="Unable to load documents" description={error} /></SettingsPageFrame>
  }

  const docs = data.documents
  const template = form?.template

  return (
    <SettingsPageFrame
      title="Document Management"
      description="Only the backend-backed document fields are editable. Missing template endpoints are marked clearly."
      actions={saveButton({ onClick: save, busy: saving })}
    >
      <ShellSection title="Document appearance" description="Real profile and template fields are editable here." actions={saveButton({ onClick: save, busy: saving })}>
        {!form || !docs ? (
          <LoadingState label="Loading document appearance" />
        ) : (
          <div className="grid gap-3 md:grid-cols-2">
            <Field label="Company name"><input className={inputClass()} value={form.profile.companyName} onChange={(event) => setForm((current) => current ? { ...current, profile: { ...current.profile, companyName: event.target.value } } : current)} /></Field>
            <Field label="Logo URL"><input className={inputClass()} value={form.profile.logoUrl ?? ''} onChange={(event) => setForm((current) => current ? { ...current, profile: { ...current.profile, logoUrl: event.target.value || null } } : current)} /></Field>
            <Field label="Primary color"><input className={inputClass()} value={form.profile.primaryColor} onChange={(event) => setForm((current) => current ? { ...current, profile: { ...current.profile, primaryColor: event.target.value } } : current)} /></Field>
            <Field label="Secondary color"><input className={inputClass()} value={form.profile.secondaryColor} onChange={(event) => setForm((current) => current ? { ...current, profile: { ...current.profile, secondaryColor: event.target.value } } : current)} /></Field>
            <Field label="Font style"><input className={inputClass()} value={template?.fontFamily ?? ''} onChange={(event) => setForm((current) => current ? { ...current, template: { ...current.template, fontFamily: event.target.value } } : current)} /></Field>
            <Field label="Layout density"><input className={inputClass()} value={template?.pageDensity ?? ''} onChange={(event) => setForm((current) => current ? { ...current, template: { ...current.template, pageDensity: event.target.value } } : current)} /></Field>
            <Field label="Header style"><input className={inputClass()} value={template?.headerStyle ?? ''} onChange={(event) => setForm((current) => current ? { ...current, template: { ...current.template, headerStyle: event.target.value } } : current)} /></Field>
            <Field label="Footer style"><input className={inputClass()} value={template?.footerStyle ?? ''} onChange={(event) => setForm((current) => current ? { ...current, template: { ...current.template, footerStyle: event.target.value } } : current)} /></Field>
            <Field label="Terms and conditions"><textarea className={`${inputClass()} min-h-24`} value={template?.defaultTermsText ?? ''} onChange={(event) => setForm((current) => current ? { ...current, template: { ...current.template, defaultTermsText: event.target.value || null } } : current)} /></Field>
            <Field label="Signature blocks"><textarea className={`${inputClass()} min-h-24`} value={template?.showSignatureBlock ? 'Enabled' : 'Disabled'} readOnly /></Field>
          </div>
        )}
      </ShellSection>

      <ShellSection title="Document templates" description="Only real templates are shown. Missing template endpoints stay visible as not configured yet.">
        <DataTable
          rows={[
            { name: 'Quote template', status: 'Not configured yet', note: 'No dedicated template endpoint is exposed for quote documents.' },
            { name: 'Invoice template', status: 'Not configured yet', note: 'No dedicated template endpoint is exposed for invoice documents.' },
            { name: 'Receipt template', status: 'Not configured yet', note: 'No dedicated template endpoint is exposed for receipt documents.' },
            { name: 'Service report template', status: 'Configured', note: 'Backed by the saved report template configuration.' },
          ]}
          rowKey={(row) => row.name}
          pageSize={10}
          columns={[
            { key: 'name', header: 'Template', cell: (row) => row.name },
            { key: 'status', header: 'Status', cell: (row) => <Badge tone={row.status === 'Configured' ? 'success' : 'neutral'}>{row.status}</Badge> },
            { key: 'note', header: 'Notes', cell: (row) => row.note },
          ]}
          emptyTitle="No templates available"
          emptyDescription="Template records will appear once the backend exposes them."
        />
      </ShellSection>
    </SettingsPageFrame>
  )
}

export function FinanceSettingsPage() {
  return (
    <SettingsPageFrame title="Finance Settings" description="Finance settings are not wired yet.">
      <ShellSection title="Finance settings are not wired yet." description="Do not show hardcoded currency, tax, or payment matching controls until a real backend endpoint exists.">
        <div className="space-y-3">
          {[
            'Currency',
            'Tax / VAT',
            'Quote rules',
            'Invoice rules',
            'Receipt rules',
            'Payment matching',
            'M-Pesa confirmation',
            'Bank payment confirmation',
            'Finance module availability',
          ].map((item) => (
            <SectionNote key={item} title={item} detail="Not configured yet." />
          ))}
        </div>
      </ShellSection>
    </SettingsPageFrame>
  )
}

export function IntakeSettingsPage() {
  const { pushToast } = useToast()
  const { data, loading, error, reload } = useAsyncData(async (signal) => ({
    intake: await settingsService.getEmailIntake(signal),
    groups: await settingsService.listAssignmentGroups(signal),
  }), { intake: null as EmailIntakeSettings | null, groups: [] as AssignmentGroupRecord[] }, [])
  const [form, setForm] = useState<UpdateEmailIntakeSettingsInput | null>(null)
  const [testing, setTesting] = useState(false)
  const [saving, setSaving] = useState(false)

  useEffect(() => {
    if (data.intake) setForm(toEmailIntakeForm(data.intake))
  }, [data.intake])

  async function save() {
    if (!form) return
    setSaving(true)
    try {
      const next = await settingsService.updateEmailIntake(form)
      setForm(toEmailIntakeForm(next))
      pushToast({ title: 'Intake saved', description: 'Email intake settings were updated.', tone: 'success' })
      await reload()
    } catch (saveError) {
      pushToast({ title: 'Save failed', description: saveError instanceof Error ? saveError.message : 'Unable to save intake settings.', tone: 'danger' })
    } finally {
      setSaving(false)
    }
  }

  async function testConnection() {
    if (!form) return
    setTesting(true)
    try {
      await settingsService.testEmailIntakeConnection(form.host, form.port)
      pushToast({ title: 'Connection test sent', description: 'The intake connection check completed.', tone: 'success' })
      await reload()
    } catch (testError) {
      pushToast({ title: 'Test failed', description: testError instanceof Error ? testError.message : 'Unable to test the intake connection.', tone: 'danger' })
    } finally {
      setTesting(false)
    }
  }

  if (loading && !data.intake) {
    return <SettingsPageFrame title="Intake Management" description="Email intake, monitoring intake, and matching rules."><LoadingState label="Loading intake settings" /></SettingsPageFrame>
  }

  if (error) {
    return <SettingsPageFrame title="Intake Management" description="Email intake, monitoring intake, and matching rules."><ErrorState title="Unable to load intake settings" description={error} /></SettingsPageFrame>
  }

  const intake = data.intake

  return (
    <SettingsPageFrame title="Intake Management" description="Email intake is wired and can be tested from here." actions={saveButton({ onClick: save, busy: saving })}>
      <ShellSection title="Email intake" description="This is the live intake form backed by the backend API." actions={<button type="button" className="button-secondary" onClick={() => void testConnection()} disabled={testing || !form}>{testing ? 'Testing...' : 'Test connection'}</button>}>
        {!form || !intake ? (
          <LoadingState label="Loading email intake" />
        ) : (
          <div className="grid gap-3 md:grid-cols-2">
            <label className="flex items-center gap-2 text-sm text-app md:col-span-2">
              <input type="checkbox" checked={form.isEnabled} onChange={(event) => setForm((current) => current ? { ...current, isEnabled: event.target.checked } : current)} />
              Enable email intake
            </label>
            <Field label="Mailbox provider"><input className={inputClass()} value={form.mailboxProvider} onChange={(event) => setForm((current) => current ? { ...current, mailboxProvider: event.target.value } : current)} /></Field>
            <Field label="Intake email address"><input className={inputClass()} value={form.intakeEmailAddress ?? ''} onChange={(event) => setForm((current) => current ? { ...current, intakeEmailAddress: event.target.value || null } : current)} /></Field>
            <Field label="Host"><input className={inputClass()} value={form.host} onChange={(event) => setForm((current) => current ? { ...current, host: event.target.value } : current)} /></Field>
            <Field label="Port"><input className={inputClass()} type="number" value={form.port} onChange={(event) => setForm((current) => current ? { ...current, port: Number(event.target.value) || 0 } : current)} /></Field>
            <Field label="Username"><input className={inputClass()} value={form.username ?? ''} onChange={(event) => setForm((current) => current ? { ...current, username: event.target.value || null } : current)} /></Field>
            <Field label="Default priority"><input className={inputClass()} value={form.defaultPriority} onChange={(event) => setForm((current) => current ? { ...current, defaultPriority: event.target.value } : current)} /></Field>
            <Field label="Allowed sender domains"><textarea className={`${inputClass()} min-h-24`} value={form.allowedSenderDomains ?? ''} onChange={(event) => setForm((current) => current ? { ...current, allowedSenderDomains: event.target.value || null } : current)} /></Field>
            <Field label="Subject parsing rules"><textarea className={`${inputClass()} min-h-24`} value={form.subjectParsingRules ?? ''} onChange={(event) => setForm((current) => current ? { ...current, subjectParsingRules: event.target.value || null } : current)} /></Field>
            <label className="flex items-center gap-2 text-sm text-app">
              <input type="checkbox" checked={form.useSsl} onChange={(event) => setForm((current) => current ? { ...current, useSsl: event.target.checked } : current)} />
              Use SSL
            </label>
            <label className="flex items-center gap-2 text-sm text-app">
              <input type="checkbox" checked={form.createWorkOrderFromUnknownSender} onChange={(event) => setForm((current) => current ? { ...current, createWorkOrderFromUnknownSender: event.target.checked } : current)} />
              Create work order from unknown sender
            </label>
          </div>
        )}
      </ShellSection>

      <ShellSection title="Routing defaults" description="Assignment group and branch defaults are shown as records, not fake toggles.">
        <DataTable
          rows={data.groups}
          rowKey={(row) => row.id}
          pageSize={8}
          columns={[
            { key: 'name', header: 'Group', cell: (row) => row.name },
            { key: 'branch', header: 'Branch', cell: (row) => row.branchName || '—' },
            { key: 'active', header: 'Active', cell: (row) => <Badge tone={row.isActive ? 'success' : 'neutral'}>{row.isActive ? 'Yes' : 'No'}</Badge> },
          ]}
          emptyTitle="No routing groups"
          emptyDescription="Assignment groups will appear here once routing is configured."
        />
      </ShellSection>
    </SettingsPageFrame>
  )
}

export function NotificationSettingsPage() {
  const { pushToast } = useToast()
  const { data, loading, error, reload } = useAsyncData(async (signal) => ({
    notifications: await settingsService.getNotifications(signal),
    emailNotifications: await settingsService.getEmailNotifications(signal),
    templates: await settingsService.listEmailTemplates(signal).catch(() => [] as EmailTemplateRecord[]),
  }), { notifications: null as NotificationSettings | null, emailNotifications: null as EmailNotificationSettings | null, templates: [] as EmailTemplateRecord[] }, [])
  const [notificationForm, setNotificationForm] = useState<NotificationSettings | null>(null)
  const [emailForm, setEmailForm] = useState<UpdateEmailNotificationSettingsInput | null>(null)
  const [savingNotifications, setSavingNotifications] = useState(false)
  const [savingEmail, setSavingEmail] = useState(false)

  useEffect(() => {
    if (data.notifications) setNotificationForm(toNotificationForm(data.notifications))
    if (data.emailNotifications) setEmailForm(toEmailNotificationForm(data.emailNotifications))
  }, [data.emailNotifications, data.notifications])

  async function saveNotifications() {
    if (!notificationForm) return
    setSavingNotifications(true)
    try {
      const next = await settingsService.updateNotifications(notificationForm)
      setNotificationForm(toNotificationForm(next))
      pushToast({ title: 'Notification rules saved', description: 'Alert toggles were updated.', tone: 'success' })
      await reload()
    } catch (saveError) {
      pushToast({ title: 'Save failed', description: saveError instanceof Error ? saveError.message : 'Unable to save notification rules.', tone: 'danger' })
    } finally {
      setSavingNotifications(false)
    }
  }

  async function saveEmailNotifications() {
    if (!emailForm) return
    setSavingEmail(true)
    try {
      const next = await settingsService.updateEmailNotifications(emailForm)
      setEmailForm(toEmailNotificationForm(next))
      pushToast({ title: 'Email delivery saved', description: 'Outbound email settings were updated.', tone: 'success' })
      await reload()
    } catch (saveError) {
      pushToast({ title: 'Save failed', description: saveError instanceof Error ? saveError.message : 'Unable to save email delivery settings.', tone: 'danger' })
    } finally {
      setSavingEmail(false)
    }
  }

  if (loading && !data.notifications) {
    return <SettingsPageFrame title="Notification Management" description="Work order alerts, SLA alerts, and delivery rules."><LoadingState label="Loading notifications" /></SettingsPageFrame>
  }

  if (error) {
    return <SettingsPageFrame title="Notification Management" description="Work order alerts, SLA alerts, and delivery rules."><ErrorState title="Unable to load notifications" description={error} /></SettingsPageFrame>
  }

  return (
    <SettingsPageFrame title="Notification Management" description="Notification settings use real tenant APIs and template records." actions={saveButton({ onClick: saveNotifications, busy: savingNotifications || savingEmail })}>
      <ShellSection title="Work order alerts" description="Operational alert toggles are stored in the notification settings endpoint." actions={saveButton({ onClick: saveNotifications, busy: savingNotifications })}>
        {!notificationForm ? <LoadingState label="Loading notification rules" /> : (
          <div className="grid gap-3 md:grid-cols-2">
            <label className="flex items-center gap-2 text-sm text-app"><input type="checkbox" checked={notificationForm.emailAlertsEnabled} onChange={(event) => setNotificationForm((current) => current ? { ...current, emailAlertsEnabled: event.target.checked } : current)} /> Email alerts enabled</label>
            <label className="flex items-center gap-2 text-sm text-app"><input type="checkbox" checked={notificationForm.smsAlertsEnabled} onChange={(event) => setNotificationForm((current) => current ? { ...current, smsAlertsEnabled: event.target.checked } : current)} /> SMS alerts enabled</label>
            <label className="flex items-center gap-2 text-sm text-app"><input type="checkbox" checked={notificationForm.workOrderAssignmentAlerts} onChange={(event) => setNotificationForm((current) => current ? { ...current, workOrderAssignmentAlerts: event.target.checked } : current)} /> Assignment alerts</label>
            <label className="flex items-center gap-2 text-sm text-app"><input type="checkbox" checked={notificationForm.licenseExpiryAlerts} onChange={(event) => setNotificationForm((current) => current ? { ...current, licenseExpiryAlerts: event.target.checked } : current)} /> SLA / licence alerts</label>
            <label className="flex items-center gap-2 text-sm text-app"><input type="checkbox" checked={notificationForm.dailyDigestEnabled} onChange={(event) => setNotificationForm((current) => current ? { ...current, dailyDigestEnabled: event.target.checked } : current)} /> Daily digest</label>
          </div>
        )}
      </ShellSection>

      <ShellSection title="Email delivery" description="Real delivery settings are editable here, with the backend doing the save." actions={saveButton({ onClick: saveEmailNotifications, busy: savingEmail })}>
        {!emailForm ? <LoadingState label="Loading email delivery" /> : (
          <div className="grid gap-3 md:grid-cols-2">
            <label className="flex items-center gap-2 text-sm text-app md:col-span-2"><input type="checkbox" checked={emailForm.isEnabled} onChange={(event) => setEmailForm((current) => current ? { ...current, isEnabled: event.target.checked } : current)} /> Email delivery enabled</label>
            <Field label="Delivery mode"><input className={inputClass()} value={emailForm.deliveryMode} onChange={(event) => setEmailForm((current) => current ? { ...current, deliveryMode: event.target.value as UpdateEmailNotificationSettingsInput['deliveryMode'] } : current)} /></Field>
            <Field label="From name"><input className={inputClass()} value={emailForm.fromName} onChange={(event) => setEmailForm((current) => current ? { ...current, fromName: event.target.value } : current)} /></Field>
            <Field label="From email"><input className={inputClass()} value={emailForm.fromEmail} onChange={(event) => setEmailForm((current) => current ? { ...current, fromEmail: event.target.value } : current)} /></Field>
            <Field label="Reply-to email"><input className={inputClass()} value={emailForm.replyToEmail ?? ''} onChange={(event) => setEmailForm((current) => current ? { ...current, replyToEmail: event.target.value || null } : current)} /></Field>
            <Field label="SMTP host"><input className={inputClass()} value={emailForm.smtpHost} onChange={(event) => setEmailForm((current) => current ? { ...current, smtpHost: event.target.value } : current)} /></Field>
            <Field label="SMTP port"><input className={inputClass()} type="number" value={emailForm.smtpPort} onChange={(event) => setEmailForm((current) => current ? { ...current, smtpPort: Number(event.target.value) || 0 } : current)} /></Field>
            <Field label="Secure mode"><input className={inputClass()} value={emailForm.secureMode} onChange={(event) => setEmailForm((current) => current ? { ...current, secureMode: event.target.value as UpdateEmailNotificationSettingsInput['secureMode'] } : current)} /></Field>
            <Field label="SMTP username"><input className={inputClass()} value={emailForm.smtpUsername ?? ''} onChange={(event) => setEmailForm((current) => current ? { ...current, smtpUsername: event.target.value || null } : current)} /></Field>
            <Field label="API endpoint"><input className={inputClass()} value={emailForm.apiEndpoint ?? ''} onChange={(event) => setEmailForm((current) => current ? { ...current, apiEndpoint: event.target.value || null } : current)} /></Field>
            <Field label="Provider name"><input className={inputClass()} value={emailForm.apiProviderName ?? ''} onChange={(event) => setEmailForm((current) => current ? { ...current, apiProviderName: event.target.value || null } : current)} /></Field>
            <Field label="Timeout seconds"><input className={inputClass()} type="number" value={emailForm.timeoutSeconds} onChange={(event) => setEmailForm((current) => current ? { ...current, timeoutSeconds: Number(event.target.value) || 0 } : current)} /></Field>
            <Field label="Max retries"><input className={inputClass()} type="number" value={emailForm.maxRetries} onChange={(event) => setEmailForm((current) => current ? { ...current, maxRetries: Number(event.target.value) || 0 } : current)} /></Field>
            <label className="flex items-center gap-2 text-sm text-app"><input type="checkbox" checked={emailForm.enableSslTls} onChange={(event) => setEmailForm((current) => current ? { ...current, enableSslTls: event.target.checked } : current)} /> Enable SSL/TLS</label>
          </div>
        )}
      </ShellSection>

      <ShellSection title="Templates" description="Email templates are records and stay in a table." >
        <DataTable
          rows={data.templates}
          rowKey={(row) => row.eventKey}
          pageSize={8}
          columns={[
            { key: 'eventKey', header: 'Event key', cell: (row) => row.eventKey },
            { key: 'name', header: 'Template', cell: (row) => row.templateName },
            { key: 'enabled', header: 'State', cell: (row) => <Badge tone={row.enabled ? 'success' : 'neutral'}>{row.enabled ? 'Enabled' : 'Disabled'}</Badge> },
          ]}
          emptyTitle="No templates loaded"
          emptyDescription="Email templates will appear here once the backend returns them."
        />
      </ShellSection>
    </SettingsPageFrame>
  )
}

export function IntegrationSettingsPage() {
  const { pushToast } = useToast()
  const { data, loading, error, reload } = useAsyncData(async (signal) => ({
    monitoring: await settingsService.getMonitoring(signal),
    webhooks: await settingsService.listMonitoringWebhooks(signal),
  }), { monitoring: null as Awaited<ReturnType<typeof settingsService.getMonitoring>> | null, webhooks: [] as MonitoringWebhookIntegrationRecord[] }, [])
  const [form, setForm] = useState<Awaited<ReturnType<typeof settingsService.getMonitoring>> | null>(null)
  const [saving, setSaving] = useState(false)
  const [selectedWebhook, setSelectedWebhook] = useState<MonitoringWebhookIntegrationRecord | null>(null)

  useEffect(() => {
    if (data.monitoring) setForm(data.monitoring)
  }, [data.monitoring])

  async function save() {
    if (!form) return
    setSaving(true)
    try {
      const next = await settingsService.updateMonitoring(form)
      setForm(next)
      pushToast({ title: 'Integration settings saved', description: 'Monitoring settings were updated.', tone: 'success' })
      await reload()
    } catch (saveError) {
      pushToast({ title: 'Save failed', description: saveError instanceof Error ? saveError.message : 'Unable to save integration settings.', tone: 'danger' })
    } finally {
      setSaving(false)
    }
  }

  if (loading && !data.monitoring) {
    return <SettingsPageFrame title="Integration Management" description="Email providers, monitoring systems, APIs, and webhooks."><LoadingState label="Loading integrations" /></SettingsPageFrame>
  }

  if (error) {
    return <SettingsPageFrame title="Integration Management" description="Email providers, monitoring systems, APIs, and webhooks."><ErrorState title="Unable to load integrations" description={error} /></SettingsPageFrame>
  }

  return (
    <SettingsPageFrame title="Integration Management" description="Monitoring integrations are real records and can be inspected row by row." actions={saveButton({ onClick: save, busy: saving })}>
      <ShellSection title="Monitoring system" description="This form uses the live monitoring settings endpoint." actions={saveButton({ onClick: save, busy: saving })}>
        {!form ? <LoadingState label="Loading monitoring settings" /> : (
          <div className="grid gap-3 md:grid-cols-2">
            <Field label="Provider name"><input className={inputClass()} value={form.providerName} onChange={(event) => setForm((current) => current ? { ...current, providerName: event.target.value } : current)} /></Field>
            <Field label="Endpoint label"><input className={inputClass()} value={form.endpointLabel ?? ''} onChange={(event) => setForm((current) => current ? { ...current, endpointLabel: event.target.value || null } : current)} /></Field>
            <Field label="Default priority"><input className={inputClass()} value={form.defaultPriority} onChange={(event) => setForm((current) => current ? { ...current, defaultPriority: event.target.value } : current)} /></Field>
            <Field label="Webhook secret"><input className={inputClass()} value={form.webhookSecret ?? ''} onChange={(event) => setForm((current) => current ? { ...current, webhookSecret: event.target.value || null } : current)} /></Field>
            <Field label="Default branch id"><input className={inputClass()} value={form.defaultBranchId ?? ''} onChange={(event) => setForm((current) => current ? { ...current, defaultBranchId: event.target.value || null } : current)} /></Field>
            <label className="flex items-center gap-2 text-sm text-app"><input type="checkbox" checked={form.autoCreateWorkOrders} onChange={(event) => setForm((current) => current ? { ...current, autoCreateWorkOrders: event.target.checked } : current)} /> Auto-create work orders</label>
            <label className="flex items-center gap-2 text-sm text-app"><input type="checkbox" checked={form.isEnabled} onChange={(event) => setForm((current) => current ? { ...current, isEnabled: event.target.checked } : current)} /> Integration enabled</label>
          </div>
        )}
      </ShellSection>

      <ShellSection title="Webhooks" description="Webhook integrations remain a compact table with a detail drawer.">
        <DataTable
          rows={data.webhooks}
          rowKey={(row) => row.id}
          pageSize={8}
          onRowClick={(row) => setSelectedWebhook(row)}
          columns={[
            { key: 'name', header: 'Name', cell: (row) => row.name },
            { key: 'tool', header: 'Tool type', cell: (row) => row.toolType },
            { key: 'priority', header: 'Priority', cell: (row) => row.defaultPriority },
            { key: 'state', header: 'State', cell: (row) => <Badge tone={row.isActive ? 'success' : 'neutral'}>{row.isActive ? 'Active' : 'Inactive'}</Badge> },
          ]}
          emptyTitle="No monitoring webhooks"
          emptyDescription="Webhook integrations will appear once they are created."
        />
      </ShellSection>

      <ShellSection title="API keys" description="If there are no API key endpoints, this area remains clearly marked.">
        {emptySettingCopy('API keys', 'Not configured yet.')}
      </ShellSection>

      <Drawer open={Boolean(selectedWebhook)} title="Webhook details" description="Read-only integration record." onClose={() => setSelectedWebhook(null)}>
        {selectedWebhook ? (
          <div className="space-y-3 text-sm">
            <Detail label="Name" value={selectedWebhook.name} />
            <Detail label="Tool type" value={selectedWebhook.toolType} />
            <Detail label="Endpoint slug" value={selectedWebhook.endpointSlug} />
            <Detail label="Endpoint URL" value={selectedWebhook.endpointUrl || '—'} />
            <Detail label="Priority" value={selectedWebhook.defaultPriority} />
            <Detail label="State" value={selectedWebhook.isActive ? 'Active' : 'Inactive'} />
            <Detail label="Last received" value={selectedWebhook.lastReceivedAt || '—'} />
            <Detail label="Last status" value={selectedWebhook.lastStatus || '—'} />
          </div>
        ) : null}
      </Drawer>
    </SettingsPageFrame>
  )
}

type AuditLogRow = {
  id: string
  userId?: string | null
  action: string
  entityName: string
  entityId: string
  details?: string | null
  createdAt: string
}

export function LogSettingsPage() {
  const { data, loading, error } = useAsyncData(async (signal) => api.get<AuditLogRow[]>('/api/audit-logs', { signal }), [] as AuditLogRow[], [])
  const [query, setQuery] = useState('')
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const [selected, setSelected] = useState<AuditLogRow | null>(null)

  const rows = useMemo(() => {
    const term = query.trim().toLowerCase()
    return (data ?? []).filter((row) => {
      const matchesQuery = !term || `${row.action} ${row.entityName} ${row.entityId} ${row.details || ''}`.toLowerCase().includes(term)
      const rowDate = new Date(row.createdAt)
      const matchesFrom = !from || rowDate >= new Date(from)
      const matchesTo = !to || rowDate <= new Date(`${to}T23:59:59`)
      return matchesQuery && matchesFrom && matchesTo
    })
  }, [data, from, query, to])

  function exportCsv() {
    const csv = [
      ['createdAt', 'action', 'entityName', 'entityId', 'details'].join(','),
      ...rows.map((row) => [row.createdAt, row.action, row.entityName, row.entityId, row.details || ''].map((value) => `"${String(value).replaceAll('"', '""')}"`).join(',')),
    ].join('\n')
    const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' })
    const url = URL.createObjectURL(blob)
    const anchor = document.createElement('a')
    anchor.href = url
    anchor.download = 'audit-logs.csv'
    anchor.click()
    URL.revokeObjectURL(url)
  }

  if (loading && !data.length) {
    return <SettingsPageFrame title="Log Management" description="Audit and system tables for operational review."><LoadingState label="Loading audit logs" /></SettingsPageFrame>
  }

  if (error) {
    return <SettingsPageFrame title="Log Management" description="Audit and system tables for operational review."><ErrorState title="Unable to load logs" description={error} /></SettingsPageFrame>
  }

  return (
    <SettingsPageFrame title="Log Management" description="Logs stay in compact tables with filters, dates, and a detail drawer." actions={<button type="button" className="button-secondary" onClick={exportCsv}><Download className="h-4 w-4" />Export CSV</button>}>
      <ShellSection title="Audit logs" description="Search, filters, date range, and compact table are the only layout patterns used here.">
        <div className="grid gap-3 md:grid-cols-[minmax(0,1.6fr)_12rem_12rem_auto]">
          <label className="flex items-center gap-2 rounded-[14px] border border-[var(--app-divider)] px-3 py-2">
            <Search className="h-4 w-4 text-muted" />
            <input className="w-full bg-transparent text-sm outline-none" placeholder="Search logs" value={query} onChange={(event) => setQuery(event.target.value)} />
          </label>
          <input className="field-input" type="date" value={from} onChange={(event) => setFrom(event.target.value)} />
          <input className="field-input" type="date" value={to} onChange={(event) => setTo(event.target.value)} />
          <button type="button" className="button-secondary" onClick={() => { setQuery(''); setFrom(''); setTo('') }}><CircleSlash2 className="h-4 w-4" />Reset</button>
        </div>

        <DataTable
          rows={rows}
          rowKey={(row) => row.id}
          pageSize={12}
          onRowClick={(row) => setSelected(row)}
          columns={[
            { key: 'created', header: 'Timestamp', cell: (row) => new Date(row.createdAt).toLocaleString() },
            { key: 'action', header: 'Action', cell: (row) => <Badge tone={toneFromAction(row.action)}>{row.action}</Badge> },
            { key: 'entity', header: 'Entity', cell: (row) => `${row.entityName} / ${row.entityId}` },
            { key: 'details', header: 'Details', cell: (row) => row.details || '—' },
          ]}
          emptyTitle="No logs found"
          emptyDescription="Adjust the filters or date range to see matching audit entries."
        />
      </ShellSection>

      <ShellSection title="Other log groups" description="Only wired tables are shown. Unavailable groups are explicitly marked not configured yet.">
        <div className="space-y-3">
          {[
            'System logs',
            'Email intake logs',
            'Monitoring intake logs',
            'Payment logs',
            'Notification logs',
            'Integration logs',
          ].map((item) => (
            <SectionNote key={item} title={item} detail="Not configured yet." />
          ))}
        </div>
      </ShellSection>

      <Drawer open={Boolean(selected)} title="Log details" description="Read-only audit log record." onClose={() => setSelected(null)}>
        {selected ? (
          <div className="space-y-3 text-sm">
            <Detail label="Timestamp" value={new Date(selected.createdAt).toLocaleString()} />
            <Detail label="Action" value={selected.action} />
            <Detail label="Entity" value={selected.entityName} />
            <Detail label="Entity ID" value={selected.entityId} />
            <Detail label="Details" value={selected.details || '—'} />
            <Detail label="User ID" value={selected.userId || '—'} />
          </div>
        ) : null}
      </Drawer>
    </SettingsPageFrame>
  )
}

export function SecuritySettingsPage() {
  const { pushToast } = useToast()
  const { data, loading, error, reload } = useAsyncData(async (signal) => settingsService.getSecurity(signal), null as TenantSecuritySettings | null, [])
  const [form, setForm] = useState<TenantSecuritySettings | null>(null)
  const [saving, setSaving] = useState(false)

  useEffect(() => {
    if (data) setForm(data)
  }, [data])

  async function save() {
    if (!form) return
    setSaving(true)
    try {
      const next = await settingsService.updateSecurity(form)
      setForm(next)
      pushToast({ title: 'Security policy saved', description: 'Password and session policy were updated.', tone: 'success' })
      await reload()
    } catch (saveError) {
      pushToast({ title: 'Save failed', description: saveError instanceof Error ? saveError.message : 'Unable to save security policy.', tone: 'danger' })
    } finally {
      setSaving(false)
    }
  }

  if (loading && !data) {
    return <SettingsPageFrame title="Security & Account" description="Password policy, session policy, exports, and ownership controls."><LoadingState label="Loading security policy" /></SettingsPageFrame>
  }

  if (error) {
    return <SettingsPageFrame title="Security & Account" description="Password policy, session policy, exports, and ownership controls."><ErrorState title="Unable to load security policy" description={error} /></SettingsPageFrame>
  }

  return (
    <SettingsPageFrame title="Security & Account" description="Security policy uses the real settings endpoint and account controls remain explicitly marked.">
      <ShellSection title="Password policy" description="These values are saved to the tenant security policy endpoint." actions={saveButton({ onClick: save, busy: saving })}>
        {!form ? <LoadingState label="Loading policy" /> : (
          <div className="grid gap-3 md:grid-cols-2">
            <Field label="Minimum password length"><input className={inputClass()} type="number" value={form.minPasswordLength} onChange={(event) => setForm((current) => current ? { ...current, minPasswordLength: Number(event.target.value) || 0 } : current)} /></Field>
            <Field label="Password rotation days"><input className={inputClass()} type="number" value={form.passwordRotationDays} onChange={(event) => setForm((current) => current ? { ...current, passwordRotationDays: Number(event.target.value) || 0 } : current)} /></Field>
            <Field label="Session timeout minutes"><input className={inputClass()} type="number" value={form.sessionTimeoutMinutes} onChange={(event) => setForm((current) => current ? { ...current, sessionTimeoutMinutes: Number(event.target.value) || 0 } : current)} /></Field>
            <label className="flex items-center gap-2 text-sm text-app"><input type="checkbox" checked={form.requireUppercase} onChange={(event) => setForm((current) => current ? { ...current, requireUppercase: event.target.checked } : current)} /> Require uppercase</label>
            <label className="flex items-center gap-2 text-sm text-app"><input type="checkbox" checked={form.requireLowercase} onChange={(event) => setForm((current) => current ? { ...current, requireLowercase: event.target.checked } : current)} /> Require lowercase</label>
            <label className="flex items-center gap-2 text-sm text-app"><input type="checkbox" checked={form.requireDigit} onChange={(event) => setForm((current) => current ? { ...current, requireDigit: event.target.checked } : current)} /> Require digit</label>
            <label className="flex items-center gap-2 text-sm text-app"><input type="checkbox" checked={form.requireSpecialCharacter} onChange={(event) => setForm((current) => current ? { ...current, requireSpecialCharacter: event.target.checked } : current)} /> Require special character</label>
            <label className="flex items-center gap-2 text-sm text-app"><input type="checkbox" checked={form.requireMfa} onChange={(event) => setForm((current) => current ? { ...current, requireMfa: event.target.checked } : current)} /> Require MFA</label>
          </div>
        )}
      </ShellSection>

      <ShellSection title="Account management" description="Export, subscription, ownership, and deactivation controls are not wired yet.">
        <div className="space-y-3">
          {[
            'Data export',
            'Subscription',
            'Workspace ownership',
            'Deactivate workspace',
            'Delete workspace',
          ].map((item) => (
            <SectionNote key={item} title={item} detail="Not configured yet." />
          ))}
        </div>
      </ShellSection>
    </SettingsPageFrame>
  )
}

function Detail({ label, value }: { label: string; value: ReactNode }) {
  return (
    <div className="grid gap-1 border-t border-[var(--app-divider)] pt-3 first:border-t-0 first:pt-0 sm:grid-cols-[10rem_minmax(0,1fr)]">
      <p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted">{label}</p>
      <div className="text-sm text-app">{value}</div>
    </div>
  )
}

function toneFromAction(action: string): 'warning' | 'danger' | 'success' | 'neutral' {
  const value = action.toLowerCase()
  if (value.includes('delete') || value.includes('deactivate') || value.includes('disable')) return 'warning'
  if (value.includes('fail') || value.includes('error')) return 'danger'
  if (value.includes('create') || value.includes('update') || value.includes('save') || value.includes('login')) return 'success'
  return 'neutral'
}
