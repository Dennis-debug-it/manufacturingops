import type { ReactNode } from 'react'
import { X } from 'lucide-react'
import { cn } from '../utils/cn'

export interface FormDrawerProps {
  open: boolean
  title: string
  children: ReactNode
  footer?: ReactNode
  onClose: () => void
  closeOnOverlayClick?: boolean
}

export function FormDrawer({ open, title, children, footer, onClose, closeOnOverlayClick = true }: FormDrawerProps) {
  if (!open) {
    return null
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center overflow-y-auto bg-slate-900/40 p-4 sm:p-6">
      <button
        type="button"
        aria-label="Close drawer"
        className="absolute inset-0"
        onClick={closeOnOverlayClick ? onClose : undefined}
      />
      <aside className="relative flex max-h-[calc(100vh-2rem)] w-full max-w-2xl flex-col overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-2xl sm:max-h-[calc(100vh-3rem)]">
        <header className="flex items-center justify-between border-b border-slate-200 px-5 py-4">
          <div>
            <h3 className="text-lg font-semibold text-slate-900">{title}</h3>
          </div>
          <button type="button" onClick={onClose} className="rounded-lg p-2 text-slate-500 transition hover:bg-slate-100 hover:text-slate-900">
            <X className="h-4 w-4" />
          </button>
        </header>
        <div className={cn('min-h-0 flex-1 overflow-y-auto px-5 py-5')}>{children}</div>
        {footer ? <footer className="border-t border-slate-200 px-5 py-4">{footer}</footer> : null}
      </aside>
    </div>
  )
}
