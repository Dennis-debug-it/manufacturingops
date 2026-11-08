import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../../../shared/api/apiClient'
import type { ConditionLevel } from '../types'

export const VEHICLE_TYPES = ['Lorry', 'Pickup', 'Tractor', 'Trailer', 'Motorcycle', 'Other'] as const

export interface VehicleDocument { type: string; label: string; expiresOn: string; daysLeft: number }

export interface FleetVehicle {
  assetId: string
  assetCode: string
  assetName: string
  location: string | null
  registrationNumber: string
  vehicleType: string
  make: string | null
  model: string | null
  year: number | null
  payloadKg: number | null
  fuelType: string
  route: string | null
  driverName: string | null
  driverPhone: string | null
  odometerMeterId: string | null
  odometerKm: number | null
  odometerAt: string | null
  engineHoursMeterId: string | null
  engineHours: number | null
  kmToService: number | null
  serviceDueOn: string | null
  servicePlanName: string | null
  openJobs: number
  documents: VehicleDocument[]
  trackerDeviceId: string | null
  lastLatitude: number | null
  lastLongitude: number | null
  lastSpeedKph: number | null
  lastPositionAt: string | null
  lastPositionSource: string | null
  fuel: { latestKmPerLitre: number | null; averageKmPerLitre: number | null; usingMore: boolean }
  condition: ConditionLevel
  operationalStatus: string
}

export interface FuelLogEntry {
  id: string
  filledAt: string
  litres: number
  cost: number | null
  odometerKm: number | null
  fullTank: boolean
  station: string | null
  notes: string | null
  recordedByName: string | null
}

export interface VehicleDetail {
  vehicle: FleetVehicle
  insuranceExpiresOn: string | null
  inspectionExpiresOn: string | null
  speedGovernorExpiresOn: string | null
  fuelLogs: FuelLogEntry[]
}

export interface SaveVehiclePayload {
  registrationNumber: string
  vehicleType: string
  orgNodeId: string | null
  make: string | null
  model: string | null
  year: number | null
  payloadKg: number | null
  fuelType: string | null
  route: string | null
  driverName: string | null
  driverPhone: string | null
  insuranceExpiresOn: string | null
  inspectionExpiresOn: string | null
  speedGovernorExpiresOn: string | null
  trackerDeviceId: string | null
  odometerKm?: number | null
  trackEngineHours?: boolean
  engineHours?: number | null
}

export interface TelematicsConnection {
  provider: string
  baseUrl: string
  username: string | null
  hasPassword: boolean
  isEnabled: boolean
  lastSyncAt: string | null
  lastSyncVehicles: number
  lastSyncError: string | null
}

const KEY = ['factory', 'fleet']

export function useFleet(orgNodeId: string) {
  return useQuery({ queryKey: [...KEY, 'list', orgNodeId], queryFn: () => api.get<FleetVehicle[]>('/api/fleet/vehicles', { orgNodeId: orgNodeId || undefined }), refetchInterval: 120_000 })
}

export function useVehicle(assetId: string | null) {
  return useQuery({ queryKey: [...KEY, 'vehicle', assetId], queryFn: () => api.get<VehicleDetail>(`/api/fleet/vehicles/${assetId}`), enabled: Boolean(assetId) })
}

export function useSaveVehicle() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ assetId, payload }: { assetId?: string | undefined; payload: SaveVehiclePayload }) =>
      assetId ? api.put<FleetVehicle>(`/api/fleet/vehicles/${assetId}`, payload) : api.post<FleetVehicle>('/api/fleet/vehicles', payload),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: KEY }),
  })
}

export function useAddFuel(assetId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: { filledAt: string | null; litres: number; cost: number | null; odometerKm: number | null; fullTank: boolean; station: string | null }) =>
      api.post<FuelLogEntry>(`/api/fleet/vehicles/${assetId}/fuel`, payload),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: KEY }),
  })
}

export function useTelematics(enabled: boolean) {
  return useQuery({ queryKey: [...KEY, 'telematics'], queryFn: () => api.get<TelematicsConnection | null>('/api/fleet/telematics'), enabled })
}

export function useSaveTelematics() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: { provider: string; baseUrl: string; username: string | null; password: string | null; isEnabled: boolean }) =>
      api.put<TelematicsConnection>('/api/fleet/telematics', payload),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: KEY }),
  })
}

export function useSyncTelematics() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () => api.post<{ fetched: number; applied: { vehicles: number; readings: number; unknownDevices: string[] } }>('/api/fleet/telematics/sync', {}),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: KEY }),
  })
}

/** Service due "soon": within 1,000 km or a fortnight. */
export function serviceDueSoon(vehicle: FleetVehicle, today = new Date()): boolean {
  if (vehicle.kmToService !== null && vehicle.kmToService <= 1000) return true
  if (vehicle.serviceDueOn !== null && (new Date(vehicle.serviceDueOn).getTime() - today.getTime()) / 86_400_000 <= 14) return true
  return false
}

/** The paper that ends first, if any ends within 30 days (or already has). */
export function worstDocument(vehicle: FleetVehicle): VehicleDocument | null {
  const first = vehicle.documents[0]
  return first && first.daysLeft <= 30 ? first : null
}

export function mapLink(lat: number, lng: number): string {
  return `https://www.openstreetmap.org/?mlat=${lat}&mlon=${lng}#map=15/${lat}/${lng}`
}
