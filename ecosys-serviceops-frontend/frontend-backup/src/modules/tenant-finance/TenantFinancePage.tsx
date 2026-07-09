import { Outlet, useLocation, useNavigate } from 'react-router-dom'
import { PageHeader } from '../../components/ui/PageHeader'
import { PageTabs, SegmentedTabs } from '../../components/ui/Workspace'

type FinanceGroupKey = 'overview' | 'documents' | 'costing-rates' | 'tax-receivables' | 'analytics' | 'settings'

type FinanceSubTab = {
  id: string
  label: string
  path: string
}

type FinanceGroup = {
  key: FinanceGroupKey
  label: string
  description: string
  path: string
  defaultPath: string
  subtabs: FinanceSubTab[]
}

const financeGroups: FinanceGroup[] = [
  {
    key: 'overview',
    label: 'Overview',
    description: 'A grouped snapshot of revenue, billing, adjustments, and attention items.',
    path: '/finance',
    defaultPath: '/finance',
    subtabs: [],
  },
  {
    key: 'documents',
    label: 'Documents',
    description: 'Quotes, invoices, receipts, notes, and proformas.',
    path: '/finance/documents',
    defaultPath: '/finance/documents/quotes',
    subtabs: [
      { id: 'quotes', label: 'Quotes / Estimates', path: '/finance/documents/quotes' },
      { id: 'proforma-invoices', label: 'Proforma Invoices', path: '/finance/documents/proforma-invoices' },
      { id: 'invoices', label: 'Invoices', path: '/finance/documents/invoices' },
      { id: 'receipts-payments', label: 'Receipts / Payments', path: '/finance/documents/receipts-payments' },
      { id: 'credit-notes', label: 'Credit Notes', path: '/finance/documents/credit-notes' },
      { id: 'debit-notes', label: 'Debit Notes', path: '/finance/documents/debit-notes' },
    ],
  },
  {
    key: 'costing-rates',
    label: 'Costing & Rates',
    description: 'Billable work, material costing, and commercial pricing controls.',
    path: '/finance/costing-rates',
    defaultPath: '/finance/costing-rates/billable-work',
    subtabs: [
      { id: 'billable-work', label: 'Billable Work', path: '/finance/costing-rates/billable-work' },
      { id: 'expenses', label: 'Expenses / Costs', path: '/finance/costing-rates/expenses' },
      { id: 'material-costing', label: 'Material Costing', path: '/finance/costing-rates/material-costing' },
      { id: 'rate-cards', label: 'Rate Cards', path: '/finance/costing-rates/rate-cards' },
      { id: 'preferential-rates', label: 'Preferential Rates', path: '/finance/costing-rates/preferential-rates' },
    ],
  },
  {
    key: 'tax-receivables',
    label: 'Tax & Receivables',
    description: 'Tax handling, statements, aging, and billing approvals.',
    path: '/finance/tax-receivables',
    defaultPath: '/finance/tax-receivables/aging',
    subtabs: [
      { id: 'tax-vat', label: 'Tax / VAT', path: '/finance/tax-receivables/tax-vat' },
      { id: 'statements', label: 'Customer Statements', path: '/finance/tax-receivables/statements' },
      { id: 'aging', label: 'Aging', path: '/finance/tax-receivables/aging' },
      { id: 'approvals', label: 'Approval Matrix', path: '/finance/tax-receivables/approvals' },
    ],
  },
  {
    key: 'analytics',
    label: 'Analytics',
    description: 'Revenue dashboard, aging analysis, client statements, and reports.',
    path: '/finance/analytics',
    defaultPath: '/finance/analytics',
    subtabs: [
      { id: 'revenue', label: 'Revenue Dashboard', path: '/finance/analytics' },
      { id: 'aging', label: 'Aging Analysis', path: '/finance/analytics?view=aging' },
      { id: 'statement', label: 'Client Statement', path: '/finance/analytics?view=statement' },
      { id: 'reports', label: 'Reports', path: '/finance/analytics?view=reports' },
    ],
  },
  {
    key: 'settings',
    label: 'Settings',
    description: 'Document numbering, templates, and finance presentation controls.',
    path: '/finance/settings',
    defaultPath: '/finance/settings',
    subtabs: [],
  },
]

function resolveGroup(pathname: string): FinanceGroup {
  if (pathname.startsWith('/finance/documents')) return financeGroups[1]
  if (pathname.startsWith('/finance/quotes') || pathname.startsWith('/finance/invoices')) return financeGroups[1]
  if (pathname.startsWith('/finance/costing-rates')) return financeGroups[2]
  if (pathname.startsWith('/finance/rate-cards') || pathname.startsWith('/finance/preferential-rates') || pathname.startsWith('/finance/billable-work')) return financeGroups[2]
  if (pathname.startsWith('/finance/tax-receivables')) return financeGroups[3]
  if (pathname.startsWith('/finance/approvals')) return financeGroups[3]
  if (pathname.startsWith('/finance/analytics')) return financeGroups[4]
  if (pathname.startsWith('/finance/reports')) return financeGroups[4]
  if (pathname.startsWith('/finance/settings')) return financeGroups[5]
  return financeGroups[0]
}

function resolveSubTab(group: FinanceGroup, pathname: string, search: string) {
  if (group.key === 'analytics') {
    const view = new URLSearchParams(search).get('view') ?? 'revenue'
    return group.subtabs.find((tab) => tab.id === view) ?? group.subtabs[0]
  }

  const legacyAliases: Record<FinanceGroupKey, Array<{ path: string; id: string }>> = {
    overview: [],
    documents: [
      { path: '/finance/quotes', id: 'quotes' },
      { path: '/finance/invoices', id: 'invoices' },
    ],
    'costing-rates': [
      { path: '/finance/rate-cards', id: 'rate-cards' },
      { path: '/finance/preferential-rates', id: 'preferential-rates' },
      { path: '/finance/billable-work', id: 'billable-work' },
    ],
    'tax-receivables': [
      { path: '/finance/approvals', id: 'approvals' },
    ],
    analytics: [
      { path: '/finance/reports', id: 'reports' },
    ],
    settings: [],
  }

  const legacyMatch = legacyAliases[group.key].find((item) => pathname === item.path || pathname.startsWith(`${item.path}/`))
  if (legacyMatch) {
    return group.subtabs.find((tab) => tab.id === legacyMatch.id) ?? group.subtabs[0] ?? null
  }

  return group.subtabs.find((tab) => pathname === tab.path || pathname.startsWith(`${tab.path}/`)) ?? group.subtabs[0] ?? null
}

export function TenantFinancePage() {
  const location = useLocation()
  const navigate = useNavigate()
  const group = resolveGroup(location.pathname)
  const activeSubTab = resolveSubTab(group, location.pathname, location.search)

  return (
    <div className="space-y-3">
      <PageHeader
        eyebrow="Finance"
        title="Tenant finance"
        description={group.description}
      />

      <SegmentedTabs
        tabs={financeGroups.map((item) => ({ id: item.key, label: item.label }))}
        activeTab={group.key}
        onChange={(key) => {
          const next = financeGroups.find((item) => item.key === key)
          if (next) navigate(next.defaultPath)
        }}
      />

      {group.subtabs.length > 0 ? (
        <PageTabs
          tabs={group.subtabs.map((tab) => ({ id: tab.id, label: tab.label }))}
          activeTab={activeSubTab?.id ?? group.subtabs[0].id}
          onChange={(id: string) => {
            const next = group.subtabs.find((tab) => tab.id === id)
            if (!next) return
            navigate(next.path)
          }}
        />
      ) : null}

      <Outlet />
    </div>
  )
}
