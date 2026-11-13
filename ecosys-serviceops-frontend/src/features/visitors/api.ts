import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, apiClient } from '../../shared/api/apiClient'

export const VISIT_PURPOSES = ['Meeting', 'Delivery', 'Contractor work', 'Inspection', 'Audit', 'Interview', 'Other'] as const
export const ID_TYPES = ['National ID', 'Passport', 'Alien ID', 'Driving licence'] as const
export const ZONE_LEVELS = ['Public', 'Controlled', 'Restricted'] as const

export type VisitStatus = 'PendingApproval' | 'Approved' | 'OnSite' | 'Completed' | 'Rejected' | 'Cancelled'
export type AttendeeState = 'Expected' | 'OnSite' | 'Overstayed' | 'Left' | 'NoShow' | 'Cancelled'
export type ZoneLevel = (typeof ZONE_LEVELS)[number]

export interface VisitorAccess { canGate: boolean; canManage: boolean }

export interface ZoneRef { id: string; name: string; level: ZoneLevel }

export interface Attendee {
  id: string
  visitorId: string
  fullName: string
  company: string | null
  phone: string | null
  idType: string | null
  idNumberMasked: string | null
  isBlocked: boolean
  blockedReason: string | null
  contractorName: string | null
  badgeNumber: string
  badgeToken: string
  state: AttendeeState
  checkedInAt: string | null
  checkedInByName: string | null
  checkedOutAt: string | null
  checkedOutByName: string | null
  idVerified: boolean
  inductionConfirmed: boolean
  ppeConfirmed: boolean
  escortName: string | null
  overrideReason: string | null
  overriddenByName: string | null
  accountedForAt: string | null
}

export interface Visit {
  id: string
  visitNumber: string
  factoryId: string
  factoryName: string
  purpose: string
  hostUserId: string
  hostName: string
  iAmHost: boolean
  expectedFrom: string
  expectedTo: string
  status: VisitStatus
  source: 'PreRegistered' | 'WalkIn'
  workOrderId: string | null
  workOrderNumber: string | null
  workPermitId: string | null
  permitNumber: string | null
  contractorId: string | null
  contractorName: string | null
  vehicleRegistration: string | null
  notes: string | null
  createdByName: string
  decidedByName: string | null
  decidedAt: string | null
  decisionNote: string | null
  completedAt: string | null
  zones: ZoneRef[]
  attendees: Attendee[]
}

export interface VisitBoard { visits: Visit[]; onSite: number; expected: number; awaitingApproval: number; overstayed: number; leftToday: number }

export interface GateIssue { code: string; message: string; overridable: boolean }
export interface GateCheck {
  requirements: { needsInduction: boolean; hasCurrentInduction: boolean; ppe: string[]; needsEscort: boolean; needsPermit: boolean }
  issues: GateIssue[]
  canEnter: boolean
  overridable: boolean
}

export interface GateConfirmation {
  idVerified: boolean
  ppeConfirmed: boolean
  recordInduction: boolean
  escortUserId: string | null
  overrideReason: string | null
}

export interface OnSitePerson {
  attendeeId: string
  visitId: string
  visitNumber: string
  factoryId: string
  factoryName: string
  fullName: string
  company: string | null
  phone: string | null
  hostName: string
  purpose: string
  vehicleRegistration: string | null
  badgeNumber: string
  checkedInAt: string
  expectedTo: string
  overstayed: boolean
  escortName: string | null
  zones: string[]
  accountedForAt: string | null
  accountedForByName: string | null
}

export interface VisitorPerson {
  id: string
  fullName: string
  phone: string | null
  email: string | null
  company: string | null
  idType: string | null
  idNumberMasked: string | null
  contractorId: string | null
  contractorName: string | null
  isBlocked: boolean
  blockedReason: string | null
  blockedByName: string | null
  lastVisitAt: string | null
}

export interface StaffOption { id: string; fullName: string; jobTitle: string | null }

export interface AccessZone {
  id: string
  factoryId: string
  factoryName: string
  orgNodeId: string | null
  orgNodeName: string | null
  name: string
  level: ZoneLevel
  requiresEscort: boolean
  requiresInduction: boolean
  requiresPermit: boolean
  requiredPpe: string | null
  displayOrder: number
  isActive: boolean
}

export interface SaveZonePayload {
  factoryId: string
  name: string
  level: ZoneLevel
  requiresEscort: boolean
  requiresInduction: boolean
  requiresPermit: boolean
  requiredPpe: string | null
  isActive: boolean
}

export interface VisitorReport {
  from: string
  to: string
  entries: number
  contractorEntries: number
  averageMinutesOnSite: number | null
  overstays: number
  noShowVisits: number
  perDay: { day: string; count: number }[]
  byPurpose: { purpose: string; count: number }[]
  overrides: { at: string; visitNumber: string; fullName: string; reason: string; byName: string | null }[]
}

export interface VisitorDetailsPayload {
  fullName: string
  phone: string | null
  email: string | null
  company: string | null
  idType: string | null
  idNumber: string | null
  visitorId: string | null
}

export interface CreateVisitPayload {
  factoryId: string
  purpose: string
  visitors: VisitorDetailsPayload[]
  hostUserId: string | null
  expectedFrom: string | null
  expectedTo: string | null
  zoneIds: string[]
  workPermitId: string | null
  contractorId: string | null
  vehicleRegistration: string | null
  notes: string | null
  walkIn: boolean
}

const KEY = ['visitors']

export function useVisitorAccess() {
  return useQuery({ queryKey: [...KEY, 'me'], queryFn: () => api.get<VisitorAccess>('/api/visitors/me'), staleTime: 300_000 })
}

export function useVisitBoard(factoryId: string) {
  return useQuery({
    queryKey: [...KEY, 'board', factoryId],
    queryFn: () => api.get<VisitBoard>('/api/visitors/board', { factoryId: factoryId || undefined }),
    refetchInterval: 30_000,
  })
}

export function useVisitRegister(filters: { factoryId: string; from: string; to: string; status: string; q: string }) {
  return useQuery({
    queryKey: [...KEY, 'register', filters],
    queryFn: () => api.get<Visit[]>('/api/visitors/visits', {
      factoryId: filters.factoryId || undefined,
      from: filters.from ? new Date(filters.from).toISOString() : undefined,
      to: filters.to ? new Date(`${filters.to}T23:59:59`).toISOString() : undefined,
      status: filters.status || undefined,
      q: filters.q.trim() || undefined,
    }),
  })
}

export function useOnSite(factoryId: string, enabled = true) {
  return useQuery({
    queryKey: [...KEY, 'on-site', factoryId],
    queryFn: () => api.get<OnSitePerson[]>('/api/visitors/on-site', { factoryId: factoryId || undefined }),
    refetchInterval: 15_000,
    enabled,
  })
}

export function useVisitorPeople(q: string, blockedOnly: boolean, enabled = true) {
  return useQuery({
    queryKey: [...KEY, 'people', q, blockedOnly],
    queryFn: () => api.get<VisitorPerson[]>('/api/visitors/people', { q: q.trim() || undefined, blockedOnly: blockedOnly || undefined }),
    enabled,
  })
}

export function useStaff(q: string) {
  return useQuery({ queryKey: [...KEY, 'staff', q], queryFn: () => api.get<StaffOption[]>('/api/visitors/staff', { q: q.trim() || undefined }), staleTime: 60_000 })
}

export function useZones(factoryId: string, includeInactive = false) {
  return useQuery({
    queryKey: [...KEY, 'zones', factoryId, includeInactive],
    queryFn: () => api.get<AccessZone[]>('/api/access-zones', { factoryId: factoryId || undefined, includeInactive: includeInactive || undefined }),
  })
}

export function useVisitorReport(factoryId: string, days: number) {
  return useQuery({
    queryKey: [...KEY, 'report', factoryId, days],
    queryFn: () => api.get<VisitorReport>('/api/visitors/report', { factoryId: factoryId || undefined, from: new Date(Date.now() - days * 86_400_000).toISOString() }),
  })
}

/** The gate checks for one person with what has been ticked so far. Nothing is saved. */
export function useGateCheck(attendeeId: string | null, confirmation: GateConfirmation) {
  return useQuery({
    queryKey: [...KEY, 'check', attendeeId, confirmation],
    queryFn: () => api.post<GateCheck>(`/api/visitors/attendees/${attendeeId}/check`, confirmation),
    enabled: Boolean(attendeeId),
    retry: false,
    placeholderData: (previous) => previous,
  })
}

function useVisitorMutation<TVariables, TResult>(fn: (variables: TVariables) => Promise<TResult>) {
  const queryClient = useQueryClient()
  return useMutation({ mutationFn: fn, onSuccess: () => void queryClient.invalidateQueries({ queryKey: KEY }) })
}

export const useCreateVisit = () => useVisitorMutation((payload: CreateVisitPayload) => api.post<Visit>('/api/visitors/visits', payload))
export const useDecideVisit = () => useVisitorMutation(({ id, approve, note }: { id: string; approve: boolean; note: string | null }) =>
  api.post<Visit>(`/api/visitors/visits/${id}/decide`, { approve, note }))
export const useCancelVisit = () => useVisitorMutation(({ id, reason }: { id: string; reason: string | null }) => api.post<Visit>(`/api/visitors/visits/${id}/cancel`, { reason }))
export const useCheckIn = () => useVisitorMutation(({ attendeeId, confirmation }: { attendeeId: string; confirmation: GateConfirmation }) =>
  api.post<Visit>(`/api/visitors/attendees/${attendeeId}/check-in`, confirmation))
export const useCheckOut = () => useVisitorMutation((attendeeId: string) => api.post<Visit>(`/api/visitors/attendees/${attendeeId}/check-out`, {}))
export const useAccounted = () => useVisitorMutation(({ attendeeId, accounted }: { attendeeId: string; accounted: boolean }) =>
  api.post<void>(`/api/visitors/attendees/${attendeeId}/accounted`, { accounted }))
export const useResetRollCall = () => useVisitorMutation((factoryId: string | null) => api.post<number>('/api/visitors/roll-call/reset', { factoryId }))
export const useBlockVisitor = () => useVisitorMutation(({ id, blocked, reason }: { id: string; blocked: boolean; reason: string | null }) =>
  api.post<VisitorPerson>(`/api/visitors/people/${id}/block`, { blocked, reason }))
export const useSaveZone = () => useVisitorMutation(({ id, payload }: { id?: string | undefined; payload: SaveZonePayload }) =>
  id ? api.put<AccessZone>(`/api/access-zones/${id}`, payload) : api.post<AccessZone>('/api/access-zones', payload))
export const useSeedZones = () => useVisitorMutation((factoryId: string) => api.post<AccessZone[]>('/api/access-zones/seed', { factoryId }))

/** Looks up a scanned or typed badge. Accepts "VISITOR:<token>", a link ending in the token, or the token itself. */
export function badgeToken(code: string): string {
  let value = code.trim()
  if (value.toUpperCase().startsWith('VISITOR:')) value = value.slice('VISITOR:'.length)
  const slash = value.lastIndexOf('/')
  return slash >= 0 ? value.slice(slash + 1) : value
}

export function lookupBadge(code: string) {
  return api.get<{ attendee: Attendee; visit: Visit }>(`/api/visitors/badges/${encodeURIComponent(badgeToken(code))}`)
}

export async function downloadBadges(visit: Visit, attendeeId?: string) {
  const response = await apiClient.get(`/api/visitors/visits/${visit.id}/badges`, { params: { attendeeId }, responseType: 'blob' })
  const url = URL.createObjectURL(response.data as Blob)
  const link = document.createElement('a')
  link.href = url
  link.download = `badges-${visit.visitNumber}.pdf`
  link.click()
  URL.revokeObjectURL(url)
}

export const STATUS_LABEL: Record<VisitStatus, string> = {
  PendingApproval: 'Awaiting host',
  Approved: 'Approved',
  OnSite: 'On site',
  Completed: 'Completed',
  Rejected: 'Turned down',
  Cancelled: 'Cancelled',
}

export const STATE_LABEL: Record<AttendeeState, string> = {
  Expected: 'Expected',
  OnSite: 'On site',
  Overstayed: 'Overstayed',
  Left: 'Left',
  NoShow: 'No show',
  Cancelled: 'Cancelled',
}

/** A datetime-local input value (browser time) for a date. */
export function toLocalInput(date: Date): string {
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`
}
