import type { ReactNode } from 'react'
import { useState } from 'react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { Toaster } from 'react-hot-toast'
import { AuthProvider } from '../shared/contexts/AuthContext'
import { TenantProvider } from '../shared/contexts/TenantContext'

export interface ProvidersProps {
  children: ReactNode
}

export function Providers({ children }: ProvidersProps) {
  const [queryClient] = useState(
    () =>
      new QueryClient({
        defaultOptions: {
          queries: {
            retry: 3,
            staleTime: 30_000,
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
          {children}
          <Toaster position="top-right" toastOptions={{ duration: 4000 }} />
        </TenantProvider>
      </AuthProvider>
    </QueryClientProvider>
  )
}
