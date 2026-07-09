import type { ReactNode } from 'react'
import { ChevronRight } from 'lucide-react'
import { Link } from 'react-router-dom'
import { ResetLayoutButton } from '../widgets/WidgetToolbar'

export type WorkspaceCrumb = {
  label: string
  to?: string
}

export function WorkspaceHeader({
  crumbs,
  title,
  description,
  meta,
  actions,
  onResetLayout,
}: {
  crumbs: WorkspaceCrumb[]
  title: string
  description?: string
  meta?: ReactNode
  actions?: ReactNode
  onResetLayout?: () => void
}) {
  return (
    <header className="workspace-header-panel space-y-2.5">
      <div className="flex flex-wrap items-center justify-between gap-2.5">
        <nav aria-label="Breadcrumbs" className="flex min-w-0 items-center gap-1.5 text-[11px] font-medium text-muted">
          {crumbs.map((crumb, index) => {
            const isLast = index === crumbs.length - 1
            return (
              <div key={`${crumb.label}-${index}`} className="flex min-w-0 items-center gap-1.5">
                {index > 0 ? <ChevronRight className="h-3.5 w-3.5 opacity-60" /> : null}
                {crumb.to && !isLast ? (
                  <Link to={crumb.to} className="truncate transition hover:text-app">
                    {crumb.label}
                  </Link>
                ) : (
                  <span className={`truncate ${isLast ? 'text-app' : ''}`}>{crumb.label}</span>
                )}
              </div>
            )
          })}
        </nav>

        <div className="flex flex-wrap items-center gap-2">
          {onResetLayout ? (
            <ResetLayoutButton onClick={onResetLayout} />
          ) : null}
          {actions}
        </div>
      </div>

      <div className="flex flex-col gap-2 lg:flex-row lg:items-start lg:justify-between">
        <div className="min-w-0">
          <h1 className="font-heading text-[1.45rem] font-semibold tracking-[-0.04em] text-app sm:text-[1.7rem]">{title}</h1>
          {description ? <p className="mt-1 max-w-4xl text-sm leading-5 text-muted">{description}</p> : null}
        </div>
        {meta ? <div className="flex flex-wrap items-center gap-2 lg:justify-end">{meta}</div> : null}
      </div>
    </header>
  )
}
