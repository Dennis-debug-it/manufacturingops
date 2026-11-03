import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../../shared/api/apiClient'
import { endpoints } from '../../shared/api/endpoints'

export type EtaNotificationEventType = 'Assigned' | 'EnRoute' | 'Arrived' | 'Completed'

export interface EtaNotificationSetting {
  id: string | null
  eventType: EtaNotificationEventType
  enabled: boolean
  subjectTemplate: string
  bodyTemplate: string
}

export interface UpsertEtaNotificationSettingPayload {
  enabled: boolean
  subjectTemplate: string
  bodyTemplate: string
}

const etaSettingsKey = ['settings', 'notifications', 'eta'] as const

export function useEtaNotificationSettings() {
  return useQuery({
    queryKey: etaSettingsKey,
    queryFn: () => api.get<EtaNotificationSetting[]>(endpoints.notificationTemplates.eta),
    staleTime: 30_000,
  })
}

export function useUpdateEtaNotificationSetting(eventType: EtaNotificationEventType) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: UpsertEtaNotificationSettingPayload) =>
      api.put<EtaNotificationSetting>(endpoints.notificationTemplates.etaEvent(eventType), payload),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: etaSettingsKey })
    },
  })
}
