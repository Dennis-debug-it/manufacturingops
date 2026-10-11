import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../../shared/api/apiClient'
import { endpoints } from '../../shared/api/endpoints'

export const ASSET_FIELD_TYPES = ['Text', 'Number', 'Date', 'Dropdown', 'TextArea'] as const

export interface AssetCategoryField {
  id: string
  assetCategoryId: string
  fieldName: string
  fieldLabel: string
  fieldType: string
  /** Comma-separated choices for a Dropdown field. */
  dropdownOptions?: string | null
  unit?: string | null
  helpText?: string | null
  isRequired: boolean
  isActive?: boolean
  displayOrder: number
}

export interface AssetCategory {
  id: string
  name: string
  code?: string | null
  description?: string | null
  parentCategoryName?: string | null
  isDefault: boolean
  isActive: boolean
  displayOrder: number
  defaultServiceIntervalMonths?: number | null
  fields: AssetCategoryField[]
}

export interface UpsertCategoryPayload {
  name: string
  parentCategoryName: string | null
  description?: string | null
  defaultServiceIntervalMonths?: number | null
  icon: string | null
  displayOrder: number
}

export interface UpsertFieldPayload {
  fieldLabel: string
  fieldType: string
  dropdownOptions: string | null
  unit: string | null
  helpText?: string | null
  isRequired: boolean
  displayOrder: number
  isActive?: boolean
}

const key = ['asset-categories']

export const activeFields = (category: AssetCategory | undefined) =>
  (category?.fields ?? []).filter((field) => field.isActive !== false).sort((a, b) => a.displayOrder - b.displayOrder)

export function useAssetCategories() {
  return useQuery({
    queryKey: key,
    queryFn: async () => {
      const data = await api.get<AssetCategory[]>(endpoints.assetCategories.list)
      return Array.isArray(data) ? data.map((category) => ({ ...category, fields: category.fields ?? [] })) : []
    },
    staleTime: 60_000,
  })
}

export function useSaveAssetCategory() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, payload }: { id?: string; payload: UpsertCategoryPayload }) =>
      id ? api.put<AssetCategory>(endpoints.assetCategories.detail(id), payload) : api.post<AssetCategory>(endpoints.assetCategories.list, payload),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: key }),
  })
}

export function useDeleteAssetCategory() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api.delete(endpoints.assetCategories.detail(id)),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: key }),
  })
}

export function useSaveAssetCategoryField(categoryId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ fieldId, payload }: { fieldId?: string; payload: UpsertFieldPayload }) =>
      fieldId
        ? api.put<AssetCategoryField>(endpoints.assetCategories.field(categoryId, fieldId), payload)
        : api.post<AssetCategoryField>(endpoints.assetCategories.fields(categoryId), payload),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: key }),
  })
}
