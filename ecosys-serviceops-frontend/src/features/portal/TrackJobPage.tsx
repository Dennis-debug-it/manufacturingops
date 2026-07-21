import { useParams } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { CheckCircle2, Clock, MapPin, Wrench } from 'lucide-react'
import { api } from '../../shared/api/apiClient'
import { endpoints } from '../../shared/api/endpoints'
import { Spinner } from '../../shared/components/Spinner'
import { formatDateTime } from '../../shared/utils/dates'
import { cn } from '../../shared/utils/cn'

interface PublicWorkOrderTracking {
  workOrderNumber: string
  title: string
  status: string
  companyName: string | null
  technicianName: string | null
  scheduledStartAt: string | null
  scheduledEndAt: string | null
  arrivalAt: string | null
  completedAt: string | null
  lastUpdatedAt: string
}

const steps = [
  { key: 'Assigned', label: 'Assigned', icon: Wrench },
  { key: 'EnRoute', label: 'En Route', icon: Clock },
  { key: 'Arrived', label: 'Arrived', icon: MapPin },
  { key: 'Completed', label: 'Completed', icon: CheckCircle2 },
] as const

function resolveStepIndex(data: PublicWorkOrderTracking): number {
  if (data.completedAt) return 3
  if (data.arrivalAt) return 2
  const status = data.status.toLowerCase()
  if (status.includes('progress') || status.includes('transit') || status.includes('route')) return 1
  return 0
}

function Shell({ children }: { children: React.ReactNode }) {
  return (
    <div className="min-h-screen bg-slate-50 px-4 py-10 sm:px-6 lg:px-8">
      <div className="mx-auto max-w-xl rounded-3xl border border-slate-200 bg-white p-8 shadow-sm">{children}</div>
    </div>
  )
}

export function TrackJobPage() {
  const { token } = useParams<{ token: string }>()

  const { data, isLoading, isError } = useQuery({
    queryKey: ['public-track-job', token],
    queryFn: () => api.get<PublicWorkOrderTracking>(endpoints.public.trackJob(token ?? '')),
    enabled: Boolean(token),
    retry: false,
    refetchInterval: 60_000,
  })

  if (isLoading) {
    return (
      <Shell>
        <Spinner label="Loading job status" />
      </Shell>
    )
  }

  if (isError || !data) {
    return (
      <Shell>
        <h1 className="text-xl font-semibold text-slate-900">Tracking link unavailable</h1>
        <p className="mt-2 text-sm leading-6 text-slate-600">This tracking link is invalid or has expired. Please contact your service provider for an update.</p>
      </Shell>
    )
  }

  const activeStep = resolveStepIndex(data)

  return (
    <Shell>
      <div className="text-xs font-semibold uppercase tracking-[0.2em] text-slate-500">{data.companyName ?? 'Service Provider'}</div>
      <h1 className="mt-2 text-xl font-semibold text-slate-900">Job {data.workOrderNumber}</h1>
      <p className="mt-1 text-sm leading-6 text-slate-600">{data.title}</p>

      <div className="mt-8 flex items-center justify-between">
        {steps.map((step, index) => {
          const Icon = step.icon
          const reached = index <= activeStep
          return (
            <div key={step.key} className="flex flex-1 flex-col items-center text-center">
              <div className="flex w-full items-center">
                <div className={cn('h-px flex-1', index === 0 ? 'opacity-0' : reached ? 'bg-slate-900' : 'bg-slate-200')} />
                <div
                  className={cn(
                    'flex h-9 w-9 shrink-0 items-center justify-center rounded-full border-2',
                    reached ? 'border-slate-900 bg-slate-900 text-white' : 'border-slate-200 bg-white text-slate-400',
                  )}
                >
                  <Icon className="h-4 w-4" />
                </div>
                <div className={cn('h-px flex-1', index === steps.length - 1 ? 'opacity-0' : reached && index < activeStep ? 'bg-slate-900' : 'bg-slate-200')} />
              </div>
              <span className={cn('mt-2 text-[11px] font-medium', reached ? 'text-slate-900' : 'text-slate-400')}>{step.label}</span>
            </div>
          )
        })}
      </div>

      <div className="mt-8 space-y-3 rounded-2xl border border-slate-200 p-4">
        <div className="flex items-center justify-between text-sm">
          <span className="text-slate-500">Status</span>
          <span className="font-medium text-slate-900">{data.status}</span>
        </div>
        {data.technicianName ? (
          <div className="flex items-center justify-between text-sm">
            <span className="text-slate-500">Technician</span>
            <span className="font-medium text-slate-900">{data.technicianName}</span>
          </div>
        ) : null}
        {data.scheduledStartAt ? (
          <div className="flex items-center justify-between text-sm">
            <span className="text-slate-500">Estimated Arrival</span>
            <span className="font-medium text-slate-900">{formatDateTime(data.scheduledStartAt)}</span>
          </div>
        ) : null}
        {data.arrivalAt ? (
          <div className="flex items-center justify-between text-sm">
            <span className="text-slate-500">Arrived</span>
            <span className="font-medium text-slate-900">{formatDateTime(data.arrivalAt)}</span>
          </div>
        ) : null}
        {data.completedAt ? (
          <div className="flex items-center justify-between text-sm">
            <span className="text-slate-500">Completed</span>
            <span className="font-medium text-slate-900">{formatDateTime(data.completedAt)}</span>
          </div>
        ) : null}
      </div>

      <p className="mt-6 text-center text-xs text-slate-400">Last updated {formatDateTime(data.lastUpdatedAt)}</p>
    </Shell>
  )
}
