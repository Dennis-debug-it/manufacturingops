import { Link } from 'react-router-dom'
import { PageHeader } from '../../shared/components/PageHeader'

export function DemoConfirmationPage() {
  return (
    <div className="min-h-screen bg-slate-50 px-4 py-10 sm:px-6 lg:px-8">
      <div className="mx-auto max-w-2xl rounded-3xl border border-slate-200 bg-white p-8 shadow-sm">
        <PageHeader
          title="Demo request received"
          subtitle="Thanks. We’ll be in touch shortly."
        />
        <div className="mt-6">
          <Link to="/login" className="inline-flex h-11 items-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)]">
            Return to login
          </Link>
        </div>
      </div>
    </div>
  )
}
