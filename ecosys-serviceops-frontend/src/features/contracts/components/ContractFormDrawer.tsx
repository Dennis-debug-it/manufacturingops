import { useState } from 'react'
import { useForm, Controller } from 'react-hook-form'
import toast from 'react-hot-toast'
import { useNavigate } from 'react-router-dom'
import { FormDrawer } from '../../../shared/components/FormDrawer'
import { useClients, useAssets } from '../../clients/api'
import { useCreateContract } from '../api'

const fieldClass = 'h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none transition focus:border-slate-900'
const labelClass = 'mb-1.5 block text-sm font-medium text-slate-700'

const contractTypes = ['ServiceAgreement', 'MaintenanceContract', 'SLA', 'ProjectBased']
const billingModels = ['Hourly', 'FixedPrice', 'TimeAndMaterials', 'Retainer', 'Callout', 'FixedVisitFee']

interface FormValues {
  contractNumber: string
  clientId: string
  name: string
  contractType: string
  startDate: string
  endDate: string
  billingModel: string
  currency: string
  contractValue: string
  renewalNoticeDays: string
  responseTimeHours: string
  resolutionTimeHours: string
  description: string
}

export function ContractFormDrawer({ open, onClose }: { open: boolean; onClose: () => void }) {
  const navigate = useNavigate()
  const { control, register, handleSubmit, watch, reset, formState: { errors, isSubmitting } } = useForm<FormValues>({
    defaultValues: {
      contractNumber: `CNT-${Date.now()}`,
      clientId: '',
      name: '',
      contractType: 'ServiceAgreement',
      startDate: '',
      endDate: '',
      billingModel: 'FixedPrice',
      currency: 'KES',
      contractValue: '',
      renewalNoticeDays: '30',
      responseTimeHours: '',
      resolutionTimeHours: '',
      description: '',
    },
  })
  const clientId = watch('clientId')
  const { data: clients } = useClients()
  const { data: assets } = useAssets(clientId ? { clientId } : undefined)
  const [assetIds, setAssetIds] = useState<string[]>([])
  const createContract = useCreateContract()

  const close = () => {
    reset()
    setAssetIds([])
    onClose()
  }

  const onSubmit = handleSubmit(async (values) => {
    if (!values.clientId || !values.startDate || !values.endDate) {
      toast.error('Client, start date, and end date are required')
      return
    }
    try {
      const created = await createContract.mutateAsync({
        contractNumber: values.contractNumber.trim(),
        clientId: values.clientId,
        name: values.name.trim(),
        contractType: values.contractType,
        status: 'Draft',
        startDate: new Date(values.startDate).toISOString(),
        endDate: new Date(values.endDate).toISOString(),
        billingModel: values.billingModel,
        currency: values.currency,
        contractValue: values.contractValue ? Number(values.contractValue) : null,
        renewalNoticeDays: Number(values.renewalNoticeDays) || 30,
        description: values.description.trim() || null,
        gracePeriodDays: 0,
        allowPreferentialRatesDuringGracePeriod: false,
        allowSlaDuringGracePeriod: false,
        gracePeriodAllowsSla: false,
        pauseSlaOnHold: true,
        responseTimeHours: values.responseTimeHours ? Number(values.responseTimeHours) : null,
        resolutionTimeHours: values.resolutionTimeHours ? Number(values.resolutionTimeHours) : null,
        siteIds: [],
        assetIds,
      })
      toast.success(`Contract ${created.contractNumber} created`)
      close()
      navigate(`/contracts/${created.id}`)
    } catch {
      // interceptor surfaces the error toast
    }
  })

  return (
    <FormDrawer
      open={open}
      title="New Contract"
      onClose={close}
      footer={
        <div className="flex items-center justify-end gap-2">
          <button type="button" onClick={close} className="inline-flex h-11 items-center justify-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">Cancel</button>
          <button type="submit" form="contract-form" disabled={isSubmitting} className="inline-flex h-11 items-center justify-center rounded-lg bg-slate-900 px-4 text-sm font-medium text-white transition hover:bg-slate-800 disabled:opacity-60">
            {isSubmitting ? 'Creating…' : 'Create Contract'}
          </button>
        </div>
      }
    >
      <form id="contract-form" onSubmit={onSubmit} className="space-y-4">
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className={labelClass} htmlFor="contract-number">Contract Number</label>
            <input id="contract-number" {...register('contractNumber', { required: true })} className={fieldClass} />
          </div>
          <div>
            <label className={labelClass} htmlFor="contract-type">Type</label>
            <select id="contract-type" {...register('contractType')} className={fieldClass}>
              {contractTypes.map((type) => (<option key={type} value={type}>{type}</option>))}
            </select>
          </div>
        </div>
        <div>
          <label className={labelClass} htmlFor="contract-name">Name</label>
          <input id="contract-name" {...register('name', { required: true })} className={fieldClass} />
          {errors.name ? <p className="mt-1 text-xs text-rose-600">Required</p> : null}
        </div>
        <div>
          <label className={labelClass} htmlFor="contract-client">Client</label>
          <Controller control={control} name="clientId" render={({ field }) => (
            <select id="contract-client" {...field} className={fieldClass}>
              <option value="">Select a client</option>
              {(clients ?? []).map((client) => (<option key={client.id} value={client.id}>{client.clientName}</option>))}
            </select>
          )} />
        </div>
        <div>
          <label className={labelClass} htmlFor="contract-assets">Covered Assets</label>
          <select
            id="contract-assets"
            multiple
            disabled={!clientId}
            value={assetIds}
            onChange={(event) => setAssetIds(Array.from(event.target.selectedOptions, (option) => option.value))}
            className="h-28 w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-slate-900 disabled:bg-slate-50"
          >
            {(assets ?? []).map((asset) => (<option key={asset.id} value={asset.id}>{asset.assetName}</option>))}
          </select>
        </div>
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className={labelClass} htmlFor="contract-start">Start Date</label>
            <input id="contract-start" type="date" {...register('startDate', { required: true })} className={fieldClass} />
          </div>
          <div>
            <label className={labelClass} htmlFor="contract-end">End Date</label>
            <input id="contract-end" type="date" {...register('endDate', { required: true })} className={fieldClass} />
          </div>
        </div>
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className={labelClass} htmlFor="contract-billing">Billing Model</label>
            <select id="contract-billing" {...register('billingModel')} className={fieldClass}>
              {billingModels.map((model) => (<option key={model} value={model}>{model}</option>))}
            </select>
          </div>
          <div>
            <label className={labelClass} htmlFor="contract-value">Contract Value</label>
            <input id="contract-value" type="number" step="0.01" {...register('contractValue')} className={fieldClass} />
          </div>
        </div>
        <div>
          <label className={labelClass} htmlFor="contract-renewal-notice">Renewal Notice (days)</label>
          <input id="contract-renewal-notice" type="number" {...register('renewalNoticeDays')} className={fieldClass} />
        </div>
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className={labelClass} htmlFor="contract-response">Response Time (hrs)</label>
            <input id="contract-response" type="number" step="0.5" {...register('responseTimeHours')} className={fieldClass} />
          </div>
          <div>
            <label className={labelClass} htmlFor="contract-resolution">Resolution Time (hrs)</label>
            <input id="contract-resolution" type="number" step="0.5" {...register('resolutionTimeHours')} className={fieldClass} />
          </div>
        </div>
        <div>
          <label className={labelClass} htmlFor="contract-description">Description</label>
          <textarea id="contract-description" {...register('description')} rows={3} className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-slate-900" />
        </div>
      </form>
    </FormDrawer>
  )
}
