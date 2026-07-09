import { api } from '../lib/api'
import { importService } from './importService'
import type { AssetRecord, UpsertAssetInput } from '../types/api'
import { asArray } from '../utils/apiDefaults'

function normalizeText(value?: string | null) {
  const trimmed = value?.trim()
  return trimmed ? trimmed : null
}

function normalizeDate(value?: string | null) {
  if (!value?.trim()) return null
  return new Date(`${value}T00:00:00.000Z`).toISOString()
}

function normalizeAssetPayload(input: UpsertAssetInput) {
  const payload = {
    clientId: input.clientId.trim(),
    branchId: input.branchId && input.branchId !== 'all' ? input.branchId : null,
    siteId: input.siteId || null,
    systemId: input.systemId || null,
    assetCategoryId: input.assetCategoryId || null,
    assetName: input.assetName.trim(),
    assetCode: input.assetCode?.trim() ?? '',
    assetType: normalizeText(input.assetType),
    ownershipType: normalizeText(input.ownershipType),
    location: normalizeText(input.location),
    serialNumber: normalizeText(input.serialNumber),
    manufacturer: normalizeText(input.manufacturer),
    model: normalizeText(input.model),
    installationDate: normalizeDate(input.installationDate),
    warrantyExpiryDate: normalizeDate(input.warrantyExpiryDate),
    recommendedPmFrequency: normalizeText(input.recommendedPmFrequency),
    pmFrequencyValue: input.pmFrequencyValue ?? null,
    pmFrequencyUnit: normalizeText(input.pmFrequencyUnit),
    autoSchedulePm: Boolean(input.autoSchedulePm),
    lastPmDate: normalizeDate(input.lastPmDate),
    nextPmDate: normalizeDate(input.nextPmDate),
    notes: normalizeText(input.notes),
    status: input.status?.trim() || 'Active',
    customFieldValues: (input.customFieldValues ?? []).map((item) => ({
      fieldDefinitionId: item.fieldDefinitionId,
      value: normalizeText(item.value) ?? '',
    })),
  }

  if (import.meta.env.DEV) {
    console.log('Asset payload', payload)
  }

  return payload
}

export const assetService = {
  async list(
    branchId?: string | null,
    signal?: AbortSignal,
    options?: { search?: string; status?: 'active' | 'inactive' | 'all'; clientId?: string | null; siteId?: string | null; systemId?: string | null; categoryId?: string | null },
  ): Promise<AssetRecord[]> {
    const response = await api.get<unknown>('/api/assets', {
      query: {
        branchId: branchId && branchId !== 'all' ? branchId : undefined,
        search: options?.search?.trim() || undefined,
        status: options?.status || undefined,
        clientId: options?.clientId || undefined,
        siteId: options?.siteId || undefined,
        systemId: options?.systemId || undefined,
        categoryId: options?.categoryId || undefined,
      },
      signal,
    })
    return asArray<AssetRecord>(response)
  },
  get(id: string, signal?: AbortSignal) {
    return api.get<AssetRecord>(`/api/assets/${id}`, { signal })
  },
  create(input: UpsertAssetInput) {
    return api.post<AssetRecord>('/api/assets', normalizeAssetPayload(input))
  },
  update(id: string, input: UpsertAssetInput) {
    return api.put<AssetRecord>(`/api/assets/${id}`, normalizeAssetPayload(input))
  },
  deactivate(id: string) {
    return api.delete<void>(`/api/assets/${id}`)
  },
  downloadImportTemplate() {
    return importService.downloadTemplate('assets')
  },
}
