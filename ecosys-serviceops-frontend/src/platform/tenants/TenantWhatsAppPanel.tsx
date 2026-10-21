import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import toast from 'react-hot-toast'
import { api } from '../../shared/api/apiClient'
import { endpoints } from '../../shared/api/endpoints'
import { cn } from '../../shared/utils/cn'

interface PlatformTenantWhatsApp {
  tenantId: string
  enabled: boolean
  usedThisMonth: number
  monthlyCap: number
  configured: boolean
}

/**
 * The tenant feature flag "whatsapp" (Release 3 design 6). Ecosys pays Meta for every message from the
 * shared number, so it is switched on per tenant here.
 */
export function TenantWhatsAppPanel({ tenantId, className }: { tenantId: string; className: string }) {
  const queryClient = useQueryClient()
  const queryKey = ['platform', 'tenants', tenantId, 'whatsapp']
  const { data, isLoading, isError } = useQuery({
    queryKey,
    queryFn: () => api.get<PlatformTenantWhatsApp>(endpoints.whatsAppNotifications.platformTenant(tenantId), undefined, { suppressGlobalErrorToast: true }),
  })
  const save = useMutation({
    mutationFn: (enabled: boolean) => api.put<PlatformTenantWhatsApp>(endpoints.whatsAppNotifications.platformTenant(tenantId), { enabled }),
    onSuccess: (result) => {
      queryClient.setQueryData(queryKey, result)
      toast.success(result.enabled ? 'WhatsApp reminders turned on' : 'WhatsApp reminders turned off')
    },
  })

  if (isLoading || isError || !data) return null

  return (
    <section className={className}>
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h3 className="text-base font-semibold text-slate-900">WhatsApp reminders</h3>
          <p className="mt-1 text-sm text-slate-500">
            Sent from the shared Ecosys number. {data.usedThisMonth.toLocaleString()} of {data.monthlyCap.toLocaleString()} messages used this month.
            {data.configured ? '' : ' The shared number is not configured on this server yet.'}
          </p>
        </div>
        <button
          type="button"
          role="switch"
          aria-checked={data.enabled}
          disabled={save.isPending}
          onClick={() => save.mutate(!data.enabled)}
          className={cn('h-9 rounded-lg px-3 text-sm font-medium ring-1 ring-inset disabled:opacity-60', data.enabled ? 'bg-[var(--app-primary)] text-white ring-transparent' : 'bg-white text-slate-600 ring-slate-300')}
        >
          {data.enabled ? 'On' : 'Off'}
        </button>
      </div>
    </section>
  )
}
