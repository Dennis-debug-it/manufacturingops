import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../../../shared/api/apiClient'
import type { ConditionAction, ConditionLevel } from '../types'

export interface ConditionMeterRow {
  meterId: string
  meterName: string
  meterType: string
  unit: string
  assetId: string
  assetName: string
  assetCode: string
  location: string | null
  currentValue: number | null
  lastReadingAt: string | null
  conditionState: ConditionLevel
  conditionSince: string | null
  warningMin: number | null
  warningMax: number | null
  alarmMin: number | null
  alarmMax: number | null
  conditionAction: ConditionAction
  isQuiet: boolean
  openEventId: string | null
  openSince: string | null
  acknowledgedAt: string | null
  workOrderId: string | null
  workOrderNumber: string | null
}

export interface ConditionOverview {
  monitored: number
  alarms: number
  warnings: number
  quiet: number
  meters: ConditionMeterRow[]
}

export interface ConditionEvent {
  id: string
  meterId: string
  meterName: string
  unit: string
  assetId: string
  assetName: string
  assetCode: string
  level: Exclude<ConditionLevel, 'Normal'>
  direction: 'High' | 'Low'
  status: 'Open' | 'Cleared'
  startedAt: string
  lastReadingAt: string
  clearedAt: string | null
  peakValue: number
  readingCount: number
  workOrderId: string | null
  workOrderNumber: string | null
  workOrderStatus: string | null
  acknowledgedAt: string | null
  acknowledgedByName: string | null
  acknowledgementNote: string | null
}

export interface ConditionTrend {
  readings: number
  slopePerDay: number | null
  heading: 'Rising' | 'Falling' | 'Steady' | null
  approachingLevel: Exclude<ConditionLevel, 'Normal'> | null
  approachingLimit: number | null
  daysToLimit: number | null
}

export interface MeterCondition {
  meterId: string
  meterName: string
  meterType: string
  unit: string
  isCumulative: boolean
  assetId: string
  assetName: string
  assetCode: string
  currentValue: number | null
  lastReadingAt: string | null
  conditionState: ConditionLevel
  conditionSince: string | null
  warningMin: number | null
  warningMax: number | null
  alarmMin: number | null
  alarmMax: number | null
  readings: { readAt: string; value: number; condition: ConditionLevel; source: string }[]
  events: ConditionEvent[]
  trend: ConditionTrend
}

export function useConditionOverview(orgNodeId: string) {
  return useQuery({
    queryKey: ['factory', 'condition', 'overview', orgNodeId],
    queryFn: () => api.get<ConditionOverview>('/api/condition-monitoring/overview', { orgNodeId: orgNodeId || undefined }),
    refetchInterval: 60_000,
  })
}

export function useConditionEvents(status: string, orgNodeId: string) {
  return useQuery({
    queryKey: ['factory', 'condition', 'events', status, orgNodeId],
    queryFn: () => api.get<ConditionEvent[]>('/api/condition-monitoring/events', { status: status || undefined, orgNodeId: orgNodeId || undefined, limit: 200 }),
  })
}

export function useMeterCondition(meterId: string | null, days: number) {
  return useQuery({
    queryKey: ['factory', 'condition', 'meter', meterId, days],
    queryFn: () => api.get<MeterCondition>(`/api/meters/${meterId}/condition`, { days }),
    enabled: Boolean(meterId),
  })
}

export function useAcknowledgeEvent() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, note }: { id: string; note: string }) =>
      api.post<ConditionEvent>(`/api/condition-monitoring/events/${id}/acknowledge`, { note: note.trim() || null }),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['factory', 'condition'] }),
  })
}

/** "Rising 0.8 °C a day; reaches the warning limit (70 °C) in about 4 days." */
export function describeTrend(trend: ConditionTrend, unit: string): string {
  if (trend.slopePerDay === null || trend.heading === null) return `Not enough readings for a trend (${trend.readings} of 5).`
  if (trend.heading === 'Steady') return 'Steady over the last two weeks.'
  const rate = `${trend.heading} ${formatRate(Math.abs(trend.slopePerDay))} ${unit} a day`.replace(/\s+/g, ' ').trim()
  if (trend.daysToLimit === null || trend.approachingLimit === null || trend.approachingLevel === null) return `${rate}, away from its limits.`
  const when = trend.daysToLimit < 1 ? 'within a day' : `in about ${Math.round(trend.daysToLimit)} day${Math.round(trend.daysToLimit) === 1 ? '' : 's'}`
  return `${rate}; reaches the ${trend.approachingLevel.toLowerCase()} limit (${formatRate(trend.approachingLimit)} ${unit}) ${when} at this rate.`
}

function formatRate(value: number): string {
  return Math.abs(value) >= 10 ? value.toFixed(0) : Math.abs(value) >= 1 ? value.toFixed(1) : value.toPrecision(2)
}
