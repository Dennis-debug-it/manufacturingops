import { api } from '../lib/api'
import { asArray } from '../utils/apiDefaults'

export interface StarterPackAssetCategoryRecord {
  id: string
  name: string
  code?: string | null
  description?: string | null
  sortOrder: number
  defaultServiceIntervalMonths?: number | null
  fieldCount: number
}

export interface StarterPackChecklistRecord {
  id: string
  name: string
  code?: string | null
  workType: 'PreventiveMaintenance' | 'BreakdownCorrective' | string
  defaultIntervalMonths?: number | null
  sectionCount: number
  itemCount: number
}

export interface StarterPackRecord {
  id: string
  name: string
  code: string
  description?: string | null
  contractorType: string
  isActive: boolean
  version: number
  isInstalled: boolean
  installationStatus?: string | null
  assetCategories: StarterPackAssetCategoryRecord[]
  checklistTemplates: StarterPackChecklistRecord[]
}

export const starterPackService = {
  async list(signal?: AbortSignal): Promise<StarterPackRecord[]> {
    const response = await api.get<unknown>('/api/starter-packs', { signal })
    return asArray<StarterPackRecord>(response)
  },

  install(id: string) {
    return api.post<StarterPackRecord>(`/api/starter-packs/${id}/install`, {})
  },
}
