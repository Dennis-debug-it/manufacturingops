import type { ReactNode } from 'react'
import { useState } from 'react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { Toaster } from 'react-hot-toast'
import { AuthProvider } from '../shared/contexts/AuthContext'
import { TenantProvider } from '../shared/contexts/TenantContext'
import { normalizeError } from '../shared/api/apiError'
import { OutboxSyncAgent } from '../features/tech/offline/hooks'

export interface ProvidersProps {
  children: ReactNode
}

export function Providers({ children }: ProvidersProps) {
  const [queryClient] = useState(
    () =>
      new QueryClient({
        defaultOptions: {
          queries: {
            // 401 (session) and 403 (permission/license) won't be fixed by retrying — and retrying
            // a suspended-license query 3x just multiplies how many identical failures the alert
            // pipeline has to dedupe. Everything else keeps bounded retries for transient failures.
            retry: (failureCount, error) => {
              const status = normalizeError(error).status
              if (status === 401 || status === 403) {
                return false
              }
              return failureCount < 3
            },
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
          <OutboxSyncAgent />
          {children}
          <Toaster position="top-right" toastOptions={{ duration: 4000 }} />
        </TenantProvider>
      </AuthProvider>
    </QueryClientProvider>
  )
}
