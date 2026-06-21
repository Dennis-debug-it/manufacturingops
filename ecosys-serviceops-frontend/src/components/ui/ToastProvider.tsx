import { createContext, useContext, useEffect, useMemo, useRef, useState } from 'react'
import type { ReactNode } from 'react'
import { X } from 'lucide-react'
import { createId } from '../../utils/id'

type ToastTone = 'success' | 'info' | 'warning' | 'danger'

type Toast = {
  id: string
  title: string
  description?: string
  tone: ToastTone
}

type ToastContextValue = {
  pushToast: (toast: Omit<Toast, 'id'>) => void
}

const ToastContext = createContext<ToastContextValue | null>(null)

export function ToastProvider({ children }: { children: ReactNode }) {
  const [toasts, setToasts] = useState<Toast[]>([])
  const timeoutsRef = useRef<number[]>([])

  useEffect(() => {
    return () => {
      timeoutsRef.current.forEach((timeoutId) => window.clearTimeout(timeoutId))
      timeoutsRef.current = []
    }
  }, [])

  const value = useMemo(
    () => ({
      pushToast: (toast: Omit<Toast, 'id'>) => {
        const next = { ...toast, id: createId('toast') }
        setToasts((current) => [next, ...current].slice(0, 4))
        const timeoutId = window.setTimeout(() => {
          setToasts((current) => current.filter((item) => item.id !== next.id))
          timeoutsRef.current = timeoutsRef.current.filter((item) => item !== timeoutId)
        }, 3200)
        timeoutsRef.current.push(timeoutId)
      },
    }),
    [],
  )

  return (
    <ToastContext.Provider value={value}>
      {children}
      <div className="fixed right-4 top-4 z-[60] flex w-full max-w-sm flex-col gap-3">
        {toasts.map((toast) => (
          <div key={toast.id} className="glass-panel rounded-[24px] p-4">
            <div className="flex items-start justify-between gap-3">
              <div>
                <p className="text-sm font-semibold text-app">{toast.title}</p>
                {toast.description ? <p className="mt-1 text-sm text-muted">{toast.description}</p> : null}
              </div>
              <button type="button" className="icon-button h-8 w-8" onClick={() => setToasts((current) => current.filter((item) => item.id !== toast.id))}>
                <X className="h-4 w-4" />
              </button>
            </div>
            <div className={`mt-3 h-1 rounded-full ${toast.tone === 'danger' ? 'bg-rose-400' : toast.tone === 'warning' ? 'bg-amber-400' : toast.tone === 'info' ? 'bg-cyan-400' : 'bg-emerald-400'}`} />
          </div>
        ))}
      </div>
    </ToastContext.Provider>
  )
}

export function useToast() {
  const context = useContext(ToastContext)
  if (!context) throw new Error('useToast must be used inside ToastProvider')
  return context
}
