import { api } from '../lib/api'
import { asArray } from '../utils/apiDefaults'

export interface AssetCategoryRecord {
  id: string
  tenantId?: string | null
  code?: string | null
  description?: string | null
  contractorType?: string | null
  parentCategoryName?: string | null
  name: string
  icon?: string | null
  isDefault: boolean
  isActive: boolean
  isSystemTemplate?: boolean
  defaultServiceIntervalMonths?: number | null
  displayOrder: number
  fields: AssetCategoryFieldRecord[]
}

export interface AssetCategoryFieldRecord {
  id: string
  assetCategoryId: string
  fieldName: string
  fieldLabel: string
  fieldType: string
  dropdownOptions?: string | null
  unit?: string | null
  helpText?: string | null
  isRequired: boolean
  isActive?: boolean
  displayOrder: number
}

export interface UpsertAssetCategoryInput {
  name: string
  parentCategoryName?: string | null
  icon?: string | null
  isDefault?: boolean
  isActive?: boolean
  displayOrder?: number
  code?: string | null
  description?: string | null
  defaultServiceIntervalMonths?: number | null
}

export interface UpsertAssetCategoryFieldInput {
  fieldName: string
  fieldLabel: string
  fieldType: string
  dropdownOptions?: string | null
  unit?: string | null
  helpText?: string | null
  isRequired?: boolean
  isActive?: boolean
  displayOrder?: number
}

export const assetCategoryService = {
  list(signal?: AbortSignal, includeInactive = false): Promise<AssetCategoryRecord[]> {
    return api.get<unknown>('/api/asset-categories', {
      signal,
      query: includeInactive ? { includeInactive: true } : undefined,
    }).then((r) => asArray<AssetCategoryRecord>(r))
  },

  get(id: string, signal?: AbortSignal) {
    return api.get<AssetCategoryRecord>(`/api/asset-categories/${id}`, { signal })
  },

  create(input: UpsertAssetCategoryInput) {
    return api.post<AssetCategoryRecord>('/api/asset-categories', input)
  },

  update(id: string, input: UpsertAssetCategoryInput) {
    return api.put<AssetCategoryRecord>(`/api/asset-categories/${id}`, input)
  },

  deactivate(id: string) {
    return api.delete<void>(`/api/asset-categories/${id}`)
  },

  addField(categoryId: string, input: UpsertAssetCategoryFieldInput) {
    return api.post<AssetCategoryFieldRecord>(`/api/asset-categories/${categoryId}/fields`, input)
  },

  updateField(categoryId: string, fieldId: string, input: UpsertAssetCategoryFieldInput) {
    return api.put<AssetCategoryFieldRecord>(`/api/asset-categories/${categoryId}/fields/${fieldId}`, input)
  },

  deleteField(categoryId: string, fieldId: string) {
    return api.delete<void>(`/api/asset-categories/${categoryId}/fields/${fieldId}`)
  },
}
