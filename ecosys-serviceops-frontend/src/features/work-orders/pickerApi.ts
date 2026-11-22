import { useQuery } from '@tanstack/react-query'
import { api } from '../../shared/api/apiClient'
import { endpoints } from '../../shared/api/endpoints'

export interface TechnicianOption {
  id: string
  fullName: string
  skillCategory: string | null
}

export interface AssignmentGroupOption {
  id: string
  name: string
  isActive?: boolean
  members?: { technicianId: string; memberName: string | null; isLead: boolean; isActive: boolean }[]
}

export interface MaterialOption {
  id: string
  itemName: string
  unitOfMeasure: string
  quantityOnHand: number
  unitCost: number | null
}

export function useTechnicianOptions() {
  return useQuery({
    queryKey: ['picker', 'technicians'],
    queryFn: () => api.get<TechnicianOption[]>(endpoints.technicians.list),
    staleTime: 60_000,
  })
}

export function useAssignmentGroupOptions() {
  return useQuery({
    queryKey: ['picker', 'assignment-groups'],
    queryFn: () => api.get<AssignmentGroupOption[]>(endpoints.assignmentGroups.list),
    staleTime: 60_000,
  })
}

export function useMaterialOptions(search: string) {
  return useQuery({
    queryKey: ['picker', 'materials', search],
    queryFn: () => api.get<MaterialOption[]>(endpoints.materials.list, { search }),
    staleTime: 30_000,
  })
}
