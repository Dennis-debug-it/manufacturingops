import { AlertCircle, Banknote, FileText, LayoutGrid, Receipt, ShieldCheck, Wallet, type LucideIcon } from 'lucide-react'
import { useMemo } from 'react'
import { Link, useLocation } from 'react-router-dom'
import { EmptyState } from '../../components/ui/EmptyState'
import { PageHeader } from '../../components/ui/PageHeader'
import { SectionCard, MetricGrid, MetricCard } from '../../components/ui/Workspace'
import { FinanceReportsPage } from './FinanceReportsPage'

type HubLink = {
  title: string
  description: string
  to: string
  icon: LucideIcon
}

function HubGrid({ links }: { links: HubLink[] }) {
  return (
    <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
      {links.map((link) => {
        const Icon = link.icon

        return (
          <Link
            key={link.to}
            to={link.to}
            className="group rounded-[18px] border border-[var(--app-divider)] bg-[var(--app-surface)] p-4 transition hover:-translate-y-0.5 hover:border-[color:var(--app-primary-soft)] hover:bg-[var(--app-subtle)]"
          >
            <div className="flex items-start gap-3">
              <div className="icon-accent rounded-[14px] p-2.5">
                <Icon className="h-4 w-4" />
              </div>
              <div className="min-w-0">
                <p className="text-sm font-semibold text-app">{link.title}</p>
                <p className="mt-1 text-sm leading-5 text-muted">{link.description}</p>
              </div>
            </div>
          </Link>
        )
      })}
    </div>
  )
}

export function FinanceComingSoonPage({
  title,
  description,
}: {
  title: string
  description: string
}) {
  return (
    <section className="space-y-4">
      <PageHeader eyebrow="Finance" title={title} description={description} />
      <SectionCard title="Coming soon" description="This screen is reserved in the navigation, but backend support is still pending.">
        <EmptyState
          title={title}
          description={description}
          action={
            <button type="button" className="button-secondary" disabled>
              Backend support required
            </button>
          }
        />
      </SectionCard>
    </section>
  )
}

export function FinanceDocumentsHubPage() {
  const links = useMemo<HubLink[]>(() => [
    { title: 'Quotes / Estimates', description: 'Prepare client-facing quotations and estimates.', to: '/finance/documents/quotes', icon: FileText },
    { title: 'Proforma Invoices', description: 'Advance billing before a tax invoice is issued.', to: '/finance/documents/proforma-invoices', icon: FileText },
    { title: 'Invoices', description: 'Issued billing records and invoice management.', to: '/finance/documents/invoices', icon: Receipt },
    { title: 'Receipts / Payments', description: 'Record receipts and payment allocations.', to: '/finance/documents/receipts-payments', icon: Banknote },
    { title: 'Credit Notes', description: 'Correct or reduce previously issued invoices.', to: '/finance/documents/credit-notes', icon: AlertCircle },
    { title: 'Debit Notes', description: 'Add charges after invoice issuance.', to: '/finance/documents/debit-notes', icon: AlertCircle },
  ], [])

  return (
    <section className="space-y-4">
      <PageHeader eyebrow="Finance" title="Documents" description="Client-facing documents and their lifecycle, organized in one place." />
      <MetricGrid className="xl:grid-cols-3">
        <MetricCard label="Document family" value="6" meta="Quotes, invoices, payments, and adjustments" />
        <MetricCard label="Status" value="Organized" meta="Structured by document intent rather than by raw screens" emphasis="accent" />
        <MetricCard label="Template control" value="Settings" meta="Use Documents & Branding for layout and numbering" />
      </MetricGrid>
      <SectionCard title="Open a document area" description="Use the tabs in the finance shell or open a document type directly.">
        <HubGrid links={links} />
      </SectionCard>
    </section>
  )
}

export function FinanceCostingRatesHubPage() {
  const links = useMemo<HubLink[]>(() => [
    { title: 'Billable Work', description: 'Billable work orders awaiting invoicing.', to: '/finance/costing-rates/billable-work', icon: Wallet },
    { title: 'Expenses / Costs', description: 'Track subcontractor, transport, labour, and job costs.', to: '/finance/costing-rates/expenses', icon: Receipt },
    { title: 'Material Costing', description: 'Issue materials into job cost tracking.', to: '/finance/costing-rates/material-costing', icon: LayoutGrid },
    { title: 'Rate Cards', description: 'Contract pricing and standard rate card maintenance.', to: '/finance/costing-rates/rate-cards', icon: FileText },
    { title: 'Preferential Rates', description: 'Client-specific pricing overrides.', to: '/finance/costing-rates/preferential-rates', icon: FileText },
  ], [])

  return (
    <section className="space-y-4">
      <PageHeader eyebrow="Finance" title="Costing & Rates" description="Work capture, pricing logic, and cost visibility for commercial operations." />
      <MetricGrid className="xl:grid-cols-3">
        <MetricCard label="Rate management" value="Centralized" meta="Standard and preferential billing controls live here" />
        <MetricCard label="Work capture" value="Billable" meta="Current work items and pricing data feed invoices" emphasis="accent" />
        <MetricCard label="Cost exposure" value="Tracked" meta="Material and expense visibility is organized here" />
      </MetricGrid>
      <SectionCard title="Open a costing area" description="The tabs in the finance shell route into the existing screens and placeholders.">
        <HubGrid links={links} />
      </SectionCard>
    </section>
  )
}

export function FinanceTaxReceivablesHubPage() {
  const links = useMemo<HubLink[]>(() => [
    { title: 'Tax / VAT', description: 'Configure compliant tax handling.', to: '/finance/tax-receivables/tax-vat', icon: ShieldCheck },
    { title: 'Customer Statements', description: 'Statement of account and client balance history.', to: '/finance/tax-receivables/statements', icon: FileText },
    { title: 'Aging', description: 'Outstanding balances grouped by age buckets.', to: '/finance/tax-receivables/aging', icon: Wallet },
    { title: 'Approval Matrix', description: 'Approval controls for rates and billable work.', to: '/finance/tax-receivables/approvals', icon: LayoutGrid },
  ], [])

  return (
    <section className="space-y-4">
      <PageHeader eyebrow="Finance" title="Tax & Receivables" description="Tax controls, client collections, aging, and billing approvals in one lane." />
      <MetricGrid className="xl:grid-cols-3">
        <MetricCard label="Receivables" value="Customer ledgers" meta="Statements and aging reports stay together" />
        <MetricCard label="Tax handling" value="VAT-ready" meta="Document-level tax controls are staged here" emphasis="accent" />
        <MetricCard label="Approvals" value="Controlled" meta="Rate and billable approvals remain visible" />
      </MetricGrid>
      <SectionCard title="Open a receivables area" description="Start from the finance shell or jump directly into the report you need.">
        <HubGrid links={links} />
      </SectionCard>
    </section>
  )
}

export function FinanceAnalyticsPage() {
  const location = useLocation()
  const view = new URLSearchParams(location.search).get('view')
  const initialTab =
    view === 'aging'
      ? 'aging'
      : view === 'statement'
        ? 'statement'
        : view === 'reports' || location.pathname.startsWith('/finance/reports')
          ? 'reports'
          : 'revenue'

  return (
    <section className="space-y-4">
      <PageHeader eyebrow="Finance" title="Analytics" description="Revenue visibility, aging, client statements, and scheduled reports." />
      <FinanceReportsPage initialTab={initialTab} />
    </section>
  )
}

export function FinanceSettingsHubPage() {
  const links = useMemo<HubLink[]>(() => [
    { title: 'Numbering Rules', description: 'Document sequence setup and preview.', to: '/settings/numbering-rules', icon: FileText },
    { title: 'Payment Terms', description: 'Re-use or extend invoice and quote terms.', to: '/settings/documents-branding', icon: Receipt },
    { title: 'Tax Settings', description: 'Document tax display and VAT controls.', to: '/settings/documents-branding', icon: ShieldCheck },
    { title: 'Document Templates', description: 'Controlled layouts for commercial documents.', to: '/settings/report-template', icon: LayoutGrid },
    { title: 'Finance Permissions', description: 'Permission model that gates finance access.', to: '/settings/users', icon: LayoutGrid },
    { title: 'Bank / Footer Details', description: 'Payment instructions and footer details.', to: '/settings/documents-branding', icon: Banknote },
  ], [])

  return (
    <section className="space-y-4">
      <PageHeader eyebrow="Finance" title="Settings" description="Document numbering, layout, templates, and payment details live here." />
      <SectionCard title="Finance settings shortcuts" description="Some items already exist as dedicated settings pages. Others are surfaced inside Documents & Branding.">
        <HubGrid links={links} />
      </SectionCard>
      <EmptyState
        title="Finance settings are organized across Settings"
        description="Use the links above for the existing settings pages and the Documents & Branding hub for visual configuration and document presentation."
      />
    </section>
  )
}

export function FinanceRevenueDashboardPage() {
  return <FinanceReportsPage initialTab="revenue" />
}

export function FinanceAgingAnalysisPage() {
  return <FinanceReportsPage initialTab="aging" />
}

export function FinanceClientStatementPage() {
  return <FinanceReportsPage initialTab="statement" />
}

export function FinanceReportsLandingPage() {
  return <FinanceReportsPage initialTab="reports" />
}
