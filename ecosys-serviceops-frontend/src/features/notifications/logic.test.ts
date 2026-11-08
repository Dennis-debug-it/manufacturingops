import { describe, expect, it } from 'vitest'
import { flattenPages, formatUnreadBadge, groupPreferences, inAppPath, markAllReadInPages, markReadInPages, relativeTime, removeFromPages } from './logic'
import type { InboxNotification, NotificationPreference } from './types'

const item = (id: string, readAt: string | null = null): InboxNotification => ({
  id, key: 'work-order.assigned', title: `Job ${id}`, body: 'Body', url: `/work-orders/${id}`, entityType: 'WorkOrder', entityId: id,
  severity: 'Info', createdAt: '2026-10-21T08:00:00Z', readAt,
})

const pages = (...items: InboxNotification[][]) => ({
  pages: items.map((page, index) => ({ items: page, hasMore: index < items.length - 1 })),
  pageParams: items.map((_, index) => (index === 0 ? null : `cursor-${index}`)),
})

describe('formatUnreadBadge', () => {
  it('hides the badge at zero and caps it at 99+', () => {
    expect(formatUnreadBadge(0)).toBeNull()
    expect(formatUnreadBadge(-3)).toBeNull()
    expect(formatUnreadBadge(1)).toBe('1')
    expect(formatUnreadBadge(99)).toBe('99')
    expect(formatUnreadBadge(100)).toBe('99+')
    expect(formatUnreadBadge(5000)).toBe('99+')
  })
})

describe('relativeTime', () => {
  const now = new Date('2026-10-21T12:00:00Z')
  it('counts up from just now to days', () => {
    expect(relativeTime('2026-10-21T11:59:30Z', now)).toBe('just now')
    expect(relativeTime('2026-10-21T11:55:00Z', now)).toBe('5m ago')
    expect(relativeTime('2026-10-21T09:00:00Z', now)).toBe('3h ago')
    expect(relativeTime('2026-10-19T12:00:00Z', now)).toBe('2d ago')
  })
  it('never shows a time in the future', () => {
    expect(relativeTime('2026-10-21T12:00:10Z', now)).toBe('just now')
  })
})

describe('mark read', () => {
  it('marks one item read and leaves the others', () => {
    const next = markReadInPages(pages([item('a'), item('b')], [item('c')]), 'b', '2026-10-21T12:00:00Z', false)
    expect(flattenPages(next).map((x) => [x.id, x.readAt])).toEqual([['a', null], ['b', '2026-10-21T12:00:00Z'], ['c', null]])
  })
  it('keeps the original read time of an already-read item', () => {
    const next = markReadInPages(pages([item('a', '2026-10-20T08:00:00Z')]), 'a', '2026-10-21T12:00:00Z', false)
    expect(flattenPages(next)[0]?.readAt).toBe('2026-10-20T08:00:00Z')
  })
  it('drops the item from an unread-only list', () => {
    expect(flattenPages(markReadInPages(pages([item('a'), item('b')]), 'a', 'now', true)).map((x) => x.id)).toEqual(['b'])
  })
  it('marks everything read, or empties an unread-only list', () => {
    expect(flattenPages(markAllReadInPages(pages([item('a')], [item('b')]), 'now', false)).every((x) => x.readAt === 'now')).toBe(true)
    expect(flattenPages(markAllReadInPages(pages([item('a')], [item('b')]), 'now', true))).toEqual([])
  })
  it('archive removes the item', () => {
    expect(flattenPages(removeFromPages(pages([item('a'), item('b')]), 'a')).map((x) => x.id)).toEqual(['b'])
  })
  it('leaves an empty cache alone', () => {
    expect(markReadInPages(undefined, 'a', 'now', false)).toBeUndefined()
  })
})

describe('inAppPath', () => {
  it('follows only in-app links', () => {
    expect(inAppPath('/work-orders/1?tab=parts')).toBe('/work-orders/1?tab=parts')
    expect(inAppPath('https://evil.example')).toBeNull()
    expect(inAppPath('//evil.example')).toBeNull()
    expect(inAppPath(null)).toBeNull()
  })
})

describe('groupPreferences', () => {
  it('groups by category in API order', () => {
    const pref = (key: string, category: string): NotificationPreference => ({
      notificationKey: key, label: key, category, emailEnabled: true, inAppEnabled: true, pushEnabled: true, whatsAppEnabled: false, channels: ['InApp', 'Push', 'Email'],
    })
    const grouped = groupPreferences([pref('a', 'Work Orders'), pref('b', 'Projects'), pref('c', 'Work Orders')])
    expect(grouped.map((g) => [g.category, g.items.map((x) => x.notificationKey)])).toEqual([['Work Orders', ['a', 'c']], ['Projects', ['b']]])
  })
})
