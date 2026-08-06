import { useQuery } from '@tanstack/react-query'
import { CheckCircle2, AlertTriangle, XCircle } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { StatCard } from '../../shared/components/StatCard'
import { formatDateTime } from '../../shared/utils/dates'
import { api } from '../../shared/api/apiClient'

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

interface InfrastructureHealth {
  uptimeSeconds: number
  cpuPercent: number
  memoryWorkingSetMb: number
  managedMemoryMb: number
  diskFreeGb: number | null
  diskTotalGb: number | null
  processorCount: number
  requestsLast5m: number
  avgLatencyMs: number
  errorRatePercent: number
}

interface PlatformHealth {
  version: string
  checkedAt: string
  queueDepth: number
  recentErrors: number
  services: ServiceHealthItem[]
  jobs: BackgroundJobItem[]
  infrastructure: InfrastructureHealth
}

function formatUptime(totalSeconds: number): string {
  const d = Math.floor(totalSeconds / 86400)
  const h = Math.floor((totalSeconds % 86400) / 3600)
  const m = Math.floor((totalSeconds % 3600) / 60)
  if (d > 0) return `${d}d ${h}h ${m}m`
  if (h > 0) return `${h}h ${m}m`
  return `${m}m`
}

function usePlatformHealth() {
  return useQuery({
    queryKey: ['platform', 'health'],
    queryFn: () => api.get<PlatformHealth>('/api/platform/health'),
    refetchInterval: 30_000,
  })
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
  const { data } = usePlatformHealth()
  const services = data?.services
  const jobs = data?.jobs

  const healthyCount = (services ?? []).filter((s) => s.status === 'Healthy').length
  const degradedCount = (services ?? []).filter((s) => s.status === 'Degraded').length
  const downCount = (services ?? []).filter((s) => s.status === 'Down').length

  return (
    <div className="space-y-6">
      <PageHeader title="System Health" subtitle={`Background jobs and service status${data ? ` · v${data.version}` : ''}.`} />

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-5">
        <StatCard label="Healthy Services" value={healthyCount} tone="success" />
        <StatCard label="Degraded" value={degradedCount} tone="warning" />
        <StatCard label="Down" value={downCount} tone="danger" />
        <StatCard label="Email Queue Depth" value={data?.queueDepth ?? 0} />
        <StatCard label="Errors (24h)" value={data?.recentErrors ?? 0} tone={(data?.recentErrors ?? 0) > 0 ? 'danger' : 'default'} />
      </div>

      {data?.infrastructure ? (
        <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
          <h2 className="text-sm font-semibold uppercase tracking-[0.18em] text-slate-500">Infrastructure</h2>
          <div className="mt-4 grid grid-cols-2 gap-4 sm:grid-cols-3 xl:grid-cols-6">
            <StatCard label="Uptime" value={formatUptime(data.infrastructure.uptimeSeconds)} />
            <StatCard
              label="CPU"
              value={`${data.infrastructure.cpuPercent}%`}
              tone={data.infrastructure.cpuPercent > 85 ? 'danger' : data.infrastructure.cpuPercent > 65 ? 'warning' : 'default'}
            />
            <StatCard label="Memory (RSS)" value={`${data.infrastructure.memoryWorkingSetMb} MB`} />
            <StatCard label="Disk Free" value={data.infrastructure.diskFreeGb != null ? `${data.infrastructure.diskFreeGb} GB` : 'n/a'} />
            <StatCard
              label="Avg Latency (5m)"
              value={`${data.infrastructure.avgLatencyMs} ms`}
              tone={data.infrastructure.avgLatencyMs > 1000 ? 'warning' : 'default'}
            />
            <StatCard
              label="Error Rate (5m)"
              value={`${data.infrastructure.errorRatePercent}%`}
              tone={data.infrastructure.errorRatePercent > 5 ? 'danger' : data.infrastructure.errorRatePercent > 1 ? 'warning' : 'default'}
            />
          </div>
          <p className="mt-3 text-xs text-slate-500">
            {data.infrastructure.requestsLast5m} requests in the last 5 min · {data.infrastructure.processorCount} vCPU · managed heap {data.infrastructure.managedMemoryMb} MB
            {data.infrastructure.diskTotalGb != null ? ` · disk ${data.infrastructure.diskFreeGb}/${data.infrastructure.diskTotalGb} GB free` : ''}
          </p>
        </div>
      ) : null}

      <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
        <h2 className="text-sm font-semibold uppercase tracking-[0.18em] text-slate-500">Services</h2>
        {(services ?? []).length === 0 ? (
          <p className="mt-3 text-sm text-slate-500">Loading service health…</p>
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
          <p className="mt-3 text-sm text-slate-500">Loading background job history…</p>
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
