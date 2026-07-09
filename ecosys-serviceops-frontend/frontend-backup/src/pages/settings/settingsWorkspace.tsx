import {
  Bell,
  Building2,
  Eye,
  FileText,
  Gauge,
  Mail,
  PlugZap,
  Receipt,
  ShieldCheck,
  Settings2,
  Users2,
  Workflow,
  type LucideIcon,
} from 'lucide-react'
import type { ReactNode } from 'react'
import { Link, Navigate } from 'react-router-dom'
import { SectionStatusBadge, SettingsRow, SettingsSection } from '../../components/ui/SettingsWorkspace'

export type SettingsWorkspaceGroup = {
  id: string
  label: string
  description: string
  icon: LucideIcon
  path: string
}

type WorkspaceLink = {
  title: string
  description: string
  href: string
  status?: ReactNode
  icon?: LucideIcon
}

type WorkspaceSection = {
  title: string
  description: string
  links: WorkspaceLink[]
}

function link(
  title: string,
  description: string,
  href: string,
  status: ReactNode = <SectionStatusBadge tone="neutral">Open</SectionStatusBadge>,
  icon?: LucideIcon,
): WorkspaceLink {
  return { title, description, href, status, icon }
}

function section(title: string, description: string, links: WorkspaceLink[]): WorkspaceSection {
  return { title, description, links }
}

function GroupPage({
  title,
  description,
  sections,
}: {
  title: string
  description: string
  sections: WorkspaceSection[]
}) {
  return (
    <div className="space-y-4">
      <section className="surface-card border border-[var(--app-divider)] p-4 sm:p-5">
        <p className="eyebrow-accent text-xs font-semibold uppercase tracking-[0.2em]">Settings Control Room</p>
        <h3 className="mt-2 text-2xl font-semibold tracking-tight text-app">{title}</h3>
        <p className="mt-2 max-w-3xl text-sm leading-6 text-muted">{description}</p>
      </section>

      <section className="surface-card divide-y divide-[var(--app-divider)] p-0">
        {sections.map((item) => (
          <SettingsSection key={item.title} title={item.title} description={item.description} className="px-4 py-4">
            <div className="space-y-1">
              {item.links.map((entry) => {
                const Icon = entry.icon
                return (
                  <SettingsRow
                    key={entry.href}
                    href={entry.href}
                    title={entry.title}
                    description={entry.description}
                    icon={Icon ? <Icon className="h-4 w-4" /> : <Settings2 className="h-4 w-4" />}
                    status={entry.status}
                  />
                )
              })}
            </div>
          </SettingsSection>
        ))}
      </section>
    </div>
  )
}

const workspaceSections: WorkspaceSection[] = [
  section(
    'Company identity',
    'Company profile and the operating identity that appears across the workspace.',
    [
      link('Company profile', 'Legal name, support contacts, country, and timezone.', '/settings/company-profile', <SectionStatusBadge tone="success">Primary</SectionStatusBadge>, Building2),
      link('Branches and outlets', 'Branch identity, activation, and outlet details.', '/settings/branches', <SectionStatusBadge tone="neutral">Location</SectionStatusBadge>, Workflow),
    ],
  ),
  section(
    'Workspace defaults',
    'A few cross-workspace settings that shape how records appear and behave.',
    [
      link('Numbering rules', 'Prefixes, counters, and document numbering behavior.', '/settings/numbering-rules', <SectionStatusBadge tone="info">Global</SectionStatusBadge>, Receipt),
      link('Documents hub', 'Branding, layouts, templates, numbering, and preview tools in one control room.', '/settings/documents', <SectionStatusBadge tone="neutral">Customer-facing</SectionStatusBadge>, FileText),
      link('Basic controls', 'Workspace defaults for currency, timezone, and visible branding.', '/settings/company-profile', <SectionStatusBadge tone="neutral">Default</SectionStatusBadge>, Settings2),
    ],
  ),
]

const teamSections: WorkspaceSection[] = [
  section(
    'People and access',
    'Users, roles, permissions, and the operational team structure.',
    [
      link('Users', 'Create users and manage access from the team workspace.', '/team-management?tab=people', <SectionStatusBadge tone="success">Active</SectionStatusBadge>, Users2),
      link('Roles and permissions', 'Review role templates and permission sets.', '/team-management?tab=roles', <SectionStatusBadge tone="success">Active</SectionStatusBadge>, ShieldCheck),
      link('Assignment groups', 'Dispatch groups, team leads, and routing membership.', '/team-management?tab=groups', <SectionStatusBadge tone="success">Active</SectionStatusBadge>, Workflow),
      link('Readiness and certifications', 'Availability, readiness, and certification coverage.', '/team-management?tab=readiness', <SectionStatusBadge tone="success">Active</SectionStatusBadge>, Gauge),
    ],
  ),
]

const operationsSections: WorkspaceSection[] = [
  section(
    'Service behavior',
    'How work orders are created, completed, and tracked through the field lifecycle.',
    [
      link('Work order rules', 'Completion guardrails, signatures, and exception handling.', '/settings/operations-policy', <SectionStatusBadge tone="success">Live</SectionStatusBadge>, Workflow),
      link('SLA and escalation', 'Response and resolution targets by priority.', '/settings/sla-plans', <SectionStatusBadge tone="success">Live</SectionStatusBadge>, Bell),
      link('Checklist templates', 'Preventive maintenance templates and reusable checklists.', '/settings/checklist-templates', <SectionStatusBadge tone="neutral">Library</SectionStatusBadge>, Workflow),
      link('Asset categories', 'Category structure used to classify assets and forms.', '/settings/asset-categories', <SectionStatusBadge tone="neutral">Taxonomy</SectionStatusBadge>, Settings2),
    ],
  ),
]

const documentsSections: WorkspaceSection[] = [
  section(
    'Documents and branding',
    'Keep logo, document identity, layouts, templates, numbering, and preview tools together in one visible control room.',
    [
      link('Open documents hub', 'Branding, document identity, layouts, templates, terms, payments, numbering, and preview.', '/settings/documents', <SectionStatusBadge tone="success">Hub</SectionStatusBadge>, FileText),
      link('Branding and colors', 'Logo, color palette, and the visible tenant identity used across the workspace.', '/settings/branding', <SectionStatusBadge tone="success">Branding</SectionStatusBadge>, Building2),
      link('Service report design', 'PDF report presentation for work order evidence and completion.', '/settings/report-template', <SectionStatusBadge tone="info">Reports</SectionStatusBadge>, FileText),
    ],
  ),
]

const financeSections: WorkspaceSection[] = [
  section(
    'Billing and payments',
    'How finance-facing settings map to quotes, invoices, receipts, and payment tracking.',
    [
      link('Finance workspace', 'Quote, invoice, receipt, and billing configuration.', '/finance/settings', <SectionStatusBadge tone="success">Active</SectionStatusBadge>, Receipt),
      link('Tax and VAT', 'Tax behavior and document display rules.', '/finance/settings', <SectionStatusBadge tone="neutral">Managed in finance</SectionStatusBadge>, Receipt),
      link('Numbering for billing documents', 'Document numbering that applies to commercial documents.', '/settings/numbering-rules', <SectionStatusBadge tone="neutral">Shared</SectionStatusBadge>, FileText),
      link('Payment instructions', 'Bank details, payment notes, and receipt matching support.', '/settings/documents#bank', <SectionStatusBadge tone="neutral">Customer-facing</SectionStatusBadge>, Receipt),
    ],
  ),
]

const automationSections: WorkspaceSection[] = [
  section(
    'Automation and intake',
    'Mailbox intake, monitoring sources, and automation rules for turning signals into work orders.',
    [
      link('Connect mailbox', 'Set up the intake mailbox and connection health.', '/settings/email-intake', <SectionStatusBadge tone="success">Step 1</SectionStatusBadge>, Mail),
      link('Choose what creates work orders', 'Define the messages and rules that should create work.', '/settings/email-intake', <SectionStatusBadge tone="success">Step 2</SectionStatusBadge>, Workflow),
      link('Match client, site, and asset', 'Resolve incoming messages to real operational records.', '/settings/email-intake', <SectionStatusBadge tone="success">Step 3</SectionStatusBadge>, Users2),
      link('Fallback behavior', 'What happens when matching fails or confidence is low.', '/settings/email-intake', <SectionStatusBadge tone="success">Step 4</SectionStatusBadge>, ShieldCheck),
      link('Test with a sample email', 'Run a simulated email before enabling intake.', '/settings/email-intake', <SectionStatusBadge tone="success">Step 5</SectionStatusBadge>, Mail),
      link('Enable intake', 'Turn intake on only after the workflow is ready.', '/settings/email-intake', <SectionStatusBadge tone="warning">Step 6</SectionStatusBadge>, Settings2),
      link('Monitoring tools', 'Webhook intake for tools such as monitoring and alerting platforms.', '/settings/monitoring-intake', <SectionStatusBadge tone="neutral">Connected</SectionStatusBadge>, PlugZap),
    ],
  ),
  section(
    'External providers',
    'Providers and connectors that support automation and message delivery.',
    [
      link('Email provider', 'Sender setup and mailbox delivery provider.', '/settings/email-notifications', <SectionStatusBadge tone="success">Connected</SectionStatusBadge>, Mail),
      link('Email templates', 'Operational email templates and notification copy.', '/settings/email-templates', <SectionStatusBadge tone="info">Templates</SectionStatusBadge>, FileText),
      link('Intake protocols', 'Rules and protocol definitions for automated work creation.', '/settings/email-intake', <SectionStatusBadge tone="neutral">Protocol</SectionStatusBadge>, Workflow),
      link('API and webhooks', 'Future API and webhook controls for external integrations.', '/settings/workspace', <SectionStatusBadge tone="neutral">Planned</SectionStatusBadge>, PlugZap),
    ],
  ),
]

const notificationSections: WorkspaceSection[] = [
  section(
    'Alerts and reminders',
    'Operational notifications, customer alerts, and reminder behavior.',
    [
      link('Notification preferences', 'Work order alerts, SLA reminders, and digest settings.', '/settings/notifications', <SectionStatusBadge tone="success">Live</SectionStatusBadge>, Bell),
      link('Email delivery', 'Outbound email sender identity and delivery health.', '/settings/email-notifications', <SectionStatusBadge tone="success">Live</SectionStatusBadge>, Mail),
      link('Email templates', 'Operational email templates and notification copy.', '/settings/email-templates', <SectionStatusBadge tone="info">Templates</SectionStatusBadge>, FileText),
    ],
  ),
]

const securitySections: WorkspaceSection[] = [
  section(
    'Security and records',
    'Sensitive controls that affect login posture, logs, exports, and workspace ownership.',
    [
      link('Login and session policy', 'Password and session security settings.', '/settings/password-rules', <SectionStatusBadge tone="success">Live</SectionStatusBadge>, ShieldCheck),
      link('Audit logs', 'Review configuration and operational changes.', '/settings/audit-logs', <SectionStatusBadge tone="success">Live</SectionStatusBadge>, FileText),
      link('Account management', 'Export data, deactivate the workspace, or manage ownership.', '/settings/account-management', <SectionStatusBadge tone="warning">Sensitive</SectionStatusBadge>, Receipt),
    ],
  ),
]

export const settingsGroups: SettingsWorkspaceGroup[] = [
  { id: 'workspace', label: 'Workspace', description: 'Company identity, branches, and shared defaults.', icon: Building2, path: '/settings/workspace' },
  { id: 'team', label: 'Team', description: 'Users, roles, permissions, and assignment groups.', icon: Users2, path: '/settings/team' },
  { id: 'operations', label: 'Service Operations', description: 'Work order behavior, SLA rules, and completion policy.', icon: Workflow, path: '/settings/operations' },
  { id: 'documents', label: 'Documents', description: 'Branding, quotes, invoices, receipts, and report appearance.', icon: FileText, path: '/settings/documents' },
  { id: 'finance', label: 'Finance', description: 'Tax, billing, and payment behavior.', icon: Receipt, path: '/settings/finance' },
  { id: 'automation', label: 'Automation & Integrations', description: 'Email intake, monitoring, providers, and automation rules.', icon: Mail, path: '/settings/automation-integrations' },
  { id: 'notifications', label: 'Notifications', description: 'Alerts, reminders, templates, and sender identity.', icon: Bell, path: '/settings/notifications' },
  { id: 'security', label: 'Security & Account', description: 'Security policy, audit history, and account control.', icon: ShieldCheck, path: '/settings/security' },
]

export const defaultSettingsGroupPath = settingsGroups[0]?.path ?? '/settings/workspace'

export const settingsPageRoutes = [
  { path: 'workspace', element: <WorkspaceSettingsPage /> },
  { path: 'team', element: <TeamSettingsPage /> },
  { path: 'operations', element: <ServiceOperationsSettingsPage /> },
  { path: 'documents', element: <DocumentsSettingsPage /> },
  { path: 'finance', element: <FinanceSettingsPage /> },
  { path: 'automation-integrations', element: <AutomationIntegrationsSettingsPage /> },
  { path: 'notifications', element: <NotificationsSettingsPage /> },
  { path: 'security', element: <SecuritySettingsPage /> },
]

export const settingsLegacyRedirects: Array<{ from: string; to: string }> = [
  { from: 'company-profile', to: 'workspace' },
  { from: 'branding', to: 'documents' },
  { from: 'branches-outlets', to: 'workspace' },
  { from: 'documents-branding', to: 'documents' },
  { from: 'report-template', to: 'documents' },
  { from: 'users', to: 'team' },
  { from: 'roles-permissions', to: 'team' },
  { from: 'assignment-groups', to: 'team' },
  { from: 'teams', to: 'team' },
  { from: 'sla-plans', to: 'operations' },
  { from: 'checklist-templates', to: 'operations' },
  { from: 'operations-policy', to: 'operations' },
  { from: 'numbering-rules', to: 'documents' },
  { from: 'numbering', to: 'documents' },
  { from: 'email-notifications', to: 'notifications' },
  { from: 'email-templates', to: 'notifications' },
  { from: 'email-intake', to: 'automation-integrations' },
  { from: 'monitoring-intake', to: 'automation-integrations' },
  { from: 'monitoring-tool-intake', to: 'automation-integrations' },
  { from: 'notifications', to: 'notifications' },
  { from: 'password-rules', to: 'security' },
  { from: 'audit-logs', to: 'security' },
  { from: 'account-management', to: 'security' },
  { from: 'danger-zone', to: 'security' },
]

export function WorkspaceSettingsPage() {
  return <GroupPage title="Workspace" description="Company identity, branches, and shared defaults live together here so setup stays organized for a tenant admin." sections={workspaceSections} />
}

export function TeamSettingsPage() {
  return <GroupPage title="Team" description="People, roles, permissions, assignment groups, and readiness are now managed as one connected workspace." sections={teamSections} />
}

export function ServiceOperationsSettingsPage() {
  return <GroupPage title="Service Operations" description="Keep work order behavior, SLA rules, and completion policy together instead of scattering them across smaller pages." sections={operationsSections} />
}

export function DocumentsSettingsPage() {
  return (
    <div className="space-y-4">
      <DocumentsPreviewSnapshot />
      <section className="surface-card divide-y divide-[var(--app-divider)] p-0">
        {documentsSections.map((item) => (
          <SettingsSection key={item.title} title={item.title} description={item.description} className="px-4 py-4">
            <div className="space-y-1">
              {item.links.map((entry) => {
                const Icon = entry.icon
                return (
                  <SettingsRow
                    key={entry.href}
                    href={entry.href}
                    title={entry.title}
                    description={entry.description}
                    icon={Icon ? <Icon className="h-4 w-4" /> : <Settings2 className="h-4 w-4" />}
                    status={entry.status}
                  />
                )
              })}
            </div>
          </SettingsSection>
        ))}
      </section>
    </div>
  )
}

export function FinanceSettingsPage() {
  return <GroupPage title="Finance" description="Tax, billing, numbering, and payment presentation stay in one controlled workspace." sections={financeSections} />
}

export function NotificationsSettingsPage() {
  return <GroupPage title="Notifications" description="Alerts, reminders, templates, and email sender settings stay visible without bouncing through unrelated pages." sections={notificationSections} />
}

export function AutomationIntegrationsSettingsPage() {
  return <GroupPage title="Automation & Integrations" description="Email intake, monitoring, providers, and automation rules now live in one place so admins do not have to jump between duplicate settings areas." sections={automationSections} />
}

export function SecuritySettingsPage() {
  return <GroupPage title="Security & Account" description="Login security, audit history, exports, and destructive workspace actions are kept together and clearly labeled." sections={securitySections} />
}

export function SettingsDefaultRedirect() {
  return <Navigate to={defaultSettingsGroupPath} replace />
}

function DocumentsPreviewSnapshot() {
  return (
    <section className="surface-card grid gap-4 border border-[var(--app-divider)] p-4 sm:p-5 xl:grid-cols-[1.05fr_0.95fr]">
      <div className="space-y-3">
        <p className="eyebrow-accent text-xs font-semibold uppercase tracking-[0.2em]">Documents</p>
        <div>
          <h3 className="text-2xl font-semibold tracking-tight text-app">Preview-first document controls</h3>
          <p className="mt-2 max-w-2xl text-sm leading-6 text-muted">
            Keep document branding, layouts, numbering, and customer-facing presentation in one place. Open the live preview editors directly from here.
          </p>
        </div>

        <div className="flex flex-wrap gap-2">
          <Link className="button-secondary inline-flex items-center gap-2" to="/settings/branding">
            <Building2 className="h-4 w-4" />
            Branding
          </Link>
          <Link className="button-primary inline-flex items-center gap-2" to="/settings/documents#preview">
            <Eye className="h-4 w-4" />
            Open preview
          </Link>
          <Link className="button-secondary inline-flex items-center gap-2" to="/settings/report-template">
            <FileText className="h-4 w-4" />
            Report design
          </Link>
          <Link className="button-secondary inline-flex items-center gap-2" to="/settings/numbering-rules">
            <Receipt className="h-4 w-4" />
            Numbering rules
          </Link>
        </div>

        <div className="grid gap-2 sm:grid-cols-3">
          <div className="rounded-2xl border border-[var(--app-divider)] bg-[var(--app-surface)] px-3 py-2">
            <p className="text-[11px] font-semibold uppercase tracking-[0.16em] text-muted">Entry points</p>
            <p className="mt-1 text-sm font-semibold text-app">1 hub, 3 direct pages</p>
          </div>
          <div className="rounded-2xl border border-[var(--app-divider)] bg-[var(--app-surface)] px-3 py-2">
            <p className="text-[11px] font-semibold uppercase tracking-[0.16em] text-muted">Preview style</p>
            <p className="mt-1 text-sm font-semibold text-app">Customer-facing</p>
          </div>
          <div className="rounded-2xl border border-[var(--app-divider)] bg-[var(--app-surface)] px-3 py-2">
            <p className="text-[11px] font-semibold uppercase tracking-[0.16em] text-muted">Shared rules</p>
            <p className="mt-1 text-sm font-semibold text-app">Document numbering</p>
          </div>
        </div>
      </div>

      <div className="overflow-hidden rounded-[20px] border border-[var(--app-divider)] bg-[#fcfbf7] p-4 shadow-sm">
        <div className="space-y-3">
          <div className="flex items-start justify-between gap-3">
            <div>
              <p className="text-[10px] font-semibold uppercase tracking-[0.22em] text-muted">Live document preview</p>
              <h4 className="mt-1 text-lg font-semibold text-app">Quote / Estimate</h4>
              <p className="mt-1 text-xs leading-5 text-muted">
                Preview the branding editor before you open the full document template workspace.
              </p>
            </div>
            <Eye className="h-5 w-5 text-muted" />
          </div>
          <div className="grid gap-2 sm:grid-cols-2">
            <div className="rounded-2xl bg-white p-3">
              <p className="text-[10px] font-semibold uppercase tracking-[0.2em] text-muted">Header</p>
              <p className="mt-1 text-sm font-semibold text-app">Logo, company name, and contact block</p>
            </div>
            <div className="rounded-2xl bg-white p-3">
              <p className="text-[10px] font-semibold uppercase tracking-[0.2em] text-muted">Footer</p>
              <p className="mt-1 text-sm font-semibold text-app">Terms, notes, and payment details</p>
            </div>
          </div>
          <div className="rounded-2xl bg-white p-3">
            <p className="text-[10px] font-semibold uppercase tracking-[0.2em] text-muted">Included controls</p>
            <div className="mt-2 flex flex-wrap gap-2 text-xs font-semibold text-app">
              <span className="rounded-full bg-[var(--app-surface)] px-2.5 py-1">Brand identity</span>
              <span className="rounded-full bg-[var(--app-surface)] px-2.5 py-1">Layouts</span>
              <span className="rounded-full bg-[var(--app-surface)] px-2.5 py-1">Templates</span>
              <span className="rounded-full bg-[var(--app-surface)] px-2.5 py-1">Preview</span>
            </div>
          </div>
        </div>
      </div>
    </section>
  )
}
