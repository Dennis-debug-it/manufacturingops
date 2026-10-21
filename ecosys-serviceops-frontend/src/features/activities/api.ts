import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, apiClient } from '../../shared/api/apiClient'
import { endpoints } from '../../shared/api/endpoints'
import type {
  ActionItem,
  ActivityDetail,
  ActivityPeople,
  ActivitySummary,
  AttendeeResponse,
  CalendarFeedStatus,
  CalendarResponse,
  LinkedEntityType,
  SaveActionItemPayload,
  SaveActivityPayload,
} from './types'

/** Activities, action items and the calendar share one key, so any change refreshes all three. */
const activitiesKey = ['activities'] as const

function useActivitiesMutation<TVariables, TResult>(fn: (variables: TVariables) => Promise<TResult>) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: fn,
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: activitiesKey }),
  })
}

export interface ActivityListFilters {
  from?: string
  to?: string
  entityType?: LinkedEntityType
  entityId?: string
  status?: string
  mine?: boolean
}

export function useActivities(filters: ActivityListFilters, enabled = true) {
  return useQuery({
    enabled,
    queryKey: [...activitiesKey, 'list', filters],
    queryFn: () => api.get<ActivitySummary[]>(endpoints.activities.list, { ...filters }),
    staleTime: 30_000,
  })
}

export function useActivity(id: string) {
  return useQuery({
    enabled: Boolean(id),
    queryKey: [...activitiesKey, 'detail', id],
    queryFn: () => api.get<ActivityDetail>(endpoints.activities.detail(id)),
  })
}

export function useActivityPeople(clientId: string | null, enabled = true) {
  return useQuery({
    enabled,
    queryKey: [...activitiesKey, 'people', clientId ?? ''],
    queryFn: () => api.get<ActivityPeople>(endpoints.activities.people, { clientId }),
    staleTime: 60_000,
  })
}

export const useSaveActivity = () =>
  useActivitiesMutation(({ id, ...payload }: SaveActivityPayload & { id?: string }) =>
    id ? api.put<ActivityDetail>(endpoints.activities.detail(id), payload) : api.post<ActivityDetail>(endpoints.activities.list, payload))

export const useCancelActivity = () =>
  useActivitiesMutation(({ id, reason }: { id: string; reason: string | null }) => api.post<ActivityDetail>(endpoints.activities.cancel(id), { reason }))

export const useRespondToActivity = () =>
  useActivitiesMutation(({ id, response }: { id: string; response: AttendeeResponse }) => api.post<ActivityDetail>(endpoints.activities.respond(id), { response }))

export const useMarkActivityHeld = () =>
  useActivitiesMutation(({ id, attendedAttendeeIds }: { id: string; attendedAttendeeIds: string[] }) =>
    api.post<ActivityDetail>(endpoints.activities.held(id), { attendedAttendeeIds }))

export const useSaveMinutes = () =>
  useActivitiesMutation(({ id, minutes }: { id: string; minutes: string | null }) => api.put<ActivityDetail>(endpoints.activities.minutes(id), { minutes }))

export const usePublishMinutes = () =>
  useActivitiesMutation((id: string) => api.post<ActivityDetail>(endpoints.activities.publishMinutes(id)))

export const useAddMinutesAddendum = () =>
  useActivitiesMutation(({ id, text }: { id: string; text: string }) => api.post<ActivityDetail>(endpoints.activities.addenda(id), { text }))

/** Downloads the minutes PDF through the authenticated client. */
export async function downloadMinutesPdf(id: string, fileName: string) {
  const response = await apiClient.get(endpoints.activities.minutesPdf(id), { responseType: 'blob' })
  const url = URL.createObjectURL(response.data as Blob)
  const link = window.document.createElement('a')
  link.href = url
  link.download = fileName
  link.click()
  URL.revokeObjectURL(url)
}

// ── Action items ────────────────────────────────────────────────────────────

export interface ActionItemFilters {
  mine?: boolean
  entityType?: LinkedEntityType
  entityId?: string
  activityId?: string
  status?: string
}

export function useActionItems(filters: ActionItemFilters, enabled = true) {
  return useQuery({
    enabled,
    queryKey: [...activitiesKey, 'action-items', filters],
    queryFn: () => api.get<ActionItem[]>(endpoints.actionItems.list, { ...filters }),
    staleTime: 30_000,
  })
}

export const useSaveActionItem = () =>
  useActivitiesMutation(({ id, ...payload }: SaveActionItemPayload & { id?: string }) =>
    id ? api.put<ActionItem>(endpoints.actionItems.detail(id), payload) : api.post<ActionItem>(endpoints.actionItems.list, payload))

// ── Calendar ──────────────────────────────────────────────────────────────────

export function useCalendar(from: string, to: string, userIds: string[]) {
  return useQuery({
    queryKey: [...activitiesKey, 'calendar', from, to, userIds.join(',')],
    queryFn: () => api.get<CalendarResponse>(endpoints.calendar.items, { from, to, userIds: userIds.length ? userIds.join(',') : undefined }),
    staleTime: 30_000,
  })
}

export function useCalendarFeed() {
  return useQuery({
    queryKey: ['calendar-feed'],
    queryFn: () => api.get<CalendarFeedStatus>(endpoints.me.calendarFeed),
  })
}

export function useCreateCalendarFeed() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () => api.post<CalendarFeedStatus>(endpoints.me.calendarFeed),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['calendar-feed'] }),
  })
}

export function useRevokeCalendarFeed() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () => api.delete(endpoints.me.calendarFeed),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['calendar-feed'] }),
  })
}
