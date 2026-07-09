import { ChevronDown } from 'lucide-react'
import { useEffect, useMemo, useState, type Dispatch, type ReactNode, type SetStateAction } from 'react'
import { assetCategoryService, type AssetCategoryRecord } from '../../services/assetCategoryService'
import { assetService } from '../../services/assetService'
import { siteService } from '../../services/siteService'
import type {
  AssetRecord,
  ClientRecord,
  ContractDetailRecord,
  ContractPmEntitlementInput,
  ContractSlaRowInput,
  UpsertContractInput,
  UserRecord,
} from '../../types/api'
import type { SiteRecord } from '../../types/api'
import { contractBillingOptions, contractGracePeriodPolicyOptions, contractRenewalBehaviorOptions, contractStatusOptions, contractTypeOptions, pmPeriodOptions, workOrderTypeOptions } from './contractUi'

type Props = {
  clients: ClientRecord[]
  users: UserRecord[]
  initialValue?: ContractDetailRecord | null
  onSubmit: (input: UpsertContractInput) => Promise<void>
  submitLabel: string
  busy: boolean
}

type ListItem = { id: string; label: string }

function todayValue() {
  return new Date().toISOString().slice(0, 10)
}

function addMonths(base: Date, months: number) {
  const next = new Date(base)
  next.setMonth(next.getMonth() + months)
  return next.toISOString().slice(0, 10)
}

function defaultSlaRow(): ContractSlaRowInput {
  return {
    priority: 'Standard',
    responseTimeHours: 4,
    resolutionTimeHours: 24,
  }
}

function normalizeSlaRows(contract?: ContractDetailRecord | null): ContractSlaRowInput[] {
  const rows = contract?.slaMatrix?.length ? contract.slaMatrix : []
  if (rows.length > 0) {
    return rows.map((row) => ({
      priority: row.priority?.trim() || 'Standard',
      responseTimeHours: row.responseTimeHours ?? null,
      resolutionTimeHours: row.resolutionTimeHours ?? null,
    }))
  }

  return [{
    priority: contract?.priorityLevel?.trim() || 'Standard',
    responseTimeHours: contract?.responseTimeHours ?? 4,
    resolutionTimeHours: contract?.resolutionTimeHours ?? 24,
  }]
}

function buildEmptyContract(): UpsertContractInput {
  const startDate = todayValue()
  return {
    contractNumber: '',
    clientId: '',
    name: '',
    contractType: 'ComprehensiveMaintenance',
    status: 'Draft',
    startDate,
    endDate: addMonths(new Date(startDate), 12),
    durationMonths: 12,
    billingModel: 'PreferentialRates',
    currency: 'KES',
    contractValue: null,
    renewalNoticeDays: 30,
    renewalBehavior: 'ManualRenewalRequired',
    description: '',
    includedServices: '',
    excludedServices: '',
    notes: '',
    renewalOwnerUserId: null,
    accountManagerUserId: null,
    internalApproverUserId: null,
    autoExpireEnabled: false,
    gracePeriodDays: 0,
    allowPreferentialRatesDuringGracePeriod: false,
    allowSlaDuringGracePeriod: false,
    gracePeriodPolicy: 'None',
    responseTimeHours: 4,
    resolutionTimeHours: 24,
    workingHoursPolicy: 'BusinessHours',
    pauseSlaOnHold: true,
    escalationEmail: '',
    priorityLevel: 'Standard',
    gracePeriodAllowsSla: false,
    slaMatrix: [defaultSlaRow()],
    siteIds: [],
    assetIds: [],
    assetCategories: [],
    workOrderTypes: [{ workOrderTypeId: null, workOrderTypeName: 'PreventiveMaintenance' }],
    pmEntitlements: [],
  }
}

function mapDetailToInput(contract: ContractDetailRecord): UpsertContractInput {
  const slaMatrix = normalizeSlaRows(contract)
  return {
    contractNumber: contract.contractNumber,
    clientId: contract.clientId,
    name: contract.name,
    contractType: contract.contractType,
    status: contract.status,
    startDate: contract.startDate.slice(0, 10),
    endDate: contract.endDate.slice(0, 10),
    durationMonths: contract.durationMonths ?? null,
    billingModel: contract.billingModel,
    currency: contract.currency,
    contractValue: contract.contractValue ?? null,
    renewalNoticeDays: contract.renewalNoticeDays,
    renewalBehavior: contract.renewalBehavior,
    description: contract.description ?? '',
    includedServices: contract.includedServices ?? '',
    excludedServices: contract.excludedServices ?? '',
    notes: contract.notes ?? '',
    renewalOwnerUserId: contract.renewalOwnerUserId ?? null,
    accountManagerUserId: contract.accountManagerUserId ?? null,
    internalApproverUserId: contract.internalApproverUserId ?? null,
    autoExpireEnabled: contract.autoExpireEnabled,
    gracePeriodDays: contract.gracePeriodDays,
    allowPreferentialRatesDuringGracePeriod: contract.allowPreferentialRatesDuringGracePeriod,
    allowSlaDuringGracePeriod: contract.allowSlaDuringGracePeriod,
    gracePeriodPolicy: contract.gracePeriodPolicy,
    responseTimeHours: slaMatrix[0]?.responseTimeHours ?? contract.responseTimeHours ?? null,
    resolutionTimeHours: slaMatrix[0]?.resolutionTimeHours ?? contract.resolutionTimeHours ?? null,
    workingHoursPolicy: contract.workingHoursPolicy ?? '',
    pauseSlaOnHold: contract.pauseSlaOnHold,
    escalationEmail: contract.escalationEmail ?? '',
    priorityLevel: slaMatrix[0]?.priority ?? contract.priorityLevel ?? '',
    gracePeriodAllowsSla: contract.gracePeriodAllowsSla,
    slaMatrix,
    siteIds: contract.sites.map((item) => item.siteId),
    assetIds: contract.assets.map((item) => item.assetId),
    assetCategories: contract.assetCategories.map((item) => ({ assetCategoryId: item.assetCategoryId, assetCategoryName: item.assetCategoryName })),
    workOrderTypes: contract.workOrderTypes.map((item) => ({ workOrderTypeId: item.workOrderTypeId, workOrderTypeName: item.workOrderTypeName })),
    pmEntitlements: contract.pmEntitlements.map((item) => ({
      assetId: item.assetId ?? null,
      assetCategory: item.assetCategory ?? '',
      workOrderType: item.workOrderType,
      visitsPerPeriod: item.visitsPerPeriod,
      periodType: item.periodType,
      includedVisits: item.includedVisits,
      usedVisits: item.usedVisits,
      effectiveFrom: item.effectiveFrom.slice(0, 10),
      effectiveTo: item.effectiveTo.slice(0, 10),
      isActive: item.isActive,
      notes: item.notes ?? '',
    })),
  }
}

function toGracePeriodPolicy(input: UpsertContractInput): UpsertContractInput['gracePeriodPolicy'] {
  if (input.allowPreferentialRatesDuringGracePeriod && input.allowSlaDuringGracePeriod) return 'Both'
  if (input.allowPreferentialRatesDuringGracePeriod) return 'PreferentialRates'
  if (input.allowSlaDuringGracePeriod || input.gracePeriodAllowsSla) return 'Sla'
  return 'None'
}

function toRenewalBehavior(input: UpsertContractInput): UpsertContractInput['renewalBehavior'] {
  if (input.renewalBehavior) return input.renewalBehavior
  return input.autoExpireEnabled ? 'AutoExpire' : 'ManualRenewalRequired'
}

export function ContractEditor({ clients, users, initialValue, onSubmit, submitLabel, busy }: Props) {
  const [form, setForm] = useState<UpsertContractInput>(() => initialValue ? mapDetailToInput(initialValue) : buildEmptyContract())
  const [sites, setSites] = useState<SiteRecord[]>([])
  const [assets, setAssets] = useState<AssetRecord[]>([])
  const [assetCategories, setAssetCategories] = useState<AssetCategoryRecord[]>([])
  const [categoryDraftId, setCategoryDraftId] = useState('')
  const [workTypeDraft, setWorkTypeDraft] = useState('PreventiveMaintenance')
  const [coreOpen, setCoreOpen] = useState(true)
  const [coverageOpen, setCoverageOpen] = useState(Boolean(initialValue))
  const [slaOpen, setSlaOpen] = useState(Boolean(initialValue))
  const [pmOpen, setPmOpen] = useState(Boolean(initialValue))

  useEffect(() => {
    setForm(initialValue ? mapDetailToInput(initialValue) : buildEmptyContract())
    setCoreOpen(true)
    setCoverageOpen(Boolean(initialValue))
    setSlaOpen(Boolean(initialValue))
    setPmOpen(Boolean(initialValue))
  }, [initialValue])

  useEffect(() => {
    if (!form.clientId) {
      setSites([])
      setAssets([])
      return
    }

    let cancelled = false
    void Promise.all([
      siteService.list(form.clientId).catch(() => [] as SiteRecord[]),
      assetService.list(null, undefined, { clientId: form.clientId }).catch(() => [] as AssetRecord[]),
    ]).then(([nextSites, nextAssets]) => {
      if (cancelled) return
      setSites(nextSites)
      setAssets(nextAssets)
    })

    return () => {
      cancelled = true
    }
  }, [form.clientId])

  useEffect(() => {
    let cancelled = false
    void assetCategoryService.list().then((items) => {
      if (!cancelled) setAssetCategories(items.filter((item) => item.isActive))
    }).catch(() => {
      if (!cancelled) setAssetCategories([])
    })

    return () => {
      cancelled = true
    }
  }, [])

  const siteLookup = useMemo(() => new Map(sites.map((item) => [item.id, item.siteName])), [sites])
  const assetLookup = useMemo(() => new Map(assets.map((item) => [item.id, item.assetName])), [assets])
  const categoryLookup = useMemo(() => new Map(assetCategories.map((item) => [item.id, item.name])), [assetCategories])

  function toggleId(list: string[], id: string) {
    return list.includes(id) ? list.filter((item) => item !== id) : [...list, id]
  }

  function addAssetCategory() {
    if (!categoryDraftId) return
    const selected = assetCategories.find((item) => item.id === categoryDraftId)
    if (!selected) return

    setForm((current) => {
      if (current.assetCategories.some((item) => item.assetCategoryId === selected.id)) {
        return current
      }
      return {
        ...current,
        assetCategories: [...current.assetCategories, { assetCategoryId: selected.id, assetCategoryName: selected.name }],
      }
    })
    setCategoryDraftId('')
  }

  function addWorkOrderType() {
    const value = workTypeDraft.trim()
    if (!value) return
    setForm((current) => {
      if (current.workOrderTypes.some((item) => item.workOrderTypeName === value || item.workOrderTypeId === value)) {
        return current
      }
      return {
        ...current,
        workOrderTypes: [...current.workOrderTypes, { workOrderTypeId: null, workOrderTypeName: value }],
      }
    })
  }

  function addPmEntitlement() {
    setForm((current) => ({
      ...current,
      pmEntitlements: [
        ...current.pmEntitlements,
        {
          assetId: null,
          assetCategory: current.assetCategories[0]?.assetCategoryName ?? '',
          workOrderType: 'PreventiveMaintenance',
          visitsPerPeriod: 1,
          periodType: 'Annual',
          includedVisits: 1,
          usedVisits: 0,
          effectiveFrom: current.startDate,
          effectiveTo: current.endDate,
          isActive: true,
          notes: '',
        },
      ],
    }))
  }

  function upsertSlaRow(index: number, patch: Partial<ContractSlaRowInput>) {
    setForm((current) => ({
      ...current,
      slaMatrix: current.slaMatrix.map((item, currentIndex) => currentIndex === index ? { ...item, ...patch } : item),
    }))
  }

  function addSlaRow() {
    setForm((current) => ({
      ...current,
      slaMatrix: [...current.slaMatrix, { priority: 'High', responseTimeHours: 2, resolutionTimeHours: 8 }],
    }))
  }

  function removeSlaRow(index: number) {
    setForm((current) => ({
      ...current,
      slaMatrix: current.slaMatrix.length === 1 ? current.slaMatrix : current.slaMatrix.filter((_, currentIndex) => currentIndex !== index),
    }))
  }

  async function handleSubmit() {
    const slaMatrix = form.slaMatrix
      .map((row) => ({
        priority: row.priority.trim() || 'Standard',
        responseTimeHours: row.responseTimeHours ?? null,
        resolutionTimeHours: row.resolutionTimeHours ?? null,
      }))
      .filter((row) => row.priority)

    const normalizedRenewalBehavior = toRenewalBehavior(form)
    const normalizedGracePeriodPolicy = form.gracePeriodPolicy ?? toGracePeriodPolicy(form)

    const cleaned: UpsertContractInput = {
      ...form,
      renewalBehavior: normalizedRenewalBehavior,
      autoExpireEnabled: normalizedRenewalBehavior === 'AutoExpire',
      gracePeriodPolicy: normalizedGracePeriodPolicy,
      allowPreferentialRatesDuringGracePeriod: normalizedGracePeriodPolicy === 'PreferentialRates' || normalizedGracePeriodPolicy === 'Both',
      allowSlaDuringGracePeriod: normalizedGracePeriodPolicy === 'Sla' || normalizedGracePeriodPolicy === 'Both',
      gracePeriodAllowsSla: normalizedGracePeriodPolicy === 'Sla' || normalizedGracePeriodPolicy === 'Both',
      contractNumber: form.contractNumber.trim(),
      name: form.name.trim(),
      currency: form.currency.trim() || 'KES',
      description: form.description?.trim() || null,
      includedServices: form.includedServices?.trim() || null,
      excludedServices: form.excludedServices?.trim() || null,
      notes: form.notes?.trim() || null,
      escalationEmail: form.escalationEmail?.trim() || null,
      workingHoursPolicy: form.workingHoursPolicy?.trim() || null,
      priorityLevel: slaMatrix[0]?.priority || form.priorityLevel?.trim() || 'Standard',
      responseTimeHours: slaMatrix[0]?.responseTimeHours ?? null,
      resolutionTimeHours: slaMatrix[0]?.resolutionTimeHours ?? null,
      assetCategories: form.assetCategories.filter((item) => item.assetCategoryId || item.assetCategoryName?.trim()),
      workOrderTypes: form.workOrderTypes.filter((item) => item.workOrderTypeId || item.workOrderTypeName?.trim()),
      slaMatrix,
      pmEntitlements: form.pmEntitlements.filter((item) => item.workOrderType.trim()),
    }
    await onSubmit(cleaned)
  }

  return (
    <div className="space-y-4">
      <CollapsibleSection
        title="Core details"
        description="Capture the commercial baseline first. Required fields are marked with an asterisk."
        open={coreOpen}
        onToggle={() => setCoreOpen((current) => !current)}
      >
        <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
          <Field label="Contract number" required helper="Unique identifier used across reports and lifecycle events.">
            <input value={form.contractNumber} onChange={(event) => setForm({ ...form, contractNumber: event.target.value })} className="field-input" />
          </Field>
          <Field label="Client" required helper="The customer that owns this contract.">
            <select value={form.clientId} onChange={(event) => setForm({ ...form, clientId: event.target.value, siteIds: [], assetIds: [] })} className="field-input">
              <option value="">Select client</option>
              {clients.map((item) => <option key={item.id} value={item.id}>{item.clientName}</option>)}
            </select>
          </Field>
          <Field label="Contract name" required helper="Short label shown throughout the app.">
            <input value={form.name} onChange={(event) => setForm({ ...form, name: event.target.value })} className="field-input" />
          </Field>
          <Field label="Contract type" required>
            <select value={form.contractType} onChange={(event) => setForm({ ...form, contractType: event.target.value as UpsertContractInput['contractType'] })} className="field-input">
              {contractTypeOptions.map((item) => <option key={item} value={item}>{item}</option>)}
            </select>
          </Field>
          <Field label="Status" required>
            <select value={form.status} onChange={(event) => setForm({ ...form, status: event.target.value as UpsertContractInput['status'] })} className="field-input">
              {contractStatusOptions.map((item) => <option key={item} value={item}>{item}</option>)}
            </select>
          </Field>
          <Field label="Billing model" required>
            <select value={form.billingModel} onChange={(event) => setForm({ ...form, billingModel: event.target.value as UpsertContractInput['billingModel'] })} className="field-input">
              {contractBillingOptions.map((item) => <option key={item} value={item}>{item}</option>)}
            </select>
          </Field>
          <Field label="Start date" required>
            <input type="date" value={form.startDate} onChange={(event) => setForm({ ...form, startDate: event.target.value })} className="field-input" />
          </Field>
          <Field label="End date" required>
            <input type="date" value={form.endDate} onChange={(event) => setForm({ ...form, endDate: event.target.value })} className="field-input" />
          </Field>
          <Field label="Duration months">
            <input type="number" min={0} value={form.durationMonths ?? ''} onChange={(event) => setForm({ ...form, durationMonths: event.target.value ? Number(event.target.value) : null })} className="field-input" />
          </Field>
          <Field label="Currency" required helper="Use a currency code such as KES or USD.">
            <input value={form.currency} onChange={(event) => setForm({ ...form, currency: event.target.value })} className="field-input" />
          </Field>
          <Field label="Contract value">
            <input type="number" min={0} value={form.contractValue ?? ''} onChange={(event) => setForm({ ...form, contractValue: event.target.value ? Number(event.target.value) : null })} className="field-input" />
          </Field>
          <Field label="Renewal behavior" required helper="Choose how the contract behaves at end date.">
            <select
              value={form.renewalBehavior ?? 'ManualRenewalRequired'}
              onChange={(event) => setForm((current) => ({
                ...current,
                renewalBehavior: event.target.value as UpsertContractInput['renewalBehavior'],
                autoExpireEnabled: event.target.value === 'AutoExpire',
              }))}
              className="field-input"
            >
              {contractRenewalBehaviorOptions.map((item) => <option key={item} value={item}>{formatPolicyLabel(item)}</option>)}
            </select>
          </Field>
          {form.renewalBehavior !== 'AutoExpire' ? (
            <Field label="Renewal notice days" helper="Used for reminders and expiring-soon calculations.">
              <input type="number" min={0} value={form.renewalNoticeDays} onChange={(event) => setForm({ ...form, renewalNoticeDays: Number(event.target.value || 0) })} className="field-input" />
            </Field>
          ) : (
            <Field label="Renewal notice days" helper="Hidden for auto-expire contracts.">
              <input type="number" min={0} value={form.renewalNoticeDays} onChange={(event) => setForm({ ...form, renewalNoticeDays: Number(event.target.value || 0) })} className="field-input" disabled />
            </Field>
          )}
        </div>
        <div className="mt-3 grid gap-3 md:grid-cols-2">
          <Field label="Description" helper="Describe the contract in plain language.">
            <textarea value={form.description ?? ''} onChange={(event) => setForm({ ...form, description: event.target.value })} className="field-input min-h-[110px]" />
          </Field>
          <Field label="Notes" helper="Internal-only context, exceptions, or follow-up reminders.">
            <textarea value={form.notes ?? ''} onChange={(event) => setForm({ ...form, notes: event.target.value })} className="field-input min-h-[110px]" />
          </Field>
        </div>
      </CollapsibleSection>

      <CollapsibleSection
        title="Coverage"
        description="Coverage follows a most-specific-wins rule: asset overrides category, category overrides site. Work order types are still treated as an allow-list."
        open={coverageOpen}
        onToggle={() => setCoverageOpen((current) => !current)}
      >
        <div className="grid gap-4 xl:grid-cols-2">
          <SelectorList
            title="Covered sites"
            items={sites.map((item) => ({ id: item.id, label: item.siteName }))}
            selectedIds={form.siteIds}
            onToggle={(id) => setForm((current) => ({ ...current, siteIds: toggleId(current.siteIds, id) }))}
          />
          <SelectorList
            title="Covered assets"
            items={assets.map((item) => ({ id: item.id, label: `${item.assetName}${item.assetCode ? ` (${item.assetCode})` : ''}` }))}
            selectedIds={form.assetIds}
            onToggle={(id) => setForm((current) => ({ ...current, assetIds: toggleId(current.assetIds, id) }))}
          />
        </div>
        <div className="mt-4 grid gap-4 xl:grid-cols-2">
          <div>
            <Field label="Covered asset categories" helper="Pick from the asset-category taxonomy to avoid duplicate labels.">
              <div className="flex gap-2">
                <select value={categoryDraftId} onChange={(event) => setCategoryDraftId(event.target.value)} className="field-input">
                  <option value="">Select category</option>
                  {assetCategories.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
                </select>
                <button type="button" className="button-secondary" onClick={addAssetCategory} disabled={!categoryDraftId}>Add</button>
              </div>
            </Field>
            <div className="mt-3 flex flex-wrap gap-2">
              {form.assetCategories.map((item, index) => (
                <button
                  key={`${item.assetCategoryId ?? item.assetCategoryName}-${index}`}
                  type="button"
                  className="rounded-full border border-app px-3 py-1 text-sm text-app"
                  onClick={() => setForm((current) => ({ ...current, assetCategories: current.assetCategories.filter((_, currentIndex) => currentIndex !== index) }))}
                >
                  {item.assetCategoryName || item.assetCategoryId || 'Category'} x
                </button>
              ))}
            </div>
          </div>
          <div>
            <Field label="Covered work order types" helper="Use the contract's work-order taxonomy.">
              <div className="flex gap-2">
                <select value={workTypeDraft} onChange={(event) => setWorkTypeDraft(event.target.value)} className="field-input">
                  {workOrderTypeOptions.map((item) => <option key={item} value={item}>{item}</option>)}
                </select>
                <button type="button" className="button-secondary" onClick={addWorkOrderType}>Add</button>
              </div>
            </Field>
            <div className="mt-3 flex flex-wrap gap-2">
              {form.workOrderTypes.map((item, index) => (
                <button
                  key={`${item.workOrderTypeName}-${index}`}
                  type="button"
                  className="rounded-full border border-app px-3 py-1 text-sm text-app"
                  onClick={() => setForm((current) => ({ ...current, workOrderTypes: current.workOrderTypes.filter((_, currentIndex) => currentIndex !== index) }))}
                >
                  {item.workOrderTypeName || 'Work type'} x
                </button>
              ))}
            </div>
          </div>
        </div>
        <div className="mt-4 grid gap-3 md:grid-cols-2">
          <Field label="Included services">
            <textarea value={form.includedServices ?? ''} onChange={(event) => setForm({ ...form, includedServices: event.target.value })} className="field-input min-h-[110px]" placeholder="Quarterly PM, emergency response, routine inspections" />
          </Field>
          <Field label="Excluded services">
            <textarea value={form.excludedServices ?? ''} onChange={(event) => setForm({ ...form, excludedServices: event.target.value })} className="field-input min-h-[110px]" placeholder="Major overhauls, consumables, civil works" />
          </Field>
        </div>
      </CollapsibleSection>

      <CollapsibleSection
        title="SLA & ownership"
        description="Set ownership, a small priority-based SLA matrix, and one clear grace-period policy."
        open={slaOpen}
        onToggle={() => setSlaOpen((current) => !current)}
      >
        <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
          <Field label="Renewal owner">
            <select value={form.renewalOwnerUserId ?? ''} onChange={(event) => setForm({ ...form, renewalOwnerUserId: event.target.value || null })} className="field-input">
              <option value="">Not assigned</option>
              {users.map((item) => <option key={item.id} value={item.id}>{item.fullName}</option>)}
            </select>
          </Field>
          <Field label="Account manager">
            <select value={form.accountManagerUserId ?? ''} onChange={(event) => setForm({ ...form, accountManagerUserId: event.target.value || null })} className="field-input">
              <option value="">Not assigned</option>
              {users.map((item) => <option key={item.id} value={item.id}>{item.fullName}</option>)}
            </select>
          </Field>
          <Field label="Internal approver">
            <select value={form.internalApproverUserId ?? ''} onChange={(event) => setForm({ ...form, internalApproverUserId: event.target.value || null })} className="field-input">
              <option value="">Not assigned</option>
              {users.map((item) => <option key={item.id} value={item.id}>{item.fullName}</option>)}
            </select>
          </Field>
          <Field label="Working hours policy">
            <input value={form.workingHoursPolicy ?? ''} onChange={(event) => setForm({ ...form, workingHoursPolicy: event.target.value })} className="field-input" />
          </Field>
          <Field label="Escalation email">
            <input value={form.escalationEmail ?? ''} onChange={(event) => setForm({ ...form, escalationEmail: event.target.value })} className="field-input" />
          </Field>
        </div>

        <div className="mt-5 space-y-3">
          <div className="flex items-center justify-between gap-3">
            <div>
              <p className="text-sm font-semibold text-app">SLA matrix</p>
              <p className="text-sm text-muted">Use the default Standard row, then add extra priorities only where service levels diverge.</p>
            </div>
            <button type="button" className="button-secondary" onClick={addSlaRow}>Add row</button>
          </div>
          <div className="space-y-3">
            {form.slaMatrix.map((row, index) => (
              <div key={`${row.priority}-${index}`} className="grid gap-3 rounded-2xl border border-app bg-[var(--app-subtle)] p-4 md:grid-cols-2 xl:grid-cols-5">
                <Field label="Priority" required>
                  <input value={row.priority} onChange={(event) => upsertSlaRow(index, { priority: event.target.value })} className="field-input" placeholder="Standard" />
                </Field>
                <Field label="Response hours" required>
                  <input type="number" min={0} value={row.responseTimeHours ?? ''} onChange={(event) => upsertSlaRow(index, { responseTimeHours: event.target.value ? Number(event.target.value) : null })} className="field-input" />
                </Field>
                <Field label="Resolution hours" required>
                  <input type="number" min={0} value={row.resolutionTimeHours ?? ''} onChange={(event) => upsertSlaRow(index, { resolutionTimeHours: event.target.value ? Number(event.target.value) : null })} className="field-input" />
                </Field>
                <div className="flex items-end">
                  <button type="button" className="button-secondary" onClick={() => removeSlaRow(index)} disabled={form.slaMatrix.length === 1}>Remove</button>
                </div>
              </div>
            ))}
          </div>
        </div>

        <div className="mt-5 rounded-2xl border border-app bg-[var(--app-subtle)] p-4">
          <p className="text-sm font-semibold text-app">Grace period policy</p>
          <p className="mt-1 text-sm text-muted">Keep the grace-period controls together so it is obvious what still applies after end date.</p>
          <div className="mt-3 grid gap-3 md:grid-cols-2 xl:grid-cols-4">
            <Field label="Policy">
              <select
                value={form.gracePeriodPolicy ?? toGracePeriodPolicy(form)}
                onChange={(event) => {
                  const policy = event.target.value as UpsertContractInput['gracePeriodPolicy']
                  setForm((current) => ({
                    ...current,
                    gracePeriodPolicy: policy,
                    allowPreferentialRatesDuringGracePeriod: policy === 'PreferentialRates' || policy === 'Both',
                    allowSlaDuringGracePeriod: policy === 'Sla' || policy === 'Both',
                    gracePeriodAllowsSla: policy === 'Sla' || policy === 'Both',
                  }))
                }}
                className="field-input"
              >
                {contractGracePeriodPolicyOptions.map((item) => <option key={item} value={item}>{formatPolicyLabel(item)}</option>)}
              </select>
            </Field>
            <Field label="Grace period days">
              <input type="number" min={0} value={form.gracePeriodDays} onChange={(event) => setForm({ ...form, gracePeriodDays: Number(event.target.value || 0) })} className="field-input" />
            </Field>
          </div>
          <div className="mt-4 grid gap-2 md:grid-cols-2">
            <Toggle label="Pause SLA when work order is on hold" checked={form.pauseSlaOnHold} onChange={(checked) => setForm({ ...form, pauseSlaOnHold: checked })} />
            <Toggle label="Auto-expire contract after end date" checked={form.autoExpireEnabled} onChange={(checked) => setForm({ ...form, autoExpireEnabled: checked, renewalBehavior: checked ? 'AutoExpire' : (form.renewalBehavior === 'AutoExpire' ? 'ManualRenewalRequired' : form.renewalBehavior) })} />
          </div>
        </div>
      </CollapsibleSection>

      <CollapsibleSection
        title="PM entitlements"
        description="Define preventive visits at the category level by default, then use asset rows only for exceptions."
        open={pmOpen}
        onToggle={() => setPmOpen((current) => !current)}
        action={<button type="button" className="button-secondary" onClick={addPmEntitlement}>Add entitlement</button>}
      >
        <div className="space-y-3">
          {form.pmEntitlements.length === 0 ? <p className="text-sm text-muted">No PM entitlements have been defined yet.</p> : null}
          {form.pmEntitlements.map((item, index) => (
            <div key={`${item.assetId ?? item.assetCategory ?? 'entitlement'}-${index}`} className="grid gap-3 rounded-2xl border border-app bg-[var(--app-subtle)] p-4 md:grid-cols-2 xl:grid-cols-6">
              <Field label="Asset">
                <select value={item.assetId ?? ''} onChange={(event) => updatePmEntitlement(setForm, index, { assetId: event.target.value || null, assetCategory: event.target.value ? '' : item.assetCategory })} className="field-input">
                  <option value="">Category / general</option>
                  {assets.map((asset) => <option key={asset.id} value={asset.id}>{asset.assetName}</option>)}
                </select>
              </Field>
              <Field label="Category" helper="Preferred default when the entitlement is shared across a family of assets.">
                <select value={item.assetCategory ?? ''} onChange={(event) => updatePmEntitlement(setForm, index, { assetCategory: event.target.value, assetId: null })} className="field-input">
                  <option value="">Select category</option>
                  {assetCategories.map((category) => <option key={category.id} value={category.name}>{category.name}</option>)}
                </select>
              </Field>
              <Field label="Work type">
                <select value={item.workOrderType} onChange={(event) => updatePmEntitlement(setForm, index, { workOrderType: event.target.value })} className="field-input">
                  {workOrderTypeOptions.map((option) => <option key={option} value={option}>{option}</option>)}
                </select>
              </Field>
              <Field label="Period">
                <select value={item.periodType} onChange={(event) => updatePmEntitlement(setForm, index, { periodType: event.target.value as ContractPmEntitlementInput['periodType'] })} className="field-input">
                  {pmPeriodOptions.map((option) => <option key={option} value={option}>{option}</option>)}
                </select>
              </Field>
              <Field label="Included visits">
                <input type="number" min={0} value={item.includedVisits} onChange={(event) => updatePmEntitlement(setForm, index, { includedVisits: Number(event.target.value || 0) })} className="field-input" />
              </Field>
              <Field label="Used visits" helper="System-managed from work order completion; shown here for review only.">
                <div className="field-input bg-[var(--app-subtle)] text-sm text-secondary">{item.usedVisits}</div>
              </Field>
              <Field label="Visits per period">
                <input type="number" min={0} value={item.visitsPerPeriod} onChange={(event) => updatePmEntitlement(setForm, index, { visitsPerPeriod: Number(event.target.value || 0) })} className="field-input" />
              </Field>
              <Field label="Effective from">
                <input type="date" value={item.effectiveFrom} onChange={(event) => updatePmEntitlement(setForm, index, { effectiveFrom: event.target.value })} className="field-input" />
              </Field>
              <Field label="Effective to">
                <input type="date" value={item.effectiveTo} onChange={(event) => updatePmEntitlement(setForm, index, { effectiveTo: event.target.value })} className="field-input" />
              </Field>
              <Field label="Notes">
                <input value={item.notes ?? ''} onChange={(event) => updatePmEntitlement(setForm, index, { notes: event.target.value })} className="field-input" />
              </Field>
              <Toggle label="Active" checked={item.isActive} onChange={(checked) => updatePmEntitlement(setForm, index, { isActive: checked })} />
              <div className="flex items-end">
                <button type="button" className="button-secondary" onClick={() => setForm((current) => ({ ...current, pmEntitlements: current.pmEntitlements.filter((_, currentIndex) => currentIndex !== index) }))}>Remove</button>
              </div>
            </div>
          ))}
        </div>
      </CollapsibleSection>

      <div className="flex justify-end">
        <button type="button" className="button-primary" onClick={() => void handleSubmit()} disabled={busy || !form.clientId || !form.name.trim()}>
          {busy ? 'Saving...' : submitLabel}
        </button>
      </div>

      <div className="hidden">
        {siteLookup.size}
        {assetLookup.size}
        {categoryLookup.size}
      </div>
    </div>
  )
}

function updatePmEntitlement(
  setForm: Dispatch<SetStateAction<UpsertContractInput>>,
  index: number,
  patch: Partial<ContractPmEntitlementInput>,
) {
  setForm((current) => ({
    ...current,
    pmEntitlements: current.pmEntitlements.map((item, currentIndex) => currentIndex === index ? { ...item, ...patch } : item),
  }))
}

function Toggle({ label, checked, onChange }: { label: string; checked: boolean; onChange: (checked: boolean) => void }) {
  return (
    <label className="flex items-center gap-3 rounded-2xl border border-app bg-[var(--app-subtle)] px-4 py-3 text-sm text-app">
      <input type="checkbox" checked={checked} onChange={(event) => onChange(event.target.checked)} />
      <span>{label}</span>
    </label>
  )
}

function Field({ label, children, required = false, helper }: { label: string; children: ReactNode; required?: boolean; helper?: string }) {
  return (
    <label className="space-y-2">
      <span className="text-sm font-medium text-app">
        {label}
        {required ? <span className="ml-1 text-[var(--app-danger)]">*</span> : null}
      </span>
      {children}
      {helper ? <p className="text-xs leading-5 text-muted">{helper}</p> : null}
    </label>
  )
}

function SelectorList({
  title,
  items,
  selectedIds,
  onToggle,
}: {
  title: string
  items: ListItem[]
  selectedIds: string[]
  onToggle: (id: string) => void
}) {
  return (
    <div>
      <p className="text-sm font-semibold text-app">{title}</p>
      <div className="mt-3 max-h-64 space-y-2 overflow-y-auto pr-1">
        {items.length === 0 ? <p className="text-sm text-muted">No records available for the selected client.</p> : null}
        {items.map((item) => (
          <label key={item.id} className="flex items-center gap-3 rounded-2xl border border-app bg-[var(--app-subtle)] px-4 py-3 text-sm text-app">
            <input type="checkbox" checked={selectedIds.includes(item.id)} onChange={() => onToggle(item.id)} />
            <span>{item.label}</span>
          </label>
        ))}
      </div>
    </div>
  )
}

function CollapsibleSection({
  title,
  description,
  action,
  open,
  onToggle,
  children,
}: {
  title: string
  description?: string
  action?: ReactNode
  open: boolean
  onToggle: () => void
  children: ReactNode
}) {
  return (
    <section className="section-panel space-y-2">
      <div className="section-card-header flex flex-wrap items-start justify-between gap-2">
        <button type="button" className="min-w-0 text-left" onClick={onToggle}>
          <span className="flex items-center gap-2">
            <ChevronDown className={`size-4 transition-transform ${open ? '' : '-rotate-90'}`} />
            <span className="font-heading text-[0.98rem] font-semibold tracking-[-0.02em] text-app sm:text-[1.04rem]">{title}</span>
          </span>
          {description ? <p className="mt-0.5 max-w-3xl text-[0.85rem] leading-5 text-muted">{description}</p> : null}
        </button>
        {action ? <div className="flex flex-wrap gap-1.5">{action}</div> : null}
      </div>
      {open ? <div>{children}</div> : null}
    </section>
  )
}

function formatPolicyLabel(value: string) {
  return value
    .replace(/([a-z])([A-Z])/g, '$1 $2')
    .replace(/\bSla\b/g, 'SLA')
}
