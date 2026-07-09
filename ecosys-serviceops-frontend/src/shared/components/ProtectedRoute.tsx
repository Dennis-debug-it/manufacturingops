import type { ReactNode } from 'react'
import { Navigate, useLocation } from 'react-router-dom'
import { useAuth } from '../hooks/useAuth'
import type { AppRole } from '../types/auth'

export interface ProtectedRouteProps {
  allowedRoles?: AppRole[]
  children: ReactNode
}

export function ProtectedRoute({ allowedRoles, children }: ProtectedRouteProps) {
  const { isReady, isAuthenticated, user } = useAuth()
  const location = useLocation()

  if (!isReady) {
    return <div className="min-h-screen bg-slate-50 p-6">Loading…</div>
  }

  if (!isAuthenticated || !user) {
    return <Navigate to="/login" replace state={{ from: location.pathname }} />
  }

  if (allowedRoles && !allowedRoles.includes(user.role)) {
    return <Navigate to="/403" replace />
  }

  return <>{children}</>
}
