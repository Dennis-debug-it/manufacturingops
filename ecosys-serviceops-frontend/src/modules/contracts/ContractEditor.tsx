import { useEffect, useMemo, useState, type Dispatch, type ReactNode, type SetStateAction } from 'react'
import { SectionPanel } from '../../components/ui/Workspace'
import type {
  AssetRecord,
  ClientRecord,
  ContractDetailRecord,
  ContractPmEntitlementInput,
  UpsertContractInput,
  UserRecord,
} from '../../types/api'
import { assetService } from '../../services/assetService'
import { siteService } from '../../services/siteService'
import type { SiteRecord } from '../../types/api'
import { contractBillingOptions, contractStatusOptions, contractTypeOptions, pmPeriodOptions, workOrderTypeOptions } from './contractUi'

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
    description: '',
    includedServices: '',
    excludedServices: '',
    notes: '',
    renewalOwnerUserId: null,
    accountManagerUserId: null,
    internalApproverUserId: null,
    autoExpireEnabled: true,
    gracePeriodDays: 0,
    allowPreferentialRatesDuringGracePeriod: false,
    allowSlaDuringGracePeriod: false,
    responseTimeHours: 4,
    resolutionTimeHours: 24,
    workingHoursPolicy: 'BusinessHours',
    pauseSlaOnHold: true,
    escalationEmail: '',
    priorityLevel: 'Standard',
    gracePeriodAllowsSla: false,
    siteIds: [],
    assetIds: [],
    assetCategories: [],
    workOrderTypes: [{ workOrderTypeId: null, workOrderTypeName: 'PreventiveMaintenance' }],
    pmEntitlements: [],
  }
}

function mapDetailToInput(contract: ContractDetailRecord): UpsertContractInput {
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
    responseTimeHours: contract.responseTimeHours ?? null,
    resolutionTimeHours: contract.resolutionTimeHours ?? null,
    workingHoursPolicy: contract.workingHoursPolicy ?? '',
    pauseSlaOnHold: contract.pauseSlaOnHold,
    escalationEmail: contract.escalationEmail ?? '',
    priorityLevel: contract.priorityLevel ?? '',
    gracePeriodAllowsSla: contract.gracePeriodAllowsSla,
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

export function ContractEditor({ clients, users, initialValue, onSubmit, submitLabel, busy }: Props) {
  const [form, setForm] = useState<UpsertContractInput>(() => initialValue ? mapDetailToInput(initialValue) : buildEmptyContract())
  const [sites, setSites] = useState<SiteRecord[]>([])
  const [assets, setAssets] = useState<AssetRecord[]>([])
  const [categoryDraft, setCategoryDraft] = useState('')
  const [workTypeDraft, setWorkTypeDraft] = useState('PreventiveMaintenance')

  useEffect(() => {
    setForm(initialValue ? mapDetailToInput(initialValue) : buildEmptyContract())
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

  const siteLookup = useMemo(() => new Map(sites.map((item) => [item.id, item.siteName])), [sites])
  const assetLookup = useMemo(() => new Map(assets.map((item) => [item.id, item.assetName])), [assets])

  function toggleId(list: string[], id: string) {
    return list.includes(id) ? list.filter((item) => item !== id) : [...list, id]
  }

  function addAssetCategory() {
    const value = categoryDraft.trim()
    if (!value) return
    setForm((current) => ({
      ...current,
      assetCategories: [...current.assetCategories, { assetCategoryId: null, assetCategoryName: value }],
    }))
    setCategoryDraft('')
  }

  function addWorkOrderType() {
    const value = workTypeDraft.trim()
    if (!value) return
    setForm((current) => ({
      ...current,
      workOrderTypes: [...current.workOrderTypes, { workOrderTypeId: null, workOrderTypeName: value }],
    }))
  }

  function addPmEntitlement() {
    setForm((current) => ({
      ...current,
      pmEntitlements: [
        ...current.pmEntitlements,
        {
          assetId: null,
          assetCategory: '',
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

  async function handleSubmit() {
    const cleaned: UpsertContractInput = {
      ...form,
      contractNumber: form.contractNumber.trim(),
      name: form.name.trim(),
      currency: form.currency.trim() || 'KES',
      description: form.description?.trim() || null,
      includedServices: form.includedServices?.trim() || null,
      excludedServices: form.excludedServices?.trim() || null,
      notes: form.notes?.trim() || null,
      escalationEmail: form.escalationEmail?.trim() || null,
      workingHoursPolicy: form.workingHoursPolicy?.trim() || null,
      priorityLevel: form.priorityLevel?.trim() || null,
      assetCategories: form.assetCategories.filter((item) => item.assetCategoryId || item.assetCategoryName?.trim()),
      workOrderTypes: form.workOrderTypes.filter((item) => item.workOrderTypeId || item.workOrderTypeName?.trim()),
      pmEntitlements: form.pmEntitlements.filter((item) => item.workOrderType.trim()),
    }
    await onSubmit(cleaned)
  }

  return (
    <div className="space-y-4">
      <SectionPanel title="Core details" description="Capture the commercial and operational baseline for this contract.">
        <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
          <Field label="Contract number"><input value={form.contractNumber} onChange={(event) => setForm({ ...form, contractNumber: event.target.value })} className="field-input" /></Field>
          <Field label="Client">
            <select value={form.clientId} onChange={(event) => setForm({ ...form, clientId: event.target.value, siteIds: [], assetIds: [] })} className="field-input">
              <option value="">Select client</option>
              {clients.map((item) => <option key={item.id} value={item.id}>{item.clientName}</option>)}
            </select>
          </Field>
          <Field label="Contract name"><input value={form.name} onChange={(event) => setForm({ ...form, name: event.target.value })} className="field-input" /></Field>
          <Field label="Contract type">
            <select value={form.contractType} onChange={(event) => setForm({ ...form, contractType: event.target.value as UpsertContractInput['contractType'] })} className="field-input">
              {contractTypeOptions.map((item) => <option key={item} value={item}>{item}</option>)}
            </select>
          </Field>
          <Field label="Status">
            <select value={form.status} onChange={(event) => setForm({ ...form, status: event.target.value as UpsertContractInput['status'] })} className="field-input">
              {contractStatusOptions.map((item) => <option key={item} value={item}>{item}</option>)}
            </select>
          </Field>
          <Field label="Billing model">
            <select value={form.billingModel} onChange={(event) => setForm({ ...form, billingModel: event.target.value as UpsertContractInput['billingModel'] })} className="field-input">
              {contractBillingOptions.map((item) => <option key={item} value={item}>{item}</option>)}
            </select>
          </Field>
          <Field label="Start date"><input type="date" value={form.startDate} onChange={(event) => setForm({ ...form, startDate: event.target.value })} className="field-input" /></Field>
          <Field label="End date"><input type="date" value={form.endDate} onChange={(event) => setForm({ ...form, endDate: event.target.value })} className="field-input" /></Field>
          <Field label="Duration months"><input type="number" min={0} value={form.durationMonths ?? ''} onChange={(event) => setForm({ ...form, durationMonths: event.target.value ? Number(event.target.value) : null })} className="field-input" /></Field>
          <Field label="Currency"><input value={form.currency} onChange={(event) => setForm({ ...form, currency: event.target.value })} className="field-input" /></Field>
          <Field label="Contract value"><input type="number" min={0} value={form.contractValue ?? ''} onChange={(event) => setForm({ ...form, contractValue: event.target.value ? Number(event.target.value) : null })} className="field-input" /></Field>
          <Field label="Renewal notice days"><input type="number" min={0} value={form.renewalNoticeDays} onChange={(event) => setForm({ ...form, renewalNoticeDays: Number(event.target.value || 0) })} className="field-input" /></Field>
        </div>
        <div className="mt-3 grid gap-3 md:grid-cols-2">
          <Field label="Description"><textarea value={form.description ?? ''} onChange={(event) => setForm({ ...form, description: event.target.value })} className="field-input min-h-[110px]" /></Field>
          <Field label="Notes"><textarea value={form.notes ?? ''} onChange={(event) => setForm({ ...form, notes: event.target.value })} className="field-input min-h-[110px]" /></Field>
        </div>
      </SectionPanel>

      <SectionPanel title="Coverage" description="Define the sites, assets, categories, and work types covered by this contract.">
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
            <p className="text-sm font-semibold text-app">Covered asset categories</p>
            <div className="mt-2 flex gap-2">
              <input value={categoryDraft} onChange={(event) => setCategoryDraft(event.target.value)} className="field-input" placeholder="Generator" />
              <button type="button" className="button-secondary" onClick={addAssetCategory}>Add</button>
            </div>
            <div className="mt-3 flex flex-wrap gap-2">
              {form.assetCategories.map((item, index) => (
                <button key={`${item.assetCategoryId ?? item.assetCategoryName}-${index}`} type="button" className="rounded-full border border-app px-3 py-1 text-sm text-app" onClick={() => setForm((current) => ({ ...current, assetCategories: current.assetCategories.filter((_, currentIndex) => currentIndex !== index) }))}>
                  {item.assetCategoryName || 'Category'} x
                </button>
              ))}
            </div>
          </div>
          <div>
            <p className="text-sm font-semibold text-app">Covered work order types</p>
            <div className="mt-2 flex gap-2">
              <select value={workTypeDraft} onChange={(event) => setWorkTypeDraft(event.target.value)} className="field-input">
                {workOrderTypeOptions.map((item) => <option key={item} value={item}>{item}</option>)}
              </select>
              <button type="button" className="button-secondary" onClick={addWorkOrderType}>Add</button>
            </div>
            <div className="mt-3 flex flex-wrap gap-2">
              {form.workOrderTypes.map((item, index) => (
                <button key={`${item.workOrderTypeName}-${index}`} type="button" className="rounded-full border border-app px-3 py-1 text-sm text-app" onClick={() => setForm((current) => ({ ...current, workOrderTypes: current.workOrderTypes.filter((_, currentIndex) => currentIndex !== index) }))}>
                  {item.workOrderTypeName || 'Work type'} x
                </button>
              ))}
            </div>
          </div>
        </div>
        <div className="mt-4 grid gap-3 md:grid-cols-2">
          <Field label="Included services"><textarea value={form.includedServices ?? ''} onChange={(event) => setForm({ ...form, includedServices: event.target.value })} className="field-input min-h-[110px]" placeholder="Quarterly PM, emergency response, routine inspections" /></Field>
          <Field label="Excluded services"><textarea value={form.excludedServices ?? ''} onChange={(event) => setForm({ ...form, excludedServices: event.target.value })} className="field-input min-h-[110px]" placeholder="Major overhauls, consumables, civil works" /></Field>
        </div>
      </SectionPanel>

      <SectionPanel title="SLA & ownership" description="Set response targets, reminder ownership, and grace-period behavior.">
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
          <Field label="Working hours policy"><input value={form.workingHoursPolicy ?? ''} onChange={(event) => setForm({ ...form, workingHoursPolicy: event.target.value })} className="field-input" /></Field>
          <Field label="Response hours"><input type="number" min={0} value={form.responseTimeHours ?? ''} onChange={(event) => setForm({ ...form, responseTimeHours: event.target.value ? Number(event.target.value) : null })} className="field-input" /></Field>
          <Field label="Resolution hours"><input type="number" min={0} value={form.resolutionTimeHours ?? ''} onChange={(event) => setForm({ ...form, resolutionTimeHours: event.target.value ? Number(event.target.value) : null })} className="field-input" /></Field>
          <Field label="Escalation email"><input value={form.escalationEmail ?? ''} onChange={(event) => setForm({ ...form, escalationEmail: event.target.value })} className="field-input" /></Field>
          <Field label="Priority label"><input value={form.priorityLevel ?? ''} onChange={(event) => setForm({ ...form, priorityLevel: event.target.value })} className="field-input" /></Field>
        </div>
        <div className="mt-4 grid gap-2 md:grid-cols-2">
          <Toggle label="Auto-expire contract after end date" checked={form.autoExpireEnabled} onChange={(checked) => setForm({ ...form, autoExpireEnabled: checked })} />
          <Toggle label="Pause SLA when work order is on hold" checked={form.pauseSlaOnHold} onChange={(checked) => setForm({ ...form, pauseSlaOnHold: checked })} />
          <Toggle label="Allow preferential rates during grace period" checked={form.allowPreferentialRatesDuringGracePeriod} onChange={(checked) => setForm({ ...form, allowPreferentialRatesDuringGracePeriod: checked })} />
          <Toggle label="Allow SLA during grace period" checked={form.allowSlaDuringGracePeriod} onChange={(checked) => setForm({ ...form, allowSlaDuringGracePeriod: checked })} />
          <Toggle label="Grace period keeps SLA operational" checked={form.gracePeriodAllowsSla} onChange={(checked) => setForm({ ...form, gracePeriodAllowsSla: checked })} />
        </div>
        <div className="mt-4 max-w-sm">
          <Field label="Grace period days"><input type="number" min={0} value={form.gracePeriodDays} onChange={(event) => setForm({ ...form, gracePeriodDays: Number(event.target.value || 0) })} className="field-input" /></Field>
        </div>
      </SectionPanel>

      <SectionPanel
        title="PM entitlements"
        description="Track included preventive maintenance visits under the contract."
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
              <Field label="Category"><input value={item.assetCategory ?? ''} onChange={(event) => updatePmEntitlement(setForm, index, { assetCategory: event.target.value, assetId: null })} className="field-input" /></Field>
              <Field label="Work type"><input value={item.workOrderType} onChange={(event) => updatePmEntitlement(setForm, index, { workOrderType: event.target.value })} className="field-input" /></Field>
              <Field label="Period">
                <select value={item.periodType} onChange={(event) => updatePmEntitlement(setForm, index, { periodType: event.target.value as ContractPmEntitlementInput['periodType'] })} className="field-input">
                  {pmPeriodOptions.map((option) => <option key={option} value={option}>{option}</option>)}
                </select>
              </Field>
              <Field label="Included visits"><input type="number" min={0} value={item.includedVisits} onChange={(event) => updatePmEntitlement(setForm, index, { includedVisits: Number(event.target.value || 0) })} className="field-input" /></Field>
              <Field label="Used visits"><input type="number" min={0} value={item.usedVisits} onChange={(event) => updatePmEntitlement(setForm, index, { usedVisits: Number(event.target.value || 0) })} className="field-input" /></Field>
              <Field label="Visits per period"><input type="number" min={0} value={item.visitsPerPeriod} onChange={(event) => updatePmEntitlement(setForm, index, { visitsPerPeriod: Number(event.target.value || 0) })} className="field-input" /></Field>
              <Field label="Effective from"><input type="date" value={item.effectiveFrom} onChange={(event) => updatePmEntitlement(setForm, index, { effectiveFrom: event.target.value })} className="field-input" /></Field>
              <Field label="Effective to"><input type="date" value={item.effectiveTo} onChange={(event) => updatePmEntitlement(setForm, index, { effectiveTo: event.target.value })} className="field-input" /></Field>
              <Field label="Notes"><input value={item.notes ?? ''} onChange={(event) => updatePmEntitlement(setForm, index, { notes: event.target.value })} className="field-input" /></Field>
              <Toggle label="Active" checked={item.isActive} onChange={(checked) => updatePmEntitlement(setForm, index, { isActive: checked })} />
              <div className="flex items-end">
                <button type="button" className="button-secondary" onClick={() => setForm((current) => ({ ...current, pmEntitlements: current.pmEntitlements.filter((_, currentIndex) => currentIndex !== index) }))}>Remove</button>
              </div>
            </div>
          ))}
        </div>
      </SectionPanel>

      <div className="flex justify-end">
        <button type="button" className="button-primary" onClick={() => void handleSubmit()} disabled={busy || !form.clientId || !form.name.trim()}>
          {busy ? 'Saving...' : submitLabel}
        </button>
      </div>

      <div className="hidden">
        {siteLookup.size}
        {assetLookup.size}
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

function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <label className="space-y-2">
      <span className="text-sm font-medium text-app">{label}</span>
      {children}
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
