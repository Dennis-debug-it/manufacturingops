import { useState, type ReactNode } from 'react'
import { ChevronDown } from 'lucide-react'
import { cn } from '../utils/cn'

export interface CollapsibleProps {
  title: string
  defaultOpen?: boolean
  action?: ReactNode
  children: ReactNode
}

export function Collapsible({ title, defaultOpen = false, action, children }: CollapsibleProps) {
  const [open, setOpen] = useState(defaultOpen)
  return (
    <div className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] shadow-sm">
      <div className="flex items-center justify-between px-5 py-4">
        <button type="button" onClick={() => setOpen((value) => !value)} className="flex flex-1 items-center justify-between text-left">
          <span className="text-sm font-semibold text-slate-900">{title}</span>
          <ChevronDown className={cn('h-4 w-4 text-slate-400 transition-transform', open && 'rotate-180')} />
        </button>
        {action}
      </div>
      {open ? <div className="border-t border-slate-100 px-5 py-4">{children}</div> : null}
    </div>
  )
}
