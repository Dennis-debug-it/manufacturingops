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
}

export function DetailLayout({ header, tabs, defaultTabId, className }: DetailLayoutProps) {
  const [searchParams, setSearchParams] = useSearchParams()
  const activeTab = searchParams.get('tab') ?? defaultTabId ?? tabs[0]?.id ?? ''

  const currentTab = useMemo(() => tabs.find((tab) => tab.id === activeTab) ?? tabs[0], [activeTab, tabs])

  if (!currentTab) {
    return null
  }

  return (
    <div className={cn('space-y-6', className)}>
      {header}
      <div className="border-b border-slate-200">
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
                    ? 'border-slate-900 text-slate-900'
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
