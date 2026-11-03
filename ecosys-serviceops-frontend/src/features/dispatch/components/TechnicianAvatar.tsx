import { cn } from '../../../shared/utils/cn'
import type { TechnicianBoardStatus } from '../boardApi'

const statusRing: Record<TechnicianBoardStatus, string> = {
  available: 'ring-emerald-400',
  'on-job': 'ring-sky-400',
  offline: 'ring-slate-300',
}

const statusDot: Record<TechnicianBoardStatus, string> = {
  available: 'bg-emerald-500',
  'on-job': 'bg-sky-500',
  offline: 'bg-slate-400',
}

const statusLabel: Record<TechnicianBoardStatus, string> = {
  available: 'Available',
  'on-job': 'On Job',
  offline: 'Offline',
}

function initials(fullName: string) {
  const parts = fullName.trim().split(/\s+/).filter(Boolean)
  if (parts.length === 0) return '?'
  if (parts.length === 1) return parts[0]!.slice(0, 2).toUpperCase()
  return `${parts[0]![0]}${parts[parts.length - 1]![0]}`.toUpperCase()
}

export interface TechnicianAvatarProps {
  fullName: string
  status: TechnicianBoardStatus
  size?: 'sm' | 'md'
}

export function TechnicianAvatar({ fullName, status, size = 'md' }: TechnicianAvatarProps) {
  const dimension = size === 'sm' ? 'h-7 w-7 text-[11px]' : 'h-9 w-9 text-xs'
  return (
    <span className="relative inline-flex shrink-0">
      <span
        className={cn(
          'inline-flex items-center justify-center rounded-full bg-slate-800 font-semibold text-white ring-2',
          dimension,
          statusRing[status],
        )}
      >
        {initials(fullName)}
      </span>
      <span
        className={cn('absolute -bottom-0.5 -right-0.5 h-2.5 w-2.5 rounded-full ring-2 ring-white', statusDot[status])}
        title={statusLabel[status]}
      />
    </span>
  )
}

export { statusLabel as technicianStatusLabel }
