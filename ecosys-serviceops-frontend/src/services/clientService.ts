import { api } from '../lib/api'
import type { ClientRecord, ClientSummaryRecord, UpsertClientInput } from '../types/api'
import { asArray } from '../utils/apiDefaults'

function normalizeText(value?: string | null) {
  const trimmed = value?.trim()
  return trimmed ? trimmed : null
}

function normalizeDate(value?: string | null) {
  if (!value?.trim()) return null
  return new Date(`${value}T00:00:00.000Z`).toISOString()
}

function normalizeClientPayload(input: UpsertClientInput) {
  return {
    clientName: input.clientName.trim(),
    clientType: normalizeText(input.clientType),
    email: normalizeText(input.email),
    phone: normalizeText(input.phone),
    location: normalizeText(input.location),
    contactPerson: normalizeText(input.contactPerson),
    contactPhone: normalizeText(input.contactPhone),
    slaDefinitionId: input.slaDefinitionId || null,
    activeStartDate: normalizeDate(input.activeStartDate),
    activeDurationValue: input.activeDurationValue ?? null,
    activeDurationUnit: normalizeText(input.activeDurationUnit),
    notes: normalizeText(input.notes),
  }
}

export const clientService = {
  async list(options?: { search?: string; status?: 'active' | 'inactive' | 'all'; signal?: AbortSignal }): Promise<ClientRecord[]> {
    const response = await api.get<unknown>('/api/clients', {
      query: {
        search: options?.search?.trim() || undefined,
        status: options?.status || undefined,
      },
      signal: options?.signal,
    })
    return asArray<ClientRecord>(response)
  },
  get(id: string, signal?: AbortSignal) {
    return api.get<ClientRecord>(`/api/clients/${id}`, { signal })
  },
  getSummary(id: string, signal?: AbortSignal) {
    return api.get<ClientSummaryRecord>(`/api/clients/${id}/summary`, { signal })
  },
  create(input: UpsertClientInput) {
    return api.post<ClientRecord>('/api/clients', normalizeClientPayload(input))
  },
  update(id: string, input: UpsertClientInput) {
    return api.put<ClientRecord>(`/api/clients/${id}`, normalizeClientPayload(input))
  },
  activate(id: string) {
    return api.patch<ClientRecord>(`/api/clients/${id}/activate`)
  },
  deactivate(id: string) {
    return api.patch<ClientRecord>(`/api/clients/${id}/deactivate`)
  },
}
