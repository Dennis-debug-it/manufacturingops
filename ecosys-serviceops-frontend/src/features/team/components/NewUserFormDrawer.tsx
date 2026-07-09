import { useForm } from 'react-hook-form'
import toast from 'react-hot-toast'
import { FormDrawer } from '../../../shared/components/FormDrawer'
import { useCreateTeamUser } from '../api'

const fieldClass = 'h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none transition focus:border-slate-900'
const labelClass = 'mb-1.5 block text-sm font-medium text-slate-700'

interface FormValues {
  fullName: string
  email: string
  phoneNumber: string
  role: 'Admin' | 'User'
  jobTitle: string
  department: string
}

export function NewUserFormDrawer({ open, onClose }: { open: boolean; onClose: () => void }) {
  const { register, handleSubmit, reset, formState: { errors, isSubmitting } } = useForm<FormValues>({
    defaultValues: { fullName: '', email: '', phoneNumber: '', role: 'User', jobTitle: '', department: '' },
  })
  const createUser = useCreateTeamUser()

  const close = () => { reset(); onClose() }

  const onSubmit = handleSubmit(async (values) => {
    try {
      const created = await createUser.mutateAsync({
        fullName: values.fullName.trim(),
        email: values.email.trim(),
        phoneNumber: values.phoneNumber.trim() || null,
        role: values.role,
        jobTitle: values.jobTitle.trim() || null,
        department: values.department.trim() || null,
        isActive: true,
        credentialDeliveryMethod: 'InviteEmail',
      })
      toast.success(`Invited ${created.fullName}`)
      close()
    } catch {
      // interceptor surfaces the error toast
    }
  })

  return (
    <FormDrawer
      open={open}
      title="Invite User"
      onClose={close}
      footer={
        <div className="flex items-center justify-end gap-2">
          <button type="button" onClick={close} className="inline-flex h-11 items-center justify-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">Cancel</button>
          <button type="submit" form="new-user-form" disabled={isSubmitting} className="inline-flex h-11 items-center justify-center rounded-lg bg-slate-900 px-4 text-sm font-medium text-white transition hover:bg-slate-800 disabled:opacity-60">
            {isSubmitting ? 'Inviting…' : 'Send Invite'}
          </button>
        </div>
      }
    >
      <form id="new-user-form" onSubmit={onSubmit} className="space-y-4">
        <div>
          <label className={labelClass} htmlFor="user-name">Full Name</label>
          <input id="user-name" {...register('fullName', { required: true })} className={fieldClass} />
          {errors.fullName ? <p className="mt-1 text-xs text-rose-600">Required</p> : null}
        </div>
        <div>
          <label className={labelClass} htmlFor="user-email">Email</label>
          <input id="user-email" type="email" {...register('email', { required: true })} className={fieldClass} />
          {errors.email ? <p className="mt-1 text-xs text-rose-600">Required</p> : null}
        </div>
        <div>
          <label className={labelClass} htmlFor="user-phone">Phone</label>
          <input id="user-phone" {...register('phoneNumber')} className={fieldClass} />
        </div>
        <div>
          <label className={labelClass} htmlFor="user-role">Role</label>
          <select id="user-role" {...register('role')} className={fieldClass}>
            <option value="User">User</option>
            <option value="Admin">Admin</option>
          </select>
        </div>
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className={labelClass} htmlFor="user-job-title">Job Title</label>
            <input id="user-job-title" {...register('jobTitle')} className={fieldClass} />
          </div>
          <div>
            <label className={labelClass} htmlFor="user-department">Department</label>
            <input id="user-department" {...register('department')} className={fieldClass} />
          </div>
        </div>
      </form>
    </FormDrawer>
  )
}
