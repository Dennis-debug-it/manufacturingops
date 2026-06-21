import { api } from '../lib/api'
import type { PlatformLicenseUsageSnapshot, PlatformSession, PlatformSummary, PlatformTenant, PlatformTenantActiveCount } from '../types/api'
import { asArray } from '../utils/apiDefaults'

export const commandCentreService = {
  getSummary(signal?: AbortSignal) {
    return api.get<PlatformSummary>('/api/platform/summary', { signal })
  },
  async getTenants(signal?: AbortSignal): Promise<PlatformTenant[]> {
    const response = await api.get<unknown>('/api/platform/tenants', { signal })
    return asArray<PlatformTenant>(response)
  },
  getTenantDetails(tenantId: string, signal?: AbortSignal) {
    return api.get<PlatformTenantActiveCount>(`/api/platform/tenants/${tenantId}/sessions/active-count`, { signal })
  },
  async getSessions(tenantId: string, signal?: AbortSignal): Promise<PlatformSession[]> {
    const response = await api.get<unknown>(`/api/platform/tenants/${tenantId}/sessions`, { signal })
    return asArray<PlatformSession>(response)
  },
  async getUsage(signal?: AbortSignal) {
    const response = await api.get<unknown>('/api/platform/usage', { signal })
    return asArray<PlatformLicenseUsageSnapshot>(response)
  },
  updateTenantStatus(tenantId: string, isActive: boolean) {
    return isActive
      ? api.post<void>(`/api/platform/tenant-licenses/${tenantId}/activate`)
      : api.post<void>(`/api/platform/tenant-licenses/${tenantId}/suspend`)
  },
}
