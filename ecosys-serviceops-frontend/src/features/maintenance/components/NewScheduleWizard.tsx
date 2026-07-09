import { useState } from 'react'
import toast from 'react-hot-toast'
import { FormDrawer } from '../../../shared/components/FormDrawer'
import { cn } from '../../../shared/utils/cn'
import { useClients, useAssets } from '../../clients/api'
import { useCreatePmPlan, usePmTemplates } from '../api'

const fieldClass = 'h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none transition focus:border-slate-900'
const labelClass = 'mb-1.5 block text-sm font-medium text-slate-700'

const frequencyOptions = ['Monthly', 'Quarterly', 'Semi-Annual', 'Annual']

export interface NewScheduleWizardProps {
  open: boolean
  onClose: () => void
}

export function NewScheduleWizard({ open, onClose }: NewScheduleWizardProps) {
  const [step, setStep] = useState(1)
  const [clientId, setClientId] = useState('')
  const [assetId, setAssetId] = useState('')
  const [pmTemplateId, setPmTemplateId] = useState('')
  const [frequency, setFrequency] = useState('Quarterly')
  const [serviceIntervalMonths, setServiceIntervalMonths] = useState('3')
  const [nextPmDate, setNextPmDate] = useState('')
  const [autoSchedule, setAutoSchedule] = useState(true)

  const { data: clients } = useClients()
  const { data: assets } = useAssets(clientId ? { clientId } : undefined)
  const { data: templates } = usePmTemplates()
  const createPlan = useCreatePmPlan()

  const selectedAsset = (assets ?? []).find((asset) => asset.id === assetId)
  const selectedTemplate = (templates ?? []).find((template) => template.id === pmTemplateId)

  const close = () => {
    setStep(1)
    setClientId('')
    setAssetId('')
    setPmTemplateId('')
    setFrequency('Quarterly')
    setServiceIntervalMonths('3')
    setNextPmDate('')
    setAutoSchedule(true)
    onClose()
  }

  const submit = async () => {
    if (!assetId) {
      toast.error('Select an asset')
      return
    }
    try {
      await createPlan.mutateAsync({
        assetId,
        pmTemplateId: pmTemplateId || null,
        serviceIntervalMonths: serviceIntervalMonths ? Number(serviceIntervalMonths) : null,
        frequency,
        autoSchedule,
        nextPmDate: nextPmDate ? new Date(nextPmDate).toISOString() : null,
        status: 'Active',
      })
      toast.success('PM schedule created')
      close()
    } catch {
      // interceptor surfaces the error toast
    }
  }

  const stepLabels = ['Asset & Coverage', 'Template & Schedule', 'Assignment & WO Settings']

  return (
    <FormDrawer
      open={open}
      title="New PM Schedule"
      onClose={close}
      footer={
        <div className="flex items-center justify-between gap-2">
          <button type="button" onClick={step === 1 ? close : () => setStep((value) => value - 1)} className="inline-flex h-11 items-center justify-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">
            {step === 1 ? 'Cancel' : 'Back'}
          </button>
          {step < 3 ? (
            <button
              type="button"
              onClick={() => setStep((value) => value + 1)}
              disabled={step === 1 && !assetId}
              className="inline-flex h-11 items-center justify-center rounded-lg bg-slate-900 px-4 text-sm font-medium text-white transition hover:bg-slate-800 disabled:cursor-not-allowed disabled:opacity-60"
            >
              Next
            </button>
          ) : (
            <button type="button" onClick={submit} disabled={createPlan.isPending} className="inline-flex h-11 items-center justify-center rounded-lg bg-slate-900 px-4 text-sm font-medium text-white transition hover:bg-slate-800 disabled:opacity-60">
              {createPlan.isPending ? 'Creating…' : 'Create Schedule'}
            </button>
          )}
        </div>
      }
    >
      <div className="mb-6 flex items-center gap-2">
        {stepLabels.map((label, index) => (
          <div key={label} className="flex flex-1 items-center gap-2">
            <div className={cn('flex h-7 w-7 shrink-0 items-center justify-center rounded-full text-xs font-semibold', step === index + 1 ? 'bg-slate-900 text-white' : step > index + 1 ? 'bg-emerald-100 text-emerald-700' : 'bg-slate-100 text-slate-500')}>
              {index + 1}
            </div>
            <span className={cn('text-xs font-medium', step === index + 1 ? 'text-slate-900' : 'text-slate-400')}>{label}</span>
          </div>
        ))}
      </div>

      {step === 1 ? (
        <div className="space-y-4">
          <div>
            <label className={labelClass} htmlFor="wizard-client">Client</label>
            <select id="wizard-client" value={clientId} onChange={(event) => { setClientId(event.target.value); setAssetId('') }} className={fieldClass}>
              <option value="">Select a client</option>
              {(clients ?? []).map((client) => (
                <option key={client.id} value={client.id}>{client.clientName}</option>
              ))}
            </select>
          </div>
          <div>
            <label className={labelClass} htmlFor="wizard-asset">Asset</label>
            <select id="wizard-asset" value={assetId} onChange={(event) => setAssetId(event.target.value)} disabled={!clientId} className={cn(fieldClass, !clientId && 'cursor-not-allowed bg-slate-50 text-slate-400')}>
              <option value="">{clientId ? 'Select an asset' : 'Select a client first'}</option>
              {(assets ?? []).map((asset) => (
                <option key={asset.id} value={asset.id}>{asset.assetName} ({asset.assetCode})</option>
              ))}
            </select>
            {selectedAsset ? <p className="mt-1 text-xs text-slate-500">{selectedAsset.siteName ?? 'No site'} · {selectedAsset.assetType ?? 'Unspecified type'}</p> : null}
          </div>
        </div>
      ) : null}

      {step === 2 ? (
        <div className="space-y-4">
          <div>
            <label className={labelClass} htmlFor="wizard-template">Checklist Template</label>
            <select id="wizard-template" value={pmTemplateId} onChange={(event) => setPmTemplateId(event.target.value)} className={fieldClass}>
              <option value="">No template</option>
              {(templates ?? []).filter((template) => template.isActive).map((template) => (
                <option key={template.id} value={template.id}>{template.name}</option>
              ))}
            </select>
            {selectedTemplate ? <p className="mt-1 text-xs text-slate-500">{selectedTemplate.checklist.length} checklist item(s) · {selectedTemplate.category}</p> : null}
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className={labelClass} htmlFor="wizard-frequency">Frequency</label>
              <select id="wizard-frequency" value={frequency} onChange={(event) => setFrequency(event.target.value)} className={fieldClass}>
                {frequencyOptions.map((option) => (
                  <option key={option} value={option}>{option}</option>
                ))}
              </select>
            </div>
            <div>
              <label className={labelClass} htmlFor="wizard-interval">Interval (months)</label>
              <input id="wizard-interval" type="number" min="1" value={serviceIntervalMonths} onChange={(event) => setServiceIntervalMonths(event.target.value)} className={fieldClass} />
            </div>
          </div>
          <div>
            <label className={labelClass} htmlFor="wizard-next-date">Next Due Date</label>
            <input id="wizard-next-date" type="date" value={nextPmDate} onChange={(event) => setNextPmDate(event.target.value)} className={fieldClass} />
          </div>
          <label className="flex items-center gap-2 text-sm text-slate-700">
            <input type="checkbox" checked={autoSchedule} onChange={(event) => setAutoSchedule(event.target.checked)} className="h-4 w-4 rounded border-slate-300" />
            Auto-schedule follow-up work orders
          </label>
        </div>
      ) : null}

      {step === 3 ? (
        <div className="space-y-4">
          <div className="rounded-2xl border border-dashed border-slate-300 bg-slate-50 p-4 text-sm text-slate-600">
            Technician/assignment-group assignment is not a stored field on the PM plan itself — each work order this schedule
            generates goes through the normal Work Order Assignment flow when it's created. Review the schedule below, then
            create it; you'll assign a technician on the generated work order from the Work Orders module.
          </div>
          <dl className="grid grid-cols-1 gap-3 text-sm">
            <div className="flex justify-between rounded-lg bg-slate-50 px-3 py-2"><dt className="text-slate-500">Asset</dt><dd className="font-medium text-slate-900">{selectedAsset?.assetName ?? '—'}</dd></div>
            <div className="flex justify-between rounded-lg bg-slate-50 px-3 py-2"><dt className="text-slate-500">Template</dt><dd className="font-medium text-slate-900">{selectedTemplate?.name ?? 'No template'}</dd></div>
            <div className="flex justify-between rounded-lg bg-slate-50 px-3 py-2"><dt className="text-slate-500">Frequency</dt><dd className="font-medium text-slate-900">{frequency} ({serviceIntervalMonths} mo)</dd></div>
            <div className="flex justify-between rounded-lg bg-slate-50 px-3 py-2"><dt className="text-slate-500">Next Due</dt><dd className="font-medium text-slate-900">{nextPmDate || 'Not set'}</dd></div>
            <div className="flex justify-between rounded-lg bg-slate-50 px-3 py-2"><dt className="text-slate-500">Auto-Schedule</dt><dd className="font-medium text-slate-900">{autoSchedule ? 'Yes' : 'No'}</dd></div>
          </dl>
        </div>
      ) : null}
    </FormDrawer>
  )
}
