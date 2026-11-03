import type { ReactNode } from 'react'
import { useState } from 'react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { Toaster } from 'react-hot-toast'
import { AuthProvider } from '../shared/contexts/AuthContext'
import { TenantProvider } from '../shared/contexts/TenantContext'
import { shouldRetryQuery } from '../shared/api/apiError'
import { OutboxSyncAgent } from '../features/tech/offline/hooks'
import { LiveNotificationsProvider } from '../features/notifications/LiveNotifications'

export interface ProvidersProps {
  children: ReactNode
}

export function Providers({ children }: ProvidersProps) {
  const [queryClient] = useState(
    () =>
      new QueryClient({
        defaultOptions: {
          queries: {
            // Retry only what can clear up by itself (offline, timeouts, 5xx). A 4xx such as
            // not-found or a licence block gives the same answer every time, and retrying it just
            // keeps the page spinning before it shows the message.
            retry: shouldRetryQuery,
            staleTime: 30_000,
            // Long enough that jobs cached for offline use survive between visits (see tech/offline/persist.ts).
            gcTime: 24 * 60 * 60 * 1000,
            refetchOnWindowFocus: false,
          },
          mutations: {
            retry: 0,
          },
        },
      }),
  )

  return (
    <QueryClientProvider client={queryClient}>
      <AuthProvider>
        <TenantProvider>
          <LiveNotificationsProvider>
            <OutboxSyncAgent />
            {children}
            <Toaster position="top-right" toastOptions={{ duration: 4000 }} />
          </LiveNotificationsProvider>
        </TenantProvider>
      </AuthProvider>
    </QueryClientProvider>
  )
}
