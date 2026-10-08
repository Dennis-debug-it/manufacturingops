import { useEffect, useMemo, useState } from 'react'
import { useForm, Controller } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import toast from 'react-hot-toast'
import { AlertTriangle } from 'lucide-react'
import { useNavigate } from 'react-router-dom'
import { FormDrawer } from '../../../shared/components/FormDrawer'
import { useDebounce } from '../../../shared/hooks/useDebounce'
import { cn } from '../../../shared/utils/cn'
import { applyApiFieldErrors } from '../../../shared/api/apiError'
import { useCreateWorkOrder, useRepeatVisitCheckForAsset, useWorkOrderTemplates } from '../api'
import { useAssetOptions, useClientOptions, useSiteOptions } from '../pickerApi'
import { AssignmentFields, emptyAssignment, toCreateAssignmentPayload, type AssignmentValue } from './AssignmentFields'
import { newWorkOrderSchema, priorityOptions, workTypeOptions, type NewWorkOrderFormValues } from '../schemas'

const fieldClass = 'h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none transition focus:border-[var(--app-primary)]'
const labelClass = 'mb-1.5 block text-sm font-medium text-slate-700'
const errorClass = 'mt-1 text-xs text-rose-600'

/**
 * The server blames the request DTO's PascalCase property names; the helper normalises casing
 * before matching. Anything blamed that isn't listed here is toasted instead of being lost.
 */
const WORK_ORDER_FORM_FIELDS = [
  'clientId',
  'siteId',
  'assetId',
  'title',
  'workType',
  'priority',
  'dueDate',
  'description',
] as const satisfies readonly (keyof NewWorkOrderFormValues)[]

export interface NewWorkOrderFormProps {
  open: boolean
  onClose: () => void
  parentWorkOrderId?: string | null
}

export function NewWorkOrderForm({ open, onClose, parentWorkOrderId = null }: NewWorkOrderFormProps) {
  const navigate = useNavigate()
  const [clientSearch, setClientSearch] = useState('')
  const [templateId, setTemplateId] = useState('')
  const [assignment, setAssignment] = useState<AssignmentValue>(emptyAssignment)
  const { data: templates } = useWorkOrderTemplates({ activeOnly: true })
  const debouncedClientSearch = useDebounce(clientSearch, 250)

  const {
    control,
    register,
    handleSubmit,
    watch,
    reset,
    getValues,
    setValue,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<NewWorkOrderFormValues>({
    resolver: zodResolver(newWorkOrderSchema),
    defaultValues: {
      clientId: '',
      siteId: '',
      assetId: '',
      title: '',
      workType: 'CorrectiveMaintenance',
      priority: 'Medium',
      dueDate: '',
      description: '',
    },
  })

  useEffect(() => {
    if (!open) {
      return
    }

    reset({
      clientId: '',
      siteId: '',
      assetId: '',
      title: '',
      workType: 'CorrectiveMaintenance',
      priority: 'Medium',
      dueDate: '',
      description: '',
    })
    setTemplateId('')
    setAssignment(emptyAssignment)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open])

  const clientId = watch('clientId')
  const siteId = watch('siteId')
  const assetId = watch('assetId')

  const { data: clients, isLoading: clientsLoading } = useClientOptions(debouncedClientSearch)
  const { data: sites, isLoading: sitesLoading } = useSiteOptions(clientId || undefined)
  const { data: assets, isLoading: assetsLoading } = useAssetOptions(clientId || undefined, siteId || undefined)
  const { data: repeatVisitCheck } = useRepeatVisitCheckForAsset(assetId || undefined)

  const createWorkOrder = useCreateWorkOrder()

  const clientOptions = useMemo(() => clients ?? [], [clients])

  /**
   * Picking a template pre-fills type, priority, title and description (without overwriting what the
   * user already typed); the server then attaches the template's checklist and planned materials.
   */
  const applyTemplate = (id: string) => {
    setTemplateId(id)
    const template = (templates ?? []).find((item) => item.id === id)
    if (!template) return
    if (workTypeOptions.some((option) => option.value === template.jobType)) {
      setValue('workType', template.jobType as NewWorkOrderFormValues['workType'])
    }
    if (priorityOptions.some((option) => option.value === template.defaultPriority)) {
      setValue('priority', template.defaultPriority as NewWorkOrderFormValues['priority'])
    }
    if (!getValues('title')?.trim()) setValue('title', template.name, { shouldValidate: true })
    if (!getValues('description')?.trim() && template.description) setValue('description', template.description)
  }

  const closeAndReset = () => {
    reset()
    setTemplateId('')
    setAssignment(emptyAssignment)
    setClientSearch('')
    onClose()
  }

  const onSubmit = handleSubmit(async (values) => {
    try {
      const created = await createWorkOrder.mutateAsync({
        clientId: values.clientId,
        siteId: values.siteId || null,
        assetId: values.assetId || null,
        title: values.title.trim(),
        description: values.description?.trim() || null,
        priority: values.priority,
        workType: values.workType,
        isPreventiveMaintenance: values.workType === 'PreventiveMaintenance',
        dueDate: values.dueDate ? new Date(values.dueDate).toISOString() : null,
        ...toCreateAssignmentPayload(assignment),
        notifyClient: true,
        parentWorkOrderId: parentWorkOrderId ?? null,
        templateId: templateId || null,
      })
      toast.success(`Work order ${created.workOrderNumber} created`)
      closeAndReset()
      navigate(`/work-orders/${created.id}`)
    } catch (error) {
      // A model-validation 400 names the field it rejected; pin the message to that input instead
      // of leaving the user with only the interceptor's one-line toast.
      applyApiFieldErrors<NewWorkOrderFormValues>(error, { setError, fields: WORK_ORDER_FORM_FIELDS })
    }
  })

  return (
    <FormDrawer
      open={open}
      title={parentWorkOrderId ? 'New Sub-task' : 'New Work Order'}
      onClose={closeAndReset}
      footer={
        <div className="flex items-center justify-end gap-2">
          <button
            type="button"
            onClick={closeAndReset}
            className="inline-flex h-11 items-center justify-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50"
          >
            Cancel
          </button>
          <button
            type="submit"
            form="new-work-order-form"
            disabled={isSubmitting}
            className="inline-flex h-11 items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:cursor-not-allowed disabled:opacity-60"
          >
            {isSubmitting ? 'Creating…' : 'Create Work Order'}
          </button>
        </div>
      }
    >
      <form id="new-work-order-form" onSubmit={onSubmit} className="space-y-4">
        <div>
          <label className={labelClass} htmlFor="wo-client">Client</label>
          <input
            id="wo-client-search"
            placeholder="Search clients…"
            value={clientSearch}
            onChange={(event) => setClientSearch(event.target.value)}
            className={cn(fieldClass, 'mb-2')}
          />
          <Controller
            control={control}
            name="clientId"
            render={({ field }) => (
              <select id="wo-client" {...field} className={fieldClass}>
                <option value="">{clientsLoading ? 'Loading clients…' : 'Select a client'}</option>
                {clientOptions.map((client) => (
                  <option key={client.id} value={client.id}>{client.clientName}</option>
                ))}
              </select>
            )}
          />
          {errors.clientId ? <p className={errorClass}>{errors.clientId.message}</p> : null}
        </div>

        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className={labelClass} htmlFor="wo-site">Site</label>
            <Controller
              control={control}
              name="siteId"
              render={({ field }) => (
                <select id="wo-site" {...field} disabled={!clientId} className={cn(fieldClass, !clientId && 'cursor-not-allowed bg-slate-50 text-slate-400')}>
                  <option value="">{!clientId ? 'Select a client first' : sitesLoading ? 'Loading sites…' : 'No site'}</option>
                  {(sites ?? []).map((site) => (
                    <option key={site.id} value={site.id}>{site.siteName}</option>
                  ))}
                </select>
              )}
            />
          </div>
          <div>
            <label className={labelClass} htmlFor="wo-asset">Asset</label>
            <Controller
              control={control}
              name="assetId"
              render={({ field }) => (
                <select id="wo-asset" {...field} disabled={!clientId} className={cn(fieldClass, !clientId && 'cursor-not-allowed bg-slate-50 text-slate-400')}>
                  <option value="">{!clientId ? 'Select a client first' : assetsLoading ? 'Loading assets…' : 'No asset'}</option>
                  {(assets ?? []).map((asset) => (
                    <option key={asset.id} value={asset.id}>{asset.assetName}</option>
                  ))}
                </select>
              )}
            />
          </div>
        </div>

        {repeatVisitCheck?.isRepeatVisit ? (
          <div className="flex items-start gap-2 rounded-xl border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-800">
            <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
            <span>
              This asset has had {repeatVisitCheck.repeatVisitCount} completed jobs in the last 30 days. Consider escalating the underlying fault.
            </span>
          </div>
        ) : null}

        <div>
          <label className={labelClass} htmlFor="wo-template">Template</label>
          <select id="wo-template" value={templateId} onChange={(event) => applyTemplate(event.target.value)} className={fieldClass}>
            <option value="">No template</option>
            {(templates ?? []).map((template) => (
              <option key={template.id} value={template.id}>
                {template.name}{template.checklistTemplateName ? ` · ${template.checklistTemplateName}` : ''}
              </option>
            ))}
          </select>
          {(templates ?? []).length === 0 ? <p className="mt-1 text-xs text-slate-500">Create reusable job templates in Settings → Work Order Templates.</p> : null}
        </div>

        <div>
          <label className={labelClass} htmlFor="wo-title">Title</label>
          <input id="wo-title" {...register('title')} placeholder="Short summary of the job" className={fieldClass} />
          {errors.title ? <p className={errorClass}>{errors.title.message}</p> : null}
        </div>

        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className={labelClass} htmlFor="wo-type">Type</label>
            <select id="wo-type" {...register('workType')} className={fieldClass}>
              {workTypeOptions.map((option) => (
                <option key={option.value} value={option.value}>{option.label}</option>
              ))}
            </select>
          </div>
          <div>
            <label className={labelClass} htmlFor="wo-priority">Priority</label>
            <select id="wo-priority" {...register('priority')} className={fieldClass}>
              {priorityOptions.map((option) => (
                <option key={option.value} value={option.value}>{option.label}</option>
              ))}
            </select>
          </div>
        </div>

        <div>
          <label className={labelClass} htmlFor="wo-due-date">Due Date</label>
          <input id="wo-due-date" type="date" {...register('dueDate')} className={fieldClass} />
        </div>

        <AssignmentFields idPrefix="wo-assign" value={assignment} onChange={setAssignment} />

        <div>
          <label className={labelClass} htmlFor="wo-description">Description</label>
          <textarea id="wo-description" {...register('description')} rows={4} placeholder="Additional details for the technician" className={cn(fieldClass, 'h-auto py-2')} />
          {errors.description ? <p className={errorClass}>{errors.description.message}</p> : null}
        </div>
      </form>
    </FormDrawer>
  )
}
