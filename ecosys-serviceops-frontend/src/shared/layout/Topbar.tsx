import type { AppRole } from '../types/auth'
import { LogOut, UserCircle2, WifiOff } from 'lucide-react'
import { Link, useNavigate } from 'react-router-dom'
import { useAuth } from '../hooks/useAuth'
import { useOnlineStatus } from '../hooks/useOnlineStatus'
import { cn } from '../utils/cn'
import { Logo } from '../components/Logo'
import { isDemoTenant } from '../demoWorkspace'

/** Shown under the user's name when they have no job title. */
const roleNames: Partial<Record<AppRole, string>> = {
  admin: 'Admin', manager: 'Manager', dispatcher: 'Dispatcher', technician: 'Technician',
  materials_controller: 'Materials Controller', finance: 'Finance', viewer: 'Viewer',
}

export function Topbar() {
  const { tenant, user, logout, isImpersonating } = useAuth()
  const online = useOnlineStatus()
  const navigate = useNavigate()

  return (
    <header className="sticky top-0 z-20 flex h-16 items-center justify-between border-b border-[var(--glass-border)] bg-[var(--glass-white-strong)] px-4 shadow-[var(--shadow-app)] backdrop-blur-[var(--glass-blur-md)] lg:px-6">
      <div className="flex min-w-0 items-center gap-3">
        <Logo variant="mark" className="h-8 w-8 shrink-0 lg:hidden" />
        <div className="truncate text-sm font-medium text-slate-900">{tenant?.companyName ?? 'Ecosys'}</div>
      </div>
      <div className="flex items-center gap-2">
        {/*
          A connectivity warning, not a capability claim. This used to sit next to a "Working
          locally" badge implying writes were queued and synced later; nothing did that (the
          IndexedDB sync layer was never imported), so a technician who trusted it lost the work.
          Every write in this app goes straight to the API, so the honest thing to say offline is
          that saving will fail.
        */}
        {!online ? (
          <span
            role="status"
            className="inline-flex items-center gap-2 rounded-full bg-amber-50 px-3 py-1 text-xs font-medium text-amber-700 ring-1 ring-inset ring-amber-200"
          >
            <WifiOff className="h-3.5 w-3.5" aria-hidden="true" />
            Offline — changes can’t be saved
          </span>
        ) : null}
        <Link to="/me" title="My account" className="flex items-center gap-2 rounded-lg px-2 py-1 text-right transition hover:bg-[var(--app-primary-soft)]">
          <UserCircle2 className="h-5 w-5 text-slate-500 sm:hidden" aria-label="My account" />
          <span className="hidden sm:block">
            <span className="block text-sm font-medium text-slate-900">{user?.fullName}</span>
            <span className="block text-xs text-slate-500">{user?.jobTitle ?? (user ? roleNames[user.role] ?? user.role : null)}</span>
          </span>
        </Link>
        <button
          type="button"
          onClick={async () => {
            // Logout itself redirects demo and impersonation sessions; don't send those to the sign-in form.
            const redirected = isImpersonating || isDemoTenant(tenant?.tenantId)
            await logout()
            if (!redirected) {
              navigate('/login')
            }
          }}
          className={cn(
            'inline-flex h-10 items-center gap-2 rounded-lg border border-[var(--app-border)] px-3 text-sm font-medium text-slate-700 transition hover:bg-[var(--app-primary-soft)]',
          )}
        >
          <LogOut className="h-4 w-4" />
          <span className="hidden sm:inline">Logout</span>
        </button>
      </div>
    </header>
  )
}
