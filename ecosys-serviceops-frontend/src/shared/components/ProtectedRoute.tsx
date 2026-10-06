import type { ReactNode } from 'react'
import { Navigate, useLocation } from 'react-router-dom'
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
    if (entitlements.isLoading) return <div className="p-6">Loading workspace…</div>
    if (location.pathname === '/dashboard' && !entitlements.hasModule('operations') && entitlements.hasModule('finance'))
      return <Navigate to="/finance" replace />
    if (!entitlements.canAccessPath(location.pathname)) return <div className="rounded-xl border border-slate-200 bg-white p-6">This feature is unavailable on your ServiceOps plan. Contact your account manager to upgrade.</div>
  }

  return <>{children}</>
}
