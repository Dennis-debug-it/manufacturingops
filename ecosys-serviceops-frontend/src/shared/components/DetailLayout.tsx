import type { ReactNode } from 'react'
import { useMemo } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { cn } from '../utils/cn'

export interface DetailTab {
  id: string
  label: string
  content: ReactNode
}

export interface DetailLayoutProps {
  header: ReactNode
  tabs: DetailTab[]
  defaultTabId?: string
  className?: string
  sidebar?: ReactNode
}

export function DetailLayout({ header, tabs, defaultTabId, className, sidebar }: DetailLayoutProps) {
  const [searchParams, setSearchParams] = useSearchParams()
  const activeTab = searchParams.get('tab') ?? defaultTabId ?? tabs[0]?.id ?? ''

  const currentTab = useMemo(() => tabs.find((tab) => tab.id === activeTab) ?? tabs[0], [activeTab, tabs])

  if (!currentTab) {
    return null
  }

  const body = (
    <div className="min-w-0 flex-1 space-y-4">
      <div className="border-b border-[var(--app-border)]">
        <nav className="flex flex-wrap gap-2">
          {tabs.map((tab) => {
            const selected = tab.id === currentTab.id
            return (
              <button
                key={tab.id}
                type="button"
                onClick={() => setSearchParams({ tab: tab.id }, { replace: true })}
                className={cn(
                  'inline-flex h-11 items-center rounded-t-lg border-b-2 px-4 text-sm font-medium transition',
                  selected
                    ? 'border-[var(--app-lime)] text-[var(--app-primary)]'
                    : 'border-transparent text-slate-500 hover:border-slate-200 hover:text-slate-900',
                )}
              >
                {tab.label}
              </button>
            )
          })}
        </nav>
      </div>
      <div>{currentTab.content}</div>
    </div>
  )

  return (
    <div className={cn('space-y-4', className)}>
      {header}
      {sidebar ? (
        <div className="flex flex-col gap-4 lg:flex-row lg:items-start">
          {body}
          <aside className="w-full shrink-0 space-y-4 lg:w-80">{sidebar}</aside>
        </div>
      ) : (
        body
      )}
    </div>
  )
}

export interface DetailLinkProps {
  to: string
  children: ReactNode
}

export function DetailLink({ to, children }: DetailLinkProps) {
  return (
    <Link to={to} className="font-medium text-slate-900 underline decoration-slate-300 underline-offset-4 transition hover:decoration-slate-900">
      {children}
    </Link>
  )
}
