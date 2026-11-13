import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, apiClient } from '../../shared/api/apiClient'

export const INCIDENT_KINDS: ReadonlyArray<{ value: string; label: string }> = [
  { value: 'UnauthorisedAccess', label: 'Unauthorised access' },
  { value: 'Theft', label: 'Theft' },
  { value: 'AttemptedTheft', label: 'Attempted theft' },
  { value: 'Intrusion', label: 'Intrusion' },
  { value: 'PerimeterDamage', label: 'Perimeter damage' },
  { value: 'Tampering', label: 'Equipment tampering' },
  { value: 'SuspiciousPerson', label: 'Suspicious person' },
  { value: 'SuspiciousVehicle', label: 'Suspicious vehicle' },
  { value: 'Hazard', label: 'Safety hazard' },
  { value: 'Fire', label: 'Fire' },
  { value: 'Emergency', label: 'Emergency' },
  { value: 'CheckpointIssue', label: 'Checkpoint issue' },
]
export const kindLabel = (kind: string) => INCIDENT_KINDS.find((x) => x.value === kind)?.label ?? kind
export const SEVERITIES = ['Minor', 'Moderate', 'Serious', 'Fatal'] as const
export const RESPONSE_STATUSES = ['Reported', 'Responding', 'Contained', 'Closed'] as const
export const CHECKPOINT_METHODS: ReadonlyArray<{ value: string; label: string }> = [
  { value: 'QrOrNfc', label: 'QR label or NFC tag' },
  { value: 'Qr', label: 'QR label' },
  { value: 'Nfc', label: 'NFC tag only (most secure)' },
]

export type RunStatus = 'Due' | 'InProgress' | 'Completed' | 'Partial' | 'Missed'
export type Validation = 'Valid' | 'Repeat' | 'OutOfSequence' | 'OutsideGeofence' | 'TooFast' | 'WrongTag' | 'NotOnRoute' | 'ClockSkew'

export const VALIDATION_LABEL: Record<Validation, string> = {
  Valid: 'Valid',
  Repeat: 'Repeat',
  OutOfSequence: 'Out of order',
  OutsideGeofence: 'Too far from the point',
  TooFast: 'Too fast',
  WrongTag: 'Wrong tag',
  NotOnRoute: 'Not on this route',
  ClockSkew: 'Phone clock wrong',
}

export const RUN_LABEL: Record<RunStatus, string> = { Due: 'Due', InProgress: 'In progress', Completed: 'Completed', Partial: 'Incomplete', Missed: 'Missed' }

export interface PatrolAccess { canPatrol: boolean; canManage: boolean }

export interface GuardScan { clientScanId: string; checkpointId: string; scannedAt: string; validation: Validation; note: string | null }
export interface GuardRun {
  id: string
  routeId: string
  routeName: string
  factoryId: string
  factoryName: string
  ordered: boolean
  dueFrom: string
  dueBy: string
  closeAt: string
  status: RunStatus
  guardName: string | null
  requiredCount: number
  validCount: number
  scans: GuardScan[]
}
export interface GuardRouteStop { checkpointId: string; sequence: number; required: boolean }
export interface GuardRoute { id: string; name: string; factoryId: string; ordered: boolean; minGapSeconds: number; stops: GuardRouteStop[] }
export interface GuardCheckpoint {
  id: string
  code: string
  name: string
  factoryId: string
  method: string
  tagToken: string
  nfcSerial: string | null
  latitude: number | null
  longitude: number | null
  radiusM: number
  instructions: string | null
  questions: string[]
}
export interface GuardBundle { generatedAt: string; runs: GuardRun[]; routes: GuardRoute[]; checkpoints: GuardCheckpoint[] }

export interface ScanPayload {
  clientScanId: string
  code: string
  scannedAt: string
  runId: string | null
  latitude: number | null
  longitude: number | null
  accuracyM: number | null
  deviceId: string
  answers: { question: string; ok: boolean }[] | null
  note: string | null
  byNfc: boolean
}

export interface ScanResult {
  scanId: string
  clientScanId: string
  checkpointId: string
  checkpointCode: string
  checkpointName: string
  scannedAt: string
  validation: Validation
  note: string | null
  distanceM: number | null
  runId: string | null
  runStatus: RunStatus | null
  validCount: number | null
  requiredCount: number | null
  incidentId: string | null
}

export interface RunRow {
  id: string
  routeId: string
  routeName: string
  factoryId: string
  factoryName: string
  adHoc: boolean
  dueFrom: string
  dueBy: string
  closeAt: string
  status: RunStatus
  guardName: string | null
  startedAt: string | null
  finishedAt: string | null
  requiredCount: number
  validCount: number
  scanCount: number
  flaggedCount: number
}

export interface PatrolBoard {
  day: string
  runs: RunRow[]
  flagged: { id: string; runId: string | null; checkpointId: string; code: string; name: string; guardName: string; scannedAt: string; validation: Validation; note: string | null }[]
  openIncidents: number
  lastScan: { code: string; name: string; guardName: string; scannedAt: string; latitude: number | null; longitude: number | null } | null
  completionPercent: number | null
  missed: number
  partial: number
  inProgress: number
}

export interface RunDetail {
  run: RunRow
  stops: { checkpointId: string; code: string; name: string; sequence: number; required: boolean }[]
  scans: {
    id: string; checkpointId: string; code: string; name: string; guardName: string; method: string; scannedAt: string; receivedAt: string
    latitude: number | null; longitude: number | null; accuracyM: number | null; distanceM: number | null; validation: Validation; note: string | null
    answersJson: string | null; guardNote: string | null
  }[]
}

export interface SecurityIncident {
  id: string
  incidentNumber: string
  kind: string
  severity: string
  occurredAt: string
  factoryId: string
  factoryName: string
  title: string
  description: string
  reportedByName: string
  status: string
  responseStatus: string | null
  ownerUserId: string | null
  ownerName: string | null
  policeObNumber: string | null
  reportedToPolice: boolean
  immediateAction: string | null
  checkpointId: string | null
  runId: string | null
  visitId: string | null
  latitude: number | null
  longitude: number | null
  closedAt: string | null
}

export interface Handover {
  id: string
  factoryId: string
  factoryName: string
  shiftName: string | null
  outgoingName: string
  incomingName: string | null
  handedOverAt: string
  openIssues: string | null
  keysAndEquipment: string | null
  acknowledgedAt: string | null
}

export interface CheckpointRow {
  id: string
  code: string
  name: string
  factoryId: string
  factoryName: string
  accessZoneId: string | null
  zoneName: string | null
  method: string
  nfcSerial: string | null
  latitude: number | null
  longitude: number | null
  radiusM: number
  instructions: string | null
  questions: string | null
  displayOrder: number
  isActive: boolean
  lastScanAt: string | null
  scansLast30Days: number
}

export interface SaveCheckpointPayload {
  factoryId: string
  name: string
  method: string
  accessZoneId: string | null
  latitude: number | null
  longitude: number | null
  radiusM: number
  instructions: string | null
  questions: string | null
  isActive: boolean
}

export interface ScheduleRow {
  id: string
  everyMinutes: number | null
  fixedTimes: string | null
  windowFrom: string
  windowTo: string
  days: number[]
  graceMinutes: number
  assignedUserId: string | null
  assignedName: string | null
  isActive: boolean
  description: string
}

export interface RouteRow {
  id: string
  name: string
  factoryId: string
  factoryName: string
  ordered: boolean
  maxDurationMinutes: number
  minGapSeconds: number
  isActive: boolean
  checkpoints: { checkpointId: string; code: string; name: string; sequence: number; required: boolean }[]
  schedules: ScheduleRow[]
}

export interface SaveRoutePayload {
  factoryId: string
  name: string
  checkpoints: { checkpointId: string; required: boolean }[]
  ordered: boolean
  maxDurationMinutes: number
  minGapSeconds: number
  isActive: boolean
}

export interface SaveSchedulePayload {
  everyMinutes: number | null
  fixedTimes: string | null
  windowFrom: string
  windowTo: string
  days: number[]
  graceMinutes: number
  assignedUserId: string | null
  isActive: boolean
}

export interface CompletionRow { name: string; patrols: number; completed: number; missed: number; checkpointPercent: number; flagged: number }
export interface PatrolReport {
  from: string
  to: string
  patrols: number
  completed: number
  partial: number
  missed: number
  checkpointPercent: number
  byRoute: CompletionRow[]
  byGuard: CompletionRow[]
  byCheckpoint: { checkpointId: string; code: string; name: string; expected: number; reached: number; missed: number }[]
  incidents: { kind: string; count: number }[]
}

const KEY = ['security']
/** Kept on the guard's phone (see tech/offline/persist.ts) so a patrol works with no signal. */
export const GUARD_KEY = [...KEY, 'guard'] as const

export function usePatrolAccess() {
  return useQuery({ queryKey: [...KEY, 'me'], queryFn: () => api.get<PatrolAccess>('/api/security/me'), staleTime: 300_000 })
}

export function useGuardBundle(enabled = true) {
  return useQuery({
    queryKey: GUARD_KEY,
    queryFn: () => api.get<GuardBundle>('/api/security/guard', undefined, { suppressGlobalErrorToast: true }),
    enabled,
    refetchInterval: 60_000,
    staleTime: 20_000,
  })
}

export function usePatrolBoard(factoryId: string, date: string) {
  return useQuery({
    queryKey: [...KEY, 'board', factoryId, date],
    queryFn: () => api.get<PatrolBoard>('/api/security/board', { factoryId: factoryId || undefined, date: date || undefined }),
    refetchInterval: 30_000,
  })
}

export function useRunDetail(id: string | null) {
  return useQuery({ queryKey: [...KEY, 'run', id], queryFn: () => api.get<RunDetail>(`/api/security/runs/${id}`), enabled: Boolean(id) })
}

export function useSecurityIncidents(factoryId: string, openOnly: boolean) {
  return useQuery({
    queryKey: [...KEY, 'incidents', factoryId, openOnly],
    queryFn: () => api.get<SecurityIncident[]>('/api/security/incidents', { factoryId: factoryId || undefined, openOnly: openOnly || undefined }),
    refetchInterval: 60_000,
  })
}

export function useHandovers(factoryId: string) {
  return useQuery({ queryKey: [...KEY, 'handovers', factoryId], queryFn: () => api.get<Handover[]>('/api/security/handovers', { factoryId: factoryId || undefined }) })
}

export function useCheckpoints(factoryId: string, includeInactive = false) {
  return useQuery({
    queryKey: [...KEY, 'checkpoints', factoryId, includeInactive],
    queryFn: () => api.get<CheckpointRow[]>('/api/security/checkpoints', { factoryId: factoryId || undefined, includeInactive: includeInactive || undefined }),
  })
}

export function useRoutes(factoryId: string) {
  return useQuery({ queryKey: [...KEY, 'routes', factoryId], queryFn: () => api.get<RouteRow[]>('/api/security/routes', { factoryId: factoryId || undefined }) })
}

export function usePatrolReport(factoryId: string, days: number) {
  return useQuery({
    queryKey: [...KEY, 'report', factoryId, days],
    queryFn: () => api.get<PatrolReport>('/api/security/report', { factoryId: factoryId || undefined, from: new Date(Date.now() - days * 86_400_000).toISOString() }),
  })
}

function useSecurityMutation<TVariables, TResult>(fn: (variables: TVariables) => Promise<TResult>) {
  const queryClient = useQueryClient()
  return useMutation({ mutationFn: fn, onSuccess: () => void queryClient.invalidateQueries({ queryKey: KEY }) })
}

export const useStartRun = () => useSecurityMutation((id: string) => api.post<RunRow>(`/api/security/runs/${id}/start`, {}))
export const useStartOwnPatrol = () => useSecurityMutation((routeId: string) => api.post<RunRow>(`/api/security/routes/${routeId}/start`, {}))
export const useReportIncident = () => useSecurityMutation((payload: {
  factoryId: string; kind: string; title: string; description: string | null; severity: string; checkpointId: string | null; runId: string | null; latitude: number | null; longitude: number | null
}) => api.post<SecurityIncident>('/api/security/incidents', payload))
export const usePanic = () => useSecurityMutation((payload: { factoryId: string; latitude: number | null; longitude: number | null; note: string | null }) => api.post<SecurityIncident>('/api/security/panic', payload))
export const useUpdateIncident = () => useSecurityMutation(({ id, ...payload }: {
  id: string; responseStatus: string | null; ownerUserId: string | null; policeObNumber: string | null; reportedToPolice: boolean | null; immediateAction: string | null
}) => api.put<SecurityIncident>(`/api/security/incidents/${id}`, payload))
export const useHandOver = () => useSecurityMutation((payload: { factoryId: string; shiftName: string | null; incomingUserId: string | null; openIssues: string | null; keysAndEquipment: string | null }) =>
  api.post<Handover>('/api/security/handovers', payload))
export const useAcknowledgeHandover = () => useSecurityMutation((id: string) => api.post<Handover>(`/api/security/handovers/${id}/acknowledge`, {}))
export const useSaveCheckpoint = () => useSecurityMutation(({ id, payload }: { id?: string | undefined; payload: SaveCheckpointPayload }) =>
  id ? api.put<CheckpointRow>(`/api/security/checkpoints/${id}`, payload) : api.post<CheckpointRow>('/api/security/checkpoints', payload))
export const useCheckpointPosition = () => useSecurityMutation(({ id, latitude, longitude, accuracyM }: { id: string; latitude: number; longitude: number; accuracyM: number | null }) =>
  api.post<CheckpointRow>(`/api/security/checkpoints/${id}/position`, { latitude, longitude, accuracyM }))
export const useCheckpointNfc = () => useSecurityMutation(({ id, serial }: { id: string; serial: string | null }) => api.post<CheckpointRow>(`/api/security/checkpoints/${id}/nfc`, { serial }))
export const useNewCheckpointLabel = () => useSecurityMutation((id: string) => api.post<CheckpointRow>(`/api/security/checkpoints/${id}/new-label`, {}))
export const useSeedCheckpoints = () => useSecurityMutation((factoryId: string) => api.post<CheckpointRow[]>('/api/security/checkpoints/seed', { factoryId }))
export const useSaveRoute = () => useSecurityMutation(({ id, payload }: { id?: string | undefined; payload: SaveRoutePayload }) =>
  id ? api.put<RouteRow>(`/api/security/routes/${id}`, payload) : api.post<RouteRow>('/api/security/routes', payload))
export const useSaveSchedule = () => useSecurityMutation(({ id, routeId, payload }: { id?: string | undefined; routeId: string; payload: SaveSchedulePayload }) =>
  id ? api.put<RouteRow>(`/api/security/schedules/${id}`, payload) : api.post<RouteRow>(`/api/security/routes/${routeId}/schedules`, payload))

export async function downloadCheckpointLabels(factoryId: string, checkpointIds?: string[]) {
  const response = await apiClient.post('/api/security/checkpoints/labels', { factoryId: factoryId || null, checkpointIds: checkpointIds ?? null }, { responseType: 'blob' })
  const url = URL.createObjectURL(response.data as Blob)
  const link = document.createElement('a')
  link.href = url
  link.download = 'checkpoint-labels.pdf'
  link.click()
  URL.revokeObjectURL(url)
}

// ── Helpers the guard app uses offline ─────────────────────────────────────────────────────────

/** NFC serials arrive as "04:a2:…"; stored as upper-case hex. */
export function normaliseSerial(serial: string): string {
  return serial.replace(/[^0-9a-f]/gi, '').toUpperCase()
}

/** Finds the checkpoint a scanned QR code or tapped NFC tag belongs to, in the phone's copy. */
export function findCheckpoint(bundle: GuardBundle | undefined, code: string, byNfc: boolean): GuardCheckpoint | null {
  if (!bundle) return null
  if (byNfc) {
    const serial = normaliseSerial(code)
    return bundle.checkpoints.find((x) => x.nfcSerial === serial) ?? null
  }
  let token = code.trim()
  if (token.toUpperCase().startsWith('CHECKPOINT:')) token = token.slice('CHECKPOINT:'.length)
  const slash = token.lastIndexOf('/')
  if (slash >= 0) token = token.slice(slash + 1)
  return bundle.checkpoints.find((x) => x.tagToken === token) ?? null
}

export type StopState = 'done' | 'flagged' | 'pending-sync' | 'next' | 'todo' | 'optional'

/**
 * Where the guard is on a patrol: each stop's state from the scans the server has and the ones still
 * waiting on the phone (shown as done-but-unsynced; the server makes the final call).
 */
export function stopStates(route: GuardRoute, scans: GuardScan[], pendingCheckpointIds: string[]): Map<string, StopState> {
  const states = new Map<string, StopState>()
  const valid = new Set(scans.filter((x) => x.validation === 'Valid').map((x) => x.checkpointId))
  const flagged = new Set(scans.filter((x) => x.validation !== 'Valid' && x.validation !== 'Repeat').map((x) => x.checkpointId))
  const pending = new Set(pendingCheckpointIds)
  let nextSet = false
  for (const stop of [...route.stops].sort((a, b) => a.sequence - b.sequence)) {
    if (valid.has(stop.checkpointId)) states.set(stop.checkpointId, 'done')
    else if (pending.has(stop.checkpointId)) states.set(stop.checkpointId, 'pending-sync')
    else if (flagged.has(stop.checkpointId)) states.set(stop.checkpointId, 'flagged')
    else if (!stop.required) states.set(stop.checkpointId, 'optional')
    else if (!nextSet) { states.set(stop.checkpointId, 'next'); nextSet = true }
    else states.set(stop.checkpointId, 'todo')
  }
  return states
}

/** A stable id for this phone, sent with scans so a supervisor can see which handset made them. */
export function deviceId(): string {
  try {
    const existing = localStorage.getItem('ecosys-device-id')
    if (existing) return existing
    const created = crypto.randomUUID()
    localStorage.setItem('ecosys-device-id', created)
    return created
  } catch {
    return 'unknown-device'
  }
}
