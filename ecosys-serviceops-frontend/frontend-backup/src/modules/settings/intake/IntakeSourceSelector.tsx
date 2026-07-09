import type { ReactNode } from 'react'
import { Cpu, Mail, Radio, Workflow } from 'lucide-react'
import { Badge } from '../../../components/ui/Badge'
import type { EmailSourceConfig, IntakeProtocolSourceType, MonitoringSourceConfig } from './types'
import { futureSourceCards, sourceTypeCards } from './types'

const sourceIcons: Record<IntakeProtocolSourceType, typeof Mail> = {
  Email: Mail,
  Monitoring: Cpu,
}

export function IntakeSourceSelector({
  name,
  isActive,
  sourceType,
  emailConfig,
  monitoringConfig,
  onNameChange,
  onActiveChange,
  onSourceTypeChange,
  onEmailConfigChange,
  onMonitoringConfigChange,
  nameError,
}: {
  name: string
  isActive: boolean
  sourceType: IntakeProtocolSourceType
  emailConfig: EmailSourceConfig
  monitoringConfig: MonitoringSourceConfig
  onNameChange: (value: string) => void
  onActiveChange: (value: boolean) => void
  onSourceTypeChange: (value: IntakeProtocolSourceType) => void
  onEmailConfigChange: (value: EmailSourceConfig) => void
  onMonitoringConfigChange: (value: MonitoringSourceConfig) => void
  nameError?: string
}) {
  return (
    <section className="surface-card h-full space-y-5">
      <div className="flex items-start justify-between gap-3">
        <div>
          <p className="eyebrow-accent text-xs font-semibold uppercase tracking-[0.24em]">Step 1</p>
          <h2 className="mt-2 text-xl font-semibold tracking-tight text-app">Select Source</h2>
          <p className="mt-2 text-sm leading-6 text-muted">Choose where signals originate and define how intake should parse them.</p>
        </div>
        <label className="panel-subtle flex items-center gap-3 rounded-2xl px-3 py-2">
          <span className="text-xs font-semibold uppercase tracking-[0.18em] text-muted">Active</span>
          <input data-testid="email-intake-enable-toggle" type="checkbox" checked={isActive} onChange={(event) => onActiveChange(event.target.checked)} />
        </label>
      </div>

      <Field label="Rule Name" hint="Required">
        <input data-testid="email-intake-rule-name-input" value={name} onChange={(event) => onNameChange(event.target.value)} className="field-input" placeholder="e.g. Critical UPS Alarm" />
        {nameError ? <p className="text-xs text-rose-300">{nameError}</p> : null}
      </Field>

      <div className="space-y-3">
        <div className="flex items-center gap-2">
          <p className="text-sm font-semibold text-app">Input Channel</p>
          <Badge tone="info">{sourceType === 'Email' ? 'Email Integration' : 'External Monitoring'}</Badge>
        </div>
        <div className="grid gap-3 lg:grid-cols-2">
          {sourceTypeCards.map((card) => {
            const Icon = sourceIcons[card.type]
            const selected = card.type === sourceType

            return (
              <button
                key={card.type}
                type="button"
                onClick={() => onSourceTypeChange(card.type)}
                className={`rounded-[28px] border p-4 text-left transition ${
                  selected
                    ? 'border-sky-400/50 bg-cyan-400/10 shadow-[0_18px_38px_rgba(14,165,233,0.12)]'
                    : 'panel-subtle hover-surface border-app'
                }`}
              >
                <div className="flex items-start justify-between gap-3">
                  <div className={`rounded-2xl p-3 ${selected ? 'icon-accent' : 'panel-subtle'}`}>
                    <Icon className="h-5 w-5" />
                  </div>
                  {selected ? <Badge tone="info">Selected</Badge> : null}
                </div>
                <p className="mt-4 text-sm font-semibold text-app">{card.label}</p>
                <p className="mt-2 text-sm leading-6 text-muted">{card.description}</p>
              </button>
            )
          })}
        </div>

        <div>
          <p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted">Future channels</p>
          <div className="mt-3 grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
            {futureSourceCards.map((card) => (
              <div key={card.label} className={`rounded-[24px] border p-4 ${card.disabled ? 'border-app/50 bg-white/5 opacity-70' : 'panel-subtle'}`}>
                <div className="flex items-center gap-3">
                  <div className="rounded-2xl p-3 panel-subtle">
                    {card.label.includes('API') ? <Workflow className="h-4 w-4 text-muted" /> : <Radio className="h-4 w-4 text-muted" />}
                  </div>
                  <div>
                    <p className="text-sm font-semibold text-app">{card.label}</p>
                    <p className="mt-1 text-xs text-muted">{card.disabled ? 'Not enabled' : 'Planned'}</p>
                  </div>
                </div>
                <p className="mt-3 text-sm leading-6 text-muted">{card.description}</p>
              </div>
            ))}
          </div>
        </div>
      </div>

      {sourceType === 'Email' ? (
        <div className="space-y-4 rounded-[28px] border border-app bg-white/5 p-4">
          <div>
            <p className="text-sm font-semibold text-app">Email Source Configuration</p>
            <p className="mt-1 text-sm text-muted">Configure the monitored mailbox, sender policy, and listener details.</p>
          </div>
          <div className="grid gap-4 md:grid-cols-2">
            <Field label="Monitored mailbox / intake email">
              <input
                value={emailConfig.intakeEmailAddress}
                onChange={(event) => onEmailConfigChange({ ...emailConfig, intakeEmailAddress: event.target.value, monitoredMailbox: event.target.value })}
                className="field-input"
                placeholder="alerts@ecosys.example"
              />
            </Field>
            <Field label="Allowed sender domains">
              <input
                value={emailConfig.allowedSenderDomains}
                onChange={(event) => onEmailConfigChange({ ...emailConfig, allowedSenderDomains: event.target.value })}
                className="field-input"
                placeholder="britam.com, partner.org"
              />
            </Field>
            <Field label="Mailbox provider">
              <select value={emailConfig.mailboxProvider} onChange={(event) => onEmailConfigChange({ ...emailConfig, mailboxProvider: event.target.value })} className="field-input">
                {['IMAP', 'POP3', 'Other'].map((option) => (
                  <option key={option} value={option}>{option}</option>
                ))}
              </select>
            </Field>
            <Field label="Subject parsing mode">
              <select value={emailConfig.subjectParsingMode} onChange={(event) => onEmailConfigChange({ ...emailConfig, subjectParsingMode: event.target.value })} className="field-input">
                {['Structured Keywords', 'Contains Match', 'Regex Pattern', 'Reference Number'].map((option) => (
                  <option key={option} value={option}>{option}</option>
                ))}
              </select>
            </Field>
            <Field label="Listener host">
              <input value={emailConfig.host} onChange={(event) => onEmailConfigChange({ ...emailConfig, host: event.target.value })} className="field-input" placeholder="imap.example.com" />
            </Field>
            <Field label="Port">
              <input type="number" value={emailConfig.port} onChange={(event) => onEmailConfigChange({ ...emailConfig, port: Number(event.target.value) || 0 })} className="field-input" />
            </Field>
            <Field label="Username">
              <input value={emailConfig.username} onChange={(event) => onEmailConfigChange({ ...emailConfig, username: event.target.value })} className="field-input" placeholder="serviceops-intake" />
            </Field>
            <Field label="Password">
              <input type="password" value={emailConfig.password} onChange={(event) => onEmailConfigChange({ ...emailConfig, password: event.target.value })} className="field-input" placeholder="Leave blank to keep current" />
            </Field>
            <Field label="Polling or listener mode">
              <select value={emailConfig.pollingMode} onChange={(event) => onEmailConfigChange({ ...emailConfig, pollingMode: event.target.value })} className="field-input">
                {['Mailbox Listener', 'Polling', 'Hybrid'].map((option) => (
                  <option key={option} value={option}>{option}</option>
                ))}
              </select>
            </Field>
            <div className="grid gap-3 md:grid-cols-2 md:self-start">
              <ToggleRow label="Known sender only" description="Only create work from approved domains and mailbox senders." checked={emailConfig.knownSenderOnly} onChange={(value) => onEmailConfigChange({ ...emailConfig, knownSenderOnly: value })} />
              <ToggleRow label="Match email body" description="Allow the criteria engine to inspect body content." checked={emailConfig.matchEmailBody} onChange={(value) => onEmailConfigChange({ ...emailConfig, matchEmailBody: value })} />
              <ToggleRow label="Attachment parsing" description="Extract attachment name and type during intake analysis." checked={emailConfig.attachmentParsing} onChange={(value) => onEmailConfigChange({ ...emailConfig, attachmentParsing: value })} />
              <ToggleRow label="Use SSL" description="Secure the mailbox listener connection." checked={emailConfig.useSsl} onChange={(value) => onEmailConfigChange({ ...emailConfig, useSsl: value })} />
            </div>
          </div>
        </div>
      ) : (
        <div className="space-y-4 rounded-[28px] border border-app bg-white/5 p-4">
          <div>
            <p className="text-sm font-semibold text-app">Monitoring Source Configuration</p>
            <p className="mt-1 text-sm text-muted">Define the monitoring tool, webhook path, and payload mapping behavior.</p>
          </div>
          <div className="grid gap-4 md:grid-cols-2">
            <Field label="Monitoring tool">
              <select value={monitoringConfig.toolType} onChange={(event) => onMonitoringConfigChange({ ...monitoringConfig, toolType: event.target.value })} className="field-input">
                {['SolarWinds', 'Datadog', 'Grafana', 'PRTG', 'Zabbix', 'Generic Webhook'].map((option) => (
                  <option key={option} value={option}>{option}</option>
                ))}
              </select>
            </Field>
            <Field label="Webhook endpoint">
              <input value={monitoringConfig.webhookEndpoint} onChange={(event) => onMonitoringConfigChange({ ...monitoringConfig, webhookEndpoint: event.target.value })} className="field-input" placeholder="https://acme.ecosys.app/api/integrations/webhooks/site-alerts" />
            </Field>
            <Field label="Secret / token indicator">
              <input value={monitoringConfig.secretStatus} onChange={(event) => onMonitoringConfigChange({ ...monitoringConfig, secretStatus: event.target.value })} className="field-input" placeholder="Generate on save" />
            </Field>
            <Field label="Event source mapping options">
              <textarea value={monitoringConfig.eventSourceMapping} onChange={(event) => onMonitoringConfigChange({ ...monitoringConfig, eventSourceMapping: event.target.value })} className="field-input min-h-[120px]" placeholder="Map alert name, severity, device, and host fields into protocol variables." />
            </Field>
            <Field label="Payload mapping JSON">
              <textarea value={monitoringConfig.payloadMappingJson} onChange={(event) => onMonitoringConfigChange({ ...monitoringConfig, payloadMappingJson: event.target.value })} className="field-input min-h-[120px]" placeholder='{ "title": "$.alertName", "severity": "$.severity" }' />
            </Field>
          </div>
        </div>
      )}
    </section>
  )
}

function Field({ label, hint, children }: { label: string; hint?: string; children: ReactNode }) {
  return (
    <label className="block space-y-2">
      <div className="flex items-center justify-between gap-3">
        <span className="text-sm font-medium text-app">{label}</span>
        {hint ? <span className="text-[11px] font-semibold uppercase tracking-[0.18em] text-muted">{hint}</span> : null}
      </div>
      {children}
    </label>
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
  onChange: (value: boolean) => void
}) {
  return (
    <label className="panel-subtle flex items-start justify-between gap-4 rounded-2xl px-4 py-3">
      <div>
        <p className="text-sm font-semibold text-app">{label}</p>
        <p className="mt-1 text-sm text-muted">{description}</p>
      </div>
      <input type="checkbox" checked={checked} onChange={(event) => onChange(event.target.checked)} />
    </label>
  )
}
