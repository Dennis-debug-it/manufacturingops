import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../../../shared/api/apiClient'

export interface AssetReliability {
  assetId: string
  assetCode: string
  assetName: string
  location: string | null
  criticality: string | null
  failures: number
  mtbfHours: number | null
  mttrHours: number | null
  downtimeHours: number
  meanDaysBetween: number | null
  variation: number | null
  pattern: 'Regular' | 'Irregular' | 'Unknown'
  meanRunBetween: number | null
  meterUnit: string | null
  topProblem: string | null
  topProblemCount: number
  lastFailureAt: string
  activePlans: number
}

export type PmSuggestionKind = 'NoPlan' | 'PlanTooLong' | 'PlanNotPreventing' | 'Random'

export interface PmSuggestion {
  assetId: string
  assetCode: string
  assetName: string
  kind: PmSuggestionKind
  reason: string
  failures: number
  meanDaysBetween: number | null
  topProblem: string | null
  triggerType: 'Calendar' | 'Meter' | null
  frequencyUnit: string | null
  frequencyInterval: number | null
  meterId: string | null
  meterInterval: number | null
  meterUnit: string | null
  planId: string | null
  planName: string | null
  currentInterval: string | null
}

export interface RepeatFailureAnalysis {
  summary: { failures: number; repeats: number; repeatRatePercent: number | null; assetsWithRepeats: number; repeatWithinDays: number }
  byMode: { problemId: string | null; problem: string; failures: number; repeats: number; assets: number; meanDaysBetweenRepeats: number | null; topCause: string | null; topRemedy: string | null }[]
  byAsset: { assetId: string; assetCode: string; assetName: string; failures: number; repeats: number; topProblem: string | null; lastFailureAt: string }[]
  chains: { assetId: string; assetCode: string; assetName: string; problem: string; failures: { workOrderId: string; workOrderNumber: string; at: string; daysSincePrevious: number | null; cause: string | null; remedy: string | null }[] }[]
}

const params = (orgNodeId: string, days: number) => ({ orgNodeId: orgNodeId || undefined, days })

export function useReliabilityAssets(orgNodeId: string, days: number) {
  return useQuery({ queryKey: ['factory', 'reliability', 'assets', orgNodeId, days], queryFn: () => api.get<AssetReliability[]>('/api/reliability/assets', params(orgNodeId, days)) })
}

export function useSuggestions(orgNodeId: string, days: number) {
  return useQuery({ queryKey: ['factory', 'reliability', 'suggestions', orgNodeId, days], queryFn: () => api.get<PmSuggestion[]>('/api/reliability/suggestions', params(orgNodeId, days)) })
}

export function useRepeatFailures(orgNodeId: string, days: number, repeatWithinDays: number | null) {
  return useQuery({
    queryKey: ['factory', 'reliability', 'repeats', orgNodeId, days, repeatWithinDays],
    queryFn: () => api.get<RepeatFailureAnalysis>('/api/reliability/repeat-failures', { ...params(orgNodeId, days), repeatWithinDays: repeatWithinDays ?? undefined }),
  })
}

export function useApplySuggestion(days: number) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (assetId: string) => api.post<{ planId: string; name: string; triggerType: string; frequency: string; meterInterval: number | null }>(`/api/reliability/suggestions/${assetId}/apply?days=${days}`, {}),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['factory', 'reliability'] })
      void queryClient.invalidateQueries({ queryKey: ['factory', 'pm'] })
    },
  })
}

/** "every 3 weeks", "every 350 h". */
export function suggestedCadence(suggestion: PmSuggestion): string | null {
  if (suggestion.triggerType === 'Meter' && suggestion.meterInterval !== null) return `every ${suggestion.meterInterval} ${suggestion.meterUnit ?? ''}`.trim()
  if (suggestion.triggerType === 'Calendar' && suggestion.frequencyInterval !== null && suggestion.frequencyUnit) {
    const unit = suggestion.frequencyUnit.toLowerCase()
    return `every ${suggestion.frequencyInterval} ${suggestion.frequencyInterval === 1 ? unit.replace(/s$/, '') : unit}`
  }
  return null
}

/** MTBF in the largest whole unit that reads naturally: hours under two days, then days. */
export function formatMtbf(hours: number | null): string {
  if (hours === null) return '—'
  return hours < 48 ? `${hours.toFixed(0)} h` : `${(hours / 24).toFixed(hours < 240 ? 1 : 0)} days`
}
