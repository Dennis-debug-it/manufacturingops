import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import toast from 'react-hot-toast'
import { Copy } from 'lucide-react'
import { FormDrawer } from '../../../shared/components/FormDrawer'
import { applyApiFieldErrors } from '../../../shared/api/apiError'
import { useCreateTeamUser } from '../api'
import type { TenantUserRole } from '../types'
import { PermissionMatrix } from './PermissionMatrix'
import { EMPTY_CAPABILITIES, FULL_CAPABILITIES, useRolePermissions, type RoleCapabilities } from '../rolePermissionsApi'

const fieldClass = 'h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none transition focus:border-[var(--app-primary)]'
const labelClass = 'mb-1.5 block text-sm font-medium text-slate-700'
const errorClass = 'mt-1 text-xs text-rose-600'

export const ROLE_OPTIONS: Array<{ value: TenantUserRole; label: string }> = [
  { value: 'Viewer', label: 'Viewer' },
  { value: 'Technician', label: 'Technician' },
  { value: 'Dispatcher', label: 'Dispatcher' },
  { value: 'Manager', label: 'Manager' },
  { value: 'finance', label: 'Finance' },
  { value: 'Admin', label: 'Admin (full access)' },
]

interface FormValues {
  fullName: string
  email: string
  username: string
  password: string
  phoneNumber: string
  jobTitle: string
  department: string
}

/**
 * The fields a server-side 400 is allowed to blame — each renders an error line below. `Role`,
 * `Permissions` and the branch properties of `CreateUserRequest` are driven by controls outside the
 * react-hook-form instance, so a message naming one of those is toasted rather than pinned.
 * `CreateUserRequest` uses PascalCase; the helper normalises the casing before matching.
 */
const FORM_FIELDS = [
  'fullName',
  'email',
  'username',
  'password',
  'phoneNumber',
  'jobTitle',
  'department',
] as const satisfies readonly (keyof FormValues)[]

interface RevealedCredentials {
  fullName: string
  username: string | null
  password: string
}

export function NewUserFormDrawer({ open, onClose }: { open: boolean; onClose: () => void }) {
  const { register, handleSubmit, reset, setError, formState: { errors, isSubmitting } } = useForm<FormValues>({
    defaultValues: { fullName: '', email: '', username: '', password: '', phoneNumber: '', jobTitle: '', department: '' },
  })
  const createUser = useCreateTeamUser()
  const { data: matrix } = useRolePermissions()

  const [role, setRole] = useState<TenantUserRole>('Viewer')
  const [caps, setCaps] = useState<RoleCapabilities>(EMPTY_CAPABILITIES)
  const [noEmail, setNoEmail] = useState(false)
  const [revealed, setRevealed] = useState<RevealedCredentials | null>(null)

  const defaultsForRole = (nextRole: TenantUserRole): RoleCapabilities => {
    if (nextRole === 'Admin') return FULL_CAPABILITIES
    const match = matrix?.roles.find((row) => row.role.toLowerCase() === nextRole.toLowerCase())
    return match ? { ...match.permissions } : { ...EMPTY_CAPABILITIES }
  }

  // Reset to a clean Viewer template each time the drawer is opened (or role defaults load).
  useEffect(() => {
    if (open) {
      setRole('Viewer')
      setCaps(defaultsForRole('Viewer'))
      setNoEmail(false)
      setRevealed(null)
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, matrix])

  const onRoleChange = (nextRole: TenantUserRole) => {
    setRole(nextRole)
    setCaps(defaultsForRole(nextRole))
  }

  const close = () => { reset(); setRevealed(null); onClose() }

  const copyPassword = async () => {
    if (!revealed) return
    try {
      await navigator.clipboard.writeText(revealed.password)
      toast.success('Password copied')
    } catch {
      toast.error('Could not copy — select and copy the password manually')
    }
  }

  const onSubmit = handleSubmit(async (values) => {
    try {
      const created = await createUser.mutateAsync({
        fullName: values.fullName.trim(),
        email: noEmail ? null : values.email.trim(),
        username: values.username.trim() || null,
        password: noEmail ? (values.password.trim() || null) : null,
        phoneNumber: values.phoneNumber.trim() || null,
        role,
        jobTitle: values.jobTitle.trim() || null,
        department: values.department.trim() || null,
        isActive: true,
        credentialDeliveryMethod: noEmail ? 'None' : 'InviteEmail',
        permissions: role === 'Admin' ? null : caps,
      })

      const temporaryPassword = created.credentialDelivery?.temporaryPassword
      if (temporaryPassword) {
        // Nothing was emailed — this is the only chance to see the password, so keep the drawer
        // open on a reveal panel instead of toasting it away.
        setRevealed({ fullName: created.fullName, username: created.username, password: temporaryPassword })
        return
      }

      toast.success(noEmail ? `Created ${created.fullName}` : `Invited ${created.fullName}`)
      close()
    } catch (error) {
      // A model-validation 400 names the field it rejected; pin the message to that input instead
      // of leaving the user with only the interceptor's one-line toast.
      applyApiFieldErrors<FormValues>(error, { setError, fields: FORM_FIELDS })
    }
  })

  if (revealed) {
    return (
      <FormDrawer
        open={open}
        title="User Created"
        onClose={close}
        footer={
          <div className="flex items-center justify-end">
            <button type="button" onClick={close} className="inline-flex h-11 items-center justify-center rounded-lg bg-[var(--app-primary)] px-5 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)]">Done</button>
          </div>
        }
      >
        <div className="space-y-4">
          <p className="text-sm text-slate-600">
            <strong>{revealed.fullName}</strong> has no email address, so nothing was sent automatically. Share these sign-in details with them directly — the password will not be shown again.
          </p>
          <div className="space-y-3 rounded-xl border border-slate-200 bg-slate-50 p-4">
            {revealed.username ? (
              <div>
                <div className="text-xs font-semibold uppercase tracking-[0.14em] text-slate-500">Username</div>
                <div className="mt-1 font-mono text-sm text-slate-900">{revealed.username}</div>
              </div>
            ) : null}
            <div>
              <div className="text-xs font-semibold uppercase tracking-[0.14em] text-slate-500">Temporary Password</div>
              <div className="mt-1 flex items-center gap-2">
                <span className="font-mono text-sm text-slate-900">{revealed.password}</span>
                <button type="button" onClick={copyPassword} className="inline-flex items-center gap-1 rounded-lg border border-slate-200 bg-white px-2 py-1 text-xs font-medium text-slate-600 transition hover:bg-slate-50">
                  <Copy className="h-3.5 w-3.5" /> Copy
                </button>
              </div>
            </div>
          </div>
          <p className="text-xs text-slate-500">Don't forget to also tell them your company name — they'll need to enter it alongside the username to sign in.</p>
        </div>
      </FormDrawer>
    )
  }

  return (
    <FormDrawer
      open={open}
      title="Invite User"
      onClose={close}
      footer={
        <div className="flex items-center justify-end gap-2">
          <button type="button" onClick={close} className="inline-flex h-11 items-center justify-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">Cancel</button>
          <button type="submit" form="new-user-form" disabled={isSubmitting} className="inline-flex h-11 items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:opacity-60">
            {isSubmitting ? 'Saving…' : noEmail ? 'Create User' : 'Send Invite'}
          </button>
        </div>
      }
    >
      <form id="new-user-form" onSubmit={onSubmit} className="space-y-4">
        <div>
          <label className={labelClass} htmlFor="user-name">Full Name</label>
          <input id="user-name" {...register('fullName', { required: true })} className={fieldClass} />
          {errors.fullName ? <p className={errorClass}>{errors.fullName.message ?? 'Required'}</p> : null}
        </div>

        <label className="flex items-center gap-2 text-sm text-slate-700">
          <input type="checkbox" checked={noEmail} onChange={(event) => setNoEmail(event.target.checked)} className="h-4 w-4 rounded border-slate-300" />
          This person doesn't have an email address
        </label>

        {noEmail ? (
          <div className="rounded-xl border border-slate-200 bg-slate-50 p-4 text-sm text-slate-600">
            They'll sign in with a username and your company name instead of an email link.
          </div>
        ) : (
          <div>
            <label className={labelClass} htmlFor="user-email">Email</label>
            <input id="user-email" type="email" {...register('email', { required: !noEmail })} className={fieldClass} />
            {errors.email ? <p className={errorClass}>{errors.email.message ?? 'Required'}</p> : null}
          </div>
        )}

        <div>
          <label className={labelClass} htmlFor="user-username">Username {noEmail ? null : <span className="text-slate-400">(optional)</span>}</label>
          <input id="user-username" placeholder="Auto-generated from full name if left blank" {...register('username')} className={fieldClass} />
          {errors.username?.message ? <p className={errorClass}>{errors.username.message}</p> : null}
        </div>

        {noEmail ? (
          <div>
            <label className={labelClass} htmlFor="user-password">Initial Password <span className="text-slate-400">(optional)</span></label>
            <input id="user-password" type="text" placeholder="Leave blank to generate one you can copy after saving" {...register('password')} className={fieldClass} />
            {errors.password?.message ? <p className={errorClass}>{errors.password.message}</p> : null}
          </div>
        ) : null}

        <div>
          <label className={labelClass} htmlFor="user-phone">Phone</label>
          <input id="user-phone" {...register('phoneNumber')} className={fieldClass} />
          {errors.phoneNumber?.message ? <p className={errorClass}>{errors.phoneNumber.message}</p> : null}
        </div>
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className={labelClass} htmlFor="user-job-title">Job Title</label>
            <input id="user-job-title" {...register('jobTitle')} className={fieldClass} />
            {errors.jobTitle?.message ? <p className={errorClass}>{errors.jobTitle.message}</p> : null}
          </div>
          <div>
            <label className={labelClass} htmlFor="user-department">Department</label>
            <input id="user-department" {...register('department')} className={fieldClass} />
            {errors.department?.message ? <p className={errorClass}>{errors.department.message}</p> : null}
          </div>
        </div>

        <div>
          <label className={labelClass} htmlFor="user-role">Role</label>
          <select id="user-role" value={role} onChange={(event) => onRoleChange(event.target.value as TenantUserRole)} className={fieldClass}>
            {ROLE_OPTIONS.map((option) => (
              <option key={option.value} value={option.value}>{option.label}</option>
            ))}
          </select>
          <p className="mt-1 text-xs text-slate-500">Picking a role pre-fills the capabilities below. Adjust the boxes to fine-tune this user.</p>
        </div>

        <div className="space-y-2">
          <div className="text-sm font-medium text-slate-700">Capabilities</div>
          {role === 'Admin' ? (
            <div className="rounded-xl border border-slate-200 bg-slate-50 px-4 py-3 text-sm text-slate-600">Admin has full access to every capability.</div>
          ) : null}
          <PermissionMatrix value={role === 'Admin' ? FULL_CAPABILITIES : caps} onChange={setCaps} disabled={role === 'Admin'} />
        </div>
      </form>
    </FormDrawer>
  )
}
