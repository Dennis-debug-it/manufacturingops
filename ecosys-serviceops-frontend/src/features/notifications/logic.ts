import type { InfiniteData } from '@tanstack/react-query'
import type { InboxNotification, NotificationChannel, NotificationPage, NotificationPreference } from './types'
import { WHATSAPP_ENABLED } from '../../shared/config/features'

/** The bell's badge: nothing at zero, capped at "99+". */
export function formatUnreadBadge(count: number): string | null {
  if (!Number.isFinite(count) || count <= 0) return null
  return count > 99 ? '99+' : String(Math.floor(count))
}

/** Short relative time for the bell: "just now", "5m ago", "3h ago", "2d ago", then the date. */
export function relativeTime(iso: string, now: Date = new Date()): string {
  const then = new Date(iso)
  const seconds = Math.max(0, Math.round((now.getTime() - then.getTime()) / 1000))
  if (seconds < 60) return 'just now'
  const minutes = Math.floor(seconds / 60)
  if (minutes < 60) return `${minutes}m ago`
  const hours = Math.floor(minutes / 60)
  if (hours < 24) return `${hours}h ago`
  const days = Math.floor(hours / 24)
  if (days < 7) return `${days}d ago`
  return then.toLocaleDateString(undefined, { day: 'numeric', month: 'short', ...(then.getFullYear() === now.getFullYear() ? {} : { year: 'numeric' }) })
}

type Pages = InfiniteData<NotificationPage, unknown>

/** Optimistic "mark read" for cached pages. An unread-only list drops the item instead. */
export function markReadInPages(data: Pages | undefined, id: string, readAt: string, unreadOnly: boolean): Pages | undefined {
  if (!data) return data
  return {
    ...data,
    pages: data.pages.map((page) => ({
      ...page,
      items: unreadOnly
        ? page.items.filter((item) => item.id !== id)
        : page.items.map((item) => (item.id === id && !item.readAt ? { ...item, readAt } : item)),
    })),
  }
}

/** Optimistic "mark all read". */
export function markAllReadInPages(data: Pages | undefined, readAt: string, unreadOnly: boolean): Pages | undefined {
  if (!data) return data
  return {
    ...data,
    pages: data.pages.map((page) => ({
      ...page,
      items: unreadOnly ? [] : page.items.map((item) => (item.readAt ? item : { ...item, readAt })),
    })),
  }
}

export function removeFromPages(data: Pages | undefined, id: string): Pages | undefined {
  if (!data) return data
  return { ...data, pages: data.pages.map((page) => ({ ...page, items: page.items.filter((item) => item.id !== id) })) }
}

export function flattenPages(data: Pages | undefined): InboxNotification[] {
  return data?.pages.flatMap((page) => page.items) ?? []
}

/** Only in-app links are followed; anything else (or nothing) stays on the current page. */
export function inAppPath(url: string | null): string | null {
  return url && url.startsWith('/') && !url.startsWith('//') ? url : null
}

/** Preferences grouped by category, in the order the API lists them. */
export function groupPreferences(preferences: NotificationPreference[]): Array<{ category: string; items: NotificationPreference[] }> {
  const groups = new Map<string, NotificationPreference[]>()
  for (const preference of preferences) {
    groups.set(preference.category, [...(groups.get(preference.category) ?? []), preference])
  }
  return [...groups].map(([category, items]) => ({ category, items }))
}

/** The channels worth a toggle, in display order. WhatsApp appears only once a key offers it (step 3d). */
export const CHANNEL_FIELDS: Array<{ channel: NotificationChannel; field: 'inAppEnabled' | 'pushEnabled' | 'emailEnabled' | 'whatsAppEnabled'; label: string }> = [
  { channel: 'InApp', field: 'inAppEnabled', label: 'Bell' },
  { channel: 'Push', field: 'pushEnabled', label: 'Push' },
  { channel: 'Email', field: 'emailEnabled', label: 'Email' },
  ...(WHATSAPP_ENABLED ? [{ channel: 'WhatsApp' as const, field: 'whatsAppEnabled' as const, label: 'WhatsApp' }] : []),
]
