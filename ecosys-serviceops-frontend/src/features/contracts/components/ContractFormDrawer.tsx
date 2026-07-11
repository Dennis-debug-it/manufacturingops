import { useState } from 'react'
import { useForm, Controller } from 'react-hook-form'
import { useQuery } from '@tanstack/react-query'
import toast from 'react-hot-toast'
import { useNavigate } from 'react-router-dom'
import { FormDrawer } from '../../../shared/components/FormDrawer'
import { api } from '../../../shared/api/apiClient'
import { endpoints } from '../../../shared/api/endpoints'
import { useClients, useAssets } from '../../clients/api'
import { useCreateContract } from '../api'

const fieldClass = 'h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none transition focus:border-slate-900'
const labelClass = 'mb-1.5 block text-sm font-medium text-slate-700'

const contractTypes = ['ServiceAgreement', 'MaintenanceContract', 'SLA', 'ProjectBased']
const billingModels = ['Hourly', 'FixedPrice', 'TimeAndMaterials', 'Retainer', 'Callout', 'FixedVisitFee']
const slaPriorities = ['Critical', 'High', 'Medium', 'Low'] as const

interface AssetCategoryOption {
  id: string
  name: string
}

function useAssetCategoryOptions() {
  return useQuery({
    queryKey: ['asset-categories'],
    queryFn: () => api.get<AssetCategoryOption[]>(endpoints.assetCategories.list),
    staleTime: 60_000,
  })
}

type SlaMatrixState = Record<(typeof slaPriorities)[number], { response: string; resolution: string }>

const emptySlaMatrix: SlaMatrixState = {
  Critical: { response: '', resolution: '' },
  High: { response: '', resolution: '' },
  Medium: { response: '', resolution: '' },
  Low: { response: '', resolution: '' },
}

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
  const { data: categories } = useAssetCategoryOptions()
  const [coverageMode, setCoverageMode] = useState<'categories' | 'assets'>('categories')
  const [assetIds, setAssetIds] = useState<string[]>([])
  const [categoryIds, setCategoryIds] = useState<string[]>([])
  const [slaMatrix, setSlaMatrix] = useState<SlaMatrixState>(emptySlaMatrix)
  const createContract = useCreateContract()

  const close = () => {
    reset()
    setAssetIds([])
    setCategoryIds([])
    setCoverageMode('categories')
    setSlaMatrix(emptySlaMatrix)
    onClose()
  }

  const setSlaCell = (priority: (typeof slaPriorities)[number], field: 'response' | 'resolution', value: string) => {
    setSlaMatrix((current) => ({ ...current, [priority]: { ...current[priority], [field]: value } }))
  }

  const toggleCategory = (id: string) => {
    setCategoryIds((current) => (current.includes(id) ? current.filter((x) => x !== id) : [...current, id]))
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
        assetIds: coverageMode === 'assets' ? assetIds : [],
        assetCategories:
          coverageMode === 'categories'
            ? categoryIds.map((id) => ({ assetCategoryId: id, assetCategoryName: (categories ?? []).find((c) => c.id === id)?.name ?? null }))
            : [],
        slaMatrix: slaPriorities
          .filter((priority) => slaMatrix[priority].response.trim() || slaMatrix[priority].resolution.trim())
          .map((priority) => ({
            priority,
            responseTimeHours: slaMatrix[priority].response.trim() ? Number(slaMatrix[priority].response) : null,
            resolutionTimeHours: slaMatrix[priority].resolution.trim() ? Number(slaMatrix[priority].resolution) : null,
          })),
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
          <span className={labelClass}>Coverage</span>
          <div className="mb-3 flex gap-2">
            {([
              ['categories', 'By asset category'],
              ['assets', 'Specific assets'],
            ] as const).map(([mode, label]) => (
              <button
                key={mode}
                type="button"
                onClick={() => setCoverageMode(mode)}
                className={`inline-flex h-10 flex-1 items-center justify-center rounded-lg border text-sm font-medium transition ${coverageMode === mode ? 'border-slate-900 bg-slate-900 text-white' : 'border-slate-200 bg-white text-slate-700 hover:bg-slate-50'}`}
              >
                {label}
              </button>
            ))}
          </div>
          {coverageMode === 'categories' ? (
            <>
              <div className="max-h-40 space-y-1 overflow-y-auto rounded-lg border border-slate-300 p-2">
                {(categories ?? []).map((category) => (
                  <label key={category.id} className="flex cursor-pointer items-center gap-2 rounded-md px-2 py-1.5 text-sm text-slate-700 hover:bg-slate-50">
                    <input type="checkbox" checked={categoryIds.includes(category.id)} onChange={() => toggleCategory(category.id)} className="h-4 w-4 rounded border-slate-300" />
                    {category.name}
                  </label>
                ))}
              </div>
              <p className="mt-1 text-xs text-slate-500">All of the client's assets in the selected categories are covered — including assets added later. No need to pick assets one by one.</p>
            </>
          ) : (
            <>
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
              <p className="mt-1 text-xs text-slate-500">Hold Ctrl/Cmd to select multiple assets. For large fleets, use category coverage instead.</p>
            </>
          )}
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
            <label className={labelClass} htmlFor="contract-response">Default Response Time (hrs)</label>
            <input id="contract-response" type="number" step="0.5" {...register('responseTimeHours')} className={fieldClass} />
          </div>
          <div>
            <label className={labelClass} htmlFor="contract-resolution">Default Resolution Time (hrs)</label>
            <input id="contract-resolution" type="number" step="0.5" {...register('resolutionTimeHours')} className={fieldClass} />
          </div>
        </div>
        <div>
          <span className={labelClass}>SLA by Priority (optional overrides)</span>
          <div className="overflow-hidden rounded-lg border border-slate-200">
            <table className="w-full text-sm">
              <thead className="bg-slate-50 text-left text-xs font-semibold uppercase tracking-wide text-slate-500">
                <tr>
                  <th className="px-3 py-2">Priority</th>
                  <th className="px-3 py-2">Response (hrs)</th>
                  <th className="px-3 py-2">Resolution (hrs)</th>
                </tr>
              </thead>
              <tbody>
                {slaPriorities.map((priority) => (
                  <tr key={priority} className="border-t border-slate-100">
                    <td className="px-3 py-2 font-medium text-slate-700">{priority}</td>
                    <td className="px-3 py-2">
                      <input
                        type="number"
                        step="0.5"
                        min="0"
                        aria-label={`${priority} response hours`}
                        value={slaMatrix[priority].response}
                        onChange={(event) => setSlaCell(priority, 'response', event.target.value)}
                        className="h-9 w-full rounded-md border border-slate-300 px-2 text-sm outline-none focus:border-slate-900"
                      />
                    </td>
                    <td className="px-3 py-2">
                      <input
                        type="number"
                        step="0.5"
                        min="0"
                        aria-label={`${priority} resolution hours`}
                        value={slaMatrix[priority].resolution}
                        onChange={(event) => setSlaCell(priority, 'resolution', event.target.value)}
                        className="h-9 w-full rounded-md border border-slate-300 px-2 text-sm outline-none focus:border-slate-900"
                      />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <p className="mt-1 text-xs text-slate-500">Rows left blank fall back to the default response/resolution times above.</p>
        </div>
        <div>
          <label className={labelClass} htmlFor="contract-description">Description</label>
          <textarea id="contract-description" {...register('description')} rows={3} className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-slate-900" />
        </div>
      </form>
    </FormDrawer>
  )
}
