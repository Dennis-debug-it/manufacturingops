import { useEffect } from 'react'
import { useForm } from 'react-hook-form'
import toast from 'react-hot-toast'
import { FormDrawer } from '../../../shared/components/FormDrawer'
import { applyApiFieldErrors } from '../../../shared/api/apiError'
import { useAuth } from '../../../shared/hooks/useAuth'
import { useUpdateTeamUser } from '../api'
import type { TeamUser, TenantUserRole } from '../types'
import { ROLE_OPTIONS } from './NewUserFormDrawer'

const fieldClass = 'h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none transition focus:border-[var(--app-primary)] disabled:bg-slate-50 disabled:text-slate-500'
const labelClass = 'mb-1.5 block text-sm font-medium text-slate-700'
const errorClass = 'mt-1 text-xs text-rose-600'

interface FormValues {
  fullName: string
  email: string
  username: string
  phoneNumber: string
  role: TenantUserRole
  jobTitle: string
  department: string
}

const FORM_FIELDS = ['fullName', 'email', 'username', 'phoneNumber', 'role', 'jobTitle', 'department'] as const satisfies readonly (keyof FormValues)[]

/** The API's role casing isn't guaranteed to match the option values; match it case-insensitively. */
const roleOptionFor = (role: string): TenantUserRole =>
  ROLE_OPTIONS.find((option) => option.value.toLowerCase() === role.toLowerCase())?.value ?? (role as TenantUserRole)

const toFormValues = (user: TeamUser): FormValues => ({
  fullName: user.fullName,
  email: user.hasEmail ? user.email : '',
  username: user.username ?? '',
  phoneNumber: user.phoneNumber ?? '',
  role: roleOptionFor(user.role),
  jobTitle: user.jobTitle ?? '',
  department: user.department ?? '',
})

/**
 * Fix a user's details after creation. PUT /api/users/{id} replaces the whole user, so everything this
 * form doesn't show (permissions, branch access, groups, status) is sent back exactly as loaded.
 */
export function EditUserDrawer({ open, onClose, user, focusRole = false }: { open: boolean; onClose: () => void; user: TeamUser; focusRole?: boolean }) {
  const { user: currentUser } = useAuth()
  const isSelf = currentUser?.userId === user.id
  const updateUser = useUpdateTeamUser(user.id)
  const { register, handleSubmit, reset, setError, watch, formState: { errors, isSubmitting } } = useForm<FormValues>({ defaultValues: toFormValues(user) })

  useEffect(() => {
    if (open) reset(toFormValues(user))
  }, [open, user, reset])

  // "Change Role" opens the same form with the role picker ready.
  useEffect(() => {
    if (!open || !focusRole) return
    const timer = window.setTimeout(() => document.getElementById('edit-user-role')?.focus(), 50)
    return () => window.clearTimeout(timer)
  }, [open, focusRole])

  const email = watch('email')
  const role = watch('role')
  const roleChanged = !isSelf && (role ?? '').toLowerCase() !== user.role.toLowerCase()
  const emailChanged = (email ?? '').trim().toLowerCase() !== (user.hasEmail ? user.email.toLowerCase() : '')

  const onSubmit = handleSubmit(async (values) => {
    if (!values.fullName.trim()) {
      setError('fullName', { message: 'Full name is required' })
      return
    }
    try {
      await updateUser.mutateAsync({
        fullName: values.fullName.trim(),
        // Blank = this person has no email address and signs in with their username.
        email: values.email.trim() || null,
        username: values.username.trim() || null,
        phoneNumber: values.phoneNumber.trim() || null,
        // A disabled select submits nothing, and your own role can't change anyway.
        role: isSelf || !roleChanged ? user.role : values.role,
        jobTitle: values.jobTitle.trim() || null,
        department: values.department.trim() || null,
        isActive: user.isActive,
        // A new role starts from that role's default capabilities; otherwise keep the custom set.
        permissions: roleChanged ? null : user.permissions,
        branchIds: user.branchIds,
        defaultBranchId: user.defaultBranchId,
        hasAllBranchAccess: user.hasAllBranchAccess,
        assignmentGroupIds: user.assignmentGroups.map((group) => group.id),
      })
      toast.success('User details updated')
      onClose()
    } catch (error) {
      applyApiFieldErrors<FormValues>(error, { setError, fields: FORM_FIELDS })
    }
  })

  return (
    <FormDrawer
      open={open}
      title="Edit User"
      onClose={onClose}
      footer={
        <div className="flex items-center justify-end gap-2">
          <button type="button" onClick={onClose} className="inline-flex h-11 items-center justify-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">Cancel</button>
          <button type="submit" form="edit-user-form" disabled={isSubmitting} className="inline-flex h-11 items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:opacity-60">
            {isSubmitting ? 'Saving…' : 'Save Changes'}
          </button>
        </div>
      }
    >
      <form id="edit-user-form" onSubmit={onSubmit} className="space-y-4">
        <div>
          <label className={labelClass} htmlFor="edit-user-name">Full Name</label>
          <input id="edit-user-name" {...register('fullName')} className={fieldClass} />
          {errors.fullName ? <p className={errorClass}>{errors.fullName.message}</p> : null}
        </div>
        <div>
          <label className={labelClass} htmlFor="edit-user-email">Email</label>
          <input id="edit-user-email" type="email" placeholder="Leave blank if this person has no email" {...register('email')} className={fieldClass} />
          {errors.email ? <p className={errorClass}>{errors.email.message}</p> : null}
          {emailChanged && user.mustChangePassword && email.trim() ? (
            <p className="mt-1 text-xs text-slate-500">They haven't signed in yet — use Resend Invite afterwards to send the setup link to the new address.</p>
          ) : null}
        </div>
        <div>
          <label className={labelClass} htmlFor="edit-user-username">Username</label>
          <input id="edit-user-username" {...register('username')} className={fieldClass} />
          <p className="mt-1 text-xs text-slate-500">Used to sign in together with the company name.</p>
          {errors.username ? <p className={errorClass}>{errors.username.message}</p> : null}
        </div>
        <div>
          <label className={labelClass} htmlFor="edit-user-phone">Phone</label>
          <input id="edit-user-phone" {...register('phoneNumber')} className={fieldClass} />
          {errors.phoneNumber ? <p className={errorClass}>{errors.phoneNumber.message}</p> : null}
        </div>
        <div>
          <label className={labelClass} htmlFor="edit-user-role">Role</label>
          <select id="edit-user-role" disabled={isSelf} {...register('role')} className={fieldClass}>
            {ROLE_OPTIONS.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
          </select>
          {isSelf ? <p className="mt-1 text-xs text-slate-500">You can't change your own role.</p>
            : roleChanged ? <p className="mt-1 text-xs text-amber-700">Changing the role resets this user's capabilities to that role's defaults.</p> : null}
        </div>
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className={labelClass} htmlFor="edit-user-title">Job Title</label>
            <input id="edit-user-title" {...register('jobTitle')} className={fieldClass} />
          </div>
          <div>
            <label className={labelClass} htmlFor="edit-user-department">Department</label>
            <input id="edit-user-department" {...register('department')} className={fieldClass} />
          </div>
        </div>
        {!isSelf ? <p className="text-xs text-slate-500">Saving signs this user out of their open sessions so the changes apply immediately.</p> : null}
      </form>
    </FormDrawer>
  )
}
