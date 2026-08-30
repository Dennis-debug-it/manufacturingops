import { useEffect, useState } from 'react'
import { X } from 'lucide-react'
import { useAuth } from '../hooks/useAuth'
import { cn } from '../utils/cn'

const roleOrientation: Record<string, string> = {
  admin: 'As an Admin, you have full access to configure your workspace, manage users, and oversee finance and operations.',
  manager: 'As a Manager, you can oversee work orders, assign technicians, and monitor operational performance.',
  dispatcher: 'As a Dispatcher, you\'ll coordinate work order assignments and keep the field team moving.',
  technician: 'As a Technician, use the Work Orders tab to see your jobs, complete checklists, capture evidence, and get signatures.',
  finance: 'As Finance, you\'ll manage invoices, quotes, and receivables from the Finance module.',
  viewer: 'As a Viewer, you have read-only access across the workspace.',
}

const steps = [
  { title: 'Welcome to ECOSYS', body: 'Your workspace is ready. This quick tour highlights what matters most for your role.' },
  { title: 'Your Role', body: null },
  { title: 'Get Started', body: 'Use the sidebar to navigate modules. Check the Onboarding checklist to finish setting up your workspace.' },
]

export function FirstLoginModal() {
  const { user } = useAuth()
  const [step, setStep] = useState(0)
  const [dismissed, setDismissed] = useState(true)

  useEffect(() => {
    if (!user) return
    const key = `ecosys.first-login-shown.${user.userId}`
    if (!localStorage.getItem(key)) {
      setDismissed(false)
      localStorage.setItem(key, '1')
    }
  }, [user])

  if (dismissed || !user) {
    return null
  }

  const close = () => setDismissed(true)
  const current = steps[step]
  if (!current) return null

  return (
    <div className="fixed inset-0 z-[100] flex items-center justify-center bg-[var(--app-primary)]/30 px-4 backdrop-blur-[2px]">
      <div className="w-full max-w-md rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white-strong)] p-6 shadow-2xl backdrop-blur-[var(--glass-blur-lg)]">
        <div className="flex items-start justify-between">
          <h3 className="text-lg font-semibold text-slate-900">{current.title}</h3>
          <button type="button" onClick={close} className="rounded-lg p-2 text-slate-500 hover:bg-slate-100"><X className="h-4 w-4" /></button>
        </div>
        <p className="mt-3 text-sm leading-6 text-slate-600">{current.body ?? roleOrientation[user.role] ?? 'Welcome aboard.'}</p>
        <div className="mt-6 flex items-center justify-between">
          <div className="flex gap-1.5">
            {steps.map((_, index) => (
              <span key={index} className={cn('h-1.5 w-6 rounded-full', index === step ? 'bg-[var(--app-lime)]' : 'bg-slate-200')} />
            ))}
          </div>
          <div className="flex gap-2">
            {step > 0 ? <button type="button" onClick={() => setStep((value) => value - 1)} className="inline-flex h-10 items-center rounded-lg border border-[var(--app-border)] px-4 text-sm font-medium text-slate-700 hover:bg-[var(--app-primary-soft)]">Back</button> : null}
            {step < steps.length - 1 ? (
              <button type="button" onClick={() => setStep((value) => value + 1)} className="inline-flex h-10 items-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white hover:bg-[var(--app-secondary)]">Next</button>
            ) : (
              <button type="button" onClick={close} className="inline-flex h-10 items-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white hover:bg-[var(--app-secondary)]">Get Started</button>
            )}
          </div>
        </div>
      </div>
    </div>
  )
}
