import { isTechnicianWorkspaceUser } from '../utils/technicianWorkspace'
import { canOpenPath } from '../utils/routeAccess'
import type { ReactNode } from 'react'
import { Link, Navigate, useLocation } from 'react-router-dom'
import { useAuth } from '../hooks/useAuth'
import type { AppRole } from '../types/auth'
import { useEntitlements } from '../hooks/useEntitlements'

export interface ProtectedRouteProps {
  allowedRoles?: AppRole[]
  children: ReactNode
}

export function ProtectedRoute({ allowedRoles, children }: ProtectedRouteProps) {
  const { isReady, isAuthenticated, user } = useAuth()
  const location = useLocation()
  const entitlements = useEntitlements()

  if (!isReady) {
    return <div className="min-h-screen bg-slate-50 p-6">Loading…</div>
  }

  if (!isAuthenticated || !user) {
    return <Navigate to="/login" replace state={{ from: location.pathname }} />
  }

  // A temporary password's token is refused everywhere except change-password, so nothing behind
  // this guard could load anyway.
  if (user.mustChangePassword) {
    return <Navigate to="/change-password" replace />
  }

  if (allowedRoles && !allowedRoles.includes(user.role)) {
    return <Navigate to="/403" replace />
  }

  if (user.role !== 'super_admin' && user.role !== 'platform_sales') {
    // Offline with nothing stored yet, the plan check can't finish; don't hold the screen on it
    // (screens that need a module still say so below, and the API enforces the plan anyway).
    const planCheckStuck = entitlements.fetchStatus === 'paused' || entitlements.isError
    if (entitlements.isLoading && !planCheckStuck) return <div className="p-6">Loading workspace…</div>
    if (location.pathname === '/dashboard' && !entitlements.hasModule('operations') && entitlements.hasModule('finance'))
      return <Navigate to="/finance" replace />
    // The stores' home is the stock page, not the field-service dashboard.
    if (location.pathname === '/dashboard' && user.role === 'materials_controller' && entitlements.canAccessPath('/inventory'))
      return <Navigate to="/inventory" replace />
    // Likewise Finance starts on Finance, not on the field-service dashboard.
    if (location.pathname === '/dashboard' && user.role === 'finance' && entitlements.canAccessPath('/finance'))
      return <Navigate to="/finance" replace />
    if (!entitlements.canAccessPath(location.pathname) && planCheckStuck && !entitlements.data) {
      return <div className="rounded-xl border border-amber-200 bg-amber-50 p-6 text-amber-800">You're offline. This page needs a connection; it will load when you're back online.</div>
    }
    if (!entitlements.canAccessPath(location.pathname)) return <div className="rounded-xl border border-slate-200 bg-white p-6">This feature is unavailable on your ServiceOps plan. Contact your account manager to upgrade.</div>
    // Technician-app users are sent from any office address to the matching /tech screen instead.
    if (!isTechnicianWorkspaceUser(user) && !canOpenPath(user, location.pathname)) {
      return (
        <div className="rounded-xl border border-slate-200 bg-white p-6">
          <h1 className="text-base font-semibold text-slate-900">You don't have access to this page</h1>
          <p className="mt-1 text-sm text-slate-600">It isn't part of your role. If you need it, ask your workspace admin.</p>
          <Link to="/" className="mt-4 inline-flex h-10 items-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white">Go to my home screen</Link>
        </div>
      )
    }
  }

  return <>{children}</>
}
