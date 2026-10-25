import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import toast from 'react-hot-toast'
import { api } from '../../shared/api/apiClient'
import { endpoints } from '../../shared/api/endpoints'
import { cn } from '../../shared/utils/cn'

type WhatsAppAddOn = 'whatsapp_notifications' | 'whatsapp_business'

interface PlatformTenantWhatsApp {
  tenantId: string
  enabled: boolean
  usedThisMonth: number
  monthlyCap: number
  configured: boolean
  addOn: WhatsAppAddOn | null
}

const options: ReadonlyArray<{ value: WhatsAppAddOn | null; label: string }> = [
  { value: null, label: 'Off' },
  { value: 'whatsapp_notifications', label: 'Notifications' },
  { value: 'whatsapp_business', label: 'Business' },
]

/**
 * The optional WhatsApp add-on. Notifications: reminders from the shared Ecosys number. Business: also the
 * tenant's own number and two-way inbox. Ecosys pays Meta for every message, so it is switched on per tenant here.
 */
export function TenantWhatsAppPanel({ tenantId, className }: { tenantId: string; className: string }) {
  const queryClient = useQueryClient()
  const queryKey = ['platform', 'tenants', tenantId, 'whatsapp']
  const { data, isLoading, isError } = useQuery({
    queryKey,
    queryFn: () => api.get<PlatformTenantWhatsApp>(endpoints.whatsAppNotifications.platformTenant(tenantId), undefined, { suppressGlobalErrorToast: true }),
  })
  const save = useMutation({
    mutationFn: (addOn: WhatsAppAddOn | null) => api.put<PlatformTenantWhatsApp>(endpoints.whatsAppNotifications.platformTenant(tenantId), { enabled: addOn !== null, addOn }),
    onSuccess: (result) => {
      queryClient.setQueryData(queryKey, result)
      // The Business add-on changes the tenant's modules (the WhatsApp inbox).
      void queryClient.invalidateQueries({ queryKey: ['platform', 'tenants', tenantId] })
      toast.success(result.addOn ? `WhatsApp ${result.addOn === 'whatsapp_business' ? 'Business' : 'Notifications'} turned on` : 'WhatsApp turned off')
    },
  })

  if (isLoading || isError || !data) return null

  return (
    <section className={className}>
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h3 className="text-base font-semibold text-slate-900">WhatsApp add-on</h3>
          <p className="mt-1 text-sm text-slate-500">
            Optional on any edition. {data.usedThisMonth.toLocaleString()} of {data.monthlyCap.toLocaleString()} messages used this month.
            {data.configured ? '' : ' The shared number is not configured on this server yet.'}
          </p>
        </div>
        <div role="radiogroup" aria-label="WhatsApp add-on" className="inline-flex overflow-hidden rounded-lg ring-1 ring-inset ring-slate-300">
          {options.map((option) => (
            <button
              key={option.label}
              type="button"
              role="radio"
              aria-checked={data.addOn === option.value}
              disabled={save.isPending}
              onClick={() => { if (data.addOn !== option.value) save.mutate(option.value) }}
              className={cn('h-9 px-3 text-sm font-medium disabled:opacity-60', data.addOn === option.value ? 'bg-[var(--app-primary)] text-white' : 'bg-white text-slate-600')}
            >
              {option.label}
            </button>
          ))}
        </div>
      </div>
    </section>
  )
}
