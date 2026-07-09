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
    <article className="flex min-h-[112px] flex-col justify-between rounded-[14px] bg-[var(--app-surface-strong)] px-4 py-3">
      <div className="flex items-start justify-between gap-4">
        <div>
          <p className="text-secondary text-[11px] font-semibold uppercase tracking-[0.18em]">{title}</p>
          <p className="mt-2 font-heading text-[1.45rem] font-semibold tracking-[-0.03em] text-app sm:text-[1.65rem]">{value}</p>
        </div>
        <div className={`rounded-[14px] p-2 ${accentClass}`}>
          <Icon className="h-4 w-4" />
        </div>
      </div>
      <p className="mt-2 text-[0.85rem] leading-5 text-muted">{detail}</p>
    </article>
  )
}
