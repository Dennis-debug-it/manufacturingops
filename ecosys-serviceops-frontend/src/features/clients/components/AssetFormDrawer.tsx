import { useForm, Controller } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import toast from 'react-hot-toast'
import { FormDrawer } from '../../../shared/components/FormDrawer'
import { useCreateAsset, useClients, useSites } from '../api'
import { assetSchema, type AssetFormValues } from '../schemas'

const fieldClass = 'h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none transition focus:border-slate-900'
const labelClass = 'mb-1.5 block text-sm font-medium text-slate-700'
const errorClass = 'mt-1 text-xs text-rose-600'

export interface AssetFormDrawerProps {
  open: boolean
  onClose: () => void
  defaultClientId?: string
  defaultSiteId?: string
}

export function AssetFormDrawer({ open, onClose, defaultClientId, defaultSiteId }: AssetFormDrawerProps) {
  const { control, register, handleSubmit, watch, reset, formState: { errors, isSubmitting } } = useForm<AssetFormValues>({
    resolver: zodResolver(assetSchema),
    defaultValues: {
      clientId: defaultClientId ?? '',
      siteId: defaultSiteId ?? '',
      assetName: '',
      assetType: '',
      location: '',
      serialNumber: '',
      manufacturer: '',
      model: '',
      installationDate: '',
      warrantyExpiryDate: '',
      notes: '',
    },
  })
  const clientId = watch('clientId')
  const { data: clients } = useClients()
  const { data: sites } = useSites(clientId || undefined)
  const createAsset = useCreateAsset()

  const close = () => {
    reset()
    onClose()
  }

  const onSubmit = handleSubmit(async (values) => {
    try {
      const created = await createAsset.mutateAsync({
        clientId: values.clientId,
        siteId: values.siteId || null,
        assetName: values.assetName.trim(),
        assetType: values.assetType?.trim() || null,
        location: values.location?.trim() || null,
        serialNumber: values.serialNumber?.trim() || null,
        manufacturer: values.manufacturer?.trim() || null,
        model: values.model?.trim() || null,
        installationDate: values.installationDate ? new Date(values.installationDate).toISOString() : null,
        warrantyExpiryDate: values.warrantyExpiryDate ? new Date(values.warrantyExpiryDate).toISOString() : null,
        notes: values.notes?.trim() || null,
      })
      toast.success(`Asset "${created.assetName}" created`)
      close()
    } catch {
      // interceptor surfaces the error toast
    }
  })

  return (
    <FormDrawer
      open={open}
      title="New Asset"
      onClose={close}
      footer={
        <div className="flex items-center justify-end gap-2">
          <button type="button" onClick={close} className="inline-flex h-11 items-center justify-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">Cancel</button>
          <button type="submit" form="asset-form" disabled={isSubmitting} className="inline-flex h-11 items-center justify-center rounded-lg bg-slate-900 px-4 text-sm font-medium text-white transition hover:bg-slate-800 disabled:cursor-not-allowed disabled:opacity-60">
            {isSubmitting ? 'Creating…' : 'Create Asset'}
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
        </div>
        <div>
          <label className={labelClass} htmlFor="asset-name">Asset Name</label>
          <input id="asset-name" {...register('assetName')} className={fieldClass} />
          {errors.assetName ? <p className={errorClass}>{errors.assetName.message}</p> : null}
        </div>
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className={labelClass} htmlFor="asset-type">Asset Type</label>
            <input id="asset-type" {...register('assetType')} className={fieldClass} />
          </div>
          <div>
            <label className={labelClass} htmlFor="asset-location">Location</label>
            <input id="asset-location" {...register('location')} className={fieldClass} />
          </div>
        </div>
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className={labelClass} htmlFor="asset-manufacturer">Manufacturer</label>
            <input id="asset-manufacturer" {...register('manufacturer')} className={fieldClass} />
          </div>
          <div>
            <label className={labelClass} htmlFor="asset-model">Model</label>
            <input id="asset-model" {...register('model')} className={fieldClass} />
          </div>
        </div>
        <div>
          <label className={labelClass} htmlFor="asset-serial">Serial Number</label>
          <input id="asset-serial" {...register('serialNumber')} className={fieldClass} />
        </div>
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className={labelClass} htmlFor="asset-install-date">Installation Date</label>
            <input id="asset-install-date" type="date" {...register('installationDate')} className={fieldClass} />
          </div>
          <div>
            <label className={labelClass} htmlFor="asset-warranty-date">Warranty Expiry</label>
            <input id="asset-warranty-date" type="date" {...register('warrantyExpiryDate')} className={fieldClass} />
          </div>
        </div>
        <div>
          <label className={labelClass} htmlFor="asset-notes">Notes</label>
          <textarea id="asset-notes" {...register('notes')} rows={3} className={`${fieldClass} h-auto py-2`} />
        </div>
      </form>
    </FormDrawer>
  )
}
