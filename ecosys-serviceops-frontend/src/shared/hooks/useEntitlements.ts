import { useQuery } from '@tanstack/react-query'
import { api } from '../api/apiClient'
import { useAuth } from './useAuth'
import { canAccessModulePath, type ServiceOpsModule } from '../utils/entitlements'

export function useEntitlements() {
  const { user, tenant, isAuthenticated } = useAuth()
  const isPlatform = user?.role === 'super_admin' || user?.role === 'platform_sales'
  const query = useQuery({
    queryKey: ['tenant-license', tenant?.tenantId],
    queryFn: () => api.get<{ planCode: string; planName: string; modules: ServiceOpsModule[]; officeSeats?: number | null; fieldSeats?: number | null; whatsAppAddOn?: string | null }>('/api/tenant/license'),
    // A temporary password's token is refused everywhere but change-password; wait until it's replaced.
    enabled: isAuthenticated && !isPlatform && !user?.mustChangePassword,
    staleTime: 30_000,
  })
  const modules = query.data?.modules ?? []
  return {
    ...query,
    isLoading: !isPlatform && query.isPending,
    modules,
    planName: query.data?.planName,
    planCode: query.data?.planCode,
    /** Office and field seats the backend enforces; null/undefined = not loaded. */
    officeSeats: query.data?.officeSeats ?? null,
    fieldSeats: query.data?.fieldSeats ?? null,
    whatsAppAddOn: query.data?.whatsAppAddOn ?? null,
    hasModule: (module: ServiceOpsModule) => isPlatform || modules.includes(module),
    canAccessPath: (path: string) => isPlatform || canAccessModulePath(path, modules),
  }
}
