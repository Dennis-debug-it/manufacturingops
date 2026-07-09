import type { ReactNode } from 'react'
import { ActionBar } from './ActionBar'

type PageHeaderMetaItem = {
  label?: string
  value: ReactNode
}

export function PageHeader({
  eyebrow,
  title,
  description,
  subtitle,
  meta,
  statusBadges,
  primaryAction,
  secondaryActions,
  actions,
}: {
  eyebrow?: string
  title: string
  description?: string
  subtitle?: ReactNode
  meta?: PageHeaderMetaItem[]
  statusBadges?: ReactNode
  primaryAction?: ReactNode
  secondaryActions?: ReactNode
  actions?: ReactNode
}) {
  const supportingCopy = subtitle ?? description
  const hasStructuredActions = Boolean(primaryAction || secondaryActions)

  return (
    <header className="space-y-2">
      <div className="flex flex-col gap-2 lg:flex-row lg:items-start lg:justify-between">
        <div className="min-w-0 flex-1">
          {eyebrow ? (
            <p className="eyebrow-accent text-[11px] font-semibold uppercase tracking-[0.22em]">{eyebrow}</p>
          ) : null}

          <div className="mt-1.5 flex flex-col gap-2 xl:flex-row xl:items-start xl:justify-between">
            <div className="min-w-0">
              <h1 className="font-heading text-[1.55rem] font-semibold tracking-[-0.04em] text-app sm:text-[1.8rem]">{title}</h1>
              {supportingCopy ? <div className="mt-1 max-w-3xl text-sm leading-5 text-muted">{supportingCopy}</div> : null}
            </div>

            {statusBadges ? <div className="flex flex-wrap items-center gap-2 xl:justify-end">{statusBadges}</div> : null}
          </div>

          {meta && meta.length > 0 ? (
            <dl className="mt-2 flex flex-wrap items-center gap-x-3 gap-y-1">
              {meta.map((item, index) => (
                <div key={`${item.label ?? 'meta'}-${index}`} className="flex min-w-0 items-center gap-2 text-sm text-secondary">
                  {item.label ? <dt className="text-muted">{item.label}</dt> : null}
                  <dd className="min-w-0 font-medium text-app">{item.value}</dd>
                </div>
              ))}
            </dl>
          ) : null}
        </div>

        {hasStructuredActions ? (
          <ActionBar primaryAction={primaryAction} secondaryActions={secondaryActions} className="lg:max-w-[30rem] lg:justify-end" />
        ) : actions ? (
          <div className="flex w-full flex-wrap items-stretch gap-2 lg:w-auto lg:justify-end">{actions}</div>
        ) : null}
      </div>
    </header>
  )
}
