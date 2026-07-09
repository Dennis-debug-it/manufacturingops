import { createBlankTenantData } from '../mock/seed'
import { getDatabase, updateDatabase } from './storage'
import { createId } from '../utils/id'
import { nowIso } from '../utils/date'
import type { AppDatabase, TenantRecord, TenantStatus } from '../types/app'

export function syncTenantMetrics(database: AppDatabase) {
  database.tenants = database.tenants.map((tenant) => ({
    ...tenant,
    activeSessionCount: database.sessions.filter((session) => session.tenantId === tenant.id && session.active).length,
    userCount: database.authAccounts.filter((account) => account.tenantId === tenant.id && account.role !== 'superadmin').length,
  }))
  return database
}

export const tenantService = {
  listTenants() {
    return syncTenantMetrics(getDatabase()).tenants
  },
  getTenant(tenantId: string) {
    return this.listTenants().find((tenant) => tenant.id === tenantId)
  },
  getTenantData(tenantId: string) {
    return getDatabase().tenantData[tenantId]
  },
  updateTenantData(tenantId: string, updater: (current: AppDatabase['tenantData'][string]) => AppDatabase['tenantData'][string]) {
    updateDatabase((database) => {
      database.tenantData[tenantId] = updater(database.tenantData[tenantId])
      return syncTenantMetrics(database)
    })
  },
  createTenant(input: { name: string; code: string; region: string; plan: string; subscriptionStatus: TenantRecord['subscriptionStatus'] }) {
    return updateDatabase((database) => {
      const tenant: TenantRecord = {
        id: createId('tenant'),
        name: input.name,
        code: input.code.toUpperCase(),
        region: input.region,
        plan: input.plan,
        status: 'Active',
        subscriptionStatus: input.subscriptionStatus,
        activeSessionCount: 0,
        userCount: 0,
        featureFlags: database.platformFeatureFlags.slice(0, 2).map((flag) => ({ ...flag })),
        createdAt: nowIso(),
      }
      database.tenants.unshift(tenant)
      database.tenantData[tenant.id] = createBlankTenantData(input.name, tenant.code)
      return syncTenantMetrics(database)
    }).tenants[0]
  },
  updateTenant(tenantId: string, patch: Partial<Pick<TenantRecord, 'name' | 'code' | 'plan' | 'region' | 'subscriptionStatus'>>) {
    updateDatabase((database) => {
      database.tenants = database.tenants.map((tenant) =>
        tenant.id === tenantId ? { ...tenant, ...patch, code: patch.code?.toUpperCase() ?? tenant.code } : tenant,
      )
      const data = database.tenantData[tenantId]
      if (data && patch.name) {
        data.settings.companyProfile.companyName = patch.name
        data.settings.companyProfile.legalName = patch.name
      }
      return syncTenantMetrics(database)
    })
  },
  setTenantStatus(tenantId: string, status: TenantStatus) {
    updateDatabase((database) => {
      database.tenants = database.tenants.map((tenant) => (tenant.id === tenantId ? { ...tenant, status } : tenant))
      return syncTenantMetrics(database)
    })
  },
  updateFeatureFlag(tenantId: string, flagId: string, status: TenantRecord['featureFlags'][number]['status']) {
    updateDatabase((database) => {
      database.tenants = database.tenants.map((tenant) =>
        tenant.id === tenantId
          ? { ...tenant, featureFlags: tenant.featureFlags.map((flag) => (flag.id === flagId ? { ...flag, status } : flag)) }
          : tenant,
      )
      return syncTenantMetrics(database)
    })
  },
}
