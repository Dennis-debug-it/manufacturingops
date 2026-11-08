import { Link } from 'react-router-dom'
import {
  Building2, Palette, FileText, Mail, Hash, ClipboardList, Boxes, Clock, Inbox,
  Radio, MessageCircle, CreditCard, ShieldCheck, Bell, Plug, LineChart,
  UserCircle, ScrollText, GitBranch, MapPin, Timer, Workflow, AlertTriangle, Rocket, Percent, LayoutDashboard,
} from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { useEntitlements } from '../../shared/hooks/useEntitlements'
import { EMAIL_INTAKE_ENABLED, WHATSAPP_ENABLED } from '../../shared/config/features'

interface SettingsGroup {
  title: string
  items: Array<{ to: string; label: string; description: string; icon: typeof Building2 }>
}

const groups: SettingsGroup[] = [
  {
    title: 'Company & Branding',
    items: [
      { to: '/settings/company', label: 'Company Profile', description: 'Legal details, addresses, and contact info.', icon: Building2 },
      { to: '/settings/branding', label: 'Branding', description: 'Logo and brand colours.', icon: Palette },
      { to: '/settings/templates', label: 'Document Templates', description: 'Layout, sections, and boilerplate text.', icon: FileText },
    ],
  },
  {
    // Everything email lives together: sending identity, inbound parsing, content, and delivery.
    title: 'Email',
    items: [
      { to: '/settings/email', label: 'Email', description: 'Who your emails come from and where replies go.', icon: Mail },
      ...(EMAIL_INTAKE_ENABLED ? [{ to: '/settings/email-intake', label: 'Email Intake & Parsing', description: 'Auto-create work orders from incoming email.', icon: Inbox }] : []),
      { to: '/settings/email-templates', label: 'Email Templates', description: 'Transactional email content.', icon: Mail },
      { to: '/settings/email/delivery-logs', label: 'Delivery Logs', description: 'Queued, sent, failed, and retried email deliveries.', icon: ScrollText },
    ],
  },
  {
    title: 'Operations',
    items: [
      { to: '/settings/numbering', label: 'Numbering Rules', description: 'Document prefixes and sequences.', icon: Hash },
      { to: '/settings/operational-defaults', label: 'Operational Defaults', description: 'Signature and completion rules new work orders follow.', icon: ClipboardList },
      { to: '/settings/pm-templates', label: 'PM Templates', description: 'Preventive maintenance checklists and trade starter packs.', icon: ClipboardList },
      { to: '/settings/running-checks', label: 'Running checks', description: 'Shift and daily checks: which checklist, which machines, how often.', icon: ClipboardList },
      { to: '/settings/organisation', label: 'Organisation', description: 'Groups, factories, sections, maintenance window and tag numbering.', icon: GitBranch },
      { to: '/settings/asset-categories', label: 'Asset Categories', description: 'Kinds of equipment and the datasheet details recorded for each.', icon: Boxes },
      { to: '/settings/wo-templates', label: 'Work Order Templates', description: 'Reusable job types with default tasks and materials.', icon: ClipboardList },
      { to: '/settings/inventory', label: 'Inventory Settings', description: 'Stock alerts and deduction rules.', icon: Boxes },
      { to: '/settings/time-attendance', label: 'Time & Attendance Settings', description: 'Work hours and leave policy.', icon: Clock },
      { to: '/settings/sla-policies', label: 'SLA Policies', description: 'Response and resolution targets per priority.', icon: Timer },
      { to: '/settings/workflows', label: 'Status Workflows', description: 'Custom work order status chains and transitions.', icon: Workflow },
      { to: '/settings/escalation-rules', label: 'Escalation Rules', description: 'Automatic escalation for stuck or at-risk work orders.', icon: AlertTriangle },
    ],
  },
  {
    title: 'Intake & Integrations',
    items: [
      { to: '/settings/monitoring', label: 'Monitoring Intake', description: 'Alert webhook routing.', icon: Radio },
      ...(WHATSAPP_ENABLED ? [{ to: '/settings/whatsapp', label: 'WhatsApp', description: 'WhatsApp Business integration.', icon: MessageCircle }] : []),
      { to: '/settings/integrations', label: 'Integrations', description: 'API access and outbound webhooks.', icon: Plug },
      { to: '/settings/sensors', label: 'Sensors', description: 'PLC, SCADA and tracker feeds into meters, over HTTP or MQTT.', icon: Radio },
    ],
  },
  {
    title: 'Finance',
    items: [
      { to: '/settings/finance-defaults', label: 'Finance Defaults', description: 'Default tax rate, payment terms, and approval rules.', icon: Percent },
      { to: '/settings/payments', label: 'Payments', description: 'M-Pesa, Stripe, and bank transfer settings.', icon: CreditCard },
      { to: '/settings/approval-matrix', label: 'Approval Matrix', description: 'Amount-based approval chains for POs and expenses.', icon: GitBranch },
    ],
  },
  {
    title: 'Access & Notifications',
    items: [
      { to: '/settings/notifications', label: 'My Notifications', description: WHATSAPP_ENABLED ? 'How you hear about each event: bell, push, email or WhatsApp.' : 'How you hear about each event: bell, push or email.', icon: Bell },
      ...(WHATSAPP_ENABLED ? [{ to: '/settings/whatsapp-reminders', label: 'WhatsApp Reminders', description: 'Which notifications may go on WhatsApp, usage and message log.', icon: MessageCircle }] : []),
      { to: '/settings/workspace-notifications', label: 'Workspace Notifications', description: 'Event notification matrix.', icon: Bell },
      { to: '/settings/notification-templates', label: 'Notification Templates', description: 'Client ETA emails for job milestones.', icon: MapPin },
      { to: '/settings/security', label: 'Security', description: 'Session and authentication policy.', icon: ShieldCheck },
      { to: '/settings/executive-dashboard', label: 'Executive Dashboard Settings', description: 'Metric targets and thresholds.', icon: LineChart },
      { to: '/settings/dashboards', label: 'Dashboards', description: 'Hide dashboard widgets for a role.', icon: LayoutDashboard },
    ],
  },
  {
    title: 'Account',
    items: [
      { to: '/settings/account', label: 'Account', description: 'Subscription and plan details.', icon: UserCircle },
      { to: '/settings/audit-logs', label: 'Audit Logs', description: 'Tenant administrative history.', icon: ScrollText },
      // The workspace setup checklist and starter packs. Fully wired, but no link anywhere in the
      // app pointed at `/onboarding`, so nobody could reach it after first sign-in.
      { to: '/onboarding', label: 'Getting Started', description: 'Workspace setup checklist and starter packs.', icon: Rocket },
    ],
  },
]

export function SettingsOverviewPage() {
  const { canAccessPath } = useEntitlements()
  const visibleGroups = groups.map((group) => ({ ...group, items: group.items.filter((item) => canAccessPath(item.to)) })).filter((group) => group.items.length > 0)
  return (
    <div className="space-y-4">
      <PageHeader title="Settings" subtitle="Company, branding, templates, integrations, and policies for this workspace." />
      {visibleGroups.map((group) => (
        <section key={group.title} className="space-y-3">
          <h2 className="text-sm font-semibold uppercase tracking-wide text-slate-500">{group.title}</h2>
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
            {group.items.map((item) => (
              <Link
                key={item.to}
                to={item.to}
                className="flex items-start gap-3 rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 shadow-sm transition hover:border-slate-300 hover:shadow"
              >
                <item.icon className="mt-0.5 h-5 w-5 shrink-0 text-slate-400" />
                <div>
                  <p className="text-sm font-medium text-slate-900">{item.label}</p>
                  <p className="mt-1 text-xs text-slate-500">{item.description}</p>
                </div>
              </Link>
            ))}
          </div>
        </section>
      ))}
    </div>
  )
}
