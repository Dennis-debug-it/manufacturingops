import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Archive, Bell, CheckCheck } from 'lucide-react'
import { EmptyState } from '../../shared/components/EmptyState'
import { PageHeader } from '../../shared/components/PageHeader'
import { Spinner } from '../../shared/components/Spinner'
import { cn } from '../../shared/utils/cn'
import { useArchiveNotification, useMarkAllNotificationsRead, useMarkNotificationRead, useNotifications, useUnreadCount } from './api'
import { useLiveNotifications } from './LiveNotifications'
import { flattenPages, inAppPath, relativeTime } from './logic'
import type { InboxNotification } from './types'

const PAGE_SIZE = 50

/** Every notification still in the inbox (90 days, R2), with an unread filter and archive. */
export function NotificationInbox() {
  const { live, enabled } = useLiveNotifications()
  const [unreadOnly, setUnreadOnly] = useState(false)
  const navigate = useNavigate()
  const list = useNotifications(unreadOnly, PAGE_SIZE, enabled)
  const unread = useUnreadCount(enabled, live)
  const markRead = useMarkNotificationRead()
  const markAllRead = useMarkAllNotificationsRead()
  const archive = useArchiveNotification()
  const items = flattenPages(list.data)
  const unreadCount = unread.data?.count ?? 0

  const open = (item: InboxNotification) => {
    if (!item.readAt) markRead.mutate(item.id)
    const path = inAppPath(item.url)
    if (path) navigate(path)
  }

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div role="tablist" aria-label="Filter notifications" className="inline-flex rounded-lg border border-slate-200 bg-white p-0.5">
          {([false, true] as const).map((value) => (
            <button
              key={String(value)}
              type="button"
              role="tab"
              aria-selected={unreadOnly === value}
              onClick={() => setUnreadOnly(value)}
              className={cn('h-8 rounded-md px-3 text-sm font-medium', unreadOnly === value ? 'bg-[var(--app-primary)] text-white' : 'text-slate-600 hover:bg-slate-50')}
            >
              {value ? `Unread${unreadCount > 0 ? ` (${unreadCount})` : ''}` : 'All'}
            </button>
          ))}
        </div>
        <button
          type="button"
          disabled={unreadCount === 0 || markAllRead.isPending}
          onClick={() => markAllRead.mutate()}
          className="inline-flex h-9 items-center gap-1.5 rounded-lg border border-slate-200 bg-white px-3 text-sm font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-50"
        >
          <CheckCheck className="h-4 w-4" />Mark all read
        </button>
      </div>

      {list.isLoading ? <Spinner label="Loading notifications" /> : null}
      {list.isError ? <p className="text-sm text-rose-700">Notifications could not be loaded.</p> : null}
      {list.isSuccess && items.length === 0 ? (
        <EmptyState icon={Bell} title={unreadOnly ? 'No unread notifications' : 'No notifications'} description="Assignments, approvals and reminders for you appear here." />
      ) : null}

      {items.length > 0 ? (
        <ul className="divide-y divide-slate-100 overflow-hidden rounded-xl border border-slate-200 bg-white">
          {items.map((item) => (
            <li key={item.id} className={cn('flex items-start gap-2 px-3 py-3', !item.readAt && 'bg-[var(--app-primary-soft)]')}>
              <button type="button" onClick={() => open(item)} className="flex min-w-0 flex-1 gap-2 text-left">
                <span className={cn('mt-1.5 h-2 w-2 shrink-0 rounded-full', item.readAt ? 'bg-transparent' : item.severity === 'Critical' ? 'bg-rose-600' : item.severity === 'Warning' ? 'bg-amber-500' : 'bg-[var(--app-primary)]')} aria-hidden="true" />
                <span className="min-w-0">
                  <span className={cn('block text-sm', item.readAt ? 'text-slate-700' : 'font-semibold text-slate-900')}>{item.title}</span>
                  <span className="block text-sm text-slate-500">{item.body}</span>
                  <span className="mt-0.5 block text-xs text-slate-400" title={new Date(item.createdAt).toLocaleString()}>{relativeTime(item.createdAt)}</span>
                </span>
              </button>
              <button
                type="button"
                onClick={() => archive.mutate(item.id)}
                title="Archive"
                aria-label={`Archive ${item.title}`}
                className="inline-flex h-8 w-8 shrink-0 items-center justify-center rounded-md text-slate-400 hover:bg-slate-100 hover:text-slate-700"
              >
                <Archive className="h-4 w-4" />
              </button>
            </li>
          ))}
        </ul>
      ) : null}

      {list.hasNextPage ? (
        <button
          type="button"
          disabled={list.isFetchingNextPage}
          onClick={() => void list.fetchNextPage()}
          className="inline-flex h-10 w-full items-center justify-center rounded-lg border border-slate-200 bg-white text-sm font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-60"
        >
          {list.isFetchingNextPage ? 'Loading…' : 'Load older'}
        </button>
      ) : null}
    </div>
  )
}

export function NotificationsPage() {
  return (
    <div className="space-y-4">
      <PageHeader title="Notifications" subtitle="Kept for 90 days. Archive the ones you're done with." />
      <NotificationInbox />
    </div>
  )
}

export function TechNotificationsPage() {
  return (
    <div className="space-y-3">
      <h1 className="text-lg font-semibold text-slate-900">Notifications</h1>
      <NotificationInbox />
    </div>
  )
}
