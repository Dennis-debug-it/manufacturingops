import { useInfiniteQuery, useMutation, useQuery, useQueryClient, type InfiniteData, type QueryClient } from '@tanstack/react-query'
import { api } from '../../shared/api/apiClient'
import { endpoints } from '../../shared/api/endpoints'
import { markAllReadInPages, markReadInPages, removeFromPages } from './logic'
import type { NotificationPage, NotificationPreference, NotificationPreferenceInput, UnreadCount } from './types'

export const notificationKeys = {
  all: ['notifications'] as const,
  unreadCount: ['notifications', 'unread-count'] as const,
  lists: ['notifications', 'list'] as const,
  list: (unreadOnly: boolean, limit: number) => ['notifications', 'list', { unreadOnly, limit }] as const,
  preferences: ['me', 'notification-preferences'] as const,
}

type Pages = InfiniteData<NotificationPage, unknown>

/**
 * The unread count behind the badge. While the live connection is up the server pushes changes;
 * while it is down (R1) the count is polled every 60 s.
 */
export function useUnreadCount(enabled: boolean, live: boolean) {
  return useQuery({
    queryKey: notificationKeys.unreadCount,
    queryFn: () => api.get<UnreadCount>(endpoints.me.notificationsUnreadCount, undefined, { suppressGlobalErrorToast: true }),
    enabled,
    staleTime: live ? Infinity : 30_000,
    refetchInterval: live ? false : 60_000,
  })
}

/** Newest first, a page at a time; the next page starts before the last item shown. */
export function useNotifications(unreadOnly: boolean, limit: number, enabled = true) {
  return useInfiniteQuery({
    queryKey: notificationKeys.list(unreadOnly, limit),
    queryFn: ({ pageParam }) =>
      api.get<NotificationPage>(endpoints.me.notifications, { unreadOnly, limit, before: pageParam ?? undefined }, { suppressGlobalErrorToast: true }),
    initialPageParam: null as string | null,
    getNextPageParam: (last) => (last.hasMore ? last.items[last.items.length - 1]?.createdAt ?? null : null),
    enabled,
  })
}

/** Applies an optimistic change to every cached list, telling it whether that list is unread-only. */
function updateLists(queryClient: QueryClient, update: (data: Pages | undefined, unreadOnly: boolean) => Pages | undefined) {
  for (const [key, data] of queryClient.getQueriesData<Pages>({ queryKey: notificationKeys.lists })) {
    const options = key[2] as { unreadOnly?: boolean } | undefined
    queryClient.setQueryData(key, update(data, Boolean(options?.unreadOnly)))
  }
}

function useInboxMutation<TVariables>(
  request: (variables: TVariables) => Promise<UnreadCount>,
  optimistic: (data: Pages | undefined, unreadOnly: boolean, variables: TVariables) => Pages | undefined,
) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: request,
    onMutate: async (variables) => {
      await queryClient.cancelQueries({ queryKey: notificationKeys.lists })
      updateLists(queryClient, (data, unreadOnly) => optimistic(data, unreadOnly, variables))
    },
    onSuccess: (result) => queryClient.setQueryData(notificationKeys.unreadCount, result),
    onError: () => void queryClient.invalidateQueries({ queryKey: notificationKeys.all }),
  })
}

export function useMarkNotificationRead() {
  return useInboxMutation(
    (id: string) => api.post<UnreadCount>(endpoints.me.notificationRead(id)),
    (data, unreadOnly, id) => markReadInPages(data, id, new Date().toISOString(), unreadOnly),
  )
}

export function useMarkAllNotificationsRead() {
  return useInboxMutation<void>(
    () => api.post<UnreadCount>(endpoints.me.notificationsReadAll),
    (data, unreadOnly) => markAllReadInPages(data, new Date().toISOString(), unreadOnly),
  )
}

export function useArchiveNotification() {
  return useInboxMutation(
    (id: string) => api.post<UnreadCount>(endpoints.me.notificationArchive(id)),
    (data, _unreadOnly, id) => removeFromPages(data, id),
  )
}

export function useNotificationPreferences() {
  return useQuery({
    queryKey: notificationKeys.preferences,
    queryFn: () => api.get<NotificationPreference[]>(endpoints.me.notificationPreferences),
  })
}

export function useSaveNotificationPreferences() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (items: NotificationPreferenceInput[]) => api.put(endpoints.me.notificationPreferences, items),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: notificationKeys.preferences }),
  })
}

export function sendTestPush() {
  return api.post(endpoints.me.pushTest, undefined, { suppressGlobalErrorToast: true })
}
