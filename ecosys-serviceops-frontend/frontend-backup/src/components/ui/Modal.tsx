import { useEffect } from 'react'
import type { ReactNode } from 'react'
import { X } from 'lucide-react'
import { UI_RESET_EVENT, cleanupBodyInteractivity } from '../../utils/appCleanup'

export function Modal({
  open,
  title,
  description,
  onClose,
  children,
  maxWidth = 'max-w-3xl',
}: {
  open: boolean
  title: string
  description: string
  onClose: () => void
  children: ReactNode
  maxWidth?: string
}) {
  useEffect(() => {
    if (!open) {
      cleanupBodyInteractivity()
      return
    }

    const { body } = document
    const previousOverflow = body.style.overflow
    body.style.overflow = 'hidden'

    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        onClose()
      }
    }

    const handleUiReset = () => {
      onClose()
    }

    window.addEventListener('keydown', handleKeyDown)
    window.addEventListener(UI_RESET_EVENT, handleUiReset)
    return () => {
      cleanupBodyInteractivity()
      body.style.overflow = previousOverflow
      window.removeEventListener('keydown', handleKeyDown)
      window.removeEventListener(UI_RESET_EVENT, handleUiReset)
    }
  }, [open, onClose])

  if (!open) {
    return null
  }

  return (
    <div data-ui-overlay="true" className="fixed inset-0 z-50 flex items-center justify-center bg-overlay/90 p-4" onClick={onClose}>
      <div className={`glass-panel flex w-[95vw] ${maxWidth} max-h-[90dvh] flex-col overflow-hidden rounded-[20px]`} onClick={(event) => event.stopPropagation()}>
        <div className="border-app flex shrink-0 items-start justify-between gap-4 border-b px-4 py-3 sm:px-5">
          <div className="min-w-0">
            <h2 className="font-heading text-lg font-semibold text-app">{title}</h2>
            <p className="mt-1 text-sm leading-5 text-muted">{description}</p>
          </div>
          <button type="button" className="icon-button h-10 w-10" onClick={onClose} aria-label="Close modal">
            <X className="h-4 w-4" />
          </button>
        </div>
        <div className="min-h-0 flex-1 overflow-y-auto px-4 py-4 pb-[max(1rem,env(safe-area-inset-bottom))] sm:px-5 sm:pb-4">
          {children}
        </div>
      </div>
    </div>
  )
}
