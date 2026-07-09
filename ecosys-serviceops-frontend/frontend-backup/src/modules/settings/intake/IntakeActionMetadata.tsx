import type { ReactNode } from 'react'
import type { MetadataActionConfig } from './types'

export function IntakeActionMetadata({
  config,
  onChange,
}: {
  config: MetadataActionConfig
  onChange: (value: MetadataActionConfig) => void
}) {
  return (
    <div className="mt-4 space-y-4 rounded-[28px] border border-app bg-white/5 p-4">
      <div>
        <p className="text-sm font-semibold text-app">Metadata capture</p>
        <p className="mt-1 text-sm text-muted">Preserve the source trail and matched rule context alongside the generated work order.</p>
      </div>
      <Field label="Add labels / tags">
        <input value={config.labels} onChange={(event) => onChange({ ...config, labels: event.target.value })} className="field-input" placeholder="critical, monitoring, auto-intake" />
      </Field>
      <div className="grid gap-3 md:grid-cols-2">
        <ToggleRow label="Store sender email" checked={config.storeSenderEmail} onChange={(value) => onChange({ ...config, storeSenderEmail: value })} />
        <ToggleRow label="Store source channel" checked={config.storeSourceChannel} onChange={(value) => onChange({ ...config, storeSourceChannel: value })} />
        <ToggleRow label="Store matched rule name" checked={config.storeMatchedRuleName} onChange={(value) => onChange({ ...config, storeMatchedRuleName: value })} />
        <ToggleRow label="Store severity" checked={config.storeSeverity} onChange={(value) => onChange({ ...config, storeSeverity: value })} />
        <ToggleRow label="Store raw payload reference" checked={config.storeRawPayloadReference} onChange={(value) => onChange({ ...config, storeRawPayloadReference: value })} />
        <ToggleRow label="Attach original message body" checked={config.attachOriginalMessageBody} onChange={(value) => onChange({ ...config, attachOriginalMessageBody: value })} />
        <ToggleRow label="Attach original monitoring JSON" checked={config.attachOriginalMonitoringJson} onChange={(value) => onChange({ ...config, attachOriginalMonitoringJson: value })} />
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

function ToggleRow({
  label,
  checked,
  onChange,
}: {
  label: string
  checked: boolean
  onChange: (value: boolean) => void
}) {
  return (
    <label className="panel-subtle flex items-center justify-between rounded-2xl px-4 py-3">
      <span className="text-sm text-app">{label}</span>
      <input type="checkbox" checked={checked} onChange={(event) => onChange(event.target.checked)} />
    </label>
  )
}
