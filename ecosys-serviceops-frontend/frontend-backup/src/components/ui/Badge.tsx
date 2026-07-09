import type { ReactNode } from 'react'
import type { BadgeTone } from '../../types/app'

const toneClassMap: Record<BadgeTone, string> = {
  default: 'badge-default',
  info: 'badge-info',
  success: 'badge-success',
  warning: 'badge-warning',
  danger: 'badge-danger',
  neutral: 'badge-neutral',
}

export function Badge({
  tone = 'default',
  children,
  className = '',
}: {
  tone?: BadgeTone
  children: ReactNode
  className?: string
}) {
  return (
    <span
      className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-[10px] font-semibold leading-none tracking-[0.12em] uppercase ${toneClassMap[tone]} ${className}`}
    >
      {children}
    </span>
  )
}
