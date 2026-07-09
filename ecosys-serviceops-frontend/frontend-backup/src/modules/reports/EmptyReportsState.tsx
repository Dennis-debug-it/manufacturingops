import { BarChart3, LayoutDashboard, RefreshCw } from 'lucide-react'
import { useNavigate } from 'react-router-dom'

export function EmptyReportsState() {
  const navigate = useNavigate()

  return (
    <section className="surface-card relative overflow-hidden">
      <div className="absolute inset-x-0 top-0 h-32 bg-gradient-to-r from-cyan-400/10 via-emerald-400/10 to-transparent" />
      <div className="relative mx-auto flex min-h-[360px] max-w-3xl flex-col items-center justify-center px-4 py-10 text-center">
        <div className="icon-accent rounded-[28px] p-5">
          <BarChart3 className="h-8 w-8" />
        </div>
        <p className="eyebrow-accent mt-6 text-xs font-semibold uppercase tracking-[0.24em]">Reports</p>
        <h2 className="mt-3 text-3xl font-semibold tracking-tight text-app">No reports available yet</h2>
        <p className="mt-4 max-w-2xl text-sm leading-7 text-muted">
          Reports will appear here as operational activity is recorded across your workspace.
        </p>
        <p className="mt-3 max-w-2xl text-sm leading-7 text-muted">
          Use the dashboard and operational pages to keep work moving while your reporting history builds.
        </p>
        <div className="mt-8 flex flex-wrap items-center justify-center gap-3">
          <button type="button" className="button-secondary" onClick={() => window.location.reload()}>
            <RefreshCw className="h-4 w-4" />
            Refresh
          </button>
          <button type="button" className="button-primary" onClick={() => navigate('/dashboard')}>
            <LayoutDashboard className="h-4 w-4" />
            Go to Dashboard
          </button>
        </div>
      </div>
    </section>
  )
}
