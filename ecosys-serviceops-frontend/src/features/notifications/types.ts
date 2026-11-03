/** One bell entry (InboxNotification in the API). */
export interface InboxNotification {
  id: string
  key: string
  title: string
  body: string
  url: string | null
  entityType: string | null
  entityId: string | null
  severity: 'Info' | 'Warning' | 'Critical'
  createdAt: string
  readAt: string | null
}

export interface NotificationPage {
  items: InboxNotification[]
  hasMore: boolean
}

export interface UnreadCount {
  count: number
}

export type NotificationChannel = 'InApp' | 'Push' | 'Email' | 'WhatsApp'

export interface NotificationPreference {
  notificationKey: string
  label: string
  category: string
  emailEnabled: boolean
  /** Show it in the bell. */
  inAppEnabled: boolean
  pushEnabled: boolean
  whatsAppEnabled: boolean
  /** The channels this notification can use; only these get a toggle. */
  channels: NotificationChannel[]
}

export type NotificationPreferenceInput = Pick<NotificationPreference, 'notificationKey' | 'emailEnabled' | 'inAppEnabled' | 'pushEnabled' | 'whatsAppEnabled'>
