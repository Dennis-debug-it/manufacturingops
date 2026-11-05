import { useEffect, useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import toast from 'react-hot-toast'
import { X } from 'lucide-react'
import { FormDrawer } from '../../../shared/components/FormDrawer'
import { useDebounce } from '../../../shared/hooks/useDebounce'
import { useAssetClasses, useAssetSearch, useFactorySettings, useSaveFactoryAsset } from '../api'
import { CRITICALITIES, OPERATIONAL_STATUSES, type AssetClass, type AssetClassField, type Criticality, type FactoryAsset, type OperationalStatus } from '../types'
import { Field, LocationSelect, inputClass, primaryButton, secondaryButton, textareaClass } from './ui'

interface FormState {
  assetName: string
  assetCode: string
  orgNodeId: string
  parent: { id: string; label: string } | null
  assetCategoryId: string
  criticality: '' | Criticality
  operationalStatus: OperationalStatus
  manufacturer: string
  model: string
  serialNumber: string
  installationDate: string
  commissioningDate: string
  warrantyStartDate: string
  warrantyExpiryDate: string
  lostProductionPerHour: string
  externalId: string
  externalSystem: string
  notes: string
  custom: Record<string, string>
}

const dateOnly = (value: string | null | undefined) => (value ? value.slice(0, 10) : '')

function initialState(asset: FactoryAsset | undefined, defaults: { orgNodeId?: string | undefined; parent?: { id: string; label: string } | undefined }): FormState {
  return {
    assetName: asset?.assetName ?? '',
    assetCode: asset?.assetCode ?? '',
    orgNodeId: asset?.orgNodeId ?? defaults.orgNodeId ?? '',
    parent: asset?.parentAssetId ? { id: asset.parentAssetId, label: `${asset.parentAssetCode} · ${asset.parentAssetName}` } : defaults.parent ?? null,
    assetCategoryId: asset?.assetCategoryId ?? '',
    criticality: asset?.criticality ?? '',
    operationalStatus: asset?.operationalStatus ?? 'Operational',
    manufacturer: asset?.manufacturer ?? '',
    model: asset?.model ?? '',
    serialNumber: asset?.serialNumber ?? '',
    installationDate: dateOnly(asset?.installationDate),
    commissioningDate: dateOnly(asset?.commissioningDate),
    warrantyStartDate: dateOnly(asset?.warrantyStartDate),
    warrantyExpiryDate: dateOnly(asset?.warrantyExpiryDate),
    lostProductionPerHour: asset?.lostProductionPerHour?.toString() ?? '',
    externalId: asset?.externalId ?? '',
    externalSystem: asset?.externalSystem ?? '',
    notes: asset?.notes ?? '',
    custom: Object.fromEntries((asset?.customFieldValues ?? []).map((value) => [value.fieldDefinitionId, value.value])),
  }
}

/** Create or edit a factory asset. A component (with a parent) may leave its location blank to take the parent's. */
export function FactoryAssetFormDrawer({ open, onClose, asset, defaultOrgNodeId, defaultParent }: {
  open: boolean
  onClose: () => void
  asset?: FactoryAsset | undefined
  defaultOrgNodeId?: string | undefined
  defaultParent?: { id: string; label: string } | undefined
}) {
  const navigate = useNavigate()
  const save = useSaveFactoryAsset()
  const { data: classes } = useAssetClasses()
  const { data: settings } = useFactorySettings()
  const [form, setForm] = useState<FormState>(() => initialState(asset, { orgNodeId: defaultOrgNodeId, parent: defaultParent }))

  useEffect(() => {
    if (open) setForm(initialState(asset, { orgNodeId: defaultOrgNodeId, parent: defaultParent }))
  }, [open, asset, defaultOrgNodeId, defaultParent])

  const set = <K extends keyof FormState>(key: K, value: FormState[K]) => setForm((current) => ({ ...current, [key]: value }))
  const selectedClass = classes?.find((item) => item.id === form.assetCategoryId)
  const classGroups = useMemo(() => groupClasses(classes ?? []), [classes])

  const submit = async () => {
    try {
      const saved = await save.mutateAsync({
        id: asset?.id,
        payload: {
          clientId: null, branchId: null, siteId: null, systemId: null,
          assetCategoryId: form.assetCategoryId || null,
          assetName: form.assetName.trim(),
          assetCode: form.assetCode.trim() || null,
          assetType: selectedClass?.name ?? null,
          ownershipType: 'Owned',
          location: null,
          serialNumber: form.serialNumber || null,
          manufacturer: form.manufacturer || null,
          model: form.model || null,
          installationDate: form.installationDate || null,
          warrantyExpiryDate: form.warrantyExpiryDate || null,
          warrantyStartDate: form.warrantyStartDate || null,
          warrantyPeriodMonths: null,
          recommendedPmFrequency: asset?.recommendedPmFrequency ?? null,
          pmFrequencyValue: null,
          pmFrequencyUnit: null,
          autoSchedulePm: false,
          lastPmDate: asset?.lastPmDate ?? null,
          nextPmDate: asset?.nextPmDate ?? null,
          notes: form.notes || null,
          status: asset?.status ?? 'Active',
          customFieldValues: (selectedClass?.fields ?? []).filter((field) => field.isActive).map((field) => ({ fieldDefinitionId: field.id, value: form.custom[field.id] || null })),
          orgNodeId: form.orgNodeId || null,
          parentAssetId: form.parent?.id ?? null,
          criticality: form.criticality || null,
          operationalStatus: form.operationalStatus,
          lostProductionPerHour: form.lostProductionPerHour ? Number(form.lostProductionPerHour) : null,
          commissioningDate: form.commissioningDate || null,
          externalId: form.externalId || null,
          externalSystem: form.externalSystem || null,
        },
      })
      toast.success(asset ? 'Asset saved' : `${saved.assetCode} added`)
      onClose()
      if (!asset) navigate(`/assets/${saved.id}`)
    } catch {
      // The API client shows the error.
    }
  }

  const missingLocation = !form.orgNodeId && !form.parent

  return (
    <FormDrawer
      open={open}
      title={asset ? `Edit ${asset.assetCode}` : form.parent ? 'Add component' : 'Add asset'}
      onClose={onClose}
      closeOnOverlayClick={false}
      footer={
        <div className="flex justify-end gap-2">
          <button type="button" className={secondaryButton} onClick={onClose}>Cancel</button>
          <button type="button" className={primaryButton} disabled={save.isPending || !form.assetName.trim() || missingLocation} onClick={() => void submit()}>
            {asset ? 'Save' : 'Add asset'}
          </button>
        </div>
      }
    >
      <div className="grid gap-4 sm:grid-cols-2">
        <Field label="Name" required className="sm:col-span-2">
          <input className={inputClass} value={form.assetName} onChange={(event) => set('assetName', event.target.value)} placeholder="e.g. Fluid bed dryer 1" />
        </Field>
        <Field label="Asset number" hint={asset ? undefined : 'Leave blank to number it automatically.'}>
          <input className={`${inputClass} font-mono`} value={form.assetCode} onChange={(event) => set('assetCode', event.target.value)} />
        </Field>
        <Field label="Asset class" hint={selectedClass?.defaultCriticality ? `Suggests criticality ${selectedClass.defaultCriticality}.` : undefined}>
          <select className={inputClass} value={form.assetCategoryId} onChange={(event) => set('assetCategoryId', event.target.value)}>
            <option value="">No class</option>
            {classGroups.map(([group, items]) => (
              <optgroup key={group} label={group}>
                {items.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
              </optgroup>
            ))}
          </select>
        </Field>
        <Field label="Location" required={!form.parent} hint={form.parent ? "Leave blank to use the parent's location." : 'Factory, section or sub-section.'}>
          <LocationSelect value={form.orgNodeId} onChange={(value) => set('orgNodeId', value)} emptyLabel={form.parent ? "Parent's location" : 'Choose a location'} />
        </Field>
        <Field label="Part of (parent asset)" hint="For a component such as a motor within a fan.">
          <ParentPicker value={form.parent} excludeId={asset?.id} onChange={(value) => set('parent', value)} />
        </Field>
        <Field label="Criticality" hint="A: stops production. B: reduces it. C: little effect.">
          <select className={inputClass} value={form.criticality} onChange={(event) => set('criticality', event.target.value as FormState['criticality'])}>
            <option value="">{selectedClass?.defaultCriticality && !asset ? `Class default (${selectedClass.defaultCriticality})` : 'Not set'}</option>
            {CRITICALITIES.map((value) => <option key={value} value={value}>{value}</option>)}
          </select>
        </Field>
        <Field label="Condition">
          <select className={inputClass} value={form.operationalStatus} onChange={(event) => set('operationalStatus', event.target.value as OperationalStatus)}>
            {OPERATIONAL_STATUSES.map((value) => <option key={value} value={value}>{value}</option>)}
          </select>
        </Field>
        <Field label="Manufacturer"><input className={inputClass} value={form.manufacturer} onChange={(event) => set('manufacturer', event.target.value)} /></Field>
        <Field label="Model"><input className={inputClass} value={form.model} onChange={(event) => set('model', event.target.value)} /></Field>
        <Field label="Serial number"><input className={inputClass} value={form.serialNumber} onChange={(event) => set('serialNumber', event.target.value)} /></Field>
        <Field label={`Production lost per hour down (${settings?.productionUnitShort ?? 'units'})`}>
          <input type="number" min={0} className={inputClass} value={form.lostProductionPerHour} onChange={(event) => set('lostProductionPerHour', event.target.value)} />
        </Field>
        <Field label="Installed"><input type="date" className={inputClass} value={form.installationDate} onChange={(event) => set('installationDate', event.target.value)} /></Field>
        <Field label="Commissioned"><input type="date" className={inputClass} value={form.commissioningDate} onChange={(event) => set('commissioningDate', event.target.value)} /></Field>
        <Field label="Warranty starts"><input type="date" className={inputClass} value={form.warrantyStartDate} onChange={(event) => set('warrantyStartDate', event.target.value)} /></Field>
        <Field label="Warranty ends"><input type="date" className={inputClass} value={form.warrantyExpiryDate} onChange={(event) => set('warrantyExpiryDate', event.target.value)} /></Field>

        {selectedClass && selectedClass.fields.some((field) => field.isActive) ? (
          <div className="space-y-3 sm:col-span-2">
            <h4 className="border-t border-slate-100 pt-3 text-sm font-semibold text-slate-900">{selectedClass.name} details</h4>
            <div className="grid gap-4 sm:grid-cols-2">
              {selectedClass.fields.filter((field) => field.isActive).sort((a, b) => a.displayOrder - b.displayOrder).map((field) => (
                <ClassFieldInput key={field.id} field={field} value={form.custom[field.id] ?? ''} onChange={(value) => set('custom', { ...form.custom, [field.id]: value })} />
              ))}
            </div>
          </div>
        ) : null}

        <Field label="ERP ID" hint="The asset's number in your ERP, if any."><input className={inputClass} value={form.externalId} onChange={(event) => set('externalId', event.target.value)} /></Field>
        <Field label="ERP system"><input className={inputClass} value={form.externalSystem} onChange={(event) => set('externalSystem', event.target.value)} /></Field>
        <Field label="Notes" className="sm:col-span-2"><textarea className={textareaClass} value={form.notes} onChange={(event) => set('notes', event.target.value)} /></Field>
      </div>
    </FormDrawer>
  )
}

function ClassFieldInput({ field, value, onChange }: { field: AssetClassField; value: string; onChange: (value: string) => void }) {
  const label = field.unit ? `${field.fieldLabel} (${field.unit})` : field.fieldLabel
  if (field.fieldType === 'Dropdown') {
    const options = (field.dropdownOptions ?? '').split(',').map((option) => option.trim()).filter(Boolean)
    return (
      <Field label={label} required={field.isRequired}>
        <select className={inputClass} value={value} onChange={(event) => onChange(event.target.value)}>
          <option value="">—</option>
          {options.map((option) => <option key={option} value={option}>{option}</option>)}
        </select>
      </Field>
    )
  }
  const type = field.fieldType === 'Number' ? 'number' : field.fieldType === 'Date' ? 'date' : 'text'
  return (
    <Field label={label} required={field.isRequired}>
      <input type={type} className={inputClass} value={value} onChange={(event) => onChange(event.target.value)} />
    </Field>
  )
}

function ParentPicker({ value, excludeId, onChange }: { value: { id: string; label: string } | null; excludeId?: string | undefined; onChange: (value: { id: string; label: string } | null) => void }) {
  const [search, setSearch] = useState('')
  const debounced = useDebounce(search, 250)
  const { data, isFetching } = useAssetSearch(debounced, debounced.trim().length >= 2)
  const results = (data?.items ?? []).filter((item) => item.id !== excludeId)

  if (value) {
    return (
      <div className="flex h-10 items-center justify-between gap-2 rounded-lg border border-slate-200 bg-slate-50 px-3 text-sm">
        <span className="truncate">{value.label}</span>
        <button type="button" aria-label="Remove parent" className="text-slate-500 hover:text-slate-900" onClick={() => onChange(null)}><X className="h-4 w-4" /></button>
      </div>
    )
  }

  return (
    <div className="relative">
      <input className={inputClass} placeholder="Search by name or number" value={search} onChange={(event) => setSearch(event.target.value)} />
      {debounced.trim().length >= 2 ? (
        <ul className="absolute z-10 mt-1 max-h-56 w-full overflow-y-auto rounded-lg border border-slate-200 bg-white shadow-lg">
          {results.length === 0 ? <li className="px-3 py-2 text-sm text-slate-500">{isFetching ? 'Searching…' : 'No matching assets'}</li> : results.map((item) => (
            <li key={item.id}>
              <button type="button" className="w-full px-3 py-2 text-left text-sm hover:bg-slate-50" onClick={() => { onChange({ id: item.id, label: `${item.assetCode} · ${item.assetName}` }); setSearch('') }}>
                <span className="font-mono text-xs text-slate-500">{item.assetCode}</span> {item.assetName}
                {item.orgNodeName ? <span className="block text-xs text-slate-400">{item.orgNodeName}</span> : null}
              </button>
            </li>
          ))}
        </ul>
      ) : null}
    </div>
  )
}

function groupClasses(classes: AssetClass[]): [string, AssetClass[]][] {
  const groups = new Map<string, AssetClass[]>()
  for (const item of classes.filter((x) => x.isActive)) {
    const key = item.parentCategoryName ?? 'Other'
    groups.set(key, [...(groups.get(key) ?? []), item])
  }
  return [...groups.entries()].map(([group, items]) => [group, items.sort((a, b) => a.name.localeCompare(b.name))])
}
