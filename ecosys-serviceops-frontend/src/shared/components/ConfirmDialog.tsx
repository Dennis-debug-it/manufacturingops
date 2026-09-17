import type { ReactNode } from 'react'
import { useEffect, useState } from 'react'
import { X } from 'lucide-react'
import { cn } from '../utils/cn'

export interface ConfirmDialogProps {
  open: boolean
  title: string
  message: string
  confirmLabel?: string
  cancelLabel?: string
  confirmText?: string
  onCancel: () => void
  onConfirm: () => void
  danger?: boolean
  icon?: ReactNode
  confirmDisabled?: boolean
  /** Extra content rendered between the message and the confirm text prompt (e.g. a reason field). */
  children?: ReactNode
}

export function ConfirmDialog({
  open,
  title,
  message,
  confirmLabel = 'Confirm',
  cancelLabel = 'Cancel',
  confirmText,
  onCancel,
  onConfirm,
  danger = false,
  icon,
  confirmDisabled = false,
  children,
}: ConfirmDialogProps) {
  const [typedValue, setTypedValue] = useState('')

  useEffect(() => {
    if (!open) {
      setTypedValue('')
    }
  }, [open])

  useEffect(() => {
    if (!open) return
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        onCancel()
      }
    }
    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  }, [open, onCancel])

  if (!open) {
    return null
  }

  const canConfirm = confirmText ? typedValue === confirmText : true

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center px-4 py-6">
      <button
        type="button"
        aria-label="Close dialog"
        className="absolute inset-0 bg-[var(--app-primary)]/30 backdrop-blur-[2px]"
        onClick={onCancel}
      />
      <div className="relative w-full max-w-lg rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white-strong)] p-6 shadow-xl backdrop-blur-[var(--glass-blur-lg)]">
        <div className="flex items-start justify-between gap-4">
          <div className="flex items-start gap-3">
            {icon}
            <div>
              <h3 className="text-lg font-semibold text-slate-900">{title}</h3>
              <p className="mt-1 text-sm leading-6 text-slate-600">{message}</p>
            </div>
          </div>
          <button type="button" onClick={onCancel} className="rounded-lg p-2 text-slate-500 transition hover:bg-slate-100 hover:text-slate-900">
            <X className="h-4 w-4" />
          </button>
        </div>
        {children}
        {confirmText ? (
          <div className="mt-4">
            <label className="mb-2 block text-sm font-medium text-slate-700">
              Type <span className="font-semibold text-slate-900">{confirmText}</span> to continue
            </label>
            <input
              value={typedValue}
              onChange={(event) => setTypedValue(event.target.value)}
              className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none transition focus:border-[var(--app-primary)]"
            />
          </div>
        ) : null}
        <div className="mt-6 flex items-center justify-end gap-2">
          <button type="button" onClick={onCancel} className="inline-flex h-11 items-center justify-center rounded-lg border border-[var(--app-border)] bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-[var(--app-primary-soft)]">
            {cancelLabel}
          </button>
          <button
            type="button"
            disabled={!canConfirm || confirmDisabled}
            onClick={onConfirm}
            className={cn(
              'inline-flex h-11 items-center justify-center rounded-lg px-4 text-sm font-medium text-white transition',
              danger ? 'bg-rose-600 hover:bg-rose-700 disabled:bg-rose-300' : 'bg-[var(--app-primary)] hover:bg-[var(--app-secondary)] disabled:bg-slate-300',
            )}
          >
            {confirmLabel}
          </button>
        </div>
      </div>
    </div>
  )
}
