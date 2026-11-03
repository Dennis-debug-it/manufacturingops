import { useState } from 'react'
import { ShieldAlert } from 'lucide-react'
import { useAuth } from '../hooks/useAuth'
import { formatRelative } from '../utils/dates'
import { isDemoTenant } from '../demoWorkspace'

export function ImpersonationBanner() {
  const { isImpersonating, impersonation, tenant, user, logout } = useAuth()
  const [exiting, setExiting] = useState(false)

  if (!isImpersonating || !impersonation) {
    return null
  }

  const demo = isDemoTenant(tenant?.tenantId)

  const exit = async () => {
    setExiting(true)
    try {
      await logout()
    } finally {
      setExiting(false)
    }
  }

  return (
    <div className="flex items-center justify-between gap-3 bg-amber-500 px-4 py-2 text-sm font-medium text-amber-950 sm:px-6">
      <div className="flex items-center gap-2">
        <ShieldAlert className="h-4 w-4 shrink-0" />
        <span>
          {demo ? <>Demo workspace: signed in as <strong>{user?.fullName}</strong> ({user?.role}). </> : null}
          {demo ? null : <>Impersonating <strong>{user?.fullName}</strong> at <strong>{tenant?.companyName}</strong> — started by {impersonation.impersonatedByName} ({formatRelative(impersonation.startedAt)}).</>}
        </span>
      </div>
      <button
        type="button"
        onClick={() => void exit()}
        disabled={exiting}
        className="inline-flex h-8 shrink-0 items-center justify-center rounded-lg bg-amber-950 px-3 text-xs font-semibold text-amber-50 transition hover:bg-amber-900 disabled:opacity-60"
      >
        {exiting ? 'Exiting…' : demo ? 'Switch role or leave demo' : 'Exit Impersonation'}
      </button>
    </div>
  )
}
