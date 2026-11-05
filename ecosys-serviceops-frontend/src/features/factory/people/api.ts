import { useMutation, useQuery, useQueryClient, type QueryClient } from '@tanstack/react-query'
import { api } from '../../../shared/api/apiClient'

export const TRADES = ['Fitter', 'Electrician', 'Boiler operator', 'Instrument technician', 'Welder', 'Mechanic', 'General hand']

export type CertificateStatus = 'Valid' | 'Expiring' | 'Expired'

export interface TechnicianCertificate {
  id: string
  technicianId: string
  technicianName: string
  certificationDefinitionId: string
  name: string
  certificateNumber: string | null
  issuedOn: string | null
  expiresOn: string | null
  status: CertificateStatus
}

export interface TeamMember {
  id: string
  fullName: string
  email: string
  phone: string | null
  trade: string | null
  homeOrgNodeId: string | null
  homeOrgNodeName: string | null
  hourlyRate: number | null
  overtimeMultiplier: number
  isActive: boolean
  hasLogin: boolean
  skills: { skill: string; level: number }[]
  certificates: TechnicianCertificate[]
}

export interface CertificateType { id: string; name: string; issuer: string | null; renewalPeriodMonths: number | null }

export interface RosterCell { technicianId: string; workDate: string; kind: 'Shift' | 'OnCall' | 'Off'; shiftName: string | null; note: string | null }
export interface RosterView { shifts: string[]; entries: RosterCell[]; leave: { technicianId: string; from: string; to: string; leaveType: string }[] }

export interface WorkloadRow {
  technicianId: string
  name: string
  trade: string | null
  rosteredHours: number
  loggedHours: number
  overtimeHours: number
  labourCost: number
  jobsCompleted: number
  openJobs: number
  openEstimatedHours: number
  utilisationPercent: number | null
  onCallDays: number
  leaveDays: number
}

const refreshPeople = (queryClient: QueryClient) => {
  void queryClient.invalidateQueries({ queryKey: ['factory', 'people'] })
}

export function useTeam() {
  return useQuery({ queryKey: ['factory', 'people', 'team'], queryFn: () => api.get<TeamMember[]>('/api/factory/people/technicians'), staleTime: 30_000 })
}

export function useCertificateTypes() {
  return useQuery({ queryKey: ['factory', 'people', 'certificate-types'], queryFn: () => api.get<CertificateType[]>('/api/factory/people/certificate-types'), staleTime: 5 * 60_000 })
}

export function useCertificates(status?: string | undefined) {
  return useQuery({ queryKey: ['factory', 'people', 'certificates', status ?? null], queryFn: () => api.get<TechnicianCertificate[]>('/api/factory/people/certificates', { status }) })
}

export function useRoster(from: string, to: string) {
  return useQuery({ queryKey: ['factory', 'people', 'roster', from, to], queryFn: () => api.get<RosterView>('/api/factory/people/roster', { from, to }) })
}

export function useWorkload(from: string, to: string) {
  return useQuery({ queryKey: ['factory', 'people', 'workload', from, to], queryFn: () => api.get<WorkloadRow[]>('/api/factory/people/workload', { from, to }) })
}

export function usePeopleAction() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ method = 'post', path, body }: { method?: 'post' | 'put' | 'delete'; path: string; body?: unknown }) => {
      const url = `/api/factory/people${path}`
      if (method === 'put') return api.put<unknown>(url, body)
      if (method === 'delete') return api.delete<unknown>(url)
      return api.post<unknown>(url, body ?? {})
    },
    onSuccess: () => refreshPeople(queryClient),
  })
}

/* ── Safety ───────────────────────────────────────────────────────────────── */

export const PERMIT_TYPE_LABELS: Record<string, string> = {
  HotWork: 'Hot work', ConfinedSpace: 'Confined space', Electrical: 'Electrical', WorkingAtHeights: 'Working at heights', General: 'General',
}

export interface PermitSummary {
  id: string
  permitNumber: string
  permitType: string
  status: 'Requested' | 'Approved' | 'Closed' | 'Rejected' | 'Cancelled'
  workOrderId: string
  workOrderNumber: string
  workOrderTitle: string
  assetCode: string | null
  validFrom: string
  validTo: string
  requestedByName: string | null
  approvedByName: string | null
  approvedAt: string | null
  checks: number
  checksDone: number
}

export interface LotoLock {
  id: string
  workOrderId: string
  workOrderNumber: string
  workOrderTitle: string
  isolationPointId: string
  pointCode: string
  pointDescription: string
  energyType: string
  assetCode: string
  lockNumber: string
  appliedByName: string
  appliedAt: string
  zeroEnergyVerified: boolean
  removedByName: string | null
  removedAt: string | null
}

export interface PermitDetail {
  permit: PermitSummary
  hazards: string | null
  workDescription: string | null
  issuedToName: string | null
  decisionNote: string | null
  gasTest: string | null
  closeOutNotes: string | null
  closedByName: string | null
  closedAt: string | null
  checks: { id: string; prompt: string; done: boolean; doneByName: string | null }[]
  locks: LotoLock[]
}

export interface IsolationPoint { id: string; assetId: string; code: string; energyType: string; description: string; location: string | null; method: string | null }

export const ENERGY_TYPES = ['Electrical', 'Mechanical', 'Pneumatic', 'Hydraulic', 'Steam', 'Thermal', 'Gravity', 'Chemical']

export const INCIDENT_KIND_LABELS: Record<string, string> = { Injury: 'Injury', NearMiss: 'Near miss', PropertyDamage: 'Damage', Fire: 'Fire', Environmental: 'Environmental' }

export interface Incident {
  id: string
  incidentNumber: string
  kind: string
  severity: string
  occurredAt: string
  orgNodeId: string
  orgNodeName: string | null
  assetId: string | null
  assetCode: string | null
  assetName: string | null
  workOrderId: string | null
  title: string
  description: string
  personsInvolved: string | null
  immediateAction: string | null
  rootCause: string | null
  lessons: string | null
  daysLost: number | null
  reportedToAuthority: boolean
  status: 'Open' | 'Investigating' | 'Closed'
  reportedByName: string
  correctiveWorkOrderId: string | null
  correctiveWorkOrderNumber: string | null
  correctiveWorkOrderStatus: string | null
  closedAt: string | null
  closedByName: string | null
  createdAt: string
}

export interface IncidentSummary { last12Months: number; injuries: number; nearMisses: number; open: number; daysLost: number; daysSinceLostTimeInjury: number | null }

export interface TrainingRecord { id: string; technicianId: string | null; personName: string; company: string | null; isContractor: boolean; course: string; category: string; completedOn: string; expiresOn: string | null; trainer: string | null; notes: string | null }

export interface PpeIssue { id: string; technicianId: string | null; personName: string; item: string; size: string | null; quantity: number; issuedOn: string; issuedByName: string | null; replaceBy: string | null; notes: string | null }

const refreshSafety = (queryClient: QueryClient) => {
  void queryClient.invalidateQueries({ queryKey: ['factory', 'safety'] })
  void queryClient.invalidateQueries({ queryKey: ['factory', 'job'] })
  void queryClient.invalidateQueries({ queryKey: ['factory', 'jobs'] })
}

export function usePermits(status?: string | undefined) {
  return useQuery({ queryKey: ['factory', 'safety', 'permits', status ?? null], queryFn: () => api.get<PermitSummary[]>('/api/factory/safety/permits', { status }) })
}

export function usePermit(id: string | null) {
  return useQuery({ queryKey: ['factory', 'safety', 'permit', id], queryFn: () => api.get<PermitDetail>(`/api/factory/safety/permits/${id}`), enabled: Boolean(id) })
}

export function useLocks() {
  return useQuery({ queryKey: ['factory', 'safety', 'locks'], queryFn: () => api.get<LotoLock[]>('/api/factory/safety/locks') })
}

export function useIsolationPoints(assetId: string | null | undefined) {
  return useQuery({
    queryKey: ['factory', 'safety', 'isolation-points', assetId],
    queryFn: () => api.get<IsolationPoint[]>(`/api/factory/safety/assets/${assetId}/isolation-points`),
    enabled: Boolean(assetId),
  })
}

export function useIncidents(filters: { kind?: string | undefined; status?: string | undefined }) {
  return useQuery({ queryKey: ['factory', 'safety', 'incidents', filters], queryFn: () => api.get<Incident[]>('/api/factory/safety/incidents', filters) })
}

export function useIncidentSummary() {
  return useQuery({ queryKey: ['factory', 'safety', 'incident-summary'], queryFn: () => api.get<IncidentSummary>('/api/factory/safety/incidents/summary') })
}

export function useTraining() {
  return useQuery({ queryKey: ['factory', 'safety', 'training'], queryFn: () => api.get<TrainingRecord[]>('/api/factory/safety/training') })
}

export function usePpe() {
  return useQuery({ queryKey: ['factory', 'safety', 'ppe'], queryFn: () => api.get<PpeIssue[]>('/api/factory/safety/ppe') })
}

export function useSafetyAction() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ method = 'post', path, body }: { method?: 'post' | 'put'; path: string; body?: unknown }) =>
      method === 'put' ? api.put<unknown>(`/api/factory/safety${path}`, body) : api.post<unknown>(`/api/factory/safety${path}`, body ?? {}),
    onSuccess: () => refreshSafety(queryClient),
  })
}
