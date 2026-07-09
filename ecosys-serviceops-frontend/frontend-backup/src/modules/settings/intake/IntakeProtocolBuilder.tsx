import type { ReactNode } from 'react'
import { Activity, CheckCircle2, Clock3, ListChecks, ShieldCheck } from 'lucide-react'
import { Badge } from '../../../components/ui/Badge'
import { InfoAlert } from '../../../components/ui/InfoAlert'
import type { AssetRecord, AssignmentGroupRecord, BranchRecord, ClientRecord, UserRecord } from '../../../types/api'
import { IntakeActionsPanel } from './IntakeActionsPanel'
import { IntakeCriteriaBuilder } from './IntakeCriteriaBuilder'
import { IntakeSourceSelector } from './IntakeSourceSelector'
import type { IntakeProtocolForm } from './types'
import { sourceChannelLabel, summarizeCriterion } from './types'

export function IntakeProtocolBuilder({
  form,
  clients,
  branches,
  assets,
  groups,
  users,
  validationErrors,
  onChange,
}: {
  form: IntakeProtocolForm
  clients: ClientRecord[]
  branches: BranchRecord[]
  assets: AssetRecord[]
  groups: AssignmentGroupRecord[]
  users: UserRecord[]
  validationErrors: string[]
  onChange: (value: IntakeProtocolForm) => void
}) {
  const criteriaCount = form.criteriaGroups.reduce((total, group) => total + group.criteria.length, 0)
  const actionLabels = [
    form.actions.createWorkOrder.enabled ? 'Create Work Order' : null,
    form.actions.sendNotification.enabled ? 'Send Notification' : null,
    form.actions.attachMetadata.enabled ? 'Attach Metadata' : null,
  ].filter((value): value is string => Boolean(value))

  const criteriaSummary = form.criteriaGroups
    .flatMap((group) => group.criteria.map((criterion) => summarizeCriterion(criterion)))
    .slice(0, 4)

  const stepCards = [
    {
      step: '01',
      title: 'Select Source',
      description: form.name.trim() ? form.name : 'Name the protocol and choose the intake channel.',
      ready: Boolean(form.name.trim()),
    },
    {
      step: '02',
      title: 'Analysis Conditions',
      description: criteriaCount > 0 ? `${criteriaCount} conditions configured` : 'Define matching logic and grouped criteria.',
      ready: criteriaCount > 0,
    },
    {
      step: '03',
      title: 'Execute Actions',
      description: actionLabels.length > 0 ? actionLabels.join(', ') : 'Choose what happens when the protocol matches.',
      ready: actionLabels.length > 0,
    },
  ]

  return (
    <div className="space-y-5">
      <div data-testid="email-intake-steps-widget" className="grid gap-3 lg:grid-cols-3">
        {stepCards.map((card) => (
          <StepCard key={card.step} step={card.step} title={card.title} description={card.description} ready={card.ready} />
        ))}
      </div>

      <div className="grid gap-5 xl:grid-cols-3">
        <IntakeSourceSelector
          name={form.name}
          isActive={form.isActive}
          sourceType={form.sourceType}
          emailConfig={form.sourceConfig.email}
          monitoringConfig={form.sourceConfig.monitoring}
          onNameChange={(name) => onChange({ ...form, name })}
          onActiveChange={(isActive) => onChange({ ...form, isActive })}
          onSourceTypeChange={(sourceType) => onChange({ ...form, sourceType })}
          onEmailConfigChange={(email) => onChange({ ...form, sourceConfig: { ...form.sourceConfig, email } })}
          onMonitoringConfigChange={(monitoring) => onChange({ ...form, sourceConfig: { ...form.sourceConfig, monitoring } })}
          nameError={validationErrors.find((error) => error === 'Rule name required')}
        />

        <IntakeCriteriaBuilder
          sourceType={form.sourceType}
          groups={form.criteriaGroups}
          onChange={(criteriaGroups) => onChange({ ...form, criteriaGroups })}
          error={validationErrors.find((error) => error === 'At least one condition required')}
        />

        <IntakeActionsPanel
          actions={form.actions}
          clients={clients}
          branches={branches}
          assets={assets}
          groups={groups}
          users={users}
          onChange={(actions) => onChange({ ...form, actions })}
          actionError={validationErrors.find((error) => error === 'At least one action required')}
          workOrderError={validationErrors.find((error) => error === 'Work order title template required')}
        />
      </div>

      <div data-testid="email-intake-preview-result" className="grid gap-5 xl:grid-cols-[1.2fr_0.8fr]">
        <section className="surface-card">
          <div className="flex flex-wrap items-start justify-between gap-3">
            <div>
              <p className="eyebrow-accent text-xs font-semibold uppercase tracking-[0.24em]">Protocol Summary</p>
              <h3 className="mt-2 text-xl font-semibold tracking-tight text-app">Validation and execution summary</h3>
              <p className="mt-2 text-sm leading-6 text-muted">Confirm the intake protocol before saving it into your settings.</p>
            </div>
            <div className="flex flex-wrap gap-2">
              <Badge tone={form.isActive ? 'success' : 'neutral'}>{form.isActive ? 'Active' : 'Draft'}</Badge>
              <Badge tone="info">{sourceChannelLabel[form.sourceType]}</Badge>
            </div>
          </div>

          {validationErrors.length > 0 ? (
            <InfoAlert title="Protocol needs attention" description={validationErrors.join(', ')} tone="warning" className="mt-5" />
          ) : (
            <InfoAlert title="Protocol ready to save" description="All required sections are configured and the protocol can be stored." tone="success" className="mt-5" />
          )}

          <div className="mt-5 grid gap-4 md:grid-cols-3">
            <MetricCard icon={<Activity className="h-4 w-4" />} label="Source" value={sourceChannelLabel[form.sourceType]} />
            <MetricCard icon={<ListChecks className="h-4 w-4" />} label="Criteria" value={`${criteriaCount} conditions`} />
            <MetricCard icon={<ShieldCheck className="h-4 w-4" />} label="Actions" value={actionLabels.length > 0 ? actionLabels.join(', ') : 'No actions selected'} />
          </div>

          <div className="mt-5 grid gap-4 md:grid-cols-2">
            <div className="panel-subtle rounded-[24px] p-4">
              <p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted">Criteria Preview</p>
              <div className="mt-3 space-y-2">
                {criteriaSummary.map((item) => (
                  <p key={item} className="text-sm text-app">{item}</p>
                ))}
                {criteriaSummary.length === 0 ? <p className="text-sm text-muted">No criteria configured yet.</p> : null}
              </div>
            </div>
            <div className="panel-subtle rounded-[24px] p-4">
              <p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted">Execution Defaults</p>
              <div className="mt-3 space-y-2 text-sm text-app">
                <p>Priority: {form.actions.createWorkOrder.priority || 'Not set'}</p>
                <p>Client: {clients.find((client) => client.id === form.actions.createWorkOrder.clientId)?.clientName || 'Not set'}</p>
                <p>Branch: {branches.find((branch) => branch.id === form.actions.createWorkOrder.branchId)?.name || 'Not set'}</p>
                <p>Assignment: {groups.find((group) => group.id === form.actions.createWorkOrder.assignmentGroupId)?.name || users.find((user) => user.id === form.actions.createWorkOrder.assignedUserId)?.fullName || 'Unassigned'}</p>
              </div>
            </div>
          </div>

          <div className="panel-accent mt-5 rounded-[24px] p-4">
            <p className="text-sm font-semibold text-app">Protocol Summary</p>
            <div className="mt-3 space-y-2 text-sm text-app">
              <p>Source: {sourceChannelLabel[form.sourceType]}</p>
              <p>Criteria: {criteriaSummary.join(', ') || 'No conditions configured yet.'}</p>
              <p>Actions: {actionLabels.join(', ') || 'No actions selected yet.'}</p>
            </div>
          </div>
        </section>

        <section className="surface-card">
          <div className="flex items-start gap-3">
            <div className="icon-accent rounded-2xl p-3">
              <Clock3 className="h-5 w-5" />
            </div>
            <div>
              <p className="eyebrow-accent text-xs font-semibold uppercase tracking-[0.24em]">Protocol Health</p>
              <h3 className="mt-2 text-xl font-semibold tracking-tight text-app">Recent execution context</h3>
            </div>
          </div>
          <div className="mt-5 space-y-3">
            <HealthRow label="Status" value={form.lastTriggerStatus || 'Not tested yet'} />
            <HealthRow label="Last triggered" value={form.lastTriggeredAt ? new Date(form.lastTriggeredAt).toLocaleString() : 'Never'} />
            <HealthRow label="Saved" value={form.updatedAt ? new Date(form.updatedAt).toLocaleString() : form.createdAt ? new Date(form.createdAt).toLocaleString() : 'Not saved yet'} />
          </div>
          {form.lastError ? (
            <InfoAlert title="Last execution issue" description={form.lastError} tone="danger" className="mt-5" />
          ) : (
            <div className="panel-accent mt-5 rounded-[24px] p-4">
              <div className="flex items-start gap-3">
                <div className="icon-accent rounded-2xl p-2">
                  <CheckCircle2 className="h-4 w-4" />
                </div>
                <div>
                  <p className="text-sm font-semibold text-app">Protocol Summary</p>
                  <p className="mt-1 text-sm leading-6 text-muted">
                    Source: {sourceChannelLabel[form.sourceType]} | Criteria: {criteriaSummary.slice(0, 2).join(', ') || 'Pending'} | Actions: {actionLabels.join(', ') || 'Pending'}
                  </p>
                </div>
              </div>
            </div>
          )}
        </section>
      </div>
    </div>
  )
}

function StepCard({
  step,
  title,
  description,
  ready,
}: {
  step: string
  title: string
  description: string
  ready: boolean
}) {
  return (
    <div className={`rounded-[26px] border px-4 py-4 ${ready ? 'border-sky-400/35 bg-cyan-400/10' : 'panel-subtle'}`}>
      <div className="flex items-center justify-between gap-3">
        <p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted">Step {step}</p>
        <Badge tone={ready ? 'success' : 'neutral'}>{ready ? 'Ready' : 'Pending'}</Badge>
      </div>
      <p className="mt-3 text-sm font-semibold text-app">{title}</p>
      <p className="mt-2 text-sm leading-6 text-muted">{description}</p>
    </div>
  )
}

function MetricCard({ icon, label, value }: { icon: ReactNode; label: string; value: string }) {
  return (
    <div className="panel-subtle rounded-[24px] p-4">
      <div className="flex items-center gap-2 text-accent">{icon}</div>
      <p className="mt-3 text-xs font-semibold uppercase tracking-[0.18em] text-muted">{label}</p>
      <p className="mt-2 text-sm font-semibold text-app">{value}</p>
    </div>
  )
}

function HealthRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="panel-subtle rounded-[22px] px-4 py-3">
      <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-muted">{label}</p>
      <p className="mt-2 text-sm font-medium text-app">{value}</p>
    </div>
  )
}
