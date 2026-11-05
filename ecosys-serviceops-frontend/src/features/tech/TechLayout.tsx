import { useEffect } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { Link, Outlet, useLocation, useNavigate } from 'react-router-dom'
import { BookOpen, CalendarClock, ClipboardCheck, ClipboardList, Factory, HardHat, LogOut, UserRound, ScanLine } from 'lucide-react'
import { Logo } from '../../shared/components/Logo'
import { useAuth } from '../../shared/hooks/useAuth'
import { useEntitlements } from '../../shared/hooks/useEntitlements'
import { usePermission } from '../../shared/hooks/usePermission'
import { isDemoTenant } from '../../shared/demoWorkspace'
import { cn } from '../../shared/utils/cn'
import { usePendingActions } from './offline/hooks'
import { OfflineStatus } from './offline/OfflineStatus'
import { startOfflinePersistence } from './offline/persist'
import { useOfflineScanIndex } from '../factory/api'
import { NotificationBell } from '../notifications/NotificationBell'
import { JobAlertsPrompt } from './JobAlerts'
import { LocationPrompt } from './LocationPrompt'
import { disablePush, resyncPush } from './push'

const SECTIONS = [
  { to: '/tech', label: 'Jobs', icon: ClipboardList, match: (path: string) => path === '/tech' || path.startsWith('/tech/jobs') },
  { to: '/tech/maintenance/jobs', label: 'Factory', icon: Factory, match: (path: string) => path.startsWith('/tech/maintenance/jobs') },
  { to: '/tech/maintenance/checks', label: 'Checks', icon: ClipboardCheck, match: (path: string) => path.startsWith('/tech/maintenance/checks') },
  { to: '/tech/scan', label: 'Scan', icon: ScanLine, match: (path: string) => path.startsWith('/tech/scan') || path.startsWith('/tech/assets') },
  { to: '/tech/site', label: 'Site', icon: HardHat, match: (path: string) => path.startsWith('/tech/site') },
  { to: '/tech/time', label: 'Time & Leave', icon: CalendarClock, match: (path: string) => path.startsWith('/tech/time') },
  { to: '/tech/guides', label: 'Guides', icon: BookOpen, match: (path: string) => path.startsWith('/tech/guides') },
  { to: '/tech/account', label: 'Me', icon: UserRound, match: (path: string) => path.startsWith('/tech/account') },
] as const

/** Phone-first shell for field technicians: their jobs, time & leave, guides and account; no office navigation. */
export function TechLayout() {
  const { tenant, user, logout, isImpersonating } = useAuth()
  const navigate = useNavigate()
  const { pathname } = useLocation()
  const queryClient = useQueryClient()
  const pending = usePendingActions()
  const userId = user?.userId
  // The muster roll is for site supervisors on tenants with the Projects module.
  const { hasModule } = useEntitlements()
  const canMarkMuster = usePermission('projects.muster.mark')
  const sections = SECTIONS.filter((section) => section.to !== '/tech/site' || (canMarkMuster && hasModule('projects')))

  // Keep this technician's jobs on the phone so the app works without a signal.
  useEffect(() => (userId ? startOfflinePersistence(queryClient, userId) : undefined), [queryClient, userId])
  // Keep the asset register on the phone so tags can be scanned with no signal.
  useOfflineScanIndex()
  // Job alerts follow whoever is signed in on this phone.
  useEffect(() => { if (userId) void resyncPush().catch(() => undefined) }, [userId])

  const signOut = async () => {
    if (pending.length > 0 && !window.confirm(`${pending.length} change${pending.length === 1 ? ' is' : 's are'} still waiting to sync. They stay on this phone and sync when you sign back in. Sign out anyway?`)) {
      return
    }
    const redirected = isImpersonating || isDemoTenant(tenant?.tenantId)
    // Stop job alerts here before the session ends, so the next person on this phone doesn't get them.
    await disablePush().catch(() => undefined)
    await logout()
    if (!redirected) navigate('/login')
  }

  return (
    <div className="min-h-screen bg-[var(--app-bg,#f1f5f4)]">
      <header className="sticky top-0 z-20 border-b border-slate-200 bg-white/95 backdrop-blur">
        <div className="mx-auto flex h-14 max-w-xl items-center justify-between gap-3 px-4">
          <Link to="/tech" className="flex min-w-0 items-center gap-2" aria-label="My jobs">
            <Logo variant="mark" tone="dark" className="h-7 w-7 shrink-0" />
            <span className="truncate text-sm font-semibold text-slate-900">{tenant?.companyName ?? 'My Jobs'}</span>
          </Link>
          <div className="flex items-center gap-2">
            <OfflineStatus />
            <NotificationBell viewAllPath="/tech/notifications" preferencesPath="/tech/account" />
            <span className="hidden truncate text-xs text-slate-500 sm:block">{user?.fullName}</span>
            <button type="button" onClick={() => void signOut()} className="inline-flex h-9 items-center gap-1.5 rounded-lg border border-slate-200 px-2.5 text-xs font-medium text-slate-700 hover:bg-slate-50">
              <LogOut className="h-3.5 w-3.5" />Sign out
            </button>
          </div>
        </div>
        <nav aria-label="Technician sections" className={cn('mx-auto grid max-w-xl gap-1 px-2 pb-2', sections.length === 5 ? 'grid-cols-5' : 'grid-cols-4')}>
          {sections.map(({ to, label, icon: Icon, match }) => {
            const active = match(pathname)
            return (
              <Link
                key={to}
                to={to}
                aria-current={active ? 'page' : undefined}
                className={cn('flex h-10 flex-col items-center justify-center gap-0.5 rounded-lg text-[11px] font-medium', active ? 'bg-[var(--app-primary)] text-white' : 'text-slate-600 hover:bg-slate-50')}
              >
                <Icon className="h-4 w-4" />{label}
              </Link>
            )
          })}
        </nav>
      </header>
      <main className="mx-auto max-w-xl px-4 pb-32 pt-4">
        <LocationPrompt />
        <JobAlertsPrompt />
        <Outlet />
      </main>
    </div>
  )
}
