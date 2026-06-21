import { Outlet, useLocation, useNavigate } from 'react-router-dom'
import { ErrorState } from '../../components/ui/ErrorState'
import { PageHeader } from '../../components/ui/PageHeader'
import { PageTabs } from '../../components/ui/Workspace'
import { useTenantFinanceAccess } from './TenantFinanceShared'

const financeTabs = [
  { id: 'overview', label: 'Overview', path: '/finance' },
  { id: 'invoices', label: 'Invoices', path: '/finance/invoices' },
  { id: 'quotes', label: 'Quotes', path: '/finance/quotes' },
  { id: 'rate-cards', label: 'Rate Cards', path: '/finance/rate-cards' },
  { id: 'preferential-rates', label: 'Preferential Rates', path: '/finance/preferential-rates' },
  { id: 'approvals', label: 'Approval Matrix', path: '/finance/approvals' },
  { id: 'billable-work', label: 'Billable Work', path: '/finance/billable-work' },
  { id: 'reports', label: 'Reports', path: '/finance/reports' },
] as const

function resolveTab(pathname: string) {
  if (pathname.includes('/finance/invoices')) return 'invoices'
  if (pathname.includes('/finance/quotes')) return 'quotes'
  if (pathname.includes('/finance/rate-cards')) return 'rate-cards'
  if (pathname.includes('/finance/preferential-rates')) return 'preferential-rates'
  if (pathname.includes('/finance/approvals')) return 'approvals'
  if (pathname.includes('/finance/billable-work')) return 'billable-work'
  if (pathname.includes('/finance/reports')) return 'reports'
  return 'overview'
}

export function TenantFinancePage() {
  const location = useLocation()
  const navigate = useNavigate()
  const canViewFinance = useTenantFinanceAccess()

  if (!canViewFinance) {
    return <ErrorState title="Finance access unavailable" description="Your account does not currently have permission to open the tenant finance module." />
  }

  return (
    <div className="space-y-4">
      <PageHeader eyebrow="Finance" title="Tenant finance" description="Invoices, quotations, rate cards, approvals, and billable work for this tenant." />
      <PageTabs
        tabs={financeTabs.map((tab) => ({ id: tab.id, label: tab.label }))}
        activeTab={resolveTab(location.pathname)}
        onChange={(id: string) => {
          const tab = financeTabs.find((t) => t.id === id)
          if (tab) navigate(tab.path)
        }}
      />
      <Outlet />
    </div>
  )
}
