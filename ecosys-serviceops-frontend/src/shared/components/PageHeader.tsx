import type { ReactNode } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { ArrowLeft } from 'lucide-react'
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
  /** Rendered inline next to the title — e.g. a status badge. Keeps state out of the action row. */
  titleAdornment?: ReactNode
  className?: string
}

export function PageHeader({ title, subtitle, breadcrumbs, primaryAction, secondaryActions, titleAdornment, className }: PageHeaderProps) {
  const navigate = useNavigate()
  const parentCrumb = breadcrumbs?.slice().reverse().find((crumb) => crumb.to)

  const goBack = () => {
    if (window.history.length > 1) {
      navigate(-1)
    } else if (parentCrumb?.to) {
      navigate(parentCrumb.to)
    }
  }

  return (
    <header className={cn('space-y-4', className)}>
      {breadcrumbs && breadcrumbs.length > 0 ? (
        <nav className="flex flex-wrap items-center gap-2 text-xs text-slate-500">
          <button
            type="button"
            onClick={goBack}
            className="inline-flex h-7 items-center gap-1 rounded-lg border border-slate-200 bg-white px-2 font-medium text-slate-600 transition hover:bg-slate-50 hover:text-slate-900"
            aria-label="Go back"
          >
            <ArrowLeft className="h-3.5 w-3.5" />
            Back
          </button>
          <span className="text-slate-300">|</span>
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
          <div className="flex flex-wrap items-center gap-3">
            <h1 className="text-2xl font-semibold tracking-tight text-slate-900 sm:text-3xl">{title}</h1>
            {titleAdornment}
          </div>
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
