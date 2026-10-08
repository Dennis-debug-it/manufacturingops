import { useEffect } from 'react'
import { addDays, addMonths, addWeeks, addYears, format } from 'date-fns'
import { useForm, Controller } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import toast from 'react-hot-toast'
import { FormDrawer } from '../../../shared/components/FormDrawer'
import { applyApiFieldErrors } from '../../../shared/api/apiError'
import { useCreateAsset, useUpdateAsset, useClients, useSites, type UpsertAssetPayload } from '../api'
import { assetSchema, type AssetFormValues } from '../schemas'
import type { Asset } from '../types'

const fieldClass = 'h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none transition focus:border-[var(--app-primary)]'
const labelClass = 'mb-1.5 block text-sm font-medium text-slate-700'
const errorClass = 'mt-1 text-xs text-rose-600'

/**
 * The fields a server-side 400 is allowed to blame — each renders an error line below, so a pinned
 * message is visible. `UpsertAssetRequest` names its properties in PascalCase; the helper
 * normalises the casing before matching. Properties this form never exposes (
 * `BranchId`, `OwnershipType`) are toasted rather than silently dropped.
 */
const FORM_FIELDS = [
  'clientId',
  'siteId',
  'assetName',
  'assetCode',
  'assetType',
  'location',
  'serialNumber',
  'manufacturer',
  'model',
  'installationDate',
  'pmFrequencyValue',
  'pmFrequencyUnit',
  'lastPmDate',
  'warrantyStartDate',
  'warrantyPeriodMonths',
  'warrantyExpiryDate',
  'notes',
] as const satisfies readonly (keyof AssetFormValues)[]

export interface AssetFormDrawerProps {
  open: boolean
  onClose: () => void
  defaultClientId?: string
  defaultSiteId?: string
  asset?: Asset
}

const PM_FREQUENCY_UNITS = [
  { value: 'days', label: 'Days' },
  { value: 'weeks', label: 'Weeks' },
  { value: 'months', label: 'Months' },
  { value: 'quarters', label: 'Quarters' },
  { value: 'years', label: 'Years' },
] as const

function toDateInput(value: string | null | undefined) {
  return value ? value.slice(0, 10) : ''
}

function toIsoDate(value: string | undefined) {
  return value ? new Date(value).toISOString() : null
}

function parseDateInput(value: string | undefined) {
  if (!value) return null
  const [year, month, day] = value.split('-').map(Number)
  const date = new Date(year!, month! - 1, day!)
  return Number.isNaN(date.getTime()) ? null : date
}

function parsePositiveInt(value: string | undefined) {
  const trimmed = value?.trim()
  return trimmed && /^\d+$/.test(trimmed) && Number(trimmed) > 0 ? Number(trimmed) : null
}

/** Mirrors the server's next-PM calculation so the form can preview the due date. */
function nextMaintenanceDate(base: Date | null, value: number | null, unit: string | undefined) {
  if (!base || !value || !unit) return null
  switch (unit) {
    case 'days': return addDays(base, value)
    case 'weeks': return addWeeks(base, value)
    case 'months': return addMonths(base, value)
    case 'quarters': return addMonths(base, value * 3)
    case 'years': return addYears(base, value)
    default: return null
  }
}

function toFormValues(asset: Asset | undefined, defaultClientId?: string, defaultSiteId?: string): AssetFormValues {
  return {
    clientId: asset?.clientId ?? defaultClientId ?? '',
    siteId: asset?.siteId ?? defaultSiteId ?? '',
    assetName: asset?.assetName ?? '',
    assetCode: asset?.assetCode ?? '',
    assetType: asset?.assetType ?? '',
    location: asset?.location ?? '',
    serialNumber: asset?.serialNumber ?? '',
    manufacturer: asset?.manufacturer ?? '',
    model: asset?.model ?? '',
    installationDate: toDateInput(asset?.installationDate),
    pmFrequencyValue: asset?.pmFrequencyValue ? String(asset.pmFrequencyValue) : '',
    pmFrequencyUnit: asset?.pmFrequencyUnit ?? '',
    lastPmDate: toDateInput(asset?.lastPmDate),
    autoSchedulePm: asset?.autoSchedulePm ?? false,
    hasWarranty: Boolean(asset?.warrantyExpiryDate || asset?.warrantyPeriodMonths),
    warrantyStartDate: toDateInput(asset?.warrantyStartDate),
    warrantyPeriodMonths: asset?.warrantyPeriodMonths ? String(asset.warrantyPeriodMonths) : '',
    warrantyExpiryDate: toDateInput(asset?.warrantyExpiryDate),
    notes: asset?.notes ?? '',
  }
}

export function AssetFormDrawer({ open, onClose, defaultClientId, defaultSiteId, asset }: AssetFormDrawerProps) {
  const isEdit = Boolean(asset)
  const { control, register, handleSubmit, watch, reset, setError, setValue, getValues, formState: { errors, isSubmitting } } = useForm<AssetFormValues>({
    resolver: zodResolver(assetSchema),
    defaultValues: toFormValues(asset, defaultClientId, defaultSiteId),
  })
  const clientId = watch('clientId')
  const installationDate = watch('installationDate')
  const pmFrequencyValue = watch('pmFrequencyValue')
  const pmFrequencyUnit = watch('pmFrequencyUnit')
  const lastPmDate = watch('lastPmDate')
  const hasWarranty = watch('hasWarranty')

  const previewNextPm = nextMaintenanceDate(
    parseDateInput(lastPmDate || installationDate),
    parsePositiveInt(pmFrequencyValue),
    pmFrequencyUnit,
  )
  const { data: clients } = useClients()
  const { data: sites } = useSites(clientId || undefined)
  const createAsset = useCreateAsset()
  const updateAsset = useUpdateAsset(asset?.id ?? '')

  useEffect(() => {
    if (open) {
      reset(toFormValues(asset, defaultClientId, defaultSiteId))
    }
  }, [open, asset, defaultClientId, defaultSiteId, reset])

  // Editing the warranty start, period, or installation date fills in the expiry (start + period).
  // It runs on user edits only, so an expiry the user overrode survives reopening the form.
  const recalculateWarrantyExpiry = () => {
    const values = getValues()
    if (!values.hasWarranty) return
    const start = parseDateInput(values.warrantyStartDate || values.installationDate)
    const months = parsePositiveInt(values.warrantyPeriodMonths)
    if (!start || !months) return
    setValue('warrantyExpiryDate', format(addMonths(start, months), 'yyyy-MM-dd'))
  }

  const close = () => {
    reset()
    onClose()
  }

  const onSubmit = handleSubmit(async (values) => {
    const frequencyValue = parsePositiveInt(values.pmFrequencyValue)
    const frequencyUnit = frequencyValue ? values.pmFrequencyUnit || null : null
    const lastPm = toIsoDate(values.lastPmDate)
    // Completing a PM work order advances the asset's next due date; keep it unless the user
    // changed the inputs it is derived from, in which case the server recalculates it.
    const scheduleUnchanged = Boolean(asset)
      && toDateInput(asset?.lastPmDate) === (values.lastPmDate ?? '')
      && toDateInput(asset?.installationDate) === (values.installationDate ?? '')
      && (asset?.pmFrequencyValue ?? null) === frequencyValue
      && (asset?.pmFrequencyUnit ?? null) === frequencyUnit
    const payload: UpsertAssetPayload = {
      clientId: values.clientId,
      siteId: values.siteId || null,
      assetName: values.assetName.trim(),
      // Blank = let the server generate one from the Asset numbering rule (on edit: keep the current code).
      assetCode: values.assetCode?.trim() || null,
      assetType: values.assetType?.trim() || null,
      location: values.location?.trim() || null,
      serialNumber: values.serialNumber?.trim() || null,
      manufacturer: values.manufacturer?.trim() || null,
      model: values.model?.trim() || null,
      installationDate: toIsoDate(values.installationDate),
      warrantyStartDate: values.hasWarranty ? toIsoDate(values.warrantyStartDate) : null,
      warrantyPeriodMonths: values.hasWarranty ? parsePositiveInt(values.warrantyPeriodMonths) : null,
      warrantyExpiryDate: values.hasWarranty ? toIsoDate(values.warrantyExpiryDate) : null,
      // A legacy free-text frequency (e.g. "Monthly") is kept until a structured one replaces it.
      recommendedPmFrequency: frequencyValue ? null : asset?.recommendedPmFrequency ?? null,
      pmFrequencyValue: frequencyValue,
      pmFrequencyUnit: frequencyUnit,
      autoSchedulePm: values.autoSchedulePm,
      lastPmDate: lastPm,
      nextPmDate: scheduleUnchanged ? asset?.nextPmDate ?? null : null,
      notes: values.notes?.trim() || null,
      branchId: asset?.branchId ?? null,
      systemId: asset?.systemId ?? null,
      assetCategoryId: asset?.assetCategoryId ?? null,
      ownershipType: asset?.ownershipType ?? null,
      status: asset?.status ?? null,
      customFieldValues: (asset?.customFieldValues ?? []).map(({ fieldDefinitionId, value }) => ({ fieldDefinitionId, value })),
    }
    try {
      if (isEdit) {
        const updated = await updateAsset.mutateAsync(payload)
        toast.success(`Asset "${updated.assetName}" updated`)
      } else {
        const created = await createAsset.mutateAsync(payload)
        toast.success(`Asset "${created.assetName}" created`)
      }
      close()
    } catch (error) {
      // A model-validation 400 names the field it rejected; pin the message to that input instead
      // of leaving the user with only the interceptor's one-line toast.
      applyApiFieldErrors<AssetFormValues>(error, { setError, fields: FORM_FIELDS })
    }
  })

  return (
    <FormDrawer
      open={open}
      title={isEdit ? 'Edit Asset' : 'New Asset'}
      onClose={close}
      footer={
        <div className="flex items-center justify-end gap-2">
          <button type="button" onClick={close} className="inline-flex h-11 items-center justify-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">Cancel</button>
          <button type="submit" form="asset-form" disabled={isSubmitting} className="inline-flex h-11 items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:cursor-not-allowed disabled:opacity-60">
            {isSubmitting ? 'Saving…' : isEdit ? 'Save Changes' : 'Create Asset'}
          </button>
        </div>
      }
    >
      <form id="asset-form" onSubmit={onSubmit} className="space-y-4">
        <div>
          <label className={labelClass} htmlFor="asset-client">Client</label>
          <Controller
            control={control}
            name="clientId"
            render={({ field }) => (
              <select id="asset-client" {...field} disabled={Boolean(defaultClientId)} className={fieldClass}>
                <option value="">Select a client</option>
                {(clients ?? []).map((client) => (
                  <option key={client.id} value={client.id}>{client.clientName}</option>
                ))}
              </select>
            )}
          />
          {errors.clientId ? <p className={errorClass}>{errors.clientId.message}</p> : null}
        </div>
        <div>
          <label className={labelClass} htmlFor="asset-site">Site</label>
          <Controller
            control={control}
            name="siteId"
            render={({ field }) => (
              <select id="asset-site" {...field} disabled={!clientId || Boolean(defaultSiteId)} className={fieldClass}>
                <option value="">No site</option>
                {(sites ?? []).map((site) => (
                  <option key={site.id} value={site.id}>{site.siteName}</option>
                ))}
              </select>
            )}
          />
          {errors.siteId ? <p className={errorClass}>{errors.siteId.message}</p> : null}
        </div>
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className={labelClass} htmlFor="asset-name">Asset Name</label>
            <input id="asset-name" {...register('assetName')} className={fieldClass} />
            {errors.assetName ? <p className={errorClass}>{errors.assetName.message}</p> : null}
          </div>
          <div>
            <label className={labelClass} htmlFor="asset-code">Asset Code</label>
            <input id="asset-code" {...register('assetCode')} placeholder={isEdit ? undefined : 'Auto-generated if blank'} className={fieldClass} />
            {errors.assetCode ? <p className={errorClass}>{errors.assetCode.message}</p> : null}
          </div>
        </div>
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className={labelClass} htmlFor="asset-type">Asset Type</label>
            <input id="asset-type" {...register('assetType')} className={fieldClass} />
            {errors.assetType ? <p className={errorClass}>{errors.assetType.message}</p> : null}
          </div>
          <div>
            <label className={labelClass} htmlFor="asset-location">Location</label>
            <input id="asset-location" {...register('location')} className={fieldClass} />
            {errors.location ? <p className={errorClass}>{errors.location.message}</p> : null}
          </div>
        </div>
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className={labelClass} htmlFor="asset-manufacturer">Manufacturer</label>
            <input id="asset-manufacturer" {...register('manufacturer')} className={fieldClass} />
            {errors.manufacturer ? <p className={errorClass}>{errors.manufacturer.message}</p> : null}
          </div>
          <div>
            <label className={labelClass} htmlFor="asset-model">Model</label>
            <input id="asset-model" {...register('model')} className={fieldClass} />
            {errors.model ? <p className={errorClass}>{errors.model.message}</p> : null}
          </div>
        </div>
        <div>
          <label className={labelClass} htmlFor="asset-serial">Serial Number</label>
          <input id="asset-serial" {...register('serialNumber')} className={fieldClass} />
          {errors.serialNumber ? <p className={errorClass}>{errors.serialNumber.message}</p> : null}
        </div>
        <div>
          <label className={labelClass} htmlFor="asset-install-date">Installation Date</label>
          <input id="asset-install-date" type="date" {...register('installationDate', { onChange: recalculateWarrantyExpiry })} className={fieldClass} />
          {errors.installationDate ? <p className={errorClass}>{errors.installationDate.message}</p> : null}
        </div>

        <fieldset className="space-y-3 rounded-xl border border-slate-200 p-4">
          <legend className="px-1 text-sm font-semibold text-slate-900">Maintenance</legend>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className={labelClass} htmlFor="asset-pm-frequency">Maintenance Every</label>
              <input id="asset-pm-frequency" type="number" min={1} step={1} inputMode="numeric" placeholder="e.g. 3" {...register('pmFrequencyValue')} className={fieldClass} />
              {errors.pmFrequencyValue ? <p className={errorClass}>{errors.pmFrequencyValue.message}</p> : null}
            </div>
            <div>
              <label className={labelClass} htmlFor="asset-pm-unit">Unit</label>
              <select id="asset-pm-unit" {...register('pmFrequencyUnit')} className={fieldClass}>
                <option value="">Select unit</option>
                {PM_FREQUENCY_UNITS.map((unit) => (
                  <option key={unit.value} value={unit.value}>{unit.label}</option>
                ))}
              </select>
              {errors.pmFrequencyUnit ? <p className={errorClass}>{errors.pmFrequencyUnit.message}</p> : null}
            </div>
          </div>
          {asset?.recommendedPmFrequency && !asset.pmFrequencyValue ? (
            <p className="text-xs text-slate-500">Current frequency: {asset.recommendedPmFrequency}. Set a value and unit to enable due-date tracking.</p>
          ) : null}
          <div>
            <label className={labelClass} htmlFor="asset-last-pm">Last Maintenance Date</label>
            <input id="asset-last-pm" type="date" max={format(new Date(), 'yyyy-MM-dd')} {...register('lastPmDate')} className={fieldClass} />
            {errors.lastPmDate ? <p className={errorClass}>{errors.lastPmDate.message}</p> : null}
          </div>
          <p className="text-xs text-slate-500">
            {previewNextPm
              ? <>Next maintenance due <span className="font-medium text-slate-700">{format(previewNextPm, 'dd MMM yyyy')}</span>{lastPmDate ? '' : ' (from installation date)'}.</>
              : 'Set a frequency and a last maintenance or installation date to calculate the next due date.'}
          </p>
          <label className="flex items-center gap-2 text-sm text-slate-700">
            <input type="checkbox" {...register('autoSchedulePm')} className="h-4 w-4 rounded border-slate-300" />
            Auto-schedule preventive maintenance
          </label>
        </fieldset>

        <fieldset className="space-y-3 rounded-xl border border-slate-200 p-4">
          <legend className="px-1 text-sm font-semibold text-slate-900">Warranty</legend>
          <label className="flex items-center gap-2 text-sm text-slate-700">
            <input type="checkbox" {...register('hasWarranty', { onChange: recalculateWarrantyExpiry })} className="h-4 w-4 rounded border-slate-300" />
            Asset is covered by a warranty
          </label>
          {hasWarranty ? (
            <>
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className={labelClass} htmlFor="asset-warranty-start">Warranty Start</label>
                  <input id="asset-warranty-start" type="date" {...register('warrantyStartDate', { onChange: recalculateWarrantyExpiry })} className={fieldClass} />
                  {errors.warrantyStartDate
                    ? <p className={errorClass}>{errors.warrantyStartDate.message}</p>
                    : <p className="mt-1 text-xs text-slate-500">Defaults to the installation date.</p>}
                </div>
                <div>
                  <label className={labelClass} htmlFor="asset-warranty-period">Warranty Period (months)</label>
                  <input id="asset-warranty-period" type="number" min={1} max={600} step={1} inputMode="numeric" placeholder="e.g. 12" {...register('warrantyPeriodMonths', { onChange: recalculateWarrantyExpiry })} className={fieldClass} />
                  {errors.warrantyPeriodMonths ? <p className={errorClass}>{errors.warrantyPeriodMonths.message}</p> : null}
                </div>
              </div>
              <div>
                <label className={labelClass} htmlFor="asset-warranty-date">Warranty Expiry</label>
                <input id="asset-warranty-date" type="date" {...register('warrantyExpiryDate')} className={fieldClass} />
                {errors.warrantyExpiryDate
                  ? <p className={errorClass}>{errors.warrantyExpiryDate.message}</p>
                  : <p className="mt-1 text-xs text-slate-500">Calculated from start + period; you can override it.</p>}
              </div>
            </>
          ) : null}
        </fieldset>
        <div>
          <label className={labelClass} htmlFor="asset-notes">Notes</label>
          <textarea id="asset-notes" {...register('notes')} rows={3} className={`${fieldClass} h-auto py-2`} />
          {errors.notes ? <p className={errorClass}>{errors.notes.message}</p> : null}
        </div>
      </form>
    </FormDrawer>
  )
}
