import type { ReactNode } from 'react'
import { Navigate } from 'react-router-dom'
import { usePortalAuth } from './PortalAuthContext'

export function PortalProtectedRoute({ children }: { children: ReactNode }) {
  const { isReady, isAuthenticated } = usePortalAuth()

  if (!isReady) {
    return <div className="flex min-h-screen items-center justify-center bg-slate-50 text-sm text-slate-500">Loading…</div>
  }

  if (!isAuthenticated) {
    return <Navigate to="/portal/login" replace />
  }

  return <>{children}</>
}
