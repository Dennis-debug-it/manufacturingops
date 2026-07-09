import { Link } from 'react-router-dom'
import { SearchX } from 'lucide-react'
import { EmptyState } from '../../shared/components/EmptyState'

export function NotFoundPage() {
  return (
    <div className="min-h-screen bg-slate-50 px-4 py-10 sm:px-6 lg:px-8">
      <div className="mx-auto max-w-2xl">
        <EmptyState
          icon={SearchX}
          title="Page not found"
          description="The route does not exist or has been moved."
          actionLabel="Go to dashboard"
          onAction={() => {
            window.location.assign('/dashboard')
          }}
        />
        <div className="mt-4 text-center">
          <Link to="/login" className="text-sm font-medium text-slate-700 underline decoration-slate-300 underline-offset-4">
            Go back to login
          </Link>
        </div>
      </div>
    </div>
  )
}
