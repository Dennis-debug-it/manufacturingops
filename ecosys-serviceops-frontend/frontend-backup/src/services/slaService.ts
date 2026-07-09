import { api } from '../lib/api'
import type { SlaDefinitionRecord, UpsertSlaDefinitionInput } from '../types/api'
import { asArray } from '../utils/apiDefaults'

export const slaService = {
  async list(signal?: AbortSignal): Promise<SlaDefinitionRecord[]> {
    const response = await api.get<unknown>('/api/sla-definitions', { signal })
    return asArray<SlaDefinitionRecord>(response)
  },

  get(id: string, signal?: AbortSignal) {
    return api.get<SlaDefinitionRecord>(`/api/sla-definitions/${id}`, { signal })
  },

  create(input: UpsertSlaDefinitionInput) {
    return api.post<SlaDefinitionRecord>('/api/sla-definitions', input)
  },

  update(id: string, input: UpsertSlaDefinitionInput) {
    return api.put<SlaDefinitionRecord>(`/api/sla-definitions/${id}`, input)
  },

  remove(id: string) {
    return api.delete<void>(`/api/sla-definitions/${id}`)
  },
}
