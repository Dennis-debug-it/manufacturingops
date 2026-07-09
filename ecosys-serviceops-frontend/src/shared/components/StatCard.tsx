import type { ReactNode } from 'react'
import { cn } from '../utils/cn'

export interface StatCardProps {
  label: string
  value: ReactNode
  icon?: ReactNode
  tone?: 'default' | 'warning' | 'danger' | 'success'
  className?: string
}

const toneClasses: Record<NonNullable<StatCardProps['tone']>, string> = {
  default: 'text-slate-900',
  warning: 'text-amber-600',
  danger: 'text-rose-600',
  success: 'text-emerald-600',
}

export function StatCard({ label, value, icon, tone = 'default', className }: StatCardProps) {
  return (
    <div className={cn('rounded-2xl border border-slate-200 bg-white p-4 shadow-sm', className)}>
      <div className="flex items-center justify-between">
        <div className="text-sm font-medium text-slate-600">{label}</div>
        {icon}
      </div>
      <div className={cn('mt-3 text-2xl font-semibold', toneClasses[tone])}>{value}</div>
    </div>
  )
}
