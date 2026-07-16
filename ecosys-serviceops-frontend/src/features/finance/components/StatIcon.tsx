import type { ReactNode } from 'react'

type StatIconTone = 'default' | 'success' | 'warning' | 'danger'

const toneClasses: Record<StatIconTone, string> = {
  default: 'bg-slate-100 text-slate-600',
  success: 'bg-emerald-50 text-emerald-600',
  warning: 'bg-amber-50 text-amber-600',
  danger: 'bg-rose-50 text-rose-600',
}

/** Tinted icon chip for the top-right of a finance StatCard. */
export function StatIcon({ tone = 'default', children }: { tone?: StatIconTone; children: ReactNode }) {
  return (
    <span className={`inline-flex h-8 w-8 items-center justify-center rounded-lg ${toneClasses[tone]}`}>{children}</span>
  )
}
