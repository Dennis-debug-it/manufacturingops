import { LogOut, WifiOff, RefreshCw } from 'lucide-react'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '../hooks/useAuth'
import { useOnlineStatus } from '../hooks/useOnlineStatus'
import { cn } from '../utils/cn'
import { Logo } from '../components/Logo'

export interface TopbarProps {
  syncingLabel?: string | null
}

export function Topbar({ syncingLabel }: TopbarProps) {
  const { tenant, user, logout } = useAuth()
  const online = useOnlineStatus()
  const navigate = useNavigate()

  return (
    <header className="sticky top-0 z-20 flex h-16 items-center justify-between border-b border-slate-200 bg-white/90 px-4 backdrop-blur lg:px-6">
      <div className="flex min-w-0 items-center gap-3">
        <Logo variant="mark" className="h-8 w-8 shrink-0 lg:hidden" />
        <div className="truncate text-sm font-medium text-slate-900">{tenant?.companyName ?? 'Ecosys'}</div>
      </div>
      <div className="flex items-center gap-2">
        {!online ? (
          <span className="inline-flex items-center gap-2 rounded-full bg-amber-50 px-3 py-1 text-xs font-medium text-amber-700 ring-1 ring-inset ring-amber-200">
            <WifiOff className="h-3.5 w-3.5" />
            Offline
          </span>
        ) : null}
        {syncingLabel ? (
          <span className="inline-flex items-center gap-2 rounded-full bg-sky-50 px-3 py-1 text-xs font-medium text-sky-700 ring-1 ring-inset ring-sky-200">
            <RefreshCw className="h-3.5 w-3.5" />
            {syncingLabel}
          </span>
        ) : null}
        <div className="hidden text-right sm:block">
          <div className="text-sm font-medium text-slate-900">{user?.fullName}</div>
          <div className="text-xs text-slate-500">{user?.jobTitle ?? user?.role}</div>
        </div>
        <button
          type="button"
          onClick={async () => {
            await logout()
            navigate('/login')
          }}
          className={cn('inline-flex h-10 items-center gap-2 rounded-lg border border-slate-200 px-3 text-sm font-medium text-slate-700 transition hover:bg-slate-50')}
        >
          <LogOut className="h-4 w-4" />
          <span className="hidden sm:inline">Logout</span>
        </button>
      </div>
    </header>
  )
}
