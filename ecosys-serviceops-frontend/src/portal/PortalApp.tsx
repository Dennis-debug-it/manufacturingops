import { Suspense } from 'react'
import { Navigate, Route, Routes } from 'react-router-dom'
import { PortalShell } from './layout/PortalShell'
import { Spinner } from '../shared/components/Spinner'
import { PortalAuthProvider } from './auth/PortalAuthContext'
import { PortalProtectedRoute } from './auth/PortalProtectedRoute'
import { PortalLoginPage } from './auth/PortalLoginPage'
import { PortalVerifyPage } from './auth/PortalVerifyPage'
import { PortalDashboardPage } from './PortalDashboardPage'
import { PortalRequestsPage } from './requests/PortalRequestsPage'
import { PortalRequestDetailPage } from './requests/PortalRequestDetailPage'
import { PortalNewRequestPage } from './requests/PortalNewRequestPage'
import { PortalAssetsPage } from './PortalAssetsPage'
import { PortalMaintenancePage } from './PortalMaintenancePage'
import { PortalInvoicesPage } from './finance/PortalInvoicesPage'
import { PortalInvoiceDetailPage } from './finance/PortalInvoiceDetailPage'
import { PortalQuotesPage } from './finance/PortalQuotesPage'
import { PortalQuoteDetailPage } from './finance/PortalQuoteDetailPage'
import { PortalContractsPage } from './PortalContractsPage'
import { PortalReportsPage } from './PortalReportsPage'
import { PortalProfilePage } from './PortalProfilePage'
import { PortalKnowledgeHomePage } from './knowledge/PortalKnowledgeHomePage'
import { PortalKnowledgeBrowsePage } from './knowledge/PortalKnowledgeBrowsePage'
import { PortalKnowledgeArticlePage } from './knowledge/PortalKnowledgeArticlePage'
import { PortalFeatureGate, portalFeatures } from './components/PortalFeatureGate'

function PortalShellRoute({ children }: { children: React.ReactNode }) {
  return (
    <PortalProtectedRoute>
      <PortalShell>{children}</PortalShell>
    </PortalProtectedRoute>
  )
}

export function PortalApp() {
  return (
    <PortalAuthProvider>
      <Suspense fallback={<Spinner label="Loading portal" />}>
        <Routes>
          <Route path="login" element={<PortalLoginPage />} />
          <Route path="verify" element={<PortalVerifyPage />} />
          <Route path="access" element={<PortalVerifyPage permanent />} />
          <Route index element={<Navigate to="dashboard" replace />} />
          <Route path="dashboard" element={<PortalShellRoute><PortalDashboardPage /></PortalShellRoute>} />
          <Route path="requests" element={<PortalShellRoute><PortalRequestsPage /></PortalShellRoute>} />
          <Route path="requests/new" element={<PortalShellRoute><PortalFeatureGate feature={portalFeatures.requestSubmission}><PortalNewRequestPage /></PortalFeatureGate></PortalShellRoute>} />
          <Route path="requests/:id" element={<PortalShellRoute><PortalRequestDetailPage /></PortalShellRoute>} />
          <Route path="assets" element={<PortalShellRoute><PortalFeatureGate feature={portalFeatures.assetRegister}><PortalAssetsPage /></PortalFeatureGate></PortalShellRoute>} />
          <Route path="maintenance" element={<PortalShellRoute><PortalMaintenancePage /></PortalShellRoute>} />
          <Route path="reports" element={<PortalShellRoute><PortalReportsPage /></PortalShellRoute>} />
          <Route path="invoices" element={<PortalShellRoute><PortalFeatureGate feature={portalFeatures.invoices}><PortalInvoicesPage /></PortalFeatureGate></PortalShellRoute>} />
          <Route path="invoices/:id" element={<PortalShellRoute><PortalFeatureGate feature={portalFeatures.invoices}><PortalInvoiceDetailPage /></PortalFeatureGate></PortalShellRoute>} />
          <Route path="quotes" element={<PortalShellRoute><PortalQuotesPage /></PortalShellRoute>} />
          <Route path="quotes/:id" element={<PortalShellRoute><PortalQuoteDetailPage /></PortalShellRoute>} />
          <Route path="contracts" element={<PortalShellRoute><PortalFeatureGate feature={portalFeatures.contracts}><PortalContractsPage /></PortalFeatureGate></PortalShellRoute>} />
          <Route path="knowledge" element={<PortalShellRoute><PortalKnowledgeHomePage /></PortalShellRoute>} />
          <Route path="knowledge/browse" element={<PortalShellRoute><PortalKnowledgeBrowsePage /></PortalShellRoute>} />
          <Route path="knowledge/articles/:slug" element={<PortalShellRoute><PortalKnowledgeArticlePage /></PortalShellRoute>} />
          <Route path="profile" element={<PortalShellRoute><PortalProfilePage /></PortalShellRoute>} />
          <Route path="*" element={<Navigate to="dashboard" replace />} />
        </Routes>
      </Suspense>
    </PortalAuthProvider>
  )
}
