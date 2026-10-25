import { useState } from 'react'
import toast from 'react-hot-toast'
import { Truck } from 'lucide-react'
import { Spinner } from '../../shared/components/Spinner'
import { useAuth } from '../../shared/hooks/useAuth'
import { cn } from '../../shared/utils/cn'
import { JobAlertsSetting } from './JobAlerts'
import { LocationSetting } from './LocationPrompt'
import { NotificationPreferencesList } from '../notifications/NotificationPreferences'
import { WhatsAppOptInCard } from '../whatsapp/WhatsAppOptInCard'
import { staffWhatsAppApi } from '../whatsapp/remindersApi'
import { useChangeMyPassword, useMyTechnician, useMyVan } from './api'

const inputClass = 'h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]'
const cardClass = 'space-y-3 rounded-xl border border-slate-200 bg-white p-4'

function ChangePassword() {
  const change = useChangeMyPassword()
  const [currentPassword, setCurrentPassword] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const mismatch = confirmPassword.length > 0 && newPassword !== confirmPassword

  const submit = async (event: React.FormEvent) => {
    event.preventDefault()
    if (!currentPassword || !newPassword || mismatch) return
    try {
      const result = await change.mutateAsync({ currentPassword, newPassword, confirmPassword })
      toast.success(result.message || 'Password changed')
      setCurrentPassword('')
      setNewPassword('')
      setConfirmPassword('')
    } catch {
      // interceptor surfaces the error toast (wrong current password, weak password)
    }
  }

  return (
    <form onSubmit={(event) => void submit(event)} className={cardClass}>
      <h2 className="text-sm font-semibold text-slate-900">Change password</h2>
      <input type="password" autoComplete="current-password" value={currentPassword} onChange={(event) => setCurrentPassword(event.target.value)} placeholder="Current password" aria-label="Current password" className={inputClass} />
      <input type="password" autoComplete="new-password" value={newPassword} onChange={(event) => setNewPassword(event.target.value)} placeholder="New password" aria-label="New password" className={inputClass} />
      <input type="password" autoComplete="new-password" value={confirmPassword} onChange={(event) => setConfirmPassword(event.target.value)} placeholder="Confirm new password" aria-label="Confirm new password" className={inputClass} />
      {mismatch ? <p className="text-xs text-rose-600">The new passwords do not match.</p> : null}
      <p className="text-xs text-slate-500">Other devices signed in to your account will be signed out.</p>
      <button type="submit" disabled={!currentPassword || !newPassword || !confirmPassword || mismatch || change.isPending} className="inline-flex h-11 w-full items-center justify-center rounded-lg bg-[var(--app-primary)] text-sm font-semibold text-white disabled:opacity-50">
        {change.isPending ? 'Saving…' : 'Change password'}
      </button>
    </form>
  )
}

function NotificationSettings() {
  return (
    <section className="space-y-2">
      <h2 className="px-1 text-sm font-semibold text-slate-900">Notifications</h2>
      <WhatsAppOptInCard source={staffWhatsAppApi} audience="staff" />
      <NotificationPreferencesList />
    </section>
  )
}

/** Stock on the van assigned to this technician; hidden when no van is assigned. */
function MyVan() {
  const { data: me } = useMyTechnician()
  const { data, isLoading } = useMyVan(me?.technicianId)
  if (isLoading) return <Spinner label="Loading van stock" />
  if (!data) return null
  const { van, stock } = data

  return (
    <section className={cardClass}>
      <h2 className="flex items-center gap-2 text-sm font-semibold text-slate-900"><Truck className="h-4 w-4 text-slate-500" />My van: {van.name}{van.registrationNumber ? ` (${van.registrationNumber})` : ''}</h2>
      {!stock.some((line) => line.quantityOnHand > 0) ? <p className="text-sm text-slate-500">The van has no stock on board.</p> : (
        <ul className="divide-y divide-slate-100 text-sm">
          {stock.filter((line) => line.quantityOnHand > 0).map((line) => (
            <li key={line.materialId} className="flex justify-between gap-3 py-1.5">
              <span className="text-slate-900">{line.materialName}</span>
              <span className={cn('shrink-0', line.quantityOnHand <= line.reorderLevel ? 'text-amber-700' : 'text-slate-500')}>{line.quantityOnHand} {line.unitOfMeasure}</span>
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}

export function TechAccountPage() {
  const { user, tenant } = useAuth()
  return (
    <div className="space-y-3">
      <section className={cardClass}>
        <h1 className="text-lg font-semibold text-slate-900">{user?.fullName}</h1>
        <dl className="grid grid-cols-[auto,1fr] gap-x-3 gap-y-1 text-sm">
          {user?.hasEmail ? (<><dt className="text-slate-500">Email</dt><dd className="truncate text-slate-900">{user.email}</dd></>) : null}
          {user?.username ? (<><dt className="text-slate-500">Username</dt><dd className="text-slate-900">{user.username}</dd></>) : null}
          {user?.jobTitle ? (<><dt className="text-slate-500">Job title</dt><dd className="text-slate-900">{user.jobTitle}</dd></>) : null}
          {tenant?.companyName ? (<><dt className="text-slate-500">Company</dt><dd className="text-slate-900">{tenant.companyName}</dd></>) : null}
        </dl>
      </section>
      <MyVan />
      <ChangePassword />
      <JobAlertsSetting />
      <LocationSetting />
      <NotificationSettings />
    </div>
  )
}
