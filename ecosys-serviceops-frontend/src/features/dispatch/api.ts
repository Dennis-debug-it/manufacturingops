import { useQuery } from '@tanstack/react-query'
import { api } from '../../shared/api/apiClient'

export type TechnicianLiveStatus = 'available' | 'on-job' | 'offline'

export interface TechnicianLocation {
  id: string
  fullName: string
  status: TechnicianLiveStatus
  latitude: number | null
  longitude: number | null
  lastLocationAt: string | null
  currentWorkOrderId: string | null
  currentWorkOrderNumber: string | null
}

export function useTechnicianLocations() {
  return useQuery({
    queryKey: ['dispatch', 'technician-locations'],
    queryFn: () => api.get<TechnicianLocation[]>('/api/dispatch/technician-locations'),
    staleTime: 15_000,
    refetchInterval: 30_000,
  })
}

export interface TechnicianLocationHistoryPoint {
  latitude: number
  longitude: number
  occurredAt: string
  eventType: string
  workOrderId: string
}

export function useTechnicianLocationHistory(technicianId: string | undefined, date: string) {
  return useQuery({
    queryKey: ['dispatch', 'technician-locations', technicianId, 'history', date],
    queryFn: () =>
      api.get<TechnicianLocationHistoryPoint[]>(`/api/dispatch/technician-locations/${technicianId}/history`, { date }),
    enabled: Boolean(technicianId),
    staleTime: 60_000,
  })
}

export interface RouteStop {
  id: string
  woNumber: string
  clientName: string
  siteAddress: string
  lat: number
  lng: number
  scheduledTime: string | null
  estimatedTravelMins: number
}

export function useOptimisedRoute(technicianId: string | undefined, date: string) {
  return useQuery({
    queryKey: ['dispatch', 'route-optimise', technicianId, date],
    queryFn: () => api.get<RouteStop[]>('/api/dispatch/route-optimise', { technicianId, date }),
    enabled: Boolean(technicianId && date),
    staleTime: 30_000,
  })
}
