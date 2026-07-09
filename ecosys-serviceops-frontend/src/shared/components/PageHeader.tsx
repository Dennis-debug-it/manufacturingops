import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { cn } from '../utils/cn'

export interface BreadcrumbItem {
  label: string
  to?: string
}

export interface PageHeaderProps {
  title: string
  subtitle?: string | undefined
  breadcrumbs?: BreadcrumbItem[] | undefined
  primaryAction?: ReactNode
  secondaryActions?: ReactNode
  className?: string
}

export function PageHeader({ title, subtitle, breadcrumbs, primaryAction, secondaryActions, className }: PageHeaderProps) {
  return (
    <header className={cn('space-y-4', className)}>
      {breadcrumbs && breadcrumbs.length > 0 ? (
        <nav className="flex flex-wrap items-center gap-2 text-xs text-slate-500">
          {breadcrumbs.map((crumb, index) => (
            <span key={`${crumb.label}-${index}`} className="flex items-center gap-2">
              {crumb.to ? (
                <Link to={crumb.to} className="font-medium text-slate-600 hover:text-slate-900">
                  {crumb.label}
                </Link>
              ) : (
                <span>{crumb.label}</span>
              )}
              {index < breadcrumbs.length - 1 ? <span>/</span> : null}
            </span>
          ))}
        </nav>
      ) : null}
      <div className="flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
        <div className="space-y-2">
          <h1 className="text-2xl font-semibold tracking-tight text-slate-900 sm:text-3xl">{title}</h1>
          {subtitle ? <p className="max-w-3xl text-sm leading-6 text-slate-600">{subtitle}</p> : null}
        </div>
        <div className="flex flex-wrap items-center gap-2">
          {secondaryActions}
          {primaryAction}
        </div>
      </div>
    </header>
  )
}
