import { api } from '../lib/api'
import type { SiteDetailRecord, SiteRecord, UpsertSiteInput } from '../types/api'
import { asArray } from '../utils/apiDefaults'

export const siteService = {
  list(clientId: string, options?: { search?: string; status?: string; signal?: AbortSignal }): Promise<SiteRecord[]> {
    return api
      .get<unknown>(`/api/clients/${clientId}/sites`, {
        query: {
          q: options?.search,
          status: options?.status,
        },
        signal: options?.signal,
      })
      .then((r) => asArray<SiteRecord>(r))
  },

  get(clientId: string, siteId: string, signal?: AbortSignal) {
    return api.get<SiteDetailRecord>(
      `/api/clients/${clientId}/sites/${siteId}`,
      { signal },
    )
  },

  create(clientId: string, input: UpsertSiteInput) {
    return api.post<SiteRecord>(`/api/clients/${clientId}/sites`, input)
  },

  update(clientId: string, siteId: string, input: UpsertSiteInput) {
    return api.put<SiteRecord>(`/api/clients/${clientId}/sites/${siteId}`, input)
  },

  deactivate(clientId: string, siteId: string) {
    return api.delete<void>(`/api/clients/${clientId}/sites/${siteId}`)
  },

  search(options?: { clientId?: string; q?: string; region?: string; signal?: AbortSignal }) {
    return api.get<SiteRecord[]>('/api/sites/search', {
      query: {
        clientId: options?.clientId,
        q: options?.q,
        region: options?.region,
      },
      signal: options?.signal,
    })
  },
}
