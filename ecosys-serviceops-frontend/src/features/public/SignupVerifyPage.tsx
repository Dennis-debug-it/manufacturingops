import { useEffect, useRef, useState } from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { resolveErrorMessage } from '../../shared/api/apiError'
import { Spinner } from '../../shared/components/Spinner'
import { useAuth } from '../../shared/hooks/useAuth'

/** The link from the "Start free trial" email: creates the workspace, signs the owner in, opens setup. */
export function SignupVerifyPage() {
  const { completeSignup } = useAuth()
  const navigate = useNavigate()
  const { hash } = useLocation()
  const [error, setError] = useState<string | null>(null)
  const started = useRef(false)

  useEffect(() => {
    if (started.current) return
    started.current = true
    const token = hash.replace(/^#/, '')
    if (!token) {
      setError('This sign-up link is incomplete. Open the link from your email again.')
      return
    }
    // Drop the token from the address bar and history before anything else happens.
    window.history.replaceState(null, '', window.location.pathname)
    completeSignup(token)
      .then(() => navigate('/onboarding', { replace: true }))
      .catch((reason: unknown) => setError(resolveErrorMessage(reason)))
  }, [completeSignup, hash, navigate])

  if (error) {
    return (
      <div className="min-h-screen bg-slate-50 px-4 py-8 text-slate-900">
        <div role="alert" className="mx-auto max-w-xl rounded-3xl border border-slate-200 bg-white p-6 text-center shadow-sm">
          <h1 className="text-lg font-semibold">We couldn't open your workspace</h1>
          <p className="mt-2 text-sm text-slate-600">{error}</p>
          <p className="mt-4 text-sm">
            <Link to="/signup" className="font-medium text-slate-900 underline">Sign up again</Link>
            {' · '}
            <Link to="/login" className="font-medium text-slate-900 underline">Sign in</Link>
          </p>
        </div>
      </div>
    )
  }

  return <Spinner label="Setting up your workspace" />
}
