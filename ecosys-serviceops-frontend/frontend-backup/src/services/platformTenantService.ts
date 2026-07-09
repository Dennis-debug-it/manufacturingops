import { api } from '../lib/api'
import type {
  PlatformSummary,
  PlatformTenant,
  PlatformTenantAuditLog,
  PlatformTenantDetail,
  PlatformTenantSummary,
  UpdatePlatformTenantStatusInput,
  UpsertPlatformTenantInput,
} from '../types/api'
import { asArray } from '../utils/apiDefaults'

export const platformTenantService = {
  getPlatformSummary(signal?: AbortSignal) {
    return api.get<PlatformSummary>('/api/platform/summary', { signal })
  },
  async getPlatformTenants(signal?: AbortSignal): Promise<PlatformTenant[]> {
    const response = await api.get<unknown>('/api/platform/tenants', { signal })
    return asArray<PlatformTenant>(response)
  },
  getPlatformTenant(tenantId: string, signal?: AbortSignal) {
    return api.get<PlatformTenantDetail>(`/api/platform/tenants/${tenantId}`, { signal })
  },
  createPlatformTenant(payload: UpsertPlatformTenantInput) {
    return api.post<PlatformTenantDetail>('/api/platform/tenants', payload)
  },
  updatePlatformTenant(tenantId: string, payload: UpsertPlatformTenantInput) {
    return api.put<PlatformTenantDetail>(`/api/platform/tenants/${tenantId}`, payload)
  },
  updatePlatformTenantStatus(tenantId: string, payload: UpdatePlatformTenantStatusInput) {
    return api.patch<PlatformTenantDetail>(`/api/platform/tenants/${tenantId}/status`, payload)
  },
  deactivatePlatformTenant(tenantId: string) {
    return api.delete<void>(`/api/platform/tenants/${tenantId}`)
  },
  getPlatformTenantSummary(tenantId: string, signal?: AbortSignal) {
    return api.get<PlatformTenantSummary>(`/api/platform/tenants/${tenantId}/summary`, { signal })
  },
  async getPlatformTenantAuditLogs(tenantId: string, signal?: AbortSignal): Promise<PlatformTenantAuditLog[]> {
    const response = await api.get<unknown>(`/api/platform/tenants/${tenantId}/audit-logs`, { signal })
    return asArray<PlatformTenantAuditLog>(response)
  },
}
