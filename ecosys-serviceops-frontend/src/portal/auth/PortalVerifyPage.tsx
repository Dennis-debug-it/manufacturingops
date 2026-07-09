import { useEffect, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { usePortalAuth } from './PortalAuthContext'

export function PortalVerifyPage() {
  const { verify } = usePortalAuth()
  const navigate = useNavigate()
  const [failed, setFailed] = useState(false)

  useEffect(() => {
    const token = window.location.hash.slice(1)
    if (!token) {
      setFailed(true)
      return
    }

    let cancelled = false
    void (async () => {
      try {
        await verify(token)
        if (!cancelled) {
          navigate('/portal/dashboard', { replace: true })
        }
      } catch {
        if (!cancelled) {
          setFailed(true)
        }
      }
    })()

    return () => {
      cancelled = true
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  if (failed) {
    return (
      <div className="flex min-h-screen flex-col items-center justify-center gap-3 bg-slate-950 px-4 text-center text-white">
        <h1 className="text-lg font-semibold">This sign-in link is invalid or has expired</h1>
        <p className="max-w-sm text-sm text-slate-300">Request a new link to sign in to the client portal.</p>
        <Link to="/portal/login" className="mt-2 inline-flex h-10 items-center rounded-xl bg-white px-4 text-sm font-semibold text-slate-950 hover:bg-slate-100">Back to sign in</Link>
      </div>
    )
  }

  return (
    <div className="flex min-h-screen items-center justify-center bg-slate-950 text-sm text-slate-300">
      Signing you in…
    </div>
  )
}
