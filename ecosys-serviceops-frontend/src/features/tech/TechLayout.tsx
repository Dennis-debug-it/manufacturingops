import { Link, Outlet, useNavigate } from 'react-router-dom'
import { LogOut } from 'lucide-react'
import { Logo } from '../../shared/components/Logo'
import { useAuth } from '../../shared/hooks/useAuth'
import { isDemoTenant } from '../../shared/demoWorkspace'

/** Phone-first shell for field technicians: just their jobs, no office navigation. */
export function TechLayout() {
  const { tenant, user, logout, isImpersonating } = useAuth()
  const navigate = useNavigate()

  const signOut = async () => {
    const redirected = isImpersonating || isDemoTenant(tenant?.tenantId)
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
            <span className="hidden truncate text-xs text-slate-500 sm:block">{user?.fullName}</span>
            <button type="button" onClick={() => void signOut()} className="inline-flex h-9 items-center gap-1.5 rounded-lg border border-slate-200 px-2.5 text-xs font-medium text-slate-700 hover:bg-slate-50">
              <LogOut className="h-3.5 w-3.5" />Sign out
            </button>
          </div>
        </div>
      </header>
      <main className="mx-auto max-w-xl px-4 pb-32 pt-4">
        <Outlet />
      </main>
    </div>
  )
}
