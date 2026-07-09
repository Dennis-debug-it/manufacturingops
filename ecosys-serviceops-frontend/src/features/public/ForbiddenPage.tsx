import { Link } from 'react-router-dom'
import { ShieldAlert } from 'lucide-react'
import { EmptyState } from '../../shared/components/EmptyState'

export function ForbiddenPage() {
  return (
    <div className="min-h-screen bg-slate-50 px-4 py-10 sm:px-6 lg:px-8">
      <div className="mx-auto max-w-2xl">
        <EmptyState
          icon={ShieldAlert}
          title="Access denied"
          description="You do not have permission to view this page."
          actionLabel="Go to dashboard"
          onAction={() => {
            window.location.assign('/dashboard')
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
