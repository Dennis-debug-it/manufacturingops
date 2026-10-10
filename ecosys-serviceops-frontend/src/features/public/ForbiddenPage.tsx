import { Link } from 'react-router-dom'
import { ShieldAlert } from 'lucide-react'
import { EmptyState } from '../../shared/components/EmptyState'
import { useAuth } from '../../shared/hooks/useAuth'

export function ForbiddenPage() {
  const { user } = useAuth()
  const isPlatformUser = user?.role === 'super_admin' || user?.role === 'platform_sales'
  return (
    <div className="min-h-screen bg-slate-50 px-4 py-10 sm:px-6 lg:px-8">
      <div className="mx-auto max-w-2xl">
        <EmptyState
          icon={ShieldAlert}
          title="Access denied"
          description={isPlatformUser
            ? "This page belongs to a workspace, so platform accounts can't open it. To see a workspace's email setup, go to Tenants, open the tenant and choose the Communication tab."
            : 'You do not have permission to view this page.'}
          actionLabel={isPlatformUser ? 'Go to Tenants' : 'Go to dashboard'}
          onAction={() => {
            window.location.assign(isPlatformUser ? '/platform/tenants' : '/dashboard')
          }}
        />
        <div className="mt-4 text-center">
          <Link to="/login" className="text-sm font-medium text-slate-700 underline decoration-slate-300 underline-offset-4">
            Sign in with a different account
          </Link>
        </div>
      </div>
    </div>
  )
}
