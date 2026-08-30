import type { KeyboardEvent as ReactKeyboardEvent, ReactNode } from 'react'
import { useCallback, useEffect, useId, useRef, useState } from 'react'
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

const FOCUSABLE_SELECTOR = [
  'a[href]',
  'button:not([disabled])',
  'input:not([disabled]):not([type="hidden"])',
  'select:not([disabled])',
  'textarea:not([disabled])',
  '[tabindex]:not([tabindex="-1"])',
].join(',')

/** Tabbable descendants, in DOM order, skipping anything currently not rendered. */
function focusableWithin(root: HTMLElement): HTMLElement[] {
  return Array.from(root.querySelectorAll<HTMLElement>(FOCUSABLE_SELECTOR)).filter(
    (element) => element.getClientRects().length > 0,
  )
}

/**
 * A modal side drawer.
 *
 * It is a real dialog: `role="dialog"` + `aria-modal`, focus moves into it on open and returns to
 * whatever opened it on close, Tab cycles inside it rather than wandering into the page behind,
 * and Escape dismisses it. Without those, a keyboard or screen-reader user could tab straight out
 * of an "open" drawer into the page underneath and never be told the drawer was there.
 *
 * Escape and the overlay are both easy to hit by accident, and every drawer in this app is a form
 * whose contents are lost on close. So a dismissal that would discard typed input asks first —
 * tracked by watching for input/change events inside the drawer, which means it works for all
 * call sites without any of them opting in. A drawer the user has not touched closes immediately.
 * Note this guards only the dismissal paths this component owns: a caller that invokes `onClose`
 * itself (a Cancel button, or a successful save) closes the drawer directly, as it should.
 */
export function FormDrawer({ open, title, children, footer, onClose, closeOnOverlayClick = true }: FormDrawerProps) {
  const titleId = useId()
  const drawerRef = useRef<HTMLElement | null>(null)
  const previouslyFocusedRef = useRef<HTMLElement | null>(null)
  const keepEditingRef = useRef<HTMLButtonElement | null>(null)
  const [dirty, setDirty] = useState(false)
  const [confirmingDiscard, setConfirmingDiscard] = useState(false)

  // A fresh open is a fresh form: nothing typed yet, and no discard prompt carried over.
  useEffect(() => {
    if (open) {
      setDirty(false)
      setConfirmingDiscard(false)
    }
  }, [open])

  useEffect(() => {
    if (!open) {
      return
    }

    previouslyFocusedRef.current = document.activeElement instanceof HTMLElement ? document.activeElement : null
    // Focus the drawer itself rather than its first field: assistive tech announces the dialog and
    // its title, and no on-screen keyboard is summoned before the user has asked for one.
    drawerRef.current?.focus()

    return () => {
      const previous = previouslyFocusedRef.current
      if (previous && document.contains(previous)) {
        previous.focus()
      }
      previouslyFocusedRef.current = null
    }
  }, [open])

  useEffect(() => {
    if (confirmingDiscard) {
      keepEditingRef.current?.focus()
    }
  }, [confirmingDiscard])

  /** Dismissal the drawer itself initiated — guarded by the unsaved-input check. */
  const requestClose = useCallback(() => {
    if (dirty) {
      setConfirmingDiscard(true)
      return
    }
    onClose()
  }, [dirty, onClose])

  const handleKeyDown = (event: ReactKeyboardEvent<HTMLDivElement>) => {
    if (event.key === 'Escape') {
      event.stopPropagation()
      if (confirmingDiscard) {
        setConfirmingDiscard(false)
        return
      }
      requestClose()
      return
    }

    if (event.key !== 'Tab') {
      return
    }

    const node = drawerRef.current
    if (!node) {
      return
    }

    const focusable = focusableWithin(node)
    if (focusable.length === 0) {
      event.preventDefault()
      node.focus()
      return
    }

    const first = focusable[0]
    const last = focusable[focusable.length - 1]
    const active = document.activeElement

    if (event.shiftKey) {
      if (active === first || active === node || !node.contains(active)) {
        event.preventDefault()
        last?.focus()
      }
      return
    }

    if (active === last || !node.contains(active)) {
      event.preventDefault()
      first?.focus()
    }
  }

  if (!open) {
    return null
  }

  return (
    // The keydown handler sits on the container rather than on `window` so it scopes to this
    // drawer: a window listener would also fire for a dialog stacked above it.
    <div className="fixed inset-0 z-50 flex" onKeyDown={handleKeyDown}>
      {/*
        Mouse affordance only: `tabIndex={-1}` keeps it out of the tab cycle, because the header's
        close button is the keyboard equivalent and two "close" stops would just be noise.
      */}
      <button
        type="button"
        tabIndex={-1}
        aria-label="Close drawer"
        className="absolute inset-0 bg-[var(--app-primary)]/30 backdrop-blur-[2px]"
        onClick={closeOnOverlayClick ? requestClose : undefined}
      />
      <aside
        ref={drawerRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        tabIndex={-1}
        className="relative ml-auto flex h-full w-full max-w-2xl flex-col border-l border-[var(--app-border)] bg-white shadow-2xl outline-none md:w-[32rem]"
      >
        <header className="flex items-center justify-between border-b border-[var(--app-border)] px-5 py-4">
          <div>
            <h3 id={titleId} className="text-lg font-semibold text-slate-900">{title}</h3>
          </div>
          <button type="button" onClick={requestClose} className="rounded-lg p-2 text-slate-500 transition hover:bg-slate-100 hover:text-slate-900">
            <X className="h-4 w-4" aria-hidden="true" />
            <span className="sr-only">Close</span>
          </button>
        </header>
        {confirmingDiscard ? (
          <div role="alert" className="flex flex-wrap items-center gap-3 border-b border-amber-200 bg-amber-50 px-5 py-3 text-sm text-amber-900">
            <span className="flex-1">You have unsaved changes. Close this drawer and discard them?</span>
            <button
              ref={keepEditingRef}
              type="button"
              onClick={() => setConfirmingDiscard(false)}
              className="inline-flex h-9 items-center justify-center rounded-lg border border-amber-300 bg-white px-3 text-sm font-medium text-amber-900 transition hover:bg-amber-100"
            >
              Keep editing
            </button>
            <button
              type="button"
              onClick={onClose}
              className="inline-flex h-9 items-center justify-center rounded-lg bg-rose-600 px-3 text-sm font-medium text-white transition hover:bg-rose-700"
            >
              Discard
            </button>
          </div>
        ) : null}
        <div
          className={cn('min-h-0 flex-1 overflow-y-auto px-5 py-5')}
          onInput={() => setDirty(true)}
          onChange={() => setDirty(true)}
        >
          {children}
        </div>
        {footer ? <footer className="border-t border-[var(--app-border)] px-5 py-4">{footer}</footer> : null}
      </aside>
    </div>
  )
}
