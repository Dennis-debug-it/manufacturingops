import { api } from '../lib/api'
import type { LicensePlan, LicenseUsageSnapshot, PlatformLicenseUsageSnapshot, TenantLicenseSnapshot } from '../types/api'
import { asArray } from '../utils/apiDefaults'

export const licenseService = {
  getTenantLicense(signal?: AbortSignal) {
    return api.get<TenantLicenseSnapshot>('/api/tenant/license', { signal })
  },
  getTenantUsage(signal?: AbortSignal) {
    return api.get<LicenseUsageSnapshot>('/api/tenant/license/usage', { signal })
  },
  getSettingsLicense(signal?: AbortSignal) {
    return api.get<TenantLicenseSnapshot>('/api/settings/license', { signal })
  },
  async getPlans(signal?: AbortSignal): Promise<LicensePlan[]> {
    const response = await api.get<unknown>('/api/platform/licensing/plans', { signal })
    return asArray<LicensePlan>(response)
  },
  async getTenantLicenses(signal?: AbortSignal): Promise<TenantLicenseSnapshot[]> {
    const response = await api.get<unknown>('/api/platform/tenant-licenses', { signal })
    return asArray<TenantLicenseSnapshot>(response)
  },
  async getPlatformUsage(signal?: AbortSignal): Promise<PlatformLicenseUsageSnapshot[]> {
    const response = await api.get<unknown>('/api/platform/licensing/usage', { signal })
    return asArray<PlatformLicenseUsageSnapshot>(response)
  },
  suspendTenant(tenantId: string) {
    return api.post<void>(`/api/platform/tenant-licenses/${tenantId}/suspend`)
  },
  activateTenant(tenantId: string) {
    return api.post<void>(`/api/platform/tenant-licenses/${tenantId}/activate`)
  },
}
