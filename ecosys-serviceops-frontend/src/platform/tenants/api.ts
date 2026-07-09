import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../../shared/api/apiClient'
import { endpoints } from '../../shared/api/endpoints'
import type { TenantImpersonationResponse } from '../../shared/types/auth'
import type { PlatformTenantAuditLog, PlatformTenantDetail, PlatformTenantListItem } from './types'

export function useTenants() {
  return useQuery({ queryKey: ['platform', 'tenants'], queryFn: () => api.get<PlatformTenantListItem[]>(endpoints.platform.tenants), staleTime: 30_000 })
}

export function useTenantDetail(id: string | undefined) {
  return useQuery({
    queryKey: ['platform', 'tenants', id],
    queryFn: () => api.get<PlatformTenantDetail>(endpoints.platform.tenant(id ?? '')),
    enabled: Boolean(id),
  })
}

export function useTenantAuditLogs(id: string | undefined) {
  return useQuery({
    queryKey: ['platform', 'tenants', id, 'audit-logs'],
    queryFn: () => api.get<PlatformTenantAuditLog[]>(endpoints.platform.tenantAuditLogs(id ?? '')),
    enabled: Boolean(id),
  })
}

export interface CreateTenantPayload {
  companyName: string
  slug?: string | undefined
  contactName?: string | undefined
  contactEmail?: string | undefined
  contactPhone?: string | undefined
  country?: string | undefined
  createDefaultAdmin?: boolean | undefined
  adminFullName?: string | undefined
  adminEmail?: string | undefined
}

export function useCreateTenant() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: CreateTenantPayload) => api.post<PlatformTenantDetail>(endpoints.platform.tenants, payload),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['platform', 'tenants'] }),
  })
}

function useTenantAction(id: string, url: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (reason?: string) => api.post<PlatformTenantDetail>(url, reason ? { reason } : undefined),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['platform', 'tenants'] })
      void queryClient.invalidateQueries({ queryKey: ['platform', 'tenants', id] })
    },
  })
}

export const useActivateTenant = (id: string) => useTenantAction(id, endpoints.platform.tenantActivate(id))
export const useSuspendTenant = (id: string) => useTenantAction(id, endpoints.platform.tenantSuspend(id))
export const useDeactivateTenant = (id: string) => useTenantAction(id, endpoints.platform.tenantDeactivate(id))
export const useExtendTenantTrial = (id: string) => useTenantAction(id, endpoints.platform.tenantExtendTrial(id))

export function useImpersonateTenant(id: string) {
  return useMutation({
    mutationFn: () => api.post<TenantImpersonationResponse>(endpoints.platform.tenantImpersonate(id)),
  })
}

export function useDeleteTenant() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api.delete<void>(endpoints.platform.tenant(id)),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['platform', 'tenants'] }),
  })
}
