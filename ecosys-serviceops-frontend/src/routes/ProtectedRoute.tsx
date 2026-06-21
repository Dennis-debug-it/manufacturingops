import { Navigate, Outlet } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import type { Role } from '../types/app'
import { normalizeRole, roleHomePath } from '../utils/roles'
import { LoadingState } from '../components/ui/LoadingState'

export function ProtectedRoute({ allow }: { allow: Role[] }) {
  const { isAuthenticated, isReady, session } = useAuth()

  if (!isReady) {
    return <LoadingState label="Loading workspace access" />
  }

  if (!isAuthenticated || !session) {
    return <Navigate to="/login" replace />
  }

  if (!allow.some((role) => normalizeRole(String(role)) === normalizeRole(String(session.role)))) {
    return <Navigate to={roleHomePath(session.role, { title: session.title, permissions: session.permissions })} replace />
  }

  return <Outlet />
}
