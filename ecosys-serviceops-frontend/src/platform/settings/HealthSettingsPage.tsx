import { useQuery } from '@tanstack/react-query'
import { CheckCircle2, AlertTriangle, XCircle } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { StatCard } from '../../shared/components/StatCard'
import { formatDateTime } from '../../shared/utils/dates'

// TODO(backend): No System Health / background-job monitoring endpoint exists yet —
// typed stub returning empty/default data until a real Platform Health API is available.
type ServiceStatus = 'Healthy' | 'Degraded' | 'Down'

interface ServiceHealthItem {
  name: string
  status: ServiceStatus
  lastCheckedAt: string
  detail: string | null
}

interface BackgroundJobItem {
  name: string
  lastRunAt: string | null
  status: 'Success' | 'Failed' | 'Running'
  durationSeconds: number | null
}

function useServiceHealth() {
  return useQuery({ queryKey: ['platform', 'settings', 'health', 'services'], queryFn: async (): Promise<ServiceHealthItem[]> => [], staleTime: Infinity })
}

function useBackgroundJobs() {
  return useQuery({ queryKey: ['platform', 'settings', 'health', 'jobs'], queryFn: async (): Promise<BackgroundJobItem[]> => [], staleTime: Infinity })
}

const statusIcon: Record<ServiceStatus, typeof CheckCircle2> = {
  Healthy: CheckCircle2,
  Degraded: AlertTriangle,
  Down: XCircle,
}

const statusColor: Record<ServiceStatus, string> = {
  Healthy: 'text-emerald-500',
  Degraded: 'text-amber-500',
  Down: 'text-rose-500',
}

export function HealthSettingsPage() {
  const { data: services } = useServiceHealth()
  const { data: jobs } = useBackgroundJobs()

  const healthyCount = (services ?? []).filter((s) => s.status === 'Healthy').length
  const degradedCount = (services ?? []).filter((s) => s.status === 'Degraded').length
  const downCount = (services ?? []).filter((s) => s.status === 'Down').length

  return (
    <div className="space-y-6">
      <PageHeader title="System Health" subtitle="Background jobs and service status across the platform." />
      <div className="rounded-2xl border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-900">
        No System Health / background-job monitoring endpoint exists yet — this is the full intended UI wired against typed stub data until a backend endpoint is connected.
      </div>

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
        <StatCard label="Healthy Services" value={healthyCount} tone="success" />
        <StatCard label="Degraded" value={degradedCount} tone="warning" />
        <StatCard label="Down" value={downCount} tone="danger" />
      </div>

      <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
        <h2 className="text-sm font-semibold uppercase tracking-[0.18em] text-slate-500">Services</h2>
        {(services ?? []).length === 0 ? (
          <p className="mt-3 text-sm text-slate-500">Service health checks will appear here once the backend endpoint is connected.</p>
        ) : (
          <ul className="mt-4 divide-y divide-slate-100">
            {(services ?? []).map((service) => {
              const Icon = statusIcon[service.status]
              return (
                <li key={service.name} className="flex items-center justify-between py-3">
                  <div className="flex items-center gap-2">
                    <Icon className={`h-4 w-4 ${statusColor[service.status]}`} />
                    <span className="text-sm font-medium text-slate-900">{service.name}</span>
                  </div>
                  <div className="text-right text-xs text-slate-500">
                    <div>{service.detail ?? service.status}</div>
                    <div>{formatDateTime(service.lastCheckedAt)}</div>
                  </div>
                </li>
              )
            })}
          </ul>
        )}
      </div>

      <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
        <h2 className="text-sm font-semibold uppercase tracking-[0.18em] text-slate-500">Background Jobs</h2>
        {(jobs ?? []).length === 0 ? (
          <p className="mt-3 text-sm text-slate-500">Background job run history will appear here once the backend endpoint is connected.</p>
        ) : (
          <ul className="mt-4 divide-y divide-slate-100">
            {(jobs ?? []).map((job) => (
              <li key={job.name} className="flex items-center justify-between py-3 text-sm">
                <span className="font-medium text-slate-900">{job.name}</span>
                <span className="text-slate-500">{job.status} · {job.lastRunAt ? formatDateTime(job.lastRunAt) : 'Never run'}{job.durationSeconds !== null ? ` · ${job.durationSeconds}s` : ''}</span>
              </li>
            ))}
          </ul>
        )}
      </div>
    </div>
  )
}
