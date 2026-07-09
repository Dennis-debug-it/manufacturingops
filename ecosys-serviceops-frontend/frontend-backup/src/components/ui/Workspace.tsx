import type { ComponentPropsWithoutRef, ReactNode } from 'react'
import { ActionBar } from './ActionBar'
import { Badge } from './Badge'
import { DetailRows, type DescriptionListItem } from './DescriptionList'
import { DataTableWrapper } from './DataTableWrapper'
import { ErrorState } from './ErrorState'
import { EmptyState } from './EmptyState'
import { PageHeader } from './PageHeader'

export function PageScaffold({
  eyebrow,
  title,
  description,
  actions,
  children,
}: {
  eyebrow?: string
  title: string
  description: string
  actions?: ReactNode
  children: ReactNode
}) {
  return (
    <div className="page-transition space-y-3">
      <PageHeader eyebrow={eyebrow} title={title} description={description} actions={actions} />
      {children}
    </div>
  )
}

export function SectionCard({
  title,
  description,
  action,
  children,
  className = '',
  ...sectionProps
}: {
  title: string
  description?: string
  action?: ReactNode
  children: ReactNode
  className?: string
} & ComponentPropsWithoutRef<'section'>) {
  return (
    <section className={`section-panel space-y-2 ${className}`.trim()} {...sectionProps}>
      <div className="section-card-header flex flex-wrap items-start justify-between gap-2">
        <div className="min-w-0">
          <h2 className="font-heading text-[0.98rem] font-semibold tracking-[-0.02em] text-app sm:text-[1.04rem]">{title}</h2>
          {description ? <p className="mt-0.5 max-w-3xl text-[0.85rem] leading-5 text-muted">{description}</p> : null}
        </div>
        {action ? <div className="flex flex-wrap gap-1.5">{action}</div> : null}
      </div>
      <div>{children}</div>
    </section>
  )
}

export function SettingsCard(props: Parameters<typeof SectionCard>[0]) {
  return <SectionCard {...props} className={`settings-card ${props.className ?? ''}`.trim()} />
}

export function SectionPanel(props: Parameters<typeof SectionCard>[0]) {
  return <SectionCard {...props} />
}

export function SettingsSection({
  title,
  description,
  actions,
  children,
  className = '',
}: {
  title: string
  description?: string
  actions?: ReactNode
  children: ReactNode
  className?: string
}) {
  return (
    <section className={`border-t border-[var(--app-divider)] py-5 first:border-t-0 first:pt-0 ${className}`.trim()}>
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <h2 className="font-heading text-[0.98rem] font-semibold tracking-[-0.02em] text-app sm:text-[1.04rem]">{title}</h2>
          {description ? <p className="mt-1 max-w-3xl text-[0.85rem] leading-5 text-muted">{description}</p> : null}
        </div>
        {actions ? <div className="flex flex-wrap items-center gap-2">{actions}</div> : null}
      </div>
      <div className="mt-4">{children}</div>
    </section>
  )
}

export function FormSection({
  title,
  description,
  children,
  columns = 2,
}: {
  title: string
  description?: string
  children: ReactNode
  columns?: 1 | 2 | 3
}) {
  const gridClass =
    columns === 1 ? 'grid-cols-1' : columns === 3 ? 'grid-cols-1 md:grid-cols-2 xl:grid-cols-3' : 'grid-cols-1 md:grid-cols-2'

  return (
    <div className="section-panel space-y-2">
      <div className="section-card-header">
        <h3 className="font-heading text-[0.98rem] font-semibold text-app sm:text-[1.04rem]">{title}</h3>
        {description ? <p className="mt-0.5 text-[0.85rem] leading-5 text-muted">{description}</p> : null}
      </div>
      <div className={`grid gap-2.5 ${gridClass}`}>{children}</div>
    </div>
  )
}

export function PageTabs<T extends string>({
  tabs,
  activeTab,
  onChange,
}: {
  tabs: Array<{ id: T; label: string }>
  activeTab: T
  onChange: (next: T) => void
}) {
  return (
    <div className="page-tabs flex flex-wrap gap-1.5">
      {tabs.map((tab) => (
        <button
          key={tab.id}
          type="button"
          onClick={() => onChange(tab.id)}
          className={`page-tab ${activeTab === tab.id ? 'page-tab-active' : ''}`}
        >
          {tab.label}
        </button>
      ))}
    </div>
  )
}

export function StickyActionFooter({
  children,
  className = '',
}: {
  children: ReactNode
  className?: string
}) {
  return <div className={`sticky-action-footer ${className}`.trim()}>{children}</div>
}

export function SearchToolbar({
  searchSlot,
  filters,
  actions,
}: {
  searchSlot?: ReactNode
  filters?: ReactNode
  actions?: ReactNode
}) {
  return (
    <div className="space-y-2">
      <div className="grid gap-2 xl:grid-cols-[minmax(0,1.8fr)_minmax(0,1fr)_auto] xl:items-end">
        <div className="min-w-0">{searchSlot}</div>
        <div className="grid gap-2 sm:grid-cols-2 xl:grid-cols-3">{filters}</div>
        {actions ? <div className="flex flex-wrap gap-2 xl:justify-end">{actions}</div> : null}
      </div>
    </div>
  )
}

export function MetricGrid({
  children,
  className = '',
}: {
  children: ReactNode
  className?: string
}) {
  return <div className={`grid gap-2 sm:grid-cols-2 xl:grid-cols-4 ${className}`.trim()}>{children}</div>
}

export function MetricCard({
  label,
  value,
  meta,
  emphasis = 'default',
}: {
  label: string
  value: ReactNode
  meta?: ReactNode
  emphasis?: 'default' | 'accent' | 'warning' | 'danger'
}) {
  const emphasisClass =
    emphasis === 'accent'
      ? 'bg-[color:color-mix(in_srgb,var(--app-primary-soft)_86%,white)]'
      : emphasis === 'warning'
        ? 'bg-[color:color-mix(in_srgb,var(--app-warning)_10%,white)]'
        : emphasis === 'danger'
          ? 'bg-[color:color-mix(in_srgb,var(--app-danger)_9%,white)]'
          : 'bg-[var(--app-surface-strong)]'

  return (
    <div className={`rounded-[14px] px-3 py-2.5 ${emphasisClass}`}>
      <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-muted">{label}</p>
      <div className="mt-1.5 text-[1.5rem] font-semibold tracking-[-0.03em] text-app sm:text-[1.65rem]">{value}</div>
      {meta ? <div className="mt-1 text-[0.85rem] leading-5 text-muted">{meta}</div> : null}
    </div>
  )
}

export function SegmentedTabs<T extends string>({
  tabs,
  activeTab,
  onChange,
}: {
  tabs: Array<{ id: T; label: string; meta?: ReactNode }>
  activeTab: T
  onChange: (next: T) => void
}) {
  return (
    <div className="flex flex-wrap gap-2">
      {tabs.map((tab) => (
        <button
          key={tab.id}
          type="button"
          onClick={() => onChange(tab.id)}
          className={`inline-flex min-h-[40px] items-center gap-2 rounded-full border px-3.5 py-2 text-sm font-semibold transition ${
            activeTab === tab.id
              ? 'border-transparent bg-[color:color-mix(in_srgb,var(--app-primary-soft)_82%,white)] text-app'
              : 'border-transparent bg-[var(--app-surface)] text-muted hover:bg-[var(--app-subtle-hover)] hover:text-app'
          }`}
        >
          <span>{tab.label}</span>
          {tab.meta ? <span className="rounded-full bg-black/5 px-2 py-0.5 text-[11px] uppercase tracking-[0.12em]">{tab.meta}</span> : null}
        </button>
      ))}
    </div>
  )
}

export function PageTransition({ children }: { children: ReactNode }) {
  return <div className="page-transition">{children}</div>
}

export function ConfirmationModal({
  title,
  description,
  children,
}: {
  title: string
  description: string
  children: ReactNode
}) {
  return (
    <div className="space-y-3">
      <div>
        <h3 className="font-heading text-lg font-semibold text-app">{title}</h3>
        <p className="mt-1 text-sm text-muted">{description}</p>
      </div>
      {children}
    </div>
  )
}

export function StatusBadge({
  tone,
  children,
  className = '',
}: {
  tone?: Parameters<typeof Badge>[0]['tone']
  children: ReactNode
  className?: string
}) {
  return (
    <Badge tone={tone} className={`status-badge ${className}`.trim()}>
      {children}
    </Badge>
  )
}

export { ActionBar, DataTableWrapper, DetailRows }
export type { DescriptionListItem }
export { EmptyState, ErrorState }
