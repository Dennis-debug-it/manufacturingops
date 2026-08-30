import { useEffect } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import toast from 'react-hot-toast'
import { FormDrawer } from '../../../shared/components/FormDrawer'
import { applyApiFieldErrors } from '../../../shared/api/apiError'
import { useCreateClient, useUpdateClient } from '../api'
import { clientSchema, type ClientFormValues } from '../schemas'
import type { Client } from '../types'

const fieldClass = 'h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none transition focus:border-[var(--app-primary)]'
const labelClass = 'mb-1.5 block text-sm font-medium text-slate-700'
const errorClass = 'mt-1 text-xs text-rose-600'

const emptyValues: ClientFormValues = { clientName: '', clientType: '', email: '', phone: '', location: '', contactPerson: '', contactPhone: '', notes: '' }

/**
 * The fields a server-side 400 is allowed to blame — every one of them renders an error line below,
 * so a message pinned here is actually visible. `UpsertClientRequest` names its properties in
 * PascalCase; the helper normalises the casing before matching. Anything the server blames that
 * isn't listed (e.g. `IsActive`, which this form never exposes) is toasted instead of being lost.
 */
const FORM_FIELDS = [
  'clientName',
  'clientType',
  'email',
  'phone',
  'location',
  'contactPerson',
  'contactPhone',
  'notes',
] as const satisfies readonly (keyof ClientFormValues)[]

export interface ClientFormDrawerProps {
  open: boolean
  onClose: () => void
  onCreated?: (clientId: string) => void
  client?: Client | null
}

export function ClientFormDrawer({ open, onClose, onCreated, client }: ClientFormDrawerProps) {
  const isEdit = Boolean(client)
  const { register, handleSubmit, reset, setError, formState: { errors, isSubmitting } } = useForm<ClientFormValues>({
    resolver: zodResolver(clientSchema),
    defaultValues: emptyValues,
  })
  const createClient = useCreateClient()
  const updateClient = useUpdateClient(client?.id ?? '')

  useEffect(() => {
    if (!open) {
      return
    }
    reset(client ? {
      clientName: client.clientName,
      clientType: client.clientType ?? '',
      email: client.email ?? '',
      phone: client.phone ?? '',
      location: client.location ?? '',
      contactPerson: client.contactPerson ?? '',
      contactPhone: client.contactPhone ?? '',
      notes: client.notes ?? '',
    } : emptyValues)
  }, [open, client, reset])

  const close = () => {
    reset(emptyValues)
    onClose()
  }

  const onSubmit = handleSubmit(async (values) => {
    const payload = {
      clientName: values.clientName.trim(),
      clientType: values.clientType?.trim() || null,
      email: values.email?.trim() || null,
      phone: values.phone?.trim() || null,
      location: values.location?.trim() || null,
      contactPerson: values.contactPerson?.trim() || null,
      contactPhone: values.contactPhone?.trim() || null,
      notes: values.notes?.trim() || null,
    }

    try {
      if (isEdit && client) {
        const updated = await updateClient.mutateAsync({ ...payload, isActive: client.isActive })
        toast.success(`Client "${updated.clientName}" updated`)
        close()
      } else {
        const created = await createClient.mutateAsync({ ...payload, isActive: true })
        toast.success(`Client "${created.clientName}" created`)
        close()
        onCreated?.(created.id)
      }
    } catch (error) {
      // A model-validation 400 names the field it rejected; pin the message to that input instead
      // of leaving the user with only the interceptor's one-line toast.
      applyApiFieldErrors<ClientFormValues>(error, { setError, fields: FORM_FIELDS })
    }
  })

  return (
    <FormDrawer
      open={open}
      title={isEdit ? 'Edit Client' : 'New Client'}
      onClose={close}
      footer={
        <div className="flex items-center justify-end gap-2">
          <button type="button" onClick={close} className="inline-flex h-11 items-center justify-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">Cancel</button>
          <button type="submit" form="client-form" disabled={isSubmitting} className="inline-flex h-11 items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:cursor-not-allowed disabled:opacity-60">
            {isSubmitting ? (isEdit ? 'Saving…' : 'Creating…') : (isEdit ? 'Save Changes' : 'Create Client')}
          </button>
        </div>
      }
    >
      <form id="client-form" onSubmit={onSubmit} className="space-y-4">
        <div>
          <label className={labelClass} htmlFor="client-name">Client Name</label>
          <input id="client-name" {...register('clientName')} className={fieldClass} />
          {errors.clientName ? <p className={errorClass}>{errors.clientName.message}</p> : null}
        </div>
        <div>
          <label className={labelClass} htmlFor="client-type">Client Type</label>
          <input id="client-type" {...register('clientType')} placeholder="e.g. Corporate, SME, Government" className={fieldClass} />
          {errors.clientType ? <p className={errorClass}>{errors.clientType.message}</p> : null}
        </div>
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className={labelClass} htmlFor="client-email">Email</label>
            <input id="client-email" type="email" {...register('email')} className={fieldClass} />
            {errors.email ? <p className={errorClass}>{errors.email.message}</p> : null}
          </div>
          <div>
            <label className={labelClass} htmlFor="client-phone">Phone</label>
            <input id="client-phone" {...register('phone')} className={fieldClass} />
            {errors.phone ? <p className={errorClass}>{errors.phone.message}</p> : null}
          </div>
        </div>
        <div>
          <label className={labelClass} htmlFor="client-location">Location</label>
          <input id="client-location" {...register('location')} className={fieldClass} />
          {errors.location ? <p className={errorClass}>{errors.location.message}</p> : null}
        </div>
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className={labelClass} htmlFor="client-contact-person">Contact Person</label>
            <input id="client-contact-person" {...register('contactPerson')} className={fieldClass} />
            {errors.contactPerson ? <p className={errorClass}>{errors.contactPerson.message}</p> : null}
          </div>
          <div>
            <label className={labelClass} htmlFor="client-contact-phone">Contact Phone</label>
            <input id="client-contact-phone" {...register('contactPhone')} className={fieldClass} />
            {errors.contactPhone ? <p className={errorClass}>{errors.contactPhone.message}</p> : null}
          </div>
        </div>
        <div>
          <label className={labelClass} htmlFor="client-notes">Notes</label>
          <textarea id="client-notes" {...register('notes')} rows={3} className={`${fieldClass} h-auto py-2`} />
          {errors.notes ? <p className={errorClass}>{errors.notes.message}</p> : null}
        </div>
      </form>
    </FormDrawer>
  )
}
