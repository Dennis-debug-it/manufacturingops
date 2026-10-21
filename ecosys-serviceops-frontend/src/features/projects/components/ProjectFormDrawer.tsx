import { useEffect } from 'react'
import { useForm } from 'react-hook-form'
import toast from 'react-hot-toast'
import { useNavigate } from 'react-router-dom'
import { FormDrawer } from '../../../shared/components/FormDrawer'
import { applyApiFieldErrors } from '../../../shared/api/apiError'
import { getActiveCurrency } from '../../../shared/utils/currency'
import { useClients, useSites } from '../../clients/api'
import { useCreateProject, useProjectPeople, useProjectTemplates, useProjectTypes, useUpdateProject, type UpsertProjectPayload } from '../api'
import type { ProjectDetail } from '../types'
import { dateInputValue, dateOrNull, fieldClass, labelClass, primaryButtonClass, secondaryButtonClass, textAreaClass, textOrNull } from './formStyles'

interface FormValues {
  name: string
  clientId: string
  siteId: string
  projectTypeId: string
  templateId: string
  projectManagerUserId: string
  originalValue: string
  currency: string
  plannedStartDate: string
  plannedEndDate: string
  description: string
  visibleInClientPortal: boolean
}

const PROJECT_FORM_FIELDS = [
  'name', 'clientId', 'siteId', 'projectTypeId', 'projectManagerUserId', 'originalValue', 'currency', 'plannedStartDate', 'plannedEndDate', 'description',
] as const satisfies readonly (keyof FormValues)[]

const emptyValues = (): FormValues => ({
  name: '',
  clientId: '',
  siteId: '',
  projectTypeId: '',
  templateId: '',
  projectManagerUserId: '',
  originalValue: '',
  currency: getActiveCurrency(),
  plannedStartDate: '',
  plannedEndDate: '',
  description: '',
  visibleInClientPortal: true,
})

const fromProject = (project: ProjectDetail): FormValues => ({
  name: project.summary.name,
  clientId: project.summary.clientId,
  siteId: project.summary.siteId ?? '',
  projectTypeId: project.summary.projectTypeId,
  templateId: '',
  projectManagerUserId: project.summary.projectManagerUserId ?? '',
  originalValue: String(project.summary.originalValue),
  currency: project.summary.currency,
  plannedStartDate: dateInputValue(project.summary.plannedStartDate),
  plannedEndDate: dateInputValue(project.summary.plannedEndDate),
  description: project.description ?? '',
  visibleInClientPortal: project.visibleInClientPortal,
})

/** Create a project, or edit one when `project` is given. The client can't change after creation. */
export function ProjectFormDrawer({ open, onClose, project }: { open: boolean; onClose: () => void; project?: ProjectDetail }) {
  const navigate = useNavigate()
  const editing = Boolean(project)
  const { register, handleSubmit, watch, reset, setValue, setError, formState: { errors, isSubmitting } } = useForm<FormValues>({ defaultValues: emptyValues() })
  const clientId = watch('clientId')
  const projectTypeId = watch('projectTypeId')
  const { data: clients } = useClients()
  const { data: sites } = useSites(clientId || undefined)
  const { data: types } = useProjectTypes()
  const { data: templates } = useProjectTemplates()
  const { data: people } = useProjectPeople(open)
  const createProject = useCreateProject()
  const updateProject = useUpdateProject(project?.summary.id ?? '')

  useEffect(() => {
    if (open) reset(project ? fromProject(project) : emptyValues())
  }, [open, project, reset])

  // A type's default template is a starting suggestion when creating a project.
  useEffect(() => {
    if (editing || !projectTypeId) return
    const defaultTemplate = types?.find((type) => type.id === projectTypeId)?.defaultTemplateId
    if (defaultTemplate) setValue('templateId', defaultTemplate)
  }, [editing, projectTypeId, types, setValue])

  const close = () => {
    reset(emptyValues())
    onClose()
  }

  const onSubmit = handleSubmit(async (values) => {
    const payload: UpsertProjectPayload = {
      name: values.name.trim(),
      clientId: values.clientId,
      siteId: values.siteId || null,
      projectTypeId: values.projectTypeId || null,
      projectManagerUserId: values.projectManagerUserId || null,
      originalValue: Number(values.originalValue || 0),
      currency: values.currency.trim().toUpperCase() || getActiveCurrency(),
      plannedStartDate: dateOrNull(values.plannedStartDate),
      plannedEndDate: dateOrNull(values.plannedEndDate),
      description: textOrNull(values.description),
      visibleInClientPortal: values.visibleInClientPortal,
      ...(editing ? {} : { templateId: values.templateId || null }),
    }
    try {
      if (project) {
        await updateProject.mutateAsync(payload)
        toast.success('Project updated')
        close()
      } else {
        const created = await createProject.mutateAsync(payload)
        toast.success(`Project ${created.summary.projectNumber} created`)
        close()
        navigate(`/projects/${created.summary.id}`)
      }
    } catch (error) {
      applyApiFieldErrors<FormValues>(error, { setError, fields: PROJECT_FORM_FIELDS })
    }
  })

  const activeTypes = (types ?? []).filter((type) => type.isActive || type.id === project?.summary.projectTypeId)

  return (
    <FormDrawer
      open={open}
      title={editing ? `Edit ${project?.summary.projectNumber}` : 'New Project'}
      onClose={close}
      footer={
        <div className="flex items-center justify-end gap-2">
          <button type="button" onClick={close} className={secondaryButtonClass}>Cancel</button>
          <button type="submit" form="project-form" disabled={isSubmitting} className={primaryButtonClass}>
            {isSubmitting ? 'Saving…' : editing ? 'Save Changes' : 'Create Project'}
          </button>
        </div>
      }
    >
      <form id="project-form" onSubmit={onSubmit} className="space-y-4">
        <div>
          <label className={labelClass} htmlFor="project-name">Project name</label>
          <input id="project-name" {...register('name', { required: true })} className={fieldClass} placeholder="Westlands HVAC Installation" />
          {errors.name ? <p className="mt-1 text-xs text-rose-600">{errors.name.message || 'Required'}</p> : null}
        </div>
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          <div>
            <label className={labelClass} htmlFor="project-client">Client</label>
            <select id="project-client" {...register('clientId', { required: true, onChange: () => setValue('siteId', '') })} disabled={editing} className={fieldClass}>
              <option value="">Select a client</option>
              {(clients ?? []).filter((client) => client.isActive || client.id === project?.summary.clientId).map((client) => (
                <option key={client.id} value={client.id}>{client.clientName}</option>
              ))}
            </select>
            {errors.clientId ? <p className="mt-1 text-xs text-rose-600">{errors.clientId.message || 'Required'}</p> : null}
          </div>
          <div>
            <label className={labelClass} htmlFor="project-site">Site</label>
            <select id="project-site" {...register('siteId')} disabled={!clientId} className={fieldClass}>
              <option value="">No specific site</option>
              {(sites ?? []).map((site) => (<option key={site.id} value={site.id}>{site.siteName}</option>))}
            </select>
          </div>
          <div>
            <label className={labelClass} htmlFor="project-type">Project type</label>
            <select id="project-type" {...register('projectTypeId')} className={fieldClass}>
              <option value="">Default type</option>
              {activeTypes.map((type) => (<option key={type.id} value={type.id}>{type.name}</option>))}
            </select>
          </div>
          {!editing ? (
            <div>
              <label className={labelClass} htmlFor="project-template">Template</label>
              <select id="project-template" {...register('templateId')} className={fieldClass}>
                <option value="">No template</option>
                {(templates ?? []).filter((template) => template.isActive).map((template) => (<option key={template.id} value={template.id}>{template.name}</option>))}
              </select>
            </div>
          ) : null}
          <div>
            <label className={labelClass} htmlFor="project-manager">Project manager</label>
            <select id="project-manager" {...register('projectManagerUserId')} className={fieldClass}>
              <option value="">Not assigned</option>
              {(people?.users ?? []).map((user) => (<option key={user.id} value={user.id}>{user.fullName}</option>))}
            </select>
          </div>
        </div>
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
          <div className="sm:col-span-2">
            <label className={labelClass} htmlFor="project-value">Contract value</label>
            <input id="project-value" type="number" min="0" step="0.01" inputMode="decimal" {...register('originalValue', { required: true })} className={fieldClass} />
            {errors.originalValue ? <p className="mt-1 text-xs text-rose-600">{errors.originalValue.message || 'Required'}</p> : null}
            {editing ? <p className="mt-1 text-xs text-slate-500">The original value. Additions and omissions are recorded as variations.</p> : null}
          </div>
          <div>
            <label className={labelClass} htmlFor="project-currency">Currency</label>
            <input id="project-currency" maxLength={3} {...register('currency')} className={fieldClass} />
          </div>
          <div>
            <label className={labelClass} htmlFor="project-start">Planned start</label>
            <input id="project-start" type="date" {...register('plannedStartDate')} className={fieldClass} />
          </div>
          <div>
            <label className={labelClass} htmlFor="project-end">Planned end</label>
            <input id="project-end" type="date" {...register('plannedEndDate')} className={fieldClass} />
            {errors.plannedEndDate ? <p className="mt-1 text-xs text-rose-600">{errors.plannedEndDate.message}</p> : null}
          </div>
        </div>
        <div>
          <label className={labelClass} htmlFor="project-description">Scope / description</label>
          <textarea id="project-description" rows={3} {...register('description')} className={textAreaClass} />
        </div>
        <label className="flex items-start gap-2 text-sm text-slate-700">
          <input type="checkbox" {...register('visibleInClientPortal')} className="mt-0.5 h-4 w-4 rounded border-slate-300" />
          <span>Show this project to the client in their portal (progress, milestones, approved variations and shared documents only).</span>
        </label>
      </form>
    </FormDrawer>
  )
}
