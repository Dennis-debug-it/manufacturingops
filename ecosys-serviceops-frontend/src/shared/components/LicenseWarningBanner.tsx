import { AlertTriangle, ShieldOff } from 'lucide-react'
import { useAuth } from '../hooks/useAuth'
import { useLicenseWarning } from '../hooks/useLicenseWarning'

/**
 * Surfaces the backend's X-License-Warning header (set by TenantLicenseEnforcementMiddleware on
 * every tenant-scoped response) so an admin finds out about a read-only or soon-to-expire license
 * by browsing normally, not only after a save/create action fails. Admin-only: renewing the
 * license or contacting Ecosys about it isn't something a Technician/Viewer/etc. can act on, and
 * the read-only block itself already surfaces via the normal error toast when any user's save
 * fails — this banner is the proactive nudge for the person who can actually do something about it.
 */
export function LicenseWarningBanner() {
  const { user } = useAuth()
  const warning = useLicenseWarning()

  if (!warning || user?.role !== 'admin') {
    return null
  }

  // The two terminal states (expired past its grace period, or suspended) block every mutation
  // right now — everything else (grace period, expiring within 7 days) is an advance notice.
  const isBlocking = /read-only|suspended/i.test(warning)

  return (
    <div
      className={
        isBlocking
          ? 'flex items-center gap-2 bg-rose-600 px-4 py-2 text-sm font-medium text-rose-50 sm:px-6'
          : 'flex items-center gap-2 bg-amber-500 px-4 py-2 text-sm font-medium text-amber-950 sm:px-6'
      }
    >
      {isBlocking ? <ShieldOff className="h-4 w-4 shrink-0" /> : <AlertTriangle className="h-4 w-4 shrink-0" />}
      <span>{warning}{isBlocking ? ' Contact your Ecosys account manager to restore full access.' : null}</span>
    </div>
  )
}
