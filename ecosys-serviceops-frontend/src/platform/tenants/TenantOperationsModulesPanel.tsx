import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import toast from 'react-hot-toast'
import { api } from '../../shared/api/apiClient'
import { endpoints } from '../../shared/api/endpoints'
import { cn } from '../../shared/utils/cn'

interface PlatformOperationsModules {
  tenantId: string
  enabled: string[]
  available: string[]
}

const LABELS: Record<string, { label: string; description: string }> = {
  visitors: { label: 'Visitor Management', description: 'Visits, host approval, gate checks, badges and the roll call.' },
  security: { label: 'Security Patrol', description: 'Guard patrols checked at checkpoints, missed-patrol alerts, incidents and the panic button.' },
}

/** The Factory Operations add-ons a tenant takes with its edition. Each switch adds its module. */
export function TenantOperationsModulesPanel({ tenantId, className }: { tenantId: string; className: string }) {
  const queryClient = useQueryClient()
  const queryKey = ['platform', 'tenants', tenantId, 'operations-modules']
  const { data, isLoading, isError } = useQuery({
    queryKey,
    queryFn: () => api.get<PlatformOperationsModules>(endpoints.platform.operationsModules(tenantId), undefined, { suppressGlobalErrorToast: true }),
  })
  const save = useMutation({
    mutationFn: (modules: string[]) => api.put<PlatformOperationsModules>(endpoints.platform.operationsModules(tenantId), { modules }),
    onSuccess: (result) => {
      queryClient.setQueryData(queryKey, result)
      void queryClient.invalidateQueries({ queryKey: ['platform', 'tenants', tenantId] })
      toast.success('Add-ons saved')
    },
  })

  if (isLoading || isError || !data) return null

  return (
    <section className={className}>
      <h3 className="text-base font-semibold text-slate-900">Factory Operations add-ons</h3>
      <p className="mt-1 text-sm text-slate-500">Optional on any edition.</p>
      <div className="mt-3 space-y-2">
        {data.available.map((module) => {
          const on = data.enabled.includes(module)
          const info = LABELS[module] ?? { label: module, description: '' }
          return (
            <div key={module} className="flex items-center justify-between gap-3 rounded-lg border border-slate-200 px-3 py-2">
              <div>
                <div className="text-sm font-medium text-slate-800">{info.label}</div>
                <div className="text-xs text-slate-500">{info.description}</div>
              </div>
              <button
                type="button"
                role="switch"
                aria-checked={on}
                aria-label={info.label}
                disabled={save.isPending}
                onClick={() => save.mutate(on ? data.enabled.filter((x) => x !== module) : [...data.enabled, module])}
                className={cn('relative h-6 w-11 shrink-0 rounded-full transition disabled:opacity-60', on ? 'bg-[var(--app-primary)]' : 'bg-slate-300')}
              >
                <span className={cn('absolute top-0.5 h-5 w-5 rounded-full bg-white shadow transition', on ? 'left-5' : 'left-0.5')} />
              </button>
            </div>
          )
        })}
      </div>
    </section>
  )
}
