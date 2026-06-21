import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { useAuth } from './AuthContext'
import type { Role } from '../types/app'
import { normalizeRole, roleHomePath } from '../utils/roles'
import { LoadingState } from '../components/ui/LoadingState'

export function RequireAuth({ allow }: { allow?: Role[] }) {
  const location = useLocation()
  const { isAuthenticated, isReady, session } = useAuth()

  if (!isReady) {
    return <LoadingState label="Loading your ServiceOps workspace" />
  }

  if (!isAuthenticated || !session) {
    return <Navigate to="/login" replace state={{ from: location.pathname }} />
  }

  if (session.mustChangePassword && location.pathname !== '/change-password') {
    return <Navigate to="/change-password" replace state={{ from: location.pathname }} />
  }

  if (allow && !allow.some((role) => normalizeRole(String(role)) === normalizeRole(String(session.role)))) {
    return <Navigate to={roleHomePath(session.role, { title: session.title, permissions: session.permissions })} replace />
  }

  return <Outlet />
}
