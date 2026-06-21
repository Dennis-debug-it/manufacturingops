import { useMemo, useState, type ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { useAuth } from '../../auth/AuthContext'
import { Badge } from '../../components/ui/Badge'
import { DataTable } from '../../components/ui/DataTable'
import { EmptyState } from '../../components/ui/EmptyState'
import { ErrorState } from '../../components/ui/ErrorState'
import { LoadingState } from '../../components/ui/LoadingState'
import { Modal } from '../../components/ui/Modal'
import { RowActionSelector } from '../../components/ui/RowActionSelector'
import { useToast } from '../../components/ui/ToastProvider'
import { MetricCard, MetricGrid, SectionCard, StickyActionFooter } from '../../components/ui/Workspace'
import { useAsyncData } from '../../hooks/useAsyncData'
import { clientService } from '../../services/clientService'
import { settingsService } from '../../services/settingsService'
import { tenantFinanceService } from '../../services/tenantFinanceService'
import { userService } from '../../services/userService'
import type {
  ClientRecord,
  FinanceApprovalRuleRecord,
  PreferentialRateRecord,
  TenantBillableWorkRecord,
  TenantFinanceOverview,
  UpsertFinanceApprovalRuleInput,
  UpsertPreferentialRateInput,
  UserRecord,
} from '../../types/api'
import { formatDateOnly } from '../../utils/date'
import { PreferentialRateBulkImportModal } from './PreferentialRateBulkImportModal'

export function useTenantFinanceAccess() {
  const { permissions } = useAuth()
  return Boolean(
    permissions?.canViewFinance
    || permissions?.canManageFinance
    || permissions?.canViewFinanceRates
    || permissions?.canManageFinanceRates
    || permissions?.canViewFinanceApprovals
    || permissions?.canManageFinanceApprovals
    || permissions?.canViewBillableWork
    || permissions?.canManageBillableWork,
  )
}

const emptyRateForm: UpsertPreferentialRateInput = {
  clientId: '',
  rateName: '',
  rateType: 'Preferential',
  itemType: 'material',
  unit: 'unit',
  defaultRate: 0,
  preferentialRate: 0,
  currency: 'KES',
  effectiveFrom: new Date().toISOString().slice(0, 10),
  effectiveTo: '',
  status: 'Active',
  requiresApproval: false,
}

const emptyApprovalRuleForm: UpsertFinanceApprovalRuleInput = {
  approvalArea: 'preferential_rate',
  thresholdAmount: 0,
  currency: 'KES',
  requiredRole: 'Tenant Admin',
  requiredApproverUserId: null,
  requiredAssignmentGroupId: null,
  sequence: 1,
  isActive: true,
}

function normalizeDate(value?: string | null) {
  if (!value?.trim()) return null
  return new Date(`${value}T00:00:00.000Z`).toISOString()
}

export function TenantFinanceOverviewSection() {
  const { data, loading, error } = useAsyncData<TenantFinanceOverview>((signal) => tenantFinanceService.getSummary(signal), {
    preferentialRatesCount: 0,
    activePreferentialRatesCount: 0,
    approvalRulesCount: 0,
    pendingRateApprovalsCount: 0,
    pendingMaterialIssueApprovalsCount: 0,
    billableWorkItemsCount: 0,
    estimatedBillableAmount: 0,
    currency: 'KES',
  }, [])

  if (loading) return <LoadingState label="Loading tenant finance overview" />
  if (error) return <ErrorState title="Unable to load finance overview" description={error} />

  return (
    <MetricGrid className="xl:grid-cols-4">
      <MetricCard label="Preferential rates" value={data.preferentialRatesCount} meta={`${data.activePreferentialRatesCount} active`} emphasis="accent" />
      <MetricCard label="Approval rules" value={data.approvalRulesCount} meta={`${data.pendingRateApprovalsCount} pending rate approvals`} />
      <MetricCard label="Material approvals" value={data.pendingMaterialIssueApprovalsCount} meta="Pending material issue approval queue" emphasis="warning" />
      <MetricCard label="Billable work" value={`${data.currency} ${data.estimatedBillableAmount.toLocaleString()}`} meta={`${data.billableWorkItemsCount} items in current billable register`} />
    </MetricGrid>
  )
}

export function TenantPreferentialRatesSection() {
  const { permissions } = useAuth()
  const { pushToast } = useToast()
  const canManageRates = Boolean(permissions?.canManageFinance || permissions?.canManageFinanceRates)
  const canApproveRates = Boolean(permissions?.canManageFinanceApprovals || permissions?.canManageFinance)
  const [editorOpen, setEditorOpen] = useState(false)
  const [importOpen, setImportOpen] = useState(false)
  const [editing, setEditing] = useState<PreferentialRateRecord | null>(null)
  const [form, setForm] = useState<UpsertPreferentialRateInput>(emptyRateForm)

  const { data, loading, error, reload } = useAsyncData(async (signal) => {
    const [rates, clients] = await Promise.all([
      tenantFinanceService.listPreferentialRates(signal),
      clientService.list({ signal, status: 'all' }),
    ])
    return { rates, clients }
  }, { rates: [] as PreferentialRateRecord[], clients: [] as ClientRecord[] }, [])

  function openEditor(rate?: PreferentialRateRecord) {
    setEditing(rate ?? null)
    setForm(rate ? {
      clientId: rate.clientId,
      rateName: rate.rateName,
      rateType: rate.rateType,
      itemType: rate.itemType,
      unit: rate.unit,
      defaultRate: rate.defaultRate,
      preferentialRate: rate.preferentialRate,
      currency: rate.currency,
      effectiveFrom: rate.effectiveFrom.slice(0, 10),
      effectiveTo: rate.effectiveTo?.slice(0, 10) ?? '',
      status: rate.status,
      requiresApproval: rate.requiresApproval,
    } : emptyRateForm)
    setEditorOpen(true)
  }

  async function saveRate() {
    try {
      const payload = {
        ...form,
        effectiveFrom: normalizeDate(form.effectiveFrom) ?? new Date().toISOString(),
        effectiveTo: normalizeDate(form.effectiveTo),
      }
      if (editing) {
        await tenantFinanceService.updatePreferentialRate(editing.id, payload)
        pushToast({ title: 'Rate updated', description: 'The client-specific rate changes were saved.', tone: 'success' })
      } else {
        await tenantFinanceService.createPreferentialRate(payload)
        pushToast({ title: 'Rate created', description: 'The preferential rate is now available for billing logic.', tone: 'success' })
      }
      setEditorOpen(false)
      setEditing(null)
      await reload()
    } catch (nextError) {
      pushToast({ title: 'Save failed', description: nextError instanceof Error ? nextError.message : 'Unable to save the preferential rate.', tone: 'danger' })
    }
  }

  async function approveRate(rate: PreferentialRateRecord) {
    try {
      await tenantFinanceService.approvePreferentialRate(rate.id)
      pushToast({ title: 'Rate approved', description: 'The preferential rate has been activated.', tone: 'success' })
      await reload()
    } catch (nextError) {
      pushToast({ title: 'Approval failed', description: nextError instanceof Error ? nextError.message : 'Unable to approve this rate.', tone: 'danger' })
    }
  }

  return (
    <>
      <SectionCard title="Client preferential rates" description="Client-specific billing overrides used when rate rules apply to labour, materials, or service items.">
        <div className="mb-4 flex flex-wrap justify-end gap-2">
          {canManageRates ? <button type="button" className="button-secondary" onClick={() => setImportOpen(true)}>Import CSV</button> : null}
          {canManageRates ? <button type="button" className="button-primary" onClick={() => openEditor()}>Add preferential rate</button> : null}
        </div>
        {loading ? <LoadingState label="Loading preferential rates" /> : null}
        {!loading && error ? <ErrorState title="Unable to load preferential rates" description={error} /> : null}
        {!loading && !error ? (
          data.rates.length === 0 ? <EmptyState title="No preferential rates yet" description="Create the first client-specific rate to override default billing values." /> : (
            <DataTable
              rows={data.rates}
              rowKey={(row) => row.id}
              minTableWidth="min-w-[1180px] w-full"
              columns={[
                { key: 'client', header: 'Client', cell: (row) => row.clientName || 'Unknown client' },
                { key: 'name', header: 'Rate', cell: (row) => <div><p className="font-semibold text-app">{row.rateName}</p><p className="mt-1 text-xs text-muted">{row.rateType} | {row.itemType}</p></div> },
                { key: 'unit', header: 'Unit', cell: (row) => row.unit },
                { key: 'defaultRate', header: 'Default Rate', cell: (row) => `${row.currency} ${row.defaultRate.toLocaleString()}` },
                { key: 'preferentialRate', header: 'Preferential Rate', cell: (row) => `${row.currency} ${row.preferentialRate.toLocaleString()}` },
                { key: 'effective', header: 'Effective Window', cell: (row) => `${formatDateOnly(row.effectiveFrom)} - ${formatDateOnly(row.effectiveTo || undefined)}` },
                { key: 'status', header: 'Status', cell: (row) => <Badge tone={row.status.toLowerCase() === 'active' ? 'success' : row.status.toLowerCase() === 'pendingapproval' ? 'warning' : 'neutral'}>{row.status}</Badge> },
                {
                  key: 'actions',
                  header: 'Actions',
                  className: 'min-w-[260px]',
                  cell: (row) => (
                    <RowActionSelector
                      options={[
                        { value: 'edit', label: 'Edit', disabled: !canManageRates },
                        { value: 'approve', label: 'Approve', disabled: !canApproveRates || row.status.toLowerCase() === 'active' },
                      ]}
                      onApply={async (action) => {
                        if (action === 'edit') openEditor(row)
                        if (action === 'approve') await approveRate(row)
                      }}
                      buttonLabel="Apply"
                    />
                  ),
                },
              ]}
            />
          )
        ) : null}
      </SectionCard>

      <Modal open={editorOpen} title={editing ? 'Edit preferential rate' : 'Add preferential rate'} description="Capture the client, rate logic, validity window, and approval posture for this preferential rate." onClose={() => setEditorOpen(false)} maxWidth="max-w-4xl">
        <div className="grid gap-4 md:grid-cols-2">
          <FinanceField label="Client">
            <select value={form.clientId} onChange={(event) => setForm((current) => ({ ...current, clientId: event.target.value }))} className="field-input">
              <option value="">Select client</option>
              {data.clients.map((client) => <option key={client.id} value={client.id}>{client.clientName}</option>)}
            </select>
          </FinanceField>
          <FinanceField label="Rate name"><input value={form.rateName} onChange={(event) => setForm((current) => ({ ...current, rateName: event.target.value }))} className="field-input" /></FinanceField>
          <FinanceField label="Rate type"><input value={form.rateType} onChange={(event) => setForm((current) => ({ ...current, rateType: event.target.value }))} className="field-input" /></FinanceField>
          <FinanceField label="Item type">
            <select value={form.itemType} onChange={(event) => setForm((current) => ({ ...current, itemType: event.target.value as UpsertPreferentialRateInput['itemType'] }))} className="field-input">
              {['labour', 'material', 'callout', 'transport', 'service', 'other'].map((item) => <option key={item} value={item}>{item}</option>)}
            </select>
          </FinanceField>
          <FinanceField label="Unit"><input value={form.unit} onChange={(event) => setForm((current) => ({ ...current, unit: event.target.value }))} className="field-input" /></FinanceField>
          <FinanceField label="Currency"><input value={form.currency} onChange={(event) => setForm((current) => ({ ...current, currency: event.target.value.toUpperCase() }))} className="field-input" /></FinanceField>
          <FinanceField label="Default rate"><input type="number" value={form.defaultRate} onChange={(event) => setForm((current) => ({ ...current, defaultRate: Number(event.target.value) || 0 }))} className="field-input" /></FinanceField>
          <FinanceField label="Preferential rate"><input type="number" value={form.preferentialRate} onChange={(event) => setForm((current) => ({ ...current, preferentialRate: Number(event.target.value) || 0 }))} className="field-input" /></FinanceField>
          <FinanceField label="Effective from"><input type="date" value={form.effectiveFrom} onChange={(event) => setForm((current) => ({ ...current, effectiveFrom: event.target.value }))} className="field-input" /></FinanceField>
          <FinanceField label="Effective to"><input type="date" value={form.effectiveTo || ''} onChange={(event) => setForm((current) => ({ ...current, effectiveTo: event.target.value }))} className="field-input" /></FinanceField>
          <FinanceField label="Status">
            <select value={form.status} onChange={(event) => setForm((current) => ({ ...current, status: event.target.value }))} className="field-input">
              <option value="Active">Active</option>
              <option value="Inactive">Inactive</option>
              <option value="PendingApproval">PendingApproval</option>
            </select>
          </FinanceField>
          <label className="panel-subtle flex items-center justify-between rounded-2xl px-4 py-3">
            <span className="text-sm text-app">Requires approval</span>
            <input type="checkbox" checked={form.requiresApproval} onChange={(event) => setForm((current) => ({ ...current, requiresApproval: event.target.checked }))} />
          </label>
        </div>
        <StickyActionFooter className="mt-6">
          <button type="button" className="button-secondary" onClick={() => setEditorOpen(false)}>Cancel</button>
          <button type="button" className="button-primary" onClick={() => void saveRate()}>Save rate</button>
        </StickyActionFooter>
      </Modal>

      <PreferentialRateBulkImportModal
        open={importOpen}
        onClose={() => setImportOpen(false)}
        onSuccess={async () => {
          await reload()
        }}
        clients={data.clients}
      />
    </>
  )
}

export function TenantApprovalMatrixSection() {
  const { permissions } = useAuth()
  const { pushToast } = useToast()
  const canManageApprovals = Boolean(permissions?.canManageFinance || permissions?.canManageFinanceApprovals)
  const [editorOpen, setEditorOpen] = useState(false)
  const [editing, setEditing] = useState<FinanceApprovalRuleRecord | null>(null)
  const [form, setForm] = useState<UpsertFinanceApprovalRuleInput>(emptyApprovalRuleForm)

  const { data, loading, error, reload } = useAsyncData(async (signal) => {
    const [rules, users, groups] = await Promise.all([
      tenantFinanceService.listApprovalRules(signal),
      userService.list(signal),
      settingsService.listAssignmentGroups(signal),
    ])
    return { rules, users, groups }
  }, { rules: [] as FinanceApprovalRuleRecord[], users: [] as UserRecord[], groups: [] as Array<{ id: string; name: string }> }, [])

  function openEditor(rule?: FinanceApprovalRuleRecord) {
    setEditing(rule ?? null)
    setForm(rule ? {
      approvalArea: rule.approvalArea,
      thresholdAmount: rule.thresholdAmount,
      currency: rule.currency,
      requiredRole: rule.requiredRole,
      requiredApproverUserId: rule.requiredApproverUserId ?? null,
      requiredAssignmentGroupId: rule.requiredAssignmentGroupId ?? null,
      sequence: rule.sequence,
      isActive: rule.isActive,
    } : emptyApprovalRuleForm)
    setEditorOpen(true)
  }

  async function saveRule() {
    try {
      if (editing) {
        await tenantFinanceService.updateApprovalRule(editing.id, form)
        pushToast({ title: 'Approval rule updated', description: 'The approval rule foundation has been saved.', tone: 'success' })
      } else {
        await tenantFinanceService.createApprovalRule(form)
        pushToast({ title: 'Approval rule created', description: 'The approval rule is now available for finance workflows.', tone: 'success' })
      }
      setEditorOpen(false)
      setEditing(null)
      await reload()
    } catch (nextError) {
      pushToast({ title: 'Save failed', description: nextError instanceof Error ? nextError.message : 'Unable to save the approval rule.', tone: 'danger' })
    }
  }

  return (
    <>
      <SectionCard title="Approval matrix" description="Threshold-based approval rules used for preferential rates, billable work, and material issue escalations.">
        <div className="mb-4 flex flex-wrap justify-end gap-2">
          {canManageApprovals ? <button type="button" className="button-primary" onClick={() => openEditor()}>Add approval rule</button> : null}
        </div>
        {loading ? <LoadingState label="Loading approval rules" /> : null}
        {!loading && error ? <ErrorState title="Unable to load approval rules" description={error} /> : null}
        {!loading && !error ? (
          data.rules.length === 0 ? <EmptyState title="No approval rules yet" description="Add the first rule to enable approval thresholds without blocking users who still need to work." /> : (
            <DataTable
              rows={data.rules}
              rowKey={(row) => row.id}
              minTableWidth="min-w-[1080px] w-full"
              columns={[
                { key: 'area', header: 'Area', cell: (row) => row.approvalArea },
                { key: 'threshold', header: 'Threshold', cell: (row) => `${row.currency} ${row.thresholdAmount.toLocaleString()}` },
                { key: 'role', header: 'Required Role', cell: (row) => row.requiredRole },
                { key: 'approver', header: 'Approver', cell: (row) => row.requiredApproverUserName || row.requiredAssignmentGroupName || 'Fallback to role' },
                { key: 'sequence', header: 'Sequence', cell: (row) => String(row.sequence) },
                { key: 'status', header: 'Status', cell: (row) => <Badge tone={row.isActive ? 'success' : 'neutral'}>{row.isActive ? 'Active' : 'Inactive'}</Badge> },
                {
                  key: 'actions',
                  header: 'Actions',
                  className: 'min-w-[220px]',
                  cell: (row) => (
                    <RowActionSelector
                      options={[{ value: 'edit', label: 'Edit', disabled: !canManageApprovals }]}
                      onApply={(action) => {
                        if (action === 'edit') openEditor(row)
                      }}
                      buttonLabel="Apply"
                    />
                  ),
                },
              ]}
            />
          )
        ) : null}
      </SectionCard>

      <Modal open={editorOpen} title={editing ? 'Edit approval rule' : 'Add approval rule'} description="Define the approval area, threshold, approver, and sequencing for tenant finance workflows." onClose={() => setEditorOpen(false)} maxWidth="max-w-4xl">
        <div className="grid gap-4 md:grid-cols-2">
          <FinanceField label="Approval area">
            <select value={form.approvalArea} onChange={(event) => setForm((current) => ({ ...current, approvalArea: event.target.value as UpsertFinanceApprovalRuleInput['approvalArea'] }))} className="field-input">
              {['preferential_rate', 'invoice', 'quotation', 'expense', 'material_issue', 'billable_work'].map((item) => <option key={item} value={item}>{item}</option>)}
            </select>
          </FinanceField>
          <FinanceField label="Threshold amount"><input type="number" value={form.thresholdAmount} onChange={(event) => setForm((current) => ({ ...current, thresholdAmount: Number(event.target.value) || 0 }))} className="field-input" /></FinanceField>
          <FinanceField label="Currency"><input value={form.currency} onChange={(event) => setForm((current) => ({ ...current, currency: event.target.value.toUpperCase() }))} className="field-input" /></FinanceField>
          <FinanceField label="Required role"><input value={form.requiredRole} onChange={(event) => setForm((current) => ({ ...current, requiredRole: event.target.value }))} className="field-input" /></FinanceField>
          <FinanceField label="Specific approver">
            <select value={form.requiredApproverUserId || ''} onChange={(event) => setForm((current) => ({ ...current, requiredApproverUserId: event.target.value || null }))} className="field-input">
              <option value="">Use role / no specific user</option>
              {data.users.map((user) => <option key={user.id} value={user.id}>{user.fullName}</option>)}
            </select>
          </FinanceField>
          <FinanceField label="Assignment group">
            <select value={form.requiredAssignmentGroupId || ''} onChange={(event) => setForm((current) => ({ ...current, requiredAssignmentGroupId: event.target.value || null }))} className="field-input">
              <option value="">No specific group</option>
              {data.groups.map((group) => <option key={group.id} value={group.id}>{group.name}</option>)}
            </select>
          </FinanceField>
          <FinanceField label="Sequence"><input type="number" min={1} value={form.sequence} onChange={(event) => setForm((current) => ({ ...current, sequence: Number(event.target.value) || 1 }))} className="field-input" /></FinanceField>
          <label className="panel-subtle flex items-center justify-between rounded-2xl px-4 py-3">
            <span className="text-sm text-app">Rule active</span>
            <input type="checkbox" checked={form.isActive} onChange={(event) => setForm((current) => ({ ...current, isActive: event.target.checked }))} />
          </label>
        </div>
        <StickyActionFooter className="mt-6">
          <button type="button" className="button-secondary" onClick={() => setEditorOpen(false)}>Cancel</button>
          <button type="button" className="button-primary" onClick={() => void saveRule()}>Save rule</button>
        </StickyActionFooter>
      </Modal>
    </>
  )
}

export function TenantBillableWorkSection() {
  const { data, loading, error } = useAsyncData<TenantBillableWorkRecord[]>((signal) => tenantFinanceService.listBillableWork(signal), [], [])
  const total = useMemo(() => data.reduce((sum, item) => sum + item.totalAmount, 0), [data])
  const currency = data[0]?.currency || 'KES'

  return (
    <div className="space-y-4">
      <MetricGrid className="xl:grid-cols-3">
        <MetricCard label="Billable items" value={data.length} meta="Material usage currently flowing into tenant finance." emphasis="accent" />
        <MetricCard label="Estimated billing" value={`${currency} ${total.toLocaleString()}`} meta="Calculated with preferential client rates where active." />
        <MetricCard label="Preferential matches" value={data.filter((item) => item.preferentialRateApplied).length} meta="Items currently using client-specific rates." />
      </MetricGrid>
      <SectionCard title="Billable work register" description="Work-order-linked costs with any active preferential client rates already applied.">
        {loading ? <LoadingState label="Loading billable work" /> : null}
        {!loading && error ? <ErrorState title="Unable to load billable work" description={error} /> : null}
        {!loading && !error ? (
          data.length === 0 ? <EmptyState title="No billable work yet" description="Issued material usage and other billable items will appear here once operational activity is recorded." /> : (
            <DataTable
              rows={data}
              rowKey={(row) => `${row.workOrderId}-${row.description}-${row.itemType}`}
              minTableWidth="min-w-[1080px] w-full"
              columns={[
                { key: 'workOrder', header: 'Work Order', cell: (row) => row.workOrderNumber ? <Link to={`/work-orders/${row.workOrderId}`} className="font-semibold text-accent-strong hover:underline">{row.workOrderNumber}</Link> : 'Unlinked' },
                { key: 'client', header: 'Client', cell: (row) => row.clientName || 'Not linked' },
                { key: 'type', header: 'Item Type', cell: (row) => row.itemType },
                { key: 'description', header: 'Description', cell: (row) => row.description },
                { key: 'quantity', header: 'Quantity', cell: (row) => `${row.quantity} ${row.unit}` },
                { key: 'rate', header: 'Applied Rate', cell: (row) => `${row.currency} ${row.appliedRate.toLocaleString()}` },
                { key: 'total', header: 'Total', cell: (row) => `${row.currency} ${row.totalAmount.toLocaleString()}` },
                { key: 'source', header: 'Source', cell: (row) => <Badge tone={row.preferentialRateApplied ? 'success' : 'neutral'}>{row.preferentialRateApplied ? 'Preferential rate' : row.source}</Badge> },
              ]}
            />
          )
        ) : null}
      </SectionCard>
    </div>
  )
}

function FinanceField({ label, children }: { label: string; children: ReactNode }) {
  return (
    <label className="block space-y-2">
      <span className="text-sm font-medium text-app">{label}</span>
      {children}
    </label>
  )
}
