import type { ReactNode } from 'react'
import { Sparkles } from 'lucide-react'
import { InfoAlert } from '../../../components/ui/InfoAlert'
import type { AssetRecord, AssignmentGroupRecord, BranchRecord, ClientRecord, UserRecord } from '../../../types/api'
import type { WorkOrderActionConfig } from './types'

const workOrderTokens = ['{{subject}}', '{{body}}', '{{sender}}', '{{receivedAt}}', '{{deviceName}}', '{{severity}}', '{{rawPayload}}']

export function IntakeActionWorkOrder({
  config,
  clients,
  branches,
  assets,
  groups,
  users,
  onChange,
  error,
}: {
  config: WorkOrderActionConfig
  clients: ClientRecord[]
  branches: BranchRecord[]
  assets: AssetRecord[]
  groups: AssignmentGroupRecord[]
  users: UserRecord[]
  onChange: (value: WorkOrderActionConfig) => void
  error?: string
}) {
  const showUnassigned = !config.assignmentGroupId && !config.assignedUserId

  return (
    <div className="mt-4 space-y-4 rounded-[28px] border border-app bg-white/5 p-4">
      <div className="grid gap-4 md:grid-cols-2">
        <Field label="Work Order Title template">
          <input value={config.workOrderTitleTemplate} onChange={(event) => onChange({ ...config, workOrderTitleTemplate: event.target.value })} className="field-input" placeholder="{{subject}}" />
        </Field>
        <Field label="Work Order Type">
          <select value={config.workOrderType} onChange={(event) => onChange({ ...config, workOrderType: event.target.value })} className="field-input">
            {['Corrective', 'Preventive', 'Inspection', 'Incident'].map((option) => (
              <option key={option} value={option}>{option}</option>
            ))}
          </select>
        </Field>
      </div>
      <Field label="Work Order Description template">
        <textarea value={config.workOrderDescriptionTemplate} onChange={(event) => onChange({ ...config, workOrderDescriptionTemplate: event.target.value })} className="field-input min-h-[120px]" placeholder="{{body}}" />
      </Field>
      <div className="panel-subtle rounded-[24px] p-4">
        <p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted">Available template tokens</p>
        <div className="mt-3">
          <TokenList tokens={workOrderTokens} />
        </div>
      </div>
      {error ? <InfoAlert title="Work order action needs attention" description={error} tone="danger" /> : null}

      <div className="grid gap-4 md:grid-cols-2">
        <Field label="Priority">
          <select value={config.priority} onChange={(event) => onChange({ ...config, priority: event.target.value })} className="field-input">
            {['Critical', 'High', 'Medium', 'Low'].map((option) => (
              <option key={option} value={option}>{option}</option>
            ))}
          </select>
        </Field>
        <Field label="Client">
          <select value={config.clientId} onChange={(event) => onChange({ ...config, clientId: event.target.value })} className="field-input">
            <option value="">No default client</option>
            {clients.map((client) => (
              <option key={client.id} value={client.id}>{client.clientName}</option>
            ))}
          </select>
        </Field>
        <Field label="Site / Branch">
          <select value={config.branchId} onChange={(event) => onChange({ ...config, branchId: event.target.value })} className="field-input">
            <option value="">No default branch</option>
            {branches.map((branch) => (
              <option key={branch.id} value={branch.id}>{branch.name}</option>
            ))}
          </select>
        </Field>
        <Field label="Asset optional">
          <select value={config.assetId} onChange={(event) => onChange({ ...config, assetId: event.target.value })} className="field-input">
            <option value="">No default asset</option>
            {assets.map((asset) => (
              <option key={asset.id} value={asset.id}>{asset.assetName}</option>
            ))}
          </select>
        </Field>
      </div>

      <div className="rounded-[24px] border border-app/80 bg-white/5 p-4">
        <div className="mb-4">
          <p className="text-sm font-semibold text-app">Assignment and routing</p>
          <p className="mt-1 text-sm text-muted">Route the work order to a dispatch queue, assign it directly, or leave it unassigned.</p>
        </div>
        <div className="grid gap-4 md:grid-cols-2">
          <Field label="Assignment Group">
            <select value={config.assignmentGroupId} onChange={(event) => onChange({ ...config, assignmentGroupId: event.target.value })} className="field-input">
              <option value="">Create as Unassigned</option>
              {groups.filter((group) => group.isActive).map((group) => (
                <option key={group.id} value={group.id}>{group.name}</option>
              ))}
            </select>
          </Field>
          <Field label="Assigned To optional">
            <select value={config.assignedUserId} onChange={(event) => onChange({ ...config, assignedUserId: event.target.value })} className="field-input">
              <option value="">No direct assignee</option>
              {users.filter((user) => user.isActive).map((user) => (
                <option key={user.id} value={user.id}>{user.fullName}</option>
              ))}
            </select>
          </Field>
        </div>

        {showUnassigned ? (
          <InfoAlert
            title="No assignment selected"
            description="This work order will be created as Unassigned until dispatch assigns it."
            tone="info"
            className="mt-4"
          />
        ) : null}
      </div>

      <div className="grid gap-4 md:grid-cols-2">
        <Field label="Due date rule">
          <select value={config.dueDateRule} onChange={(event) => onChange({ ...config, dueDateRule: event.target.value })} className="field-input">
            {['None', 'Same day', 'Within 4 hours', 'Within 8 hours', 'Next business day'].map((option) => (
              <option key={option} value={option}>{option}</option>
            ))}
          </select>
        </Field>
        <Field label="Tags">
          <input value={config.tags} onChange={(event) => onChange({ ...config, tags: event.target.value })} className="field-input" placeholder="power, ups, critical" />
        </Field>
      </div>

      <label className="panel-subtle flex items-start justify-between gap-4 rounded-2xl px-4 py-3">
        <div>
          <p className="text-sm font-semibold text-app">Auto-create immediately</p>
          <p className="mt-1 text-sm text-muted">Create the work order as soon as the rule matches without extra approval steps.</p>
        </div>
        <input type="checkbox" checked={config.autoCreateImmediately} onChange={(event) => onChange({ ...config, autoCreateImmediately: event.target.checked })} />
      </label>

      <div className="panel-accent rounded-[24px] p-4">
        <div className="flex items-start gap-3">
          <div className="icon-accent rounded-2xl p-2">
            <Sparkles className="h-4 w-4" />
          </div>
          <div>
            <p className="text-sm font-semibold text-app">Assignment behavior</p>
            <p className="mt-1 text-sm leading-6 text-muted">Assignment group routes to a queue, assigned user routes directly, and leaving both blank creates an unassigned work order.</p>
          </div>
        </div>
      </div>
    </div>
  )
}

function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <label className="block space-y-2">
      <span className="text-sm font-medium text-app">{label}</span>
      {children}
    </label>
  )
}

function TokenList({ tokens }: { tokens: string[] }) {
  return (
    <div className="flex flex-wrap gap-2">
      {tokens.map((token) => (
        <span key={token} className="rounded-full border border-app bg-white/5 px-3 py-1 text-xs font-semibold text-accent">
          {token}
        </span>
      ))}
    </div>
  )
}
