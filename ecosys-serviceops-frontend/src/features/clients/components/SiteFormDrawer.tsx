import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import toast from 'react-hot-toast'
import { FormDrawer } from '../../../shared/components/FormDrawer'
import { useCreateSite } from '../api'
import { siteSchema, type SiteFormValues } from '../schemas'

const fieldClass = 'h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none transition focus:border-slate-900'
const labelClass = 'mb-1.5 block text-sm font-medium text-slate-700'
const errorClass = 'mt-1 text-xs text-rose-600'

export interface SiteFormDrawerProps {
  open: boolean
  clientId: string
  onClose: () => void
}

export function SiteFormDrawer({ open, clientId, onClose }: SiteFormDrawerProps) {
  const { register, handleSubmit, reset, formState: { errors, isSubmitting } } = useForm<SiteFormValues>({
    resolver: zodResolver(siteSchema),
    defaultValues: { siteName: '', streetAddress: '', townCity: '', county: '', country: '', contactPerson: '', contactPhone: '', contactEmail: '', operatingHours: '', accessNotes: '' },
  })
  const createSite = useCreateSite(clientId)

  const close = () => {
    reset()
    onClose()
  }

  const onSubmit = handleSubmit(async (values) => {
    try {
      await createSite.mutateAsync({
        siteName: values.siteName.trim(),
        siteType: values.siteType || null,
        streetAddress: values.streetAddress?.trim() || null,
        townCity: values.townCity?.trim() || null,
        county: values.county?.trim() || null,
        country: values.country?.trim() || null,
        contactPerson: values.contactPerson?.trim() || null,
        contactPhone: values.contactPhone?.trim() || null,
        contactEmail: values.contactEmail?.trim() || null,
        operatingHours: values.operatingHours?.trim() || null,
        accessNotes: values.accessNotes?.trim() || null,
      })
      toast.success('Site created')
      close()
    } catch {
      // interceptor surfaces the error toast
    }
  })

  return (
    <FormDrawer
      open={open}
      title="New Site"
      onClose={close}
      footer={
        <div className="flex items-center justify-end gap-2">
          <button type="button" onClick={close} className="inline-flex h-11 items-center justify-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">Cancel</button>
          <button type="submit" form="site-form" disabled={isSubmitting} className="inline-flex h-11 items-center justify-center rounded-lg bg-slate-900 px-4 text-sm font-medium text-white transition hover:bg-slate-800 disabled:cursor-not-allowed disabled:opacity-60">
            {isSubmitting ? 'Creating…' : 'Create Site'}
          </button>
        </div>
      }
    >
      <form id="site-form" onSubmit={onSubmit} className="space-y-4">
        <div>
          <label className={labelClass} htmlFor="site-name">Site Name</label>
          <input id="site-name" {...register('siteName')} className={fieldClass} />
          {errors.siteName ? <p className={errorClass}>{errors.siteName.message}</p> : null}
        </div>
        <div>
          <label className={labelClass} htmlFor="site-type">Site Type</label>
          <select id="site-type" {...register('siteType')} className={fieldClass}>
            <option value="">Select a type</option>
            {['Branch', 'HQ', 'Data Centre', 'Warehouse', 'Factory', 'Other'].map((type) => (
              <option key={type} value={type}>{type}</option>
            ))}
          </select>
        </div>
        <div>
          <label className={labelClass} htmlFor="site-address">Street Address</label>
          <input id="site-address" {...register('streetAddress')} className={fieldClass} />
        </div>
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className={labelClass} htmlFor="site-town">Town / City</label>
            <input id="site-town" {...register('townCity')} className={fieldClass} />
          </div>
          <div>
            <label className={labelClass} htmlFor="site-county">County</label>
            <input id="site-county" {...register('county')} className={fieldClass} />
          </div>
        </div>
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className={labelClass} htmlFor="site-contact-person">Contact Person</label>
            <input id="site-contact-person" {...register('contactPerson')} className={fieldClass} />
          </div>
          <div>
            <label className={labelClass} htmlFor="site-contact-phone">Contact Phone</label>
            <input id="site-contact-phone" {...register('contactPhone')} className={fieldClass} />
          </div>
        </div>
        <div>
          <label className={labelClass} htmlFor="site-contact-email">Contact Email</label>
          <input id="site-contact-email" type="email" {...register('contactEmail')} className={fieldClass} />
          {errors.contactEmail ? <p className={errorClass}>{errors.contactEmail.message}</p> : null}
        </div>
        <div>
          <label className={labelClass} htmlFor="site-operating-hours">Operating Hours</label>
          <input id="site-operating-hours" {...register('operatingHours')} className={fieldClass} />
        </div>
        <div>
          <label className={labelClass} htmlFor="site-access-notes">Access Notes</label>
          <textarea id="site-access-notes" {...register('accessNotes')} rows={3} className={`${fieldClass} h-auto py-2`} />
        </div>
      </form>
    </FormDrawer>
  )
}
