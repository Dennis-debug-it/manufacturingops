import { BellRing, Database, FilePlus2 } from 'lucide-react'
import type { AssetRecord, AssignmentGroupRecord, BranchRecord, ClientRecord, UserRecord } from '../../../types/api'
import { IntakeActionMetadata } from './IntakeActionMetadata'
import { IntakeActionNotification } from './IntakeActionNotification'
import { IntakeActionWorkOrder } from './IntakeActionWorkOrder'
import type { IntakeActionsConfig } from './types'

export function IntakeActionsPanel({
  actions,
  clients,
  branches,
  assets,
  groups,
  users,
  onChange,
  actionError,
  workOrderError,
}: {
  actions: IntakeActionsConfig
  clients: ClientRecord[]
  branches: BranchRecord[]
  assets: AssetRecord[]
  groups: AssignmentGroupRecord[]
  users: UserRecord[]
  onChange: (value: IntakeActionsConfig) => void
  actionError?: string
  workOrderError?: string
}) {
  const enabledCount = [actions.createWorkOrder.enabled, actions.sendNotification.enabled, actions.attachMetadata.enabled].filter(Boolean).length
  const cards = [
    { key: 'createWorkOrder' as const, title: 'Create Work Order', description: 'Open a work order with templates, defaults, routing, and due date rules.', Icon: FilePlus2, enabled: actions.createWorkOrder.enabled },
    { key: 'sendNotification' as const, title: 'Send Notification', description: 'Notify teams, dispatch, or branch stakeholders when intake matches.', Icon: BellRing, enabled: actions.sendNotification.enabled },
    { key: 'attachMetadata' as const, title: 'Attach Metadata', description: 'Persist labels, source intelligence, and raw references alongside the work order.', Icon: Database, enabled: actions.attachMetadata.enabled },
  ]

  return (
    <section className="surface-card h-full space-y-5">
      <div>
        <p className="eyebrow-accent text-xs font-semibold uppercase tracking-[0.24em]">Step 3</p>
        <h2 className="mt-2 text-xl font-semibold tracking-tight text-app">Execute Actions</h2>
        <p className="mt-2 text-sm leading-6 text-muted">Select one or more actions to run when the intake protocol matches the configured conditions.</p>
      </div>
      <div className="panel-subtle rounded-[24px] px-4 py-3">
        <p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted">Action coverage</p>
        <p className="mt-2 text-sm font-semibold text-app">{enabledCount} of 3 action cards enabled</p>
      </div>
      {actionError ? <p className="text-sm text-rose-300">{actionError}</p> : null}

      <div className="space-y-4">
        {cards.map((card) => {
          const Icon = card.Icon

          return (
            <article key={card.key} className={`rounded-[30px] border p-4 transition ${card.enabled ? 'border-sky-400/35 bg-cyan-400/10 shadow-[0_18px_38px_rgba(14,165,233,0.12)]' : 'panel-subtle'}`}>
              <div className="flex items-start justify-between gap-3">
                <div className="flex items-start gap-3">
                  <div className={`rounded-2xl p-3 ${card.enabled ? 'icon-accent' : 'panel-subtle'}`}>
                    <Icon className="h-5 w-5" />
                  </div>
                  <div>
                    <p className="text-sm font-semibold text-app">{card.title}</p>
                    <p className="mt-1 text-sm leading-6 text-muted">{card.description}</p>
                  </div>
                </div>
                <input
                  data-testid={card.key === 'createWorkOrder' ? 'email-intake-create-workorder-button' : undefined}
                  type="checkbox"
                  checked={card.enabled}
                  onChange={(event) =>
                    onChange({
                      ...actions,
                      [card.key]: {
                        ...actions[card.key],
                        enabled: event.target.checked,
                      },
                    })
                  }
                />
              </div>

              {card.key === 'createWorkOrder' && actions.createWorkOrder.enabled ? (
                <IntakeActionWorkOrder config={actions.createWorkOrder} clients={clients} branches={branches} assets={assets} groups={groups} users={users} onChange={(value) => onChange({ ...actions, createWorkOrder: value })} error={workOrderError} />
              ) : null}
              {card.key === 'sendNotification' && actions.sendNotification.enabled ? <IntakeActionNotification config={actions.sendNotification} onChange={(value) => onChange({ ...actions, sendNotification: value })} /> : null}
              {card.key === 'attachMetadata' && actions.attachMetadata.enabled ? <IntakeActionMetadata config={actions.attachMetadata} onChange={(value) => onChange({ ...actions, attachMetadata: value })} /> : null}
            </article>
          )
        })}
      </div>
    </section>
  )
}
