import { api } from '../lib/api'
import { importService } from './importService'
import type { MaterialItem, StockMovement, UpsertMaterialInput } from '../types/api'
import { asArray } from '../utils/apiDefaults'

export const materialService = {
  async list(options?: { branchId?: string | null; lowStockOnly?: boolean; search?: string; status?: 'active' | 'inactive' | 'all'; signal?: AbortSignal }): Promise<MaterialItem[]> {
    const response = await api.get<unknown>('/api/materials', {
      query: {
        branchId: options?.branchId && options.branchId !== 'all' ? options.branchId : undefined,
        lowStockOnly: options?.lowStockOnly ? true : undefined,
        search: options?.search?.trim() || undefined,
        status: options?.status || undefined,
      },
      signal: options?.signal,
    })
    return asArray<MaterialItem>(response)
  },
  get(id: string, branchId?: string | null, signal?: AbortSignal) {
    return api.get<MaterialItem>(`/api/materials/${id}`, {
      query: { branchId: branchId && branchId !== 'all' ? branchId : undefined },
      signal,
    })
  },
  create(input: UpsertMaterialInput) {
    return api.post<MaterialItem>('/api/materials', input)
  },
  update(id: string, input: UpsertMaterialInput) {
    return api.put<MaterialItem>(`/api/materials/${id}`, input)
  },
  remove(id: string) {
    return api.delete<void>(`/api/materials/${id}`)
  },
  stockIn(id: string, input: { branchId?: string | null; quantity: number; unitCost?: number | null; reason?: string; referenceNumber?: string }) {
    return api.post<MaterialItem>(`/api/materials/${id}/replenish`, input)
  },
  stockOut(id: string, input: { branchId?: string | null; quantity: number; reason?: string }) {
    return api.post<MaterialItem>(`/api/materials/${id}/adjust`, {
      branchId: input.branchId,
      quantityChange: Math.abs(input.quantity) * -1,
      reason: input.reason,
    })
  },
  adjust(id: string, input: { branchId?: string | null; quantityChange: number; reason?: string }) {
    return api.post<MaterialItem>(`/api/materials/${id}/adjust`, input)
  },
  async getMovements(id: string, branchId?: string | null, signal?: AbortSignal): Promise<StockMovement[]> {
    const response = await api.get<unknown>(`/api/materials/${id}/movements`, {
      query: { branchId: branchId && branchId !== 'all' ? branchId : undefined },
      signal,
    })
    return asArray<StockMovement>(response)
  },
  downloadImportTemplate() {
    return importService.downloadTemplate('materials')
  },
}
