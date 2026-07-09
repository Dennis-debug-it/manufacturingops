import type { ReactNode } from 'react'
import { AlertCircle, AlertTriangle, CheckCircle2, Info } from 'lucide-react'

const toneStyles = {
  info: {
    wrapper: 'border-sky-400/30 bg-sky-500/10 text-sky-100',
    icon: 'icon-accent',
    Icon: Info,
  },
  warning: {
    wrapper: 'border-amber-400/30 bg-amber-500/12 text-amber-100',
    icon: 'icon-amber',
    Icon: AlertTriangle,
  },
  success: {
    wrapper: 'border-emerald-400/30 bg-emerald-500/12 text-emerald-100',
    icon: 'icon-emerald',
    Icon: CheckCircle2,
  },
  danger: {
    wrapper: 'border-rose-400/30 bg-rose-500/12 text-rose-100',
    icon: 'icon-rose',
    Icon: AlertCircle,
  },
} as const

export function InfoAlert({
  title,
  description,
  tone = 'info',
  icon,
  className = '',
}: {
  title: string
  description: string
  tone?: keyof typeof toneStyles
  icon?: ReactNode
  className?: string
}) {
  const config = toneStyles[tone]
  const Icon = config.Icon

  return (
    <div className={`rounded-3xl border px-4 py-4 ${config.wrapper} ${className}`.trim()}>
      <div className="flex items-start gap-3">
        <div className={`shrink-0 rounded-2xl p-2 ${config.icon}`}>
          {icon ?? <Icon className="h-4 w-4" />}
        </div>
        <div>
          <p className="text-sm font-semibold text-app">{title}</p>
          <p className="mt-1 text-sm leading-6 text-muted">{description}</p>
        </div>
      </div>
    </div>
  )
}
