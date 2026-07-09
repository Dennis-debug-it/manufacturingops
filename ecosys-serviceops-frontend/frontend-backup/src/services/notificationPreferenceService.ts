import { api } from '../lib/api'
import type { UpdateNotificationPreferenceInput, UserNotificationPreference } from '../types/api'

export const notificationPreferenceService = {
  getMyNotificationPreferences(signal?: AbortSignal): Promise<UserNotificationPreference[]> {
    return api.get<UserNotificationPreference[]>('/api/me/notification-preferences', { signal })
  },

  updateMyNotificationPreferences(prefs: UpdateNotificationPreferenceInput[]): Promise<void> {
    return api.put<void>('/api/me/notification-preferences', prefs)
  },
}
