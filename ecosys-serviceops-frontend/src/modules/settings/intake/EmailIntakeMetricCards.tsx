import { Badge } from '../../../components/ui/Badge'
import type { EmailIntakeMetric } from './emailIntakeModels'

const toneToBadge: Record<EmailIntakeMetric['tone'], 'neutral' | 'success' | 'warning' | 'danger' | 'info'> = {
  neutral: 'neutral',
  success: 'success',
  warning: 'warning',
  danger: 'danger',
  info: 'info',
}

export function EmailIntakeMetricCards({ metrics }: { metrics: EmailIntakeMetric[] }) {
  return (
    <section data-testid="email-intake-summary-cards" className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
      {metrics.map((metric) => (
        <article key={metric.id} className="surface-card">
          <div className="flex items-start justify-between gap-3">
            <p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted">{metric.label}</p>
            <Badge tone={toneToBadge[metric.tone]}>{metric.tone}</Badge>
          </div>
          <p className="mt-3 text-lg font-semibold text-app">{metric.value}</p>
        </article>
      ))}
    </section>
  )
}
