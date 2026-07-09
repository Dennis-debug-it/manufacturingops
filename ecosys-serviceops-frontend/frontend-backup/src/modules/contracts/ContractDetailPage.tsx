import { ArrowLeft, Pencil, RefreshCw } from 'lucide-react'
import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { useAuth } from '../../auth/AuthContext'
import { AttachmentPanel } from '../../components/ui/AttachmentPanel'
import { DataTableWrapper, DetailRows, PageTabs, SectionPanel, StatusBadge } from '../../components/ui/Workspace'
import { ErrorState } from '../../components/ui/ErrorState'
import { LoadingState } from '../../components/ui/LoadingState'
import { Modal } from '../../components/ui/Modal'
import { PageHeader } from '../../components/ui/PageHeader'
import { useToast } from '../../components/ui/ToastProvider'
import { useAsyncData } from '../../hooks/useAsyncData'
import { attachmentService, ATTACHMENT_ENTITY_TYPES } from '../../services/attachmentService'
import { clientService } from '../../services/clientService'
import { contractService } from '../../services/contractService'
import { userService } from '../../services/userService'
import type {
  AttachmentRecord,
  ContractReminderRuleRecord,
  UpsertContractInput,
  UpsertContractRateCardInput,
  UpsertContractReminderRuleInput,
} from '../../types/api'
import { formatDateOnly, formatDateTime } from '../../utils/date'
import { ContractEditor } from './ContractEditor'
import {
  formatContractLabel,
  renderBillingModelBadge,
  renderContractStatusBadge,
  renderCoverageStatusBadge,
  renderRenewalRiskBadge,
} from './contractUi'

type DetailTab =
  | 'overview'
  | 'coverage'
  | 'sla'
  | 'rate-card'
  | 'pm-entitlements'
  | 'billing-rules'
  | 'lifecycle'
  | 'renewals'
  | 'notifications'
  | 'work-orders'
  | 'documents'
  | 'history'

const tabs: Array<{ id: DetailTab; label: string }> = [
  { id: 'overview', label: 'Overview' },
  { id: 'coverage', label: 'Coverage' },
  { id: 'sla', label: 'SLA' },
  { id: 'rate-card', label: 'Rate Card' },
  { id: 'pm-entitlements', label: 'PM Entitlements' },
  { id: 'billing-rules', label: 'Billing Rules' },
  { id: 'lifecycle', label: 'Lifecycle' },
  { id: 'renewals', label: 'Renewals' },
  { id: 'notifications', label: 'Notifications' },
  { id: 'work-orders', label: 'Work Orders' },
  { id: 'documents', label: 'Documents' },
  { id: 'history', label: 'History' },
]

export function ContractDetailPage() {
  const { id = '' } = useParams()
  const { session } = useAuth()
  const { pushToast } = useToast()
  const [activeTab, setActiveTab] = useState<DetailTab>('overview')
  const [editOpen, setEditOpen] = useState(false)
  const [busy, setBusy] = useState<string | null>(null)
  const [contractAttachments, setContractAttachments] = useState<AttachmentRecord[]>([])
  const [referenceNow] = useState(() => Date.now())
  const [reminderForm, setReminderForm] = useState<UpsertContractReminderRuleInput>({
    daysBeforeExpiry: 30,
    notifyAccountManager: true,
    notifyRenewalOwner: true,
    notifySupervisor: false,
    notifyFinance: false,
    notifyTenantAdmin: true,
    emailEnabled: true,
    inAppEnabled: false,
    escalationEnabled: false,
    escalationAfterDays: null,
    isActive: true,
  })
  const [rateCardForm, setRateCardForm] = useState<UpsertContractRateCardInput>({
    name: '',
    description: '',
    currency: 'KES',
    isDefault: true,
    effectiveFrom: null,
    effectiveTo: null,
    isActive: true,
  })

  const canViewPricing = Boolean(
    session?.permissions?.canViewFinance
    || session?.permissions?.canManageFinance
    || session?.permissions?.canViewFinanceRates
    || session?.permissions?.canManageFinanceRates
    || session?.permissions?.canViewBillableWork
    || session?.permissions?.canManageBillableWork
    || session?.role === 'tenantadmin'
    || session?.role === 'admin',
  )
  const canManageContracts = Boolean(session?.permissions?.canManageSettings || session?.role === 'tenantadmin' || session?.role === 'admin')

  const { data: contract, loading, error, reload } = useAsyncData(
    (signal) => contractService.get(id, signal),
    null,
    [id],
  )
  const { data: clients } = useAsyncData((signal) => clientService.list({ status: 'active', signal }), [], [])
  const { data: users } = useAsyncData((signal) => userService.list(signal), [], [])

  useEffect(() => {
    if (!contract) {
      setContractAttachments([])
      return
    }

    let cancelled = false
    void attachmentService.list(ATTACHMENT_ENTITY_TYPES.Contract, contract.id)
      .then((items) => {
        if (!cancelled) setContractAttachments(items)
      })
      .catch(() => {
        if (!cancelled) setContractAttachments([])
      })

    return () => {
      cancelled = true
    }
  }, [contract])

  const lifecycleActions = (() => {
    if (!contract || !canManageContracts) return []
    switch (contract.status) {
      case 'Draft':
        return [
          { label: 'Submit for approval', action: () => lifecycleCall('submit-approval') },
          { label: 'Cancel', action: () => lifecycleCall('cancel', promptReason('Cancellation reason')) },
        ]
      case 'PendingApproval':
        return [
          { label: 'Approve', action: () => lifecycleCall('approve') },
          { label: 'Cancel', action: () => lifecycleCall('cancel', promptReason('Cancellation reason')) },
        ]
      case 'Active':
      case 'ExpiringSoon':
        return [
          { label: 'Activate / recalc', action: () => lifecycleCall('recalculate-lifecycle') },
          { label: 'Start renewal', action: () => lifecycleCall('start-renewal', promptReason('Renewal note')) },
          { label: 'Suspend', action: () => lifecycleCall('suspend', promptReason('Suspension reason')) },
          { label: 'Terminate', action: () => lifecycleCall('terminate', promptReason('Termination reason')) },
        ]
      case 'RenewalInProgress':
        return [
          { label: 'Create renewal contract', action: () => renewContract() },
          { label: 'Cancel', action: () => lifecycleCall('cancel', promptReason('Cancellation reason')) },
        ]
      case 'Suspended':
        return [
          { label: 'Resume', action: () => lifecycleCall('resume') },
          { label: 'Terminate', action: () => lifecycleCall('terminate', promptReason('Termination reason')) },
        ]
      case 'Expired':
        return [
          { label: 'Create renewal contract', action: () => renewContract() },
          { label: 'Recalculate', action: () => lifecycleCall('recalculate-lifecycle') },
        ]
      default:
        return []
    }
  })()

  async function lifecycleCall(
    action: 'submit-approval' | 'approve' | 'recalculate-lifecycle' | 'start-renewal' | 'suspend' | 'resume' | 'terminate' | 'cancel',
    reason?: string | null,
  ) {
    if (!contract) return
    setBusy(action)
    try {
      switch (action) {
        case 'submit-approval':
          await contractService.submitApproval(contract.id, reason)
          break
        case 'approve':
          await contractService.approve(contract.id, reason)
          break
        case 'recalculate-lifecycle':
          await contractService.recalculateLifecycle(contract.id)
          break
        case 'start-renewal':
          await contractService.startRenewal(contract.id, reason)
          break
        case 'suspend':
          await contractService.suspend(contract.id, reason)
          break
        case 'resume':
          await contractService.resume(contract.id, reason)
          break
        case 'terminate':
          await contractService.terminate(contract.id, null, reason)
          break
        case 'cancel':
          await contractService.cancel(contract.id, reason)
          break
      }
      pushToast({ title: 'Lifecycle updated', description: 'The contract status was updated successfully.', tone: 'success' })
      await reload()
    } catch (nextError) {
      pushToast({ title: 'Action failed', description: nextError instanceof Error ? nextError.message : 'Unable to update the contract lifecycle.', tone: 'danger' })
    } finally {
      setBusy(null)
    }
  }

  async function renewContract() {
    if (!contract) return
    const startDate = window.prompt('New contract start date (YYYY-MM-DD)', contract.endDate.slice(0, 10))
    if (!startDate) return
    const endDate = window.prompt('New contract end date (YYYY-MM-DD)')
    if (!endDate) return
    setBusy('renew')
    try {
      const result = await contractService.renew(contract.id, {
        startDate,
        endDate,
        durationMonths: null,
        activateNewContract: false,
        submitForApproval: true,
        name: `${contract.name} Renewal`,
        contractValue: contract.contractValue ?? null,
        notes: 'Created from renewal workflow.',
      })
      pushToast({ title: 'Renewal contract created', description: `New contract ${result.newContractNumber} is ready for review.`, tone: 'success' })
      await reload()
    } catch (nextError) {
      pushToast({ title: 'Renewal failed', description: nextError instanceof Error ? nextError.message : 'Unable to create the renewal contract.', tone: 'danger' })
    } finally {
      setBusy(null)
    }
  }

  async function saveContract(input: UpsertContractInput) {
    if (!contract) return
    setBusy('save')
    try {
      await contractService.update(contract.id, input)
      pushToast({ title: 'Contract updated', description: 'Coverage and contract settings were saved.', tone: 'success' })
      setEditOpen(false)
      await reload()
    } catch (nextError) {
      pushToast({ title: 'Save failed', description: nextError instanceof Error ? nextError.message : 'Unable to update the contract.', tone: 'danger' })
    } finally {
      setBusy(null)
    }
  }

  async function addReminderRule() {
    if (!contract) return
    setBusy('reminder')
    try {
      await contractService.createReminderRule(contract.id, reminderForm)
      pushToast({ title: 'Reminder rule saved', description: 'The notification threshold was added.', tone: 'success' })
      await reload()
    } catch (nextError) {
      pushToast({ title: 'Save failed', description: nextError instanceof Error ? nextError.message : 'Unable to save the reminder rule.', tone: 'danger' })
    } finally {
      setBusy(null)
    }
  }

  async function addRateCard() {
    if (!contract) return
    setBusy('rate-card')
    try {
      await contractService.createRateCard(contract.id, rateCardForm)
      pushToast({ title: 'Rate card created', description: 'You can now add rules inside this contract rate card.', tone: 'success' })
      setRateCardForm({ ...rateCardForm, name: '', description: '' })
      await reload()
    } catch (nextError) {
      pushToast({ title: 'Save failed', description: nextError instanceof Error ? nextError.message : 'Unable to create the rate card.', tone: 'danger' })
    } finally {
      setBusy(null)
    }
  }

  if (loading) return <LoadingState label="Loading contract" />
  if (error || !contract) return <ErrorState title="Unable to load contract" description={error || 'The contract could not be found.'} />

  const daysToExpiry = Math.ceil((new Date(contract.endDate).getTime() - referenceNow) / 86400000)

  const overviewItems = [
    { label: 'Client', value: contract.clientName || '-' },
    { label: 'Type', value: formatContractLabel(contract.contractType) },
    { label: 'Billing', value: renderBillingModelBadge(contract.billingModel) },
    { label: 'Status', value: renderContractStatusBadge(contract.status) },
    { label: 'Start date', value: formatDateOnly(contract.startDate) },
    { label: 'End date', value: formatDateOnly(contract.endDate) },
    { label: 'Renewal risk', value: renderRenewalRiskBadge(contract.renewalRisk) },
    { label: 'Renewal owner', value: contract.renewalOwnerName || '-' },
    { label: 'Account manager', value: contract.accountManagerName || '-' },
    { label: 'Response SLA', value: contract.responseTimeHours ? `${contract.responseTimeHours} hrs` : '-' },
    { label: 'Resolution SLA', value: contract.resolutionTimeHours ? `${contract.resolutionTimeHours} hrs` : '-' },
    { label: 'Grace period', value: `${contract.gracePeriodDays} days` },
  ]

  return (
    <div className="space-y-3">
      <PageHeader
        eyebrow="Contracts"
        title={contract.contractNumber}
        description={contract.name}
        actions={(
          <div className="flex flex-wrap gap-2">
            <Link to="/contracts" className="button-secondary"><ArrowLeft className="h-4 w-4" />Back</Link>
            <button type="button" className="button-secondary" onClick={() => void reload()}><RefreshCw className="h-4 w-4" />Refresh</button>
            {canManageContracts ? <button type="button" className="button-primary" onClick={() => setEditOpen(true)}><Pencil className="h-4 w-4" />Edit contract</button> : null}
          </div>
        )}
        statusBadges={(
          <>
            {renderContractStatusBadge(contract.status)}
            {renderRenewalRiskBadge(contract.renewalRisk)}
            {renderBillingModelBadge(contract.billingModel)}
          </>
        )}
      />

      <PageTabs tabs={tabs} activeTab={activeTab} onChange={setActiveTab} />

      {activeTab === 'overview' ? (
        <SectionPanel title="Overview" description="Metadata, ownership, and dates.">
          <DetailRows items={overviewItems} columns={3} />
          {contract.description ? <p className="mt-4 text-sm text-secondary">{contract.description}</p> : null}
        </SectionPanel>
      ) : null}

      {activeTab === 'coverage' ? (
        <div className="space-y-3">
          <SectionPanel title="Covered sites" description="Sites linked to this contract.">
            <SimpleTable headers={['Site code', 'Site name']} rows={contract.sites.map((item) => [item.siteCode || '-', item.siteName || '-'])} />
          </SectionPanel>
          <SectionPanel title="Covered assets" description="Assets linked to this contract.">
            <SimpleTable headers={['Asset code', 'Asset name']} rows={contract.assets.map((item) => [item.assetCode || '-', item.assetName || '-'])} />
          </SectionPanel>
          <SectionPanel title="Asset categories & work order types" description="Fallback coverage controls.">
            <div className="grid gap-3 xl:grid-cols-2">
              <SimpleTable headers={['Asset category']} rows={contract.assetCategories.map((item) => [item.assetCategoryName || '-'])} />
              <SimpleTable headers={['Work order type']} rows={contract.workOrderTypes.map((item) => [formatContractLabel(item.workOrderTypeName)])} />
            </div>
            <div className="mt-4 grid gap-4 xl:grid-cols-2">
              <TextPanel title="Included services" value={contract.includedServices} />
              <TextPanel title="Excluded services" value={contract.excludedServices} />
            </div>
          </SectionPanel>
        </div>
      ) : null}

      {activeTab === 'sla' ? (
        <SectionPanel title="SLA rules" description="Response, resolution, and grace-period behavior.">
          <DetailRows
            items={[
              { label: 'Response time', value: contract.responseTimeHours ? `${contract.responseTimeHours} hours` : '-' },
              { label: 'Resolution time', value: contract.resolutionTimeHours ? `${contract.resolutionTimeHours} hours` : '-' },
              { label: 'Working hours policy', value: contract.workingHoursPolicy || '-' },
              { label: 'Pause on hold', value: contract.pauseSlaOnHold ? 'Yes' : 'No' },
              { label: 'Escalation email', value: contract.escalationEmail || '-' },
              { label: 'Priority level', value: contract.priorityLevel || '-' },
              { label: 'Allow SLA during grace period', value: contract.allowSlaDuringGracePeriod ? 'Yes' : 'No' },
            ]}
            columns={3}
          />
        </SectionPanel>
      ) : null}

      {activeTab === 'rate-card' ? (
        <div className="space-y-3">
          {canViewPricing ? (
            <SectionPanel title="Rate cards" description="Preferential and included pricing rules.">
              <DataTableWrapper>
                <table className="w-full min-w-[1080px]">
                  <thead className="table-head">
                    <tr>
                      {['Name', 'Currency', 'Default', 'Effective', 'Status', 'Rules'].map((header) => (
                        <th key={header} className="px-4 py-3 text-left text-[11px] font-semibold uppercase tracking-[0.16em] text-muted">{header}</th>
                      ))}
                    </tr>
                  </thead>
                  <tbody>
                    {contract.rateCards.map((item) => (
                      <tr key={item.id} className="table-row border-t border-app">
                        <td className="px-4 py-3">
                          <div>
                            <p className="font-semibold text-app">{item.name}</p>
                            <p className="mt-1 text-xs text-muted">{item.description || 'No description'}</p>
                          </div>
                        </td>
                        <td className="px-4 py-3 text-sm text-secondary">{item.currency}</td>
                        <td className="px-4 py-3">{item.isDefault ? <StatusBadge tone="success">Default</StatusBadge> : <StatusBadge tone="default">Optional</StatusBadge>}</td>
                        <td className="px-4 py-3 text-sm text-secondary">{formatDateOnly(item.effectiveFrom || undefined)} to {formatDateOnly(item.effectiveTo || undefined)}</td>
                        <td className="px-4 py-3">{item.isActive ? <StatusBadge tone="success">Active</StatusBadge> : <StatusBadge tone="default">Inactive</StatusBadge>}</td>
                        <td className="px-4 py-3 text-sm text-secondary">{item.rules.length}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </DataTableWrapper>
            </SectionPanel>
          ) : (
            <SectionPanel title="Rate cards" description="Pricing details are restricted for your role.">
              <p className="text-sm text-muted">You can still review coverage and lifecycle without seeing contract rates.</p>
            </SectionPanel>
          )}
          {canManageContracts && canViewPricing ? (
            <SectionPanel title="Add rate card" description="Create a new rate card before rate rules.">
              <div className="grid gap-2.5 md:grid-cols-2 xl:grid-cols-4">
                <label className="space-y-2"><span className="text-sm font-medium text-app">Name</span><input value={rateCardForm.name} onChange={(event) => setRateCardForm({ ...rateCardForm, name: event.target.value })} className="field-input" /></label>
                <label className="space-y-2"><span className="text-sm font-medium text-app">Currency</span><input value={rateCardForm.currency} onChange={(event) => setRateCardForm({ ...rateCardForm, currency: event.target.value })} className="field-input" /></label>
                <label className="space-y-2"><span className="text-sm font-medium text-app">Effective from</span><input type="date" value={rateCardForm.effectiveFrom || ''} onChange={(event) => setRateCardForm({ ...rateCardForm, effectiveFrom: event.target.value || null })} className="field-input" /></label>
                <label className="space-y-2"><span className="text-sm font-medium text-app">Effective to</span><input type="date" value={rateCardForm.effectiveTo || ''} onChange={(event) => setRateCardForm({ ...rateCardForm, effectiveTo: event.target.value || null })} className="field-input" /></label>
              </div>
              <div className="mt-3 grid gap-2.5 md:grid-cols-[minmax(0,1fr)_auto_auto] md:items-end">
                <label className="space-y-2"><span className="text-sm font-medium text-app">Description</span><input value={rateCardForm.description || ''} onChange={(event) => setRateCardForm({ ...rateCardForm, description: event.target.value })} className="field-input" /></label>
                <label className="flex items-center gap-2 text-sm text-app"><input type="checkbox" checked={rateCardForm.isDefault} onChange={(event) => setRateCardForm({ ...rateCardForm, isDefault: event.target.checked })} />Default</label>
                <button type="button" className="button-primary" onClick={() => void addRateCard()} disabled={busy === 'rate-card' || !rateCardForm.name.trim()}>{busy === 'rate-card' ? 'Saving...' : 'Create rate card'}</button>
              </div>
            </SectionPanel>
          ) : null}
        </div>
      ) : null}

      {activeTab === 'pm-entitlements' ? (
        <SectionPanel title="PM entitlements" description="Included preventive visits.">
          <SimpleTable
            headers={['Asset / category', 'Work type', 'Period', 'Included', 'Used', 'Remaining', 'Effective dates', 'Status']}
            rows={contract.pmEntitlements.map((item) => [
              item.assetName || item.assetCategory || '-',
              item.workOrderType,
              item.periodType,
              String(item.includedVisits),
              String(item.usedVisits),
              String(item.remainingVisits),
              `${formatDateOnly(item.effectiveFrom)} to ${formatDateOnly(item.effectiveTo)}`,
              item.isActive ? 'Active' : 'Inactive',
            ])}
          />
        </SectionPanel>
      ) : null}

      {activeTab === 'billing-rules' ? (
        <SectionPanel title="Billing rules" description="How work orders are priced here.">
          <DetailRows
            items={[
              { label: 'Billing model', value: renderBillingModelBadge(contract.billingModel) },
              { label: 'Currency', value: contract.currency },
              { label: 'Preferential rates during grace period', value: contract.allowPreferentialRatesDuringGracePeriod ? 'Yes' : 'No' },
              { label: 'Contract value', value: canViewPricing ? (contract.contractValue?.toLocaleString() || '-') : 'Restricted' },
              { label: 'Rate cards', value: canViewPricing ? String(contract.rateCards.length) : 'Restricted' },
              { label: 'Notes', value: contract.notes || '-' },
            ]}
            columns={3}
          />
        </SectionPanel>
      ) : null}

      {activeTab === 'lifecycle' ? (
        <div className="space-y-3">
          <SectionPanel title="Lifecycle summary" description="Only valid lifecycle actions are shown.">
            <DetailRows
              items={[
                { label: 'Current status', value: renderContractStatusBadge(contract.status) },
                { label: 'Start date', value: formatDateOnly(contract.startDate) },
                { label: 'End date', value: formatDateOnly(contract.endDate) },
                { label: 'Days to expiry', value: String(daysToExpiry) },
                { label: 'Renewal risk', value: renderRenewalRiskBadge(contract.renewalRisk) },
                { label: 'Renewal behavior', value: formatContractLabel(contract.renewalBehavior) },
                { label: 'Renewal owner', value: contract.renewalOwnerName || '-' },
                { label: 'Account manager', value: contract.accountManagerName || '-' },
                { label: 'Last reminder', value: formatDateTime(contract.lastReminderSentAt || undefined) },
                { label: 'Next reminder', value: formatDateTime(contract.nextReminderAt || undefined) },
                { label: 'Grace period', value: `${contract.gracePeriodDays} days (${formatContractLabel(contract.gracePeriodPolicy)})` },
              ]}
              columns={3}
            />
            <div className="mt-4 flex flex-wrap gap-2">
              {lifecycleActions.map((item) => (
                <button key={item.label} type="button" className="button-secondary" onClick={() => void item.action()} disabled={busy !== null}>
                  {busy ? 'Working...' : item.label}
                </button>
              ))}
              {lifecycleActions.length === 0 ? <p className="text-sm text-muted">No lifecycle action is available for your role or the current contract status.</p> : null}
            </div>
          </SectionPanel>
          <SectionPanel title="Lifecycle timeline" description="Recorded transitions and reminders.">
            <SimpleTable
              headers={['When', 'Event', 'Transition', 'Actor', 'Reason / note']}
              rows={contract.lifecycleEvents.map((item) => [
                formatDateTime(item.createdAt),
                formatContractLabel(item.eventType),
                `${item.oldStatus || '-'} to ${item.newStatus || '-'}`,
                item.createdByUserName || 'System',
                item.reason || item.note || '-',
              ])}
            />
          </SectionPanel>
        </div>
      ) : null}

      {activeTab === 'renewals' ? (
        <SectionPanel title="Renewals" description="Track renewal progress and links.">
          <DetailRows
            items={[
              { label: 'Renewed from', value: contract.renewedFromContractId || '-' },
              { label: 'Renewed to', value: contract.renewedToContractId || '-' },
              { label: 'Renewal started', value: formatDateTime(contract.renewalStartedAt || undefined) },
            ]}
            columns={3}
          />
          <div className="mt-4">
            <SimpleTable
              headers={['Status', 'Owner', 'Started', 'Completed', 'Notes']}
              rows={contract.renewals.map((item) => [
                formatContractLabel(item.renewalStatus),
                item.ownerUserId || '-',
                formatDateTime(item.renewalStartedAt || undefined),
                formatDateTime(item.renewalCompletedAt || undefined),
                item.notes || '-',
              ])}
            />
          </div>
        </SectionPanel>
      ) : null}

      {activeTab === 'notifications' ? (
        <div className="space-y-3">
          <SectionPanel title="Reminder rules" description="Expiry thresholds and delivery preferences.">
            <SimpleTable
              headers={['Days before expiry', 'Email', 'In-app', 'Escalation', 'Audience', 'Status']}
              rows={contract.reminderRules.map((item) => reminderRow(item))}
            />
          </SectionPanel>
          {canManageContracts ? (
            <SectionPanel title="Add reminder rule" description="Create threshold-specific reminders.">
              <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
                <label className="space-y-2"><span className="text-sm font-medium text-app">Days before expiry</span><input type="number" value={reminderForm.daysBeforeExpiry} onChange={(event) => setReminderForm({ ...reminderForm, daysBeforeExpiry: Number(event.target.value || 0) })} className="field-input" /></label>
                <label className="space-y-2"><span className="text-sm font-medium text-app">Escalation after days</span><input type="number" value={reminderForm.escalationAfterDays ?? ''} onChange={(event) => setReminderForm({ ...reminderForm, escalationAfterDays: event.target.value ? Number(event.target.value) : null })} className="field-input" /></label>
                <ToggleInline label="Email" checked={reminderForm.emailEnabled} onChange={(checked) => setReminderForm({ ...reminderForm, emailEnabled: checked })} />
                <ToggleInline label="In-app" checked={reminderForm.inAppEnabled} onChange={(checked) => setReminderForm({ ...reminderForm, inAppEnabled: checked })} />
                <ToggleInline label="Renewal owner" checked={reminderForm.notifyRenewalOwner} onChange={(checked) => setReminderForm({ ...reminderForm, notifyRenewalOwner: checked })} />
                <ToggleInline label="Account manager" checked={reminderForm.notifyAccountManager} onChange={(checked) => setReminderForm({ ...reminderForm, notifyAccountManager: checked })} />
                <ToggleInline label="Finance" checked={reminderForm.notifyFinance} onChange={(checked) => setReminderForm({ ...reminderForm, notifyFinance: checked })} />
                <ToggleInline label="Supervisor" checked={reminderForm.notifySupervisor} onChange={(checked) => setReminderForm({ ...reminderForm, notifySupervisor: checked })} />
              </div>
              <div className="mt-4 flex justify-end">
                <button type="button" className="button-primary" onClick={() => void addReminderRule()} disabled={busy === 'reminder'}>{busy === 'reminder' ? 'Saving...' : 'Add reminder rule'}</button>
              </div>
            </SectionPanel>
          ) : null}
        </div>
      ) : null}

      {activeTab === 'work-orders' ? (
        <SectionPanel title="Related work orders" description="Work orders that used this contract snapshot.">
          <DataTableWrapper>
            <table className="w-full min-w-[860px]">
              <thead className="table-head">
                <tr>
                  {['Work order', 'Status', 'Coverage', 'Billing', 'Estimated charge', 'Created'].map((header) => (
                    <th key={header} className="px-4 py-3 text-left text-[11px] font-semibold uppercase tracking-[0.16em] text-muted">{header}</th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {contract.workOrders.map((item) => (
                  <tr key={item.id} className="table-row border-t border-app">
                    <td className="px-4 py-3">
                      <Link to={`/work-orders/${item.id}`} className="font-semibold text-accent-strong hover:underline">{item.workOrderNumber}</Link>
                      <p className="mt-1 text-xs text-muted">{item.title}</p>
                    </td>
                    <td className="px-4 py-3">{renderContractStatusBadge(item.status)}</td>
                    <td className="px-4 py-3">{renderCoverageStatusBadge(item.coverageStatus)}</td>
                    <td className="px-4 py-3">{renderBillingModelBadge(item.billingModel)}</td>
                    <td className="px-4 py-3 text-sm text-secondary">{canViewPricing ? (item.estimatedCharge?.toLocaleString() || '-') : 'Restricted'}</td>
                    <td className="px-4 py-3 text-sm text-secondary">{formatDateTime(item.createdAt)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </DataTableWrapper>
        </SectionPanel>
      ) : null}

      {activeTab === 'documents' ? (
        <SectionPanel title="Documents" description="Upload the signed contract PDF, amendments, and supporting files.">
          <AttachmentPanel
            entityType={ATTACHMENT_ENTITY_TYPES.Contract}
            entityId={contract?.id ?? id}
            attachments={contractAttachments}
            onUploaded={(attachment) => setContractAttachments((current) => [...current, attachment])}
            onDeleted={(attachmentId) => setContractAttachments((current) => current.filter((item) => item.id !== attachmentId))}
          />
        </SectionPanel>
      ) : null}

      {activeTab === 'history' ? (
        <SectionPanel title="History" description="Lifecycle and reminder history.">
          <SimpleTable
            headers={['When', 'Event', 'Status', 'Actor', 'Note']}
            rows={contract.lifecycleEvents.map((item) => [
              formatDateTime(item.createdAt),
              formatContractLabel(item.eventType),
              item.newStatus || item.oldStatus || '-',
              item.createdByUserName || 'System',
              item.note || item.reason || '-',
            ])}
          />
        </SectionPanel>
      ) : null}

      <Modal open={editOpen} title={`Edit ${contract.contractNumber}`} description="Update the contract baseline, coverage, and PM entitlements." onClose={() => setEditOpen(false)} maxWidth="max-w-6xl">
        <ContractEditor clients={clients} users={users} initialValue={contract} onSubmit={saveContract} submitLabel="Save changes" busy={busy === 'save'} />
      </Modal>
    </div>
  )
}

function promptReason(label: string) {
  const value = window.prompt(label)
  return value?.trim() || null
}

function reminderRow(item: ContractReminderRuleRecord) {
  const audience = [
    item.notifyAccountManager ? 'Account manager' : null,
    item.notifyRenewalOwner ? 'Renewal owner' : null,
    item.notifySupervisor ? 'Supervisor' : null,
    item.notifyFinance ? 'Finance' : null,
    item.notifyTenantAdmin ? 'Tenant admin' : null,
  ].filter(Boolean).join(', ')

  return [
    String(item.daysBeforeExpiry),
    item.emailEnabled ? 'Enabled' : 'Off',
    item.inAppEnabled ? 'Enabled' : 'Off',
    item.escalationEnabled ? `After ${item.escalationAfterDays ?? 0} days` : 'Off',
    audience || '-',
    item.isActive ? 'Active' : 'Inactive',
  ]
}

function ToggleInline({ label, checked, onChange }: { label: string; checked: boolean; onChange: (checked: boolean) => void }) {
  return (
    <label className="flex items-center gap-2 border-t border-[var(--app-divider)] py-3 text-sm text-app first:border-t-0">
      <input type="checkbox" checked={checked} onChange={(event) => onChange(event.target.checked)} />
      <span>{label}</span>
    </label>
  )
}

function TextPanel({ title, value }: { title: string; value?: string | null }) {
  return (
    <div className="border-t border-[var(--app-divider)] pt-3 first:border-t-0 first:pt-0">
      <p className="text-xs font-semibold uppercase tracking-[0.16em] text-muted">{title}</p>
      <p className="mt-2 text-sm text-secondary">{value || 'None recorded.'}</p>
    </div>
  )
}

function SimpleTable({ headers, rows }: { headers: string[]; rows: string[][] }) {
  return (
    <DataTableWrapper>
      <table className="w-full min-w-[680px]">
        <thead className="table-head">
          <tr>
            {headers.map((header) => <th key={header} className="px-4 py-3 text-left text-[11px] font-semibold uppercase tracking-[0.16em] text-muted">{header}</th>)}
          </tr>
        </thead>
        <tbody>
          {rows.map((row, rowIndex) => (
            <tr key={`${row.join('-')}-${rowIndex}`} className="table-row border-t border-app">
              {row.map((cell, cellIndex) => <td key={`${cellIndex}-${cell}`} className="px-4 py-3 text-sm text-secondary">{cell}</td>)}
            </tr>
          ))}
          {rows.length === 0 ? (
            <tr className="border-t border-app">
              <td className="px-4 py-6 text-sm text-muted" colSpan={headers.length}>No records available.</td>
            </tr>
          ) : null}
        </tbody>
      </table>
    </DataTableWrapper>
  )
}
