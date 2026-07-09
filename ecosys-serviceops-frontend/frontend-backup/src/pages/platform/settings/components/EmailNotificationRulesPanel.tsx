import { ErrorState } from '../../../../components/ui/ErrorState'
import { LoadingState } from '../../../../components/ui/LoadingState'
import { useAsyncData } from '../../../../hooks/useAsyncData'
import { platformSettingsService } from '../../../../services/platformSettingsService'

const statusTone: Record<string, string> = {
  Active: 'bg-emerald-500/15 text-emerald-700',
  PendingHook: 'bg-amber-500/15 text-amber-700',
  Disabled: 'bg-slate-500/15 text-slate-700',
}

export function EmailNotificationRulesPanel() {
  const { data, loading, error } = useAsyncData(() => platformSettingsService.listNotificationRules(), [], [])

  if (loading) return <LoadingState label="Loading notification rules" />
  if (error) return <ErrorState title="Unable to load notification rules" description={error} />

  return (
    <section className="space-y-4">
      <div className="rounded-[28px] border border-app bg-app/40 p-4">
        <p className="text-sm text-muted">Some notification categories are configurable now, while automated dispatch hooks will be activated as each workflow is completed.</p>
      </div>

      <div className="space-y-3">
        {data.map((rule) => (
          <article key={rule.eventKey} className="rounded-[28px] border border-app bg-[var(--app-card)] p-4">
            <div className="flex flex-wrap items-start justify-between gap-3">
              <div>
                <p className="text-base font-semibold text-app">{rule.displayName}</p>
                <p className="mt-1 font-mono text-xs uppercase tracking-[0.14em] text-muted">{rule.eventKey}</p>
              </div>
              <span className={`rounded-full px-3 py-1 text-xs font-semibold ${statusTone[rule.dispatchStatus] || statusTone.Disabled}`}>
                {rule.dispatchStatus}
              </span>
            </div>

            <div className="mt-4 grid gap-3 md:grid-cols-2 xl:grid-cols-4">
              <RuleMeta label="Template" value={rule.templateKey} mono />
              <RuleMeta label="Recipient" value={rule.recipientStrategy} />
              <RuleMeta label="Sender" value={rule.senderScope} />
              <RuleMeta label="Channels" value={formatChannels(rule.supportedChannels)} />
            </div>

            <p className="mt-4 text-sm text-app">{rule.description}</p>
            <p className="mt-2 text-xs text-muted">{rule.notes}</p>
          </article>
        ))}
      </div>
    </section>
  )
}

function RuleMeta({ label, value, mono = false }: { label: string; value: string; mono?: boolean }) {
  return (
    <div className="rounded-2xl border border-app bg-app/40 px-3 py-3">
      <p className="text-[11px] uppercase tracking-[0.16em] text-muted">{label}</p>
      <p className={`mt-2 text-sm text-app ${mono ? 'font-mono' : ''}`}>{value}</p>
    </div>
  )
}

function formatChannels(channels: string[]) {
  return Array.from(new Set(channels)).join(' | ')
}
