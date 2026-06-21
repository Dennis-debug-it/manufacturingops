import { Navigate, Outlet } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import { roleHomePath } from '../utils/roles'
import { LoadingState } from '../components/ui/LoadingState'

export function GuestRoute() {
  const { isAuthenticated, isReady, session } = useAuth()

  if (!isReady) {
    return <LoadingState label="Preparing sign-in" />
  }

  if (isAuthenticated && session) {
    if (session.mustChangePassword) {
      return <Navigate to="/change-password" replace />
    }
    return <Navigate to={roleHomePath(session.role, { title: session.title, permissions: session.permissions })} replace />
  }

  return <Outlet />
}
