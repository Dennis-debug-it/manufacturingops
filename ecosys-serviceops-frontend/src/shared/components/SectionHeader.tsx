import type { ReactNode } from 'react'

export interface SectionHeaderProps {
  title: string
  description?: string | undefined
  action?: ReactNode
}

export function SectionHeader({ title, description, action }: SectionHeaderProps) {
  return (
    <div className="flex flex-col gap-3 border-b border-[var(--app-border)] pb-4 sm:flex-row sm:items-start sm:justify-between">
      <div>
        <h2 className="text-base font-semibold text-slate-900">{title}</h2>
        {description ? <p className="mt-1 text-sm leading-6 text-slate-600">{description}</p> : null}
      </div>
      {action ? <div>{action}</div> : null}
    </div>
  )
}
