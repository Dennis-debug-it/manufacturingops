import type { ReactNode } from 'react'
import { createContext, useContext, useMemo } from 'react'
import { useAuthContext } from './AuthContext'

export interface TenantContextValue {
  tenantId: string | null
  tenantName: string | null
  country: string | null
  industry: string | null
  logoUrl: string | null
  primaryColor: string | null
  secondaryColor: string | null
  showPoweredByEcosys: boolean
}

const TenantContext = createContext<TenantContextValue | undefined>(undefined)

export interface TenantProviderProps {
  children: ReactNode
}

export function TenantProvider({ children }: TenantProviderProps) {
  const { tenant } = useAuthContext()

  const value = useMemo<TenantContextValue>(() => ({
    tenantId: tenant?.tenantId ?? null,
    tenantName: tenant?.companyName ?? null,
    country: tenant?.country ?? null,
    industry: tenant?.industry ?? null,
    logoUrl: tenant?.logoUrl ?? null,
    primaryColor: tenant?.primaryColor ?? null,
    secondaryColor: tenant?.secondaryColor ?? null,
    showPoweredByEcosys: tenant?.showPoweredByEcosys ?? true,
  }), [tenant])

  return <TenantContext.Provider value={value}>{children}</TenantContext.Provider>
}

export function useTenantContext() {
  const context = useContext(TenantContext)
  if (!context) {
    throw new Error('useTenantContext must be used within TenantProvider')
  }
  return context
}
