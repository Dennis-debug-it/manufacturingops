import type { ReactNode } from 'react'
import { Navigate } from 'react-router-dom'
import { Spinner } from '../../shared/components/Spinner'
import { usePortalSettings, type PortalSettings } from '../api'

export type PortalFeature = (settings: PortalSettings) => boolean

export const portalFeatures = {
  requestSubmission: (s) => s.allowRequestSubmission,
  quoteResponses: (s) => s.allowQuoteResponses,
  invoices: (s) => s.showInvoices,
  contracts: (s) => s.showContracts,
  assetRegister: (s) => s.showAssetRegister,
} satisfies Record<string, PortalFeature>

/** True only once settings have loaded and the feature is on — gated UI stays hidden rather than flashing. */
export function usePortalFeature(feature: PortalFeature) {
  const { data } = usePortalSettings()
  return data ? feature(data) : false
}

/** Route guard for pages the tenant can switch off; a disabled page redirects to the dashboard. */
export function PortalFeatureGate({ feature, children }: { feature: PortalFeature; children: ReactNode }) {
  const { data, isLoading } = usePortalSettings()

  if (isLoading) {
    return <Spinner label="Loading" />
  }

  if (data && !feature(data)) {
    return <Navigate to="/portal/dashboard" replace />
  }

  return <>{children}</>
}
