import { useState } from 'react'
import toast from 'react-hot-toast'
import { Pencil } from 'lucide-react'
import { PageHeader } from '../shared/components/PageHeader'
import { Spinner } from '../shared/components/Spinner'
import { formatDateTime } from '../shared/utils/dates'
import { usePortalProfile, useUpdatePortalProfile } from './api'

export function PortalProfilePage() {
  const { data, isLoading, isError } = usePortalProfile()
  const updateProfile = useUpdatePortalProfile()
  const [editing, setEditing] = useState(false)
  const [fullName, setFullName] = useState('')
  const [phone, setPhone] = useState('')

  if (isLoading) {
    return <Spinner label="Loading profile" />
  }

  if (isError || !data) {
    return <div className="rounded-2xl border border-dashed border-slate-300 bg-white p-8 text-center text-sm text-slate-600">Profile could not be loaded.</div>
  }

  const startEditing = () => {
    setFullName(data.fullName)
    setPhone(data.phone ?? '')
    setEditing(true)
  }

  const submit = async () => {
    if (!fullName.trim()) {
      toast.error('Full name is required.')
      return
    }

    try {
      await updateProfile.mutateAsync({ fullName: fullName.trim(), phone: phone.trim() || undefined })
      toast.success('Profile updated')
      setEditing(false)
    } catch {
      toast.error('Could not update profile — please try again.')
    }
  }

  if (editing) {
    return (
      <div className="space-y-4">
        <PageHeader title="Profile" subtitle="Your client portal contact details." />
        <div className="max-w-xl space-y-4 rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 shadow-sm">
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Full Name</label>
            <input value={fullName} onChange={(event) => setFullName(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]" />
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Phone</label>
            <input value={phone} onChange={(event) => setPhone(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]" />
          </div>
          <div className="flex gap-2 pt-1">
            <button type="button" onClick={() => setEditing(false)} className="inline-flex h-11 flex-1 items-center justify-center rounded-lg border border-slate-300 bg-white px-4 text-sm font-medium text-slate-700 hover:bg-slate-50">Cancel</button>
            <button type="button" disabled={updateProfile.isPending} onClick={() => void submit()} className="inline-flex h-11 flex-1 items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white hover:bg-[var(--app-secondary)] disabled:opacity-60">Save Changes</button>
          </div>
        </div>
      </div>
    )
  }

  const rows: Array<[string, string]> = [
    ['Full Name', data.fullName],
    ['Email', data.email],
    ['Phone', data.phone ?? '—'],
    ['Account', data.clientName],
    ['Last Sign-In', data.lastLoginAt ? formatDateTime(data.lastLoginAt) : '—'],
  ]

  return (
    <div className="space-y-4">
      <PageHeader
        title="Profile"
        subtitle="Your client portal contact details."
        primaryAction={<button type="button" onClick={startEditing} className="inline-flex h-11 items-center gap-2 rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white hover:bg-[var(--app-secondary)]"><Pencil className="h-4 w-4" />Edit Profile</button>}
      />
      <div className="max-w-xl rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 shadow-sm">
        <dl className="space-y-4">
          {rows.map(([label, value]) => (
            <div key={label} className="flex items-center justify-between border-b border-slate-100 pb-3 last:border-0 last:pb-0">
              <dt className="text-sm text-slate-500">{label}</dt>
              <dd className="text-sm font-medium text-slate-900">{value}</dd>
            </div>
          ))}
        </dl>
      </div>
      <div className="max-w-xl rounded-2xl border border-dashed border-slate-300 bg-white p-4 text-sm text-slate-600">
        Email cannot be changed here — contact your account manager to update your sign-in address.
      </div>
    </div>
  )
}
