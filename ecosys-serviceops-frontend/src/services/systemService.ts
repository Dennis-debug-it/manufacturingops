import { api } from '../lib/api'
import type { SystemDetail, SystemRecord, UpsertSystemInput } from '../types/api'
import { asArray } from '../utils/apiDefaults'

function normalizeDate(value?: string | null) {
  if (!value?.trim()) return null
  return new Date(`${value}T00:00:00.000Z`).toISOString()
}

function normalizeText(value?: string | null) {
  const trimmed = value?.trim()
  return trimmed ? trimmed : null
}

function normalizePayload(input: UpsertSystemInput) {
  return {
    systemName: input.systemName.trim(),
    systemType: input.systemType.trim(),
    description: normalizeText(input.description),
    status: normalizeText(input.status),
    manufacturer: normalizeText(input.manufacturer),
    model: normalizeText(input.model),
    serialNumber: normalizeText(input.serialNumber),
    installationDate: normalizeDate(input.installationDate),
    notes: normalizeText(input.notes),
  }
}

export const systemService = {
  async list(clientId: string, siteId: string, signal?: AbortSignal): Promise<SystemRecord[]> {
    const response = await api.get<unknown>(`/api/clients/${clientId}/sites/${siteId}/systems`, { signal })
    return asArray<SystemRecord>(response)
  },
  get(clientId: string, siteId: string, systemId: string, signal?: AbortSignal) {
    return api.get<SystemDetail>(`/api/clients/${clientId}/sites/${siteId}/systems/${systemId}`, { signal })
  },
  getById(systemId: string, signal?: AbortSignal) {
    return api.get<SystemDetail>(`/api/systems/${systemId}`, { signal })
  },
  create(clientId: string, siteId: string, input: UpsertSystemInput) {
    return api.post<SystemRecord>(`/api/clients/${clientId}/sites/${siteId}/systems`, normalizePayload(input))
  },
  update(clientId: string, siteId: string, systemId: string, input: UpsertSystemInput) {
    return api.put<SystemRecord>(`/api/clients/${clientId}/sites/${siteId}/systems/${systemId}`, normalizePayload(input))
  },
  updateById(systemId: string, input: UpsertSystemInput) {
    return api.put<SystemRecord>(`/api/systems/${systemId}`, normalizePayload(input))
  },
  remove(systemId: string) {
    return api.delete<void>(`/api/systems/${systemId}`)
  },
}
