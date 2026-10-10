import { useState } from 'react'
import toast from 'react-hot-toast'
import { FormDrawer } from '../../shared/components/FormDrawer'
import { useCreateTenantWorkspaceUser } from '../api'

/**
 * Creates an extra user inside a tenant workspace from the platform side — most often a second or
 * third tenant admin, since tenant creation only ever seeds one. Only tenant roles are offered:
 * platform staff accounts are created on the Platform Users screen instead.
 */
const workspaceRoles = [
  { value: 'Admin', label: 'Admin — full workspace access' },
  { value: 'Manager', label: 'Manager' },
  { value: 'Dispatcher', label: 'Dispatcher' },
  { value: 'Technician', label: 'Technician' },
  { value: 'MaterialsController', label: 'Materials Controller' },
  { value: 'finance', label: 'Finance' },
  { value: 'Viewer', label: 'Viewer' },
]

const inputClass =
  'h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)] disabled:bg-slate-50'

export interface NewWorkspaceUserDrawerProps {
  open: boolean
  tenantId: string | undefined
  tenantName: string
  onClose: () => void
}

export function NewWorkspaceUserDrawer({ open, tenantId, tenantName, onClose }: NewWorkspaceUserDrawerProps) {
  const createUser = useCreateTenantWorkspaceUser(tenantId)
  const [fullName, setFullName] = useState('')
  const [email, setEmail] = useState('')
  const [phone, setPhone] = useState('')
  const [jobTitle, setJobTitle] = useState('')
  const [role, setRole] = useState('Admin')

  const close = () => {
    setFullName('')
    setEmail('')
    setPhone('')
    setJobTitle('')
    setRole('Admin')
    onClose()
  }

  const submit = async (event: React.FormEvent) => {
    event.preventDefault()
    try {
      const result = await createUser.mutateAsync({
        fullName: fullName.trim(),
        email: email.trim(),
        phone: phone.trim() === '' ? null : phone.trim(),
        role,
        jobTitle: jobTitle.trim() === '' ? null : jobTitle.trim(),
      })
      if (result.invitationSent) {
        toast.success(result.message)
      } else {
        toast.error(result.message)
      }
      close()
    } catch {
      // interceptor surfaces the error
    }
  }

  return (
    <FormDrawer
      open={open}
      title={`Add workspace user — ${tenantName}`}
      onClose={close}
      footer={
        <div className="flex items-center justify-end gap-2">
          <button type="button" onClick={close} className="inline-flex h-11 items-center justify-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">
            Cancel
          </button>
          <button
            type="submit"
            form="new-workspace-user-form"
            disabled={createUser.isPending}
            className="inline-flex h-11 items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:opacity-60"
          >
            {createUser.isPending ? 'Creating…' : 'Create & send setup link'}
          </button>
        </div>
      }
    >
      <form id="new-workspace-user-form" onSubmit={(event) => void submit(event)} className="space-y-4">
        <p className="rounded-lg bg-slate-50 p-3 text-xs text-slate-600">
          This account lives inside <strong>{tenantName}</strong> and can only hold a workspace role. To create Ecosys
          platform staff instead, use Platform → Users.
        </p>
        <div>
          <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="workspaceUserName">Full name</label>
          <input id="workspaceUserName" required value={fullName} onChange={(event) => setFullName(event.target.value)} className={inputClass} />
        </div>
        <div>
          <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="workspaceUserEmail">Email</label>
          <input id="workspaceUserEmail" type="email" required value={email} onChange={(event) => setEmail(event.target.value)} className={inputClass} />
        </div>
        <div>
          <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="workspaceUserRole">Workspace role</label>
          <select id="workspaceUserRole" value={role} onChange={(event) => setRole(event.target.value)} className={`${inputClass} bg-white`}>
            {workspaceRoles.map((option) => (
              <option key={option.value} value={option.value}>{option.label}</option>
            ))}
          </select>
        </div>
        <div>
          <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="workspaceUserJobTitle">Job title</label>
          <input id="workspaceUserJobTitle" value={jobTitle} onChange={(event) => setJobTitle(event.target.value)} className={inputClass} />
        </div>
        <div>
          <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="workspaceUserPhone">Phone</label>
          <input id="workspaceUserPhone" value={phone} onChange={(event) => setPhone(event.target.value)} className={inputClass} />
        </div>
      </form>
    </FormDrawer>
  )
}
