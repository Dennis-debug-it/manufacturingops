import type { LucideIcon } from 'lucide-react'

export function StatCard({
  title,
  value,
  detail,
  icon: Icon,
  accent = 'cyan',
}: {
  title: string
  value: string
  detail: string
  icon: LucideIcon
  accent?: 'cyan' | 'emerald' | 'amber' | 'rose'
}) {
  const accentClass =
    accent === 'emerald'
      ? 'icon-emerald'
      : accent === 'amber'
        ? 'icon-amber'
        : accent === 'rose'
          ? 'icon-rose'
          : 'icon-accent'

  return (
    <article className="surface-card flex min-h-[152px] flex-col justify-between">
      <div className="flex items-start justify-between gap-4">
        <div>
          <p className="text-secondary text-[11px] font-semibold uppercase tracking-[0.18em]">{title}</p>
          <p className="mt-4 font-heading text-[2rem] font-semibold tracking-[-0.03em] text-app">{value}</p>
        </div>
        <div className={`rounded-[14px] p-3 ${accentClass}`}>
          <Icon className="h-5 w-5" />
        </div>
      </div>
      <p className="mt-4 text-sm leading-6 text-muted">{detail}</p>
    </article>
  )
}
