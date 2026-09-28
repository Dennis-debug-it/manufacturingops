import { useQuery } from '@tanstack/react-query'
import { api } from '../api/apiClient'
import { useAuth } from './useAuth'
import { canAccessModulePath, type ServiceOpsModule } from '../utils/entitlements'

export function useEntitlements() {
  const { user, tenant, isAuthenticated } = useAuth()
  const isPlatform = user?.role === 'super_admin' || user?.role === 'platform_sales'
  const query = useQuery({
    queryKey: ['tenant-license', tenant?.tenantId],
    queryFn: () => api.get<{ planCode: string; planName: string; modules: ServiceOpsModule[]; maxUsers?: number | null }>('/api/tenant/license'),
    enabled: isAuthenticated && !isPlatform,
    staleTime: 30_000,
  })
  const modules = query.data?.modules ?? []
  return {
    ...query,
    isLoading: !isPlatform && query.isPending,
    modules,
    planName: query.data?.planName,
    planCode: query.data?.planCode,
    /** Licensed seats the backend enforces; null/undefined = no limit. */
    maxUsers: query.data?.maxUsers ?? null,
    hasModule: (module: ServiceOpsModule) => isPlatform || modules.includes(module),
    canAccessPath: (path: string) => isPlatform || canAccessModulePath(path, modules),
  }
}
