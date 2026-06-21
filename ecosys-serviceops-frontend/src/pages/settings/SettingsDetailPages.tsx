import {
  BellRing,
  BriefcaseBusiness,
  Building2,
  Clock3,
  Download,
  FileDigit,
  History,
  Mail,
  MessageSquareShare,
  ShieldCheck,
  ShieldEllipsis,
  Siren,
  SlidersHorizontal,
  Users2,
  Wrench,
  type LucideIcon,
} from 'lucide-react'
import type { ReactNode } from 'react'
import { Badge } from '../../components/ui/Badge'
import { PageHeader } from '../../components/ui/PageHeader'
import { useAuth } from '../../auth/AuthContext'
import { api } from '../../lib/api'
import { useAsyncData } from '../../hooks/useAsyncData'
import { tenantService } from '../../services/tenantService'
import { useAppStore } from '../../store/appStore'
import type { BadgeTone, TenantData, WorkOrderRulesSettings } from '../../types/app'
import { formatDateOnly, formatDateTime } from '../../utils/date'
import { LoadingState } from '../../components/ui/LoadingState'
import { ErrorState } from '../../components/ui/ErrorState'

type Snapshot = {
  tenantId?: string
  tenantName: string
  data?: TenantData
  sessions: Array<{
    id: string
    startedAt: string
    lastActiveAt: string
    active: boolean
    role: string
  }>
}

type KeyValueItem = {
  label: string
  value: string
}

function useSettingsSnapshot(): Snapshot {
  const { session } = useAuth()
  const tenantId = session?.tenantId
  const snapshot = useAppStore((database) => ({
    data: tenantId ? database.tenantData[tenantId] : undefined,
    sessions: tenantId
      ? database.sessions
          .filter((item) => item.tenantId === tenantId)
          .map((item) => ({
            id: item.id,
            startedAt: item.startedAt,
            lastActiveAt: item.lastActiveAt,
            active: item.active,
            role: item.role,
          }))
      : [],
  }))

  return {
    tenantId,
    tenantName: session?.tenantName ?? 'ServiceOps Tenant',
    data: snapshot.data,
    sessions: snapshot.sessions,
  }
}

function toneFromBoolean(value: boolean): BadgeTone {
  return value ? 'success' : 'neutral'
}

function toneFromIntegration(status: TenantData['settings']['emailIntegration']['status']): BadgeTone {
  if (status === 'Connected') return 'success'
  if (status === 'Attention') return 'warning'
  return 'neutral'
}

function branchName(data: TenantData, branchId: string) {
  return data.branches.find((branch) => branch.id === branchId)?.name ?? 'Unknown branch'
}

function titleCountMap(data: TenantData) {
  const map = new Map<string, { count: number; admins: number; users: number }>()

  data.users.forEach((user) => {
    const key = user.jobTitle || 'Unassigned title'
    const current = map.get(key) ?? { count: 0, admins: 0, users: 0 }
    current.count += 1
    if (user.role === 'Admin') current.admins += 1
    if (user.role === 'User') current.users += 1
    map.set(key, current)
  })

  return [...map.entries()]
    .map(([title, counts]) => ({ title, ...counts }))
    .sort((left, right) => right.count - left.count || left.title.localeCompare(right.title))
}

function ruleLabel(key: keyof WorkOrderRulesSettings) {
  if (key === 'allowMissingClientWithReason') return 'Allow missing client with reason'
  if (key === 'allowMissingAssetWithReason') return 'Allow missing asset with reason'
  if (key === 'requireAssignmentGroupBeforeDispatch') return 'Require assignment group before dispatch'
  return 'Require client acknowledgement before closure'
}

function ruleDescription(key: keyof WorkOrderRulesSettings) {
  if (key === 'allowMissingClientWithReason') return 'Lets dispatch capture exceptional work orders while preserving accountability.'
  if (key === 'allowMissingAssetWithReason') return 'Supports early incident logging before the affected asset is positively identified.'
  if (key === 'requireAssignmentGroupBeforeDispatch') return 'Keeps technician routing aligned to operating teams before work leaves the desk.'
  return 'Prevents closure until the client has confirmed handover or a valid exception is recorded.'
}

function SettingsPageFrame({
  title,
  description,
  children,
}: {
  title: string
  description: string
  children: ReactNode
}) {
  return (
    <div className="space-y-4">
      <PageHeader eyebrow="Settings" title={title} description={description} />
      {children}
    </div>
  )
}

function SummaryGrid({
  items,
}: {
  items: Array<{ label: string; value: string; detail: string }>
}) {
  return (
    <section className="grid gap-4 md:grid-cols-3">
      {items.map((item) => (
        <article key={item.label} className="surface-card">
          <p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted">{item.label}</p>
          <p className="mt-3 text-3xl font-semibold tracking-tight text-app">{item.value}</p>
          <p className="mt-3 text-sm text-muted">{item.detail}</p>
        </article>
      ))}
    </section>
  )
}

function SectionCard({
  title,
  description,
  icon: Icon,
  children,
}: {
  title: string
  description?: string
  icon?: LucideIcon
  children: ReactNode
}) {
  return (
    <section className="surface-card">
      <div className="flex items-start gap-4">
        {Icon ? (
          <div className="icon-accent rounded-2xl p-3">
            <Icon className="h-5 w-5" />
          </div>
        ) : null}
        <div className="min-w-0 flex-1">
          <h2 className="text-xl font-semibold text-app">{title}</h2>
          {description ? <p className="mt-2 text-sm leading-6 text-muted">{description}</p> : null}
        </div>
      </div>
      <div className="mt-5">{children}</div>
    </section>
  )
}

function KeyValueGrid({ items }: { items: KeyValueItem[] }) {
  return (
    <div className="grid gap-3 md:grid-cols-2">
      {items.map((item) => (
        <div key={item.label} className="panel-subtle rounded-2xl px-4 py-3">
          <p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted">{item.label}</p>
          <p className="mt-2 text-sm font-medium text-app">{item.value}</p>
        </div>
      ))}
    </div>
  )
}

function ToggleRow({
  label,
  description,
  checked,
  onChange,
}: {
  label: string
  description: string
  checked: boolean
  onChange: (checked: boolean) => void
}) {
  return (
    <label className="panel-subtle flex items-center justify-between gap-4 rounded-2xl px-4 py-4">
      <div className="min-w-0">
        <p className="text-sm font-semibold text-app">{label}</p>
        <p className="mt-1 text-sm text-muted">{description}</p>
      </div>
      <input
        type="checkbox"
        className="h-4 w-4 accent-cyan-400"
        checked={checked}
        onChange={(event) => onChange(event.target.checked)}
      />
    </label>
  )
}

function EmptyPlaceholder({ title, detail }: { title: string; detail: string }) {
  return (
    <div className="surface-card">
      <p className="text-sm font-semibold text-app">{title}</p>
      <p className="mt-2 text-sm text-muted">{detail}</p>
    </div>
  )
}

export function CompanyProfileSettingsPage() {
  const { session } = useAuth()
  const { data, loading, error } = useAsyncData(
    (signal) => api.get<{ companyName: string; legalName: string; email: string; phone: string | null; country: string; timezone: string; address: string | null }>('/api/tenant/profile', { signal }),
    null,
    [],
  )

  if (loading && !data) return <SettingsPageFrame title="Company Profile" description="Keep legal identity, support channels, and operating geography aligned across the workspace."><LoadingState label="Loading company profile" /></SettingsPageFrame>
  if (error) return <SettingsPageFrame title="Company Profile" description="Keep legal identity, support channels, and operating geography aligned across the workspace."><ErrorState title="Unable to load company profile" description={error} /></SettingsPageFrame>

  return (
    <SettingsPageFrame
      title="Company Profile"
      description="Keep legal identity, support channels, and operating geography aligned across the workspace."
    >
      <SummaryGrid
        items={[
          { label: 'Tenant', value: session?.tenantName ?? '—', detail: 'Displayed across tenant-scoped workspace screens.' },
          { label: 'Company', value: data?.companyName ?? '—', detail: 'Legal company name on record.' },
          { label: 'Country', value: data?.country ?? '—', detail: 'Operating geography for this tenant.' },
        ]}
      />

      <SectionCard title="Organization record" description="Core company values used for support and platform-facing identity." icon={Building2}>
        <KeyValueGrid
          items={[
            { label: 'Company Name', value: data?.companyName ?? '—' },
            { label: 'Legal Name', value: data?.legalName ?? '—' },
            { label: 'Support Email', value: data?.email ?? '—' },
            { label: 'Support Phone', value: data?.phone ?? '—' },
            { label: 'Country', value: data?.country ?? '—' },
            { label: 'Timezone', value: data?.timezone ?? '—' },
            { label: 'Address', value: data?.address ?? '—' },
          ]}
        />
      </SectionCard>
    </SettingsPageFrame>
  )
}

export function JobTitlesSettingsPage() {
  const { data } = useSettingsSnapshot()

  if (!data) {
    return <SettingsPageFrame title="Job Titles" description="Role taxonomy is unavailable right now."><EmptyPlaceholder title="No job title data loaded" detail="User role metadata will populate this view once tenant data is available." /></SettingsPageFrame>
  }

  const titles = titleCountMap(data)

  return (
    <SettingsPageFrame
      title="Job Titles"
      description="Review how tenant roles are labeled across users so approvals, escalations, and reporting stay consistent."
    >
      <SummaryGrid
        items={[
          { label: 'Unique Titles', value: String(titles.length), detail: 'Distinct job title values currently assigned to tenant users.' },
          { label: 'Admins', value: String(data.users.filter((user) => user.role === 'Admin').length), detail: 'Users with administrative authority in the tenant.' },
          { label: 'Users', value: String(data.users.filter((user) => user.role === 'User').length), detail: 'Operational users relying on this title taxonomy.' },
        ]}
      />

      <SectionCard title="Title directory" description="A compact directory of current titles and where they are used." icon={BriefcaseBusiness}>
        <div className="grid gap-3 md:grid-cols-2">
          {titles.map((item) => (
            <article key={item.title} className="panel-subtle rounded-2xl px-4 py-4">
              <div className="flex items-center justify-between gap-3">
                <p className="text-sm font-semibold text-app">{item.title}</p>
                <Badge tone="info">{item.count} assigned</Badge>
              </div>
              <p className="mt-2 text-sm text-muted">{item.admins} admin, {item.users} user</p>
            </article>
          ))}
        </div>
      </SectionCard>
    </SettingsPageFrame>
  )
}

export function SessionTrackingSettingsPage() {
  const { data, sessions } = useSettingsSnapshot()

  if (!data) {
    return <SettingsPageFrame title="Session Tracking" description="Session data is unavailable right now."><EmptyPlaceholder title="No session policy loaded" detail="Security and active session telemetry will appear here once the tenant snapshot is ready." /></SettingsPageFrame>
  }

  const activeSessions = sessions.filter((session) => session.active)

  return (
    <SettingsPageFrame
      title="Session Tracking"
      description="Monitor login activity and align active sessions with the tenant's security posture."
    >
      <SummaryGrid
        items={[
          { label: 'Tracked Sessions', value: String(sessions.length), detail: 'Sessions recorded in the local tenant workspace store.' },
          { label: 'Active Sessions', value: String(activeSessions.length), detail: 'Sessions still marked active for this tenant.' },
          { label: 'Session Timeout', value: `${data.settings.security.sessionTimeoutHours}h`, detail: 'Current inactivity threshold defined in security settings.' },
        ]}
      />

      <div className="grid gap-4 xl:grid-cols-[0.75fr_1.25fr]">
        <SectionCard title="Policy snapshot" description="Session controls currently configured for the tenant." icon={ShieldCheck}>
          <KeyValueGrid
            items={[
              { label: 'Max Concurrent Sessions', value: String(data.settings.security.maxConcurrentSessions) },
              { label: 'MFA Required', value: data.settings.security.mfaRequired ? 'Required' : 'Optional' },
              { label: 'Password Rotation', value: `${data.settings.security.passwordRotationDays} days` },
            ]}
          />
        </SectionCard>

        <SectionCard title="Recent sessions" description="This view is ready for live telemetry as the backend session feed expands." icon={History}>
          {sessions.length > 0 ? (
            <div className="space-y-3">
              {sessions.slice(0, 8).map((session) => (
                <article key={session.id} className="panel-subtle rounded-2xl px-4 py-4">
                  <div className="flex items-center justify-between gap-3">
                    <p className="text-sm font-semibold text-app">Session {session.id.slice(-6)}</p>
                    <Badge tone={toneFromBoolean(session.active)}>{session.active ? 'Active' : 'Closed'}</Badge>
                  </div>
                  <p className="mt-2 text-sm text-muted">Role: {session.role}</p>
                  <p className="mt-1 text-sm text-muted">Started {formatDateTime(session.startedAt)} | Last active {formatDateTime(session.lastActiveAt)}</p>
                </article>
              ))}
            </div>
          ) : (
            <EmptyPlaceholder title="No session records yet" detail="The layout is in place for live session tracking once backend session persistence is surfaced into the tenant app." />
          )}
        </SectionCard>
      </div>
    </SettingsPageFrame>
  )
}

export function AssignmentGroupsSettingsPage() {
  const { data } = useSettingsSnapshot()

  if (!data) {
    return <SettingsPageFrame title="Assignment Groups" description="Assignment group data is unavailable right now."><EmptyPlaceholder title="No group data loaded" detail="Dispatch groups will appear here once the tenant snapshot is available." /></SettingsPageFrame>
  }

  return (
    <SettingsPageFrame
      title="Assignment Groups"
      description="Define the operating groups used for dispatch, routing, and ownership across branches."
    >
      <SummaryGrid
        items={[
          { label: 'Groups', value: String(data.settings.technicianGroups.length), detail: 'Assignment groups available to dispatch and planning flows.' },
          { label: 'Technicians', value: String(data.technicians.length), detail: 'Technicians currently mapped into operational groups.' },
          { label: 'Branches Covered', value: String(data.branches.length), detail: 'Branch footprint currently supported by group routing.' },
        ]}
      />

      <SectionCard title="Group directory" description="Compact group cards ready for future editing flows." icon={Users2}>
        <div className="grid gap-3 md:grid-cols-2">
          {data.settings.technicianGroups.map((group) => {
            const technicians = data.technicians.filter((technician) => technician.groupId === group.id)
            return (
              <article key={group.id} className="panel-subtle rounded-2xl px-4 py-4">
                <div className="flex items-center justify-between gap-3">
                  <p className="text-sm font-semibold text-app">{group.name}</p>
                  <Badge tone="info">{technicians.length} techs</Badge>
                </div>
                <p className="mt-2 text-sm text-muted">Supervisor: {group.supervisor}</p>
                <p className="mt-1 text-sm text-muted">Branches: {group.branchIds.map((id) => branchName(data, id)).join(', ')}</p>
              </article>
            )
          })}
        </div>
      </SectionCard>
    </SettingsPageFrame>
  )
}

export function WorkOrderRulesSettingsPage() {
  const { data, tenantId } = useSettingsSnapshot()

  if (!data || !tenantId) {
    return <SettingsPageFrame title="Work Order Rules" description="Work order rules are unavailable right now."><EmptyPlaceholder title="No rule set loaded" detail="Rule toggles will appear here after tenant settings have initialized." /></SettingsPageFrame>
  }

  const currentTenantId = tenantId
  const rules = data.settings.workOrderRules

  function updateRule(key: keyof WorkOrderRulesSettings, value: boolean) {
    tenantService.updateTenantData(currentTenantId, (current) => ({
      ...current,
      settings: {
        ...current.settings,
        workOrderRules: {
          ...current.settings.workOrderRules,
          [key]: value,
        },
      },
    }))
  }

  return (
    <SettingsPageFrame
      title="Work Order Rules"
      description="Set the operating guardrails for capture, dispatch, and closure across the work order lifecycle."
    >
      <SummaryGrid
        items={[
          { label: 'Exception Rules', value: '2', detail: 'Rules currently allowing exceptional capture with a reason.' },
          { label: 'Dispatch Controls', value: rules.requireAssignmentGroupBeforeDispatch ? 'Locked' : 'Flexible', detail: 'Assignment enforcement before dispatch is sent to the field.' },
          { label: 'Closure Controls', value: rules.requireClientAcknowledgementBeforeClosure ? 'Acknowledged' : 'Optional', detail: 'Client handover requirement before work can be closed.' },
        ]}
      />

      <div className="grid gap-4 xl:grid-cols-[1.05fr_0.95fr]">
        <SectionCard title="Workflow toggles" description="These switches apply immediately to the tenant settings model and are ready for future backend enforcement." icon={SlidersHorizontal}>
          <div className="space-y-3">
            {(Object.keys(rules) as Array<keyof WorkOrderRulesSettings>).map((key) => (
              <ToggleRow
                key={key}
                label={ruleLabel(key)}
                description={ruleDescription(key)}
                checked={rules[key]}
                onChange={(checked) => updateRule(key, checked)}
              />
            ))}
          </div>
        </SectionCard>

        <SectionCard title="Operational intent" description="Why these guardrails matter in day-to-day service delivery." icon={Siren}>
          <div className="space-y-3">
            <div className="panel-subtle rounded-2xl px-4 py-4">
              <p className="text-sm font-semibold text-app">Capture flexibility</p>
              <p className="mt-2 text-sm text-muted">Missing client or asset exceptions help teams capture urgent incidents without losing traceability.</p>
            </div>
            <div className="panel-subtle rounded-2xl px-4 py-4">
              <p className="text-sm font-semibold text-app">Dispatch discipline</p>
              <p className="mt-2 text-sm text-muted">Assignment groups prevent ambiguous ownership when work is handed to the field.</p>
            </div>
            <div className="panel-subtle rounded-2xl px-4 py-4">
              <p className="text-sm font-semibold text-app">Closure confidence</p>
              <p className="mt-2 text-sm text-muted">Client acknowledgement helps ensure service completion is both delivered and accepted.</p>
            </div>
          </div>
        </SectionCard>
      </div>
    </SettingsPageFrame>
  )
}

export function SlaContractsSettingsPage() {
  const { data } = useSettingsSnapshot()

  if (!data) {
    return <SettingsPageFrame title="SLA & Contracts" description="SLA data is unavailable right now."><EmptyPlaceholder title="No SLA data loaded" detail="SLA summaries will appear here once tenant data is available." /></SettingsPageFrame>
  }

  const linkedClients = data.clients.filter((client) => client.slaRuleId).length

  return (
    <SettingsPageFrame
      title="SLA & Contracts"
      description="Review response commitments, resolution targets, and how clients are mapped to contractual service levels."
    >
      <SummaryGrid
        items={[
          { label: 'SLA Rules', value: String(data.slaRules.length), detail: 'Rule definitions currently active in the tenant.' },
          { label: 'Linked Clients', value: String(linkedClients), detail: 'Clients currently mapped to a contractual SLA rule.' },
          { label: 'Critical Response', value: `${data.slaRules[0]?.responseTimeHours ?? 0}h`, detail: 'Fastest response target configured in the current rule set.' },
        ]}
      />

      <SectionCard title="SLA matrix" description="Current contractual response and escalation definitions." icon={ShieldEllipsis}>
        <div className="grid gap-3 md:grid-cols-2">
          {data.slaRules.map((rule) => (
            <article key={rule.id} className="panel-subtle rounded-2xl px-4 py-4">
              <div className="flex items-center justify-between gap-3">
                <p className="text-sm font-semibold text-app">{rule.name}</p>
                <Badge tone={rule.priorityLevel === 'Critical' ? 'danger' : rule.priorityLevel === 'High' ? 'warning' : 'info'}>
                  {rule.priorityLevel}
                </Badge>
              </div>
              <p className="mt-2 text-sm text-muted">Response {rule.responseTimeHours}h | Resolution {rule.resolutionTimeHours}h</p>
              <p className="mt-1 text-sm text-muted">Escalation: {rule.escalationPath}</p>
            </article>
          ))}
        </div>
      </SectionCard>
    </SettingsPageFrame>
  )
}

export function NumberingRulesSettingsPage() {
  const { data } = useSettingsSnapshot()

  if (!data) {
    return <SettingsPageFrame title="Numbering Rules" description="Numbering rule data is unavailable right now."><EmptyPlaceholder title="No numbering rules loaded" detail="Branch-based prefixes and counters will appear here after tenant settings initialize." /></SettingsPageFrame>
  }

  return (
    <SettingsPageFrame
      title="Numbering Rules"
      description="Manage the prefixes and next counters used for work orders, assets, and requisitions by branch."
    >
      <SummaryGrid
        items={[
          { label: 'Branch Rule Sets', value: String(data.settings.numberingRules.length), detail: 'Rule packs available across the tenant branch footprint.' },
          { label: 'Work Order Prefixes', value: String(new Set(data.settings.numberingRules.map((rule) => rule.workOrderPrefix)).size), detail: 'Distinct work order prefixes currently in circulation.' },
          { label: 'Reset Model', value: data.settings.numberingRules[0]?.resetRule ?? 'Not set', detail: 'Current numbering reset cadence in use.' },
        ]}
      />

      <SectionCard title="Branch numbering" description="Compact branch-by-branch numbering controls for future editing flows." icon={FileDigit}>
        <div className="grid gap-3 lg:grid-cols-2">
          {data.settings.numberingRules.map((rule) => (
            <article key={rule.branchId} className="panel-subtle rounded-2xl px-4 py-4">
              <p className="text-sm font-semibold text-app">{rule.branchName}</p>
              <div className="mt-3 grid gap-3 md:grid-cols-2">
                <div>
                  <p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted">Work Orders</p>
                  <p className="mt-1 text-sm text-app">{rule.workOrderPrefix}-{String(rule.nextWorkOrderNumber).padStart(6, '0')}</p>
                </div>
                <div>
                  <p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted">Assets</p>
                  <p className="mt-1 text-sm text-app">{rule.assetPrefix}-{String(rule.nextAssetNumber).padStart(6, '0')}</p>
                </div>
                <div>
                  <p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted">Requisitions</p>
                  <p className="mt-1 text-sm text-app">{rule.requisitionPrefix}-{String(rule.nextRequisitionNumber).padStart(6, '0')}</p>
                </div>
                <div>
                  <p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted">Reset Rule</p>
                  <p className="mt-1 text-sm text-app">{rule.resetRule}</p>
                </div>
              </div>
            </article>
          ))}
        </div>
      </SectionCard>
    </SettingsPageFrame>
  )
}

export function PreventiveMaintenanceTemplatesSettingsPage() {
  const { data } = useSettingsSnapshot()

  if (!data) {
    return <SettingsPageFrame title="Preventive Maintenance Templates" description="PM template data is unavailable right now."><EmptyPlaceholder title="No PM templates loaded" detail="Template and PM rule summaries will appear here once tenant settings are available." /></SettingsPageFrame>
  }

  return (
    <SettingsPageFrame
      title="Preventive Maintenance Templates"
      description="Review reusable PM templates and the recurring rules that turn them into scheduled operational work."
    >
      <SummaryGrid
        items={[
          { label: 'Asset Templates', value: String(data.settings.assetTemplates.length), detail: 'Reusable template definitions for recurring maintenance work.' },
          { label: 'PM Rules', value: String(data.settings.pmRules.length), detail: 'Scheduling rules defining maintenance cadence by asset category.' },
          { label: 'Auto-Scheduled', value: String(data.settings.pmRules.filter((rule) => rule.autoSchedule).length), detail: 'Rules currently configured to auto-generate PM work.' },
        ]}
      />

      <div className="grid gap-4 xl:grid-cols-2">
        <SectionCard title="Templates" description="Checklist-ready maintenance templates." icon={Wrench}>
          <div className="space-y-3">
            {data.settings.assetTemplates.map((template) => (
              <article key={template.id} className="panel-subtle rounded-2xl px-4 py-4">
                <div className="flex items-center justify-between gap-3">
                  <p className="text-sm font-semibold text-app">{template.name}</p>
                  <Badge tone={template.autoSchedulePm ? 'success' : 'neutral'}>{template.autoSchedulePm ? 'Auto PM' : 'Manual PM'}</Badge>
                </div>
                <p className="mt-2 text-sm text-muted">{template.category} | Every {template.pmFrequencyDays} days</p>
                <p className="mt-1 text-sm text-muted">{template.checklistSummary}</p>
              </article>
            ))}
          </div>
        </SectionCard>

        <SectionCard title="Scheduling rules" description="Category-level PM cadence controls." icon={Clock3}>
          <div className="space-y-3">
            {data.settings.pmRules.map((rule) => (
              <article key={rule.id} className="panel-subtle rounded-2xl px-4 py-4">
                <div className="flex items-center justify-between gap-3">
                  <p className="text-sm font-semibold text-app">{rule.name}</p>
                  <Badge tone={rule.enabled ? 'success' : 'neutral'}>{rule.enabled ? 'Enabled' : 'Paused'}</Badge>
                </div>
                <p className="mt-2 text-sm text-muted">{rule.assetCategory} | Every {rule.frequencyDays} days</p>
                <p className="mt-1 text-sm text-muted">{rule.autoSchedule ? 'Auto-scheduling is active for this rule.' : 'Rule is available for manual planning flows.'}</p>
              </article>
            ))}
          </div>
        </SectionCard>
      </div>
    </SettingsPageFrame>
  )
}

export function EmailSettingsPage() {
  const { data } = useSettingsSnapshot()

  if (!data) {
    return <SettingsPageFrame title="Email Settings" description="Email settings are unavailable right now."><EmptyPlaceholder title="No email integration loaded" detail="Outbound email configuration will appear here once tenant settings are ready." /></SettingsPageFrame>
  }

  const { emailIntegration } = data.settings

  return (
    <SettingsPageFrame
      title="Email Settings"
      description="Configure the sender identity and health status for outbound service communication."
    >
      <SummaryGrid
        items={[
          { label: 'Status', value: emailIntegration.status, detail: 'Current email integration health reported by the tenant settings model.' },
          { label: 'Sender', value: emailIntegration.senderName, detail: 'Default sender displayed to clients and internal recipients.' },
          { label: 'Last Check', value: formatDateOnly(emailIntegration.lastCheckedAt), detail: 'Most recent verification timestamp for the mail connection.' },
        ]}
      />

      <SectionCard title="SMTP profile" description="Current outbound mail profile for client communication." icon={Mail}>
        <div className="grid gap-3 md:grid-cols-2">
          <div className="panel-subtle rounded-2xl px-4 py-4">
            <div className="flex items-center justify-between gap-3">
              <p className="text-sm font-semibold text-app">Connection Health</p>
              <Badge tone={toneFromIntegration(emailIntegration.status)}>{emailIntegration.status}</Badge>
            </div>
            <p className="mt-2 text-sm text-muted">The UI is ready for deeper SMTP credential management as backend endpoints are exposed.</p>
          </div>
          <div className="panel-subtle rounded-2xl px-4 py-4">
            <p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted">Host</p>
            <p className="mt-2 text-sm text-app">{emailIntegration.smtpHost}</p>
            <p className="mt-1 text-sm text-muted">{emailIntegration.senderEmail}</p>
          </div>
        </div>
      </SectionCard>
    </SettingsPageFrame>
  )
}

export function NotificationsSettingsPage() {
  const { data } = useSettingsSnapshot()

  if (!data) {
    return <SettingsPageFrame title="Notifications" description="Notification settings are unavailable right now."><EmptyPlaceholder title="No notification settings loaded" detail="Alert preferences will appear here once tenant settings are available." /></SettingsPageFrame>
  }

  const { notifications } = data.settings

  return (
    <SettingsPageFrame
      title="Notifications"
      description="Review how operational alerts, digests, and client-facing acknowledgements are currently configured."
    >
      <SummaryGrid
        items={[
          { label: 'Critical Alerts', value: notifications.criticalAlerts ? 'On' : 'Off', detail: 'High-priority dispatch and service issue alerts.' },
          { label: 'Daily Digest', value: notifications.dailyDigest ? 'On' : 'Off', detail: 'Scheduled operational digest for ongoing tenant activity.' },
          { label: 'Ack Emails', value: notifications.emailAcknowledgements ? 'On' : 'Off', detail: 'Client acknowledgement emails on service handover.' },
        ]}
      />

      <SectionCard title="Notification switches" description="Compact readiness view for the current notification mix." icon={BellRing}>
        <div className="grid gap-3 md:grid-cols-3">
          <article className="panel-subtle rounded-2xl px-4 py-4">
            <div className="flex items-center justify-between gap-3">
              <p className="text-sm font-semibold text-app">Critical Alerts</p>
              <Badge tone={toneFromBoolean(notifications.criticalAlerts)}>{notifications.criticalAlerts ? 'Enabled' : 'Disabled'}</Badge>
            </div>
            <p className="mt-2 text-sm text-muted">Escalates urgent operational issues quickly.</p>
          </article>
          <article className="panel-subtle rounded-2xl px-4 py-4">
            <div className="flex items-center justify-between gap-3">
              <p className="text-sm font-semibold text-app">Daily Digest</p>
              <Badge tone={toneFromBoolean(notifications.dailyDigest)}>{notifications.dailyDigest ? 'Enabled' : 'Disabled'}</Badge>
            </div>
            <p className="mt-2 text-sm text-muted">Summarizes daily activity for tenant stakeholders.</p>
          </article>
          <article className="panel-subtle rounded-2xl px-4 py-4">
            <div className="flex items-center justify-between gap-3">
              <p className="text-sm font-semibold text-app">Acknowledgements</p>
              <Badge tone={toneFromBoolean(notifications.emailAcknowledgements)}>{notifications.emailAcknowledgements ? 'Enabled' : 'Disabled'}</Badge>
            </div>
            <p className="mt-2 text-sm text-muted">Confirms closure and handover events to clients.</p>
          </article>
        </div>
      </SectionCard>
    </SettingsPageFrame>
  )
}

export function SmsWhatsAppSettingsPage() {
  const { data } = useSettingsSnapshot()

  return (
    <SettingsPageFrame
      title="SMS / WhatsApp"
      description="Prepare outbound mobile communication channels for urgent alerts, dispatch prompts, and client updates."
    >
      <SummaryGrid
        items={[
          { label: 'SMS Gateway', value: 'Planned', detail: 'Ready for provider onboarding and template controls.' },
          { label: 'WhatsApp Flows', value: 'Planned', detail: 'Future conversational notifications and acknowledgements.' },
          { label: 'Client Reach', value: String(data?.clients.length ?? 0), detail: 'Client accounts that could eventually consume mobile messaging.' },
        ]}
      />

      <SectionCard title="Channel readiness" description="A compact placeholder for future messaging provider setup." icon={MessageSquareShare}>
        <div className="grid gap-3 md:grid-cols-2">
          <article className="panel-subtle rounded-2xl px-4 py-4">
            <p className="text-sm font-semibold text-app">SMS Alerts</p>
            <p className="mt-2 text-sm text-muted">Use this area later for provider credentials, sending pools, and alert templates.</p>
          </article>
          <article className="panel-subtle rounded-2xl px-4 py-4">
            <p className="text-sm font-semibold text-app">WhatsApp Updates</p>
            <p className="mt-2 text-sm text-muted">This slot is ready for interactive status updates and client acknowledgement journeys.</p>
          </article>
        </div>
      </SectionCard>
    </SettingsPageFrame>
  )
}

export function IntegrationsSettingsPage() {
  const { data } = useSettingsSnapshot()

  return (
    <SettingsPageFrame
      title="Integrations"
      description="Track operational integrations and keep the tenant ready for external systems, messaging, and data exchange."
    >
      <SummaryGrid
        items={[
          { label: 'Email', value: data?.settings.emailIntegration.status ?? 'Not Configured', detail: 'Current outbound email integration state.' },
          { label: 'Future Connectors', value: '3', detail: 'Suggested slots for ERP, webhooks, and messaging connectors.' },
          { label: 'Expansion Ready', value: 'Yes', detail: 'Layout is ready for future integration detail screens.' },
        ]}
      />

      <SectionCard title="Connector stack" description="Current and planned integration surfaces." icon={Mail}>
        <div className="grid gap-3 md:grid-cols-3">
          <article className="panel-subtle rounded-2xl px-4 py-4">
            <p className="text-sm font-semibold text-app">Email</p>
            <p className="mt-2 text-sm text-muted">{data?.settings.emailIntegration.smtpHost ?? 'No host configured yet'}</p>
          </article>
          <article className="panel-subtle rounded-2xl px-4 py-4">
            <p className="text-sm font-semibold text-app">Webhooks</p>
            <p className="mt-2 text-sm text-muted">Reserved for event delivery into external systems.</p>
          </article>
          <article className="panel-subtle rounded-2xl px-4 py-4">
            <p className="text-sm font-semibold text-app">ERP / Finance</p>
            <p className="mt-2 text-sm text-muted">Reserved for downstream work order, SLA, and billing sync.</p>
          </article>
        </div>
      </SectionCard>
    </SettingsPageFrame>
  )
}

export function SecuritySettingsPage() {
  const { data } = useSettingsSnapshot()

  if (!data) {
    return <SettingsPageFrame title="Security" description="Security settings are unavailable right now."><EmptyPlaceholder title="No security settings loaded" detail="Tenant security controls will appear here once the settings model is available." /></SettingsPageFrame>
  }

  const { security } = data.settings

  return (
    <SettingsPageFrame
      title="Security"
      description="Review the core controls that shape tenant login posture, session length, and password policy."
    >
      <SummaryGrid
        items={[
          { label: 'Session Timeout', value: `${security.sessionTimeoutHours}h`, detail: 'Maximum inactivity period before a session should expire.' },
          { label: 'Concurrent Sessions', value: String(security.maxConcurrentSessions), detail: 'Allowed simultaneous sessions per account.' },
          { label: 'MFA', value: security.mfaRequired ? 'Required' : 'Optional', detail: 'Whether additional login verification is enforced.' },
        ]}
      />

      <SectionCard title="Security controls" description="Current tenant security posture and recommended operating view." icon={ShieldCheck}>
        <KeyValueGrid
          items={[
            { label: 'Password Rotation', value: `${security.passwordRotationDays} days` },
            { label: 'Session Timeout', value: `${security.sessionTimeoutHours} hours` },
            { label: 'Max Concurrent Sessions', value: String(security.maxConcurrentSessions) },
            { label: 'Multi-Factor Authentication', value: security.mfaRequired ? 'Required' : 'Not required' },
          ]}
        />
      </SectionCard>
    </SettingsPageFrame>
  )
}

type AuditLogEntry = { id: string; action: string; actorName: string; entityType: string; detail: string | null; createdAt: string }

export function AuditLogsSettingsPage() {
  const { data, loading, error } = useAsyncData(
    (signal) => api.get<AuditLogEntry[]>('/api/audit-logs', { signal }),
    null,
    [],
  )

  const entries = data ?? []

  if (loading && !data) return <SettingsPageFrame title="Audit Logs" description="Review the most recent tenant configuration and operational audit entries."><LoadingState label="Loading audit logs" /></SettingsPageFrame>
  if (error) return <SettingsPageFrame title="Audit Logs" description="Review the most recent tenant configuration and operational audit entries."><ErrorState title="Unable to load audit logs" description={error} /></SettingsPageFrame>

  return (
    <SettingsPageFrame
      title="Audit Logs"
      description="Review the most recent tenant configuration and operational audit entries."
    >
      <SummaryGrid
        items={[
          { label: 'Audit Events', value: String(entries.length), detail: 'Total tenant audit events returned by the API.' },
          { label: 'Latest Entry', value: formatDateOnly(entries[0]?.createdAt), detail: 'Most recent timestamp in the tenant log.' },
          { label: 'Tracked Scope', value: 'Tenant', detail: 'This view stays within tenant-level audit history.' },
        ]}
      />

      <SectionCard title="Recent audit activity" description="Latest entries across the tenant operational workspace." icon={History}>
        <div className="space-y-3">
          {entries.slice(0, 8).map((entry) => (
            <article key={entry.id} className="panel-subtle rounded-2xl px-4 py-4">
              <div className="flex items-center justify-between gap-3">
                <p className="text-sm font-semibold text-app">{entry.action}</p>
                <p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted">{formatDateTime(entry.createdAt)}</p>
              </div>
              <p className="mt-2 text-sm text-muted">{entry.actorName} | {entry.entityType}</p>
              {entry.detail ? <p className="mt-1 text-sm text-muted">{entry.detail}</p> : null}
            </article>
          ))}
          {entries.length === 0 ? <EmptyPlaceholder title="No audit events yet" detail="Audit entries will appear here as users make changes in the workspace." /> : null}
        </div>
      </SectionCard>
    </SettingsPageFrame>
  )
}

export function BackupExportSettingsPage() {
  const { data } = useSettingsSnapshot()

  return (
    <SettingsPageFrame
      title="Backup & Export"
      description="Prepare the tenant for safe data retention, export workflows, and future scheduled backup controls."
    >
      <SummaryGrid
        items={[
          { label: 'Export Scope', value: `${data?.workOrders.length ?? 0} work orders`, detail: 'Operational records currently available for future export workflows.' },
          { label: 'Branches', value: String(data?.branches.length ?? 0), detail: 'Branch footprint covered by future backup jobs and export packs.' },
          { label: 'Status', value: 'Ready', detail: 'Layout is prepared for future backup jobs, archives, and export actions.' },
        ]}
      />

      <SectionCard title="Data portability" description="A compact placeholder for backup and export actions." icon={Download}>
        <div className="grid gap-3 md:grid-cols-2">
          <article className="panel-subtle rounded-2xl px-4 py-4">
            <p className="text-sm font-semibold text-app">Scheduled backups</p>
            <p className="mt-2 text-sm text-muted">Use this slot later for automated retention schedules, destinations, and restore checkpoints.</p>
          </article>
          <article className="panel-subtle rounded-2xl px-4 py-4">
            <p className="text-sm font-semibold text-app">On-demand exports</p>
            <p className="mt-2 text-sm text-muted">CSV, audit, and document exports can expand here without changing the settings structure again.</p>
          </article>
        </div>
      </SectionCard>
    </SettingsPageFrame>
  )
}
