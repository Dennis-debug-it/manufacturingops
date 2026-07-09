import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { consumeImpersonationRelay, setImpersonationSession } from '../../shared/api/sessionStore'
import { Spinner } from '../../shared/components/Spinner'

export function ImpersonationEntryPage() {
  const [failed, setFailed] = useState(false)

  useEffect(() => {
    const relayId = window.location.hash.slice(1)
    const session = relayId ? consumeImpersonationRelay(relayId) : null
    if (!session) {
      setFailed(true)
      return
    }
    setImpersonationSession(session)
    window.location.replace('/dashboard')
  }, [])

  if (failed) {
    return (
      <div className="flex min-h-screen flex-col items-center justify-center gap-3 bg-slate-50 px-4 text-center">
        <h1 className="text-lg font-semibold text-slate-900">This impersonation link has expired</h1>
        <p className="max-w-sm text-sm text-slate-600">It may have already been opened in another tab, or the link is no longer valid. Start a new impersonation session from Tenant Management.</p>
        <Link to="/login" className="mt-2 inline-flex h-10 items-center rounded-lg bg-slate-900 px-4 text-sm font-medium text-white hover:bg-slate-800">Go to sign in</Link>
      </div>
    )
  }

  return <Spinner label="Starting impersonation session" />
}
