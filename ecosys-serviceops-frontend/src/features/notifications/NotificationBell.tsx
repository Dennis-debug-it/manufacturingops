import { useEffect, useRef, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { Bell, CheckCheck, Settings2 } from 'lucide-react'
import { Spinner } from '../../shared/components/Spinner'
import { cn } from '../../shared/utils/cn'
import { useMarkAllNotificationsRead, useMarkNotificationRead, useNotifications, useUnreadCount } from './api'
import { useLiveNotifications } from './LiveNotifications'
import { formatUnreadBadge, inAppPath, relativeTime } from './logic'
import type { InboxNotification } from './types'

const PANEL_SIZE = 10

export interface NotificationBellProps {
  /** The full inbox page: /notifications in the office, /tech/notifications in the technician app. */
  viewAllPath: string
  preferencesPath: string
  className?: string
}

/** The bell: unread badge, and a panel of recent notifications. Clicking one marks it read and opens it. */
export function NotificationBell({ viewAllPath, preferencesPath, className }: NotificationBellProps) {
  const { live, enabled } = useLiveNotifications()
  const [open, setOpen] = useState(false)
  const containerRef = useRef<HTMLDivElement>(null)
  const navigate = useNavigate()
  const unread = useUnreadCount(enabled, live)
  const recent = useNotifications(false, PANEL_SIZE, enabled && open)
  const markRead = useMarkNotificationRead()
  const markAllRead = useMarkAllNotificationsRead()
  const badge = formatUnreadBadge(unread.data?.count ?? 0)
  const items = recent.data?.pages[0]?.items ?? []

  useEffect(() => {
    if (!open) return undefined
    const onPointer = (event: PointerEvent) => {
      if (!containerRef.current?.contains(event.target as Node)) setOpen(false)
    }
    const onKey = (event: KeyboardEvent) => { if (event.key === 'Escape') setOpen(false) }
    document.addEventListener('pointerdown', onPointer)
    document.addEventListener('keydown', onKey)
    return () => {
      document.removeEventListener('pointerdown', onPointer)
      document.removeEventListener('keydown', onKey)
    }
  }, [open])

  if (!enabled) return null

  const openItem = (item: InboxNotification) => {
    if (!item.readAt) markRead.mutate(item.id)
    setOpen(false)
    const path = inAppPath(item.url)
    if (path) navigate(path)
  }

  return (
    <div ref={containerRef} className={cn('relative', className)}>
      <button
        type="button"
        onClick={() => setOpen((value) => !value)}
        aria-haspopup="true"
        aria-expanded={open}
        aria-label={badge ? `Notifications, ${unread.data?.count} unread` : 'Notifications'}
        className="relative inline-flex h-10 w-10 items-center justify-center rounded-lg text-slate-600 transition hover:bg-[var(--app-primary-soft)]"
      >
        <Bell className="h-5 w-5" />
        {badge ? (
          <span className="absolute -right-0.5 -top-0.5 min-w-[1.25rem] rounded-full bg-rose-600 px-1 text-center text-[10px] font-semibold leading-5 text-white" aria-hidden="true">
            {badge}
          </span>
        ) : null}
      </button>
      {open ? (
        <div role="dialog" aria-label="Notifications" className="absolute right-0 z-30 mt-2 w-80 max-w-[calc(100vw-2rem)] overflow-hidden rounded-xl border border-slate-200 bg-white shadow-lg">
          <div className="flex items-center justify-between gap-2 border-b border-slate-100 px-3 py-2">
            <span className="text-sm font-semibold text-slate-900">Notifications</span>
            <span className="flex items-center gap-1">
              <button
                type="button"
                disabled={!badge || markAllRead.isPending}
                onClick={() => markAllRead.mutate()}
                className="inline-flex h-8 items-center gap-1 rounded-md px-2 text-xs font-medium text-slate-600 hover:bg-slate-50 disabled:opacity-40"
              >
                <CheckCheck className="h-3.5 w-3.5" />Mark all read
              </button>
              <Link to={preferencesPath} onClick={() => setOpen(false)} title="Notification settings" aria-label="Notification settings" className="inline-flex h-8 w-8 items-center justify-center rounded-md text-slate-500 hover:bg-slate-50">
                <Settings2 className="h-4 w-4" />
              </Link>
            </span>
          </div>
          <div className="max-h-96 overflow-y-auto">
            {recent.isLoading ? <div className="p-4"><Spinner label="Loading notifications" /></div> : null}
            {recent.isError ? <p className="p-4 text-sm text-rose-700">Notifications could not be loaded.</p> : null}
            {recent.isSuccess && items.length === 0 ? <p className="p-6 text-center text-sm text-slate-500">You're all caught up.</p> : null}
            <ul className="divide-y divide-slate-100">
              {items.map((item) => (
                <li key={item.id}>
                  <button type="button" onClick={() => openItem(item)} className={cn('flex w-full gap-2 px-3 py-2.5 text-left hover:bg-slate-50', !item.readAt && 'bg-[var(--app-primary-soft)]')}>
                    <span className={cn('mt-1.5 h-2 w-2 shrink-0 rounded-full', item.readAt ? 'bg-transparent' : item.severity === 'Critical' ? 'bg-rose-600' : item.severity === 'Warning' ? 'bg-amber-500' : 'bg-[var(--app-primary)]')} aria-hidden="true" />
                    <span className="min-w-0 flex-1">
                      <span className={cn('block truncate text-sm', item.readAt ? 'text-slate-700' : 'font-semibold text-slate-900')}>{item.title}</span>
                      <span className="line-clamp-2 block text-xs text-slate-500">{item.body}</span>
                      <span className="mt-0.5 block text-[11px] text-slate-400">{relativeTime(item.createdAt)}</span>
                    </span>
                  </button>
                </li>
              ))}
            </ul>
          </div>
          <Link to={viewAllPath} onClick={() => setOpen(false)} className="block border-t border-slate-100 px-3 py-2 text-center text-sm font-medium text-[var(--app-primary)] hover:bg-slate-50">
            View all
          </Link>
        </div>
      ) : null}
    </div>
  )
}
