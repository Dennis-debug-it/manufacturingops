import { ArrowRight } from 'lucide-react'
import { useEffect, useRef, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useAuth } from '../../auth/AuthContext'
import { EcosysLogo } from '../../components/brand'
import { useThemeMode } from '../../context/ThemeContext'
import { cleanupBodyInteractivity, dispatchUiReset } from '../../utils/appCleanup'
import { roleHomePath } from '../../utils/roles'

export function LoginPage() {
  const navigate = useNavigate()
  const { login, loading } = useAuth()
  const { theme } = useThemeMode()
  const [form, setForm] = useState({ email: '', password: '' })
  const [error, setError] = useState('')
  const [submitPhase, setSubmitPhase] = useState<'signing-in' | 'loading-workspace' | null>(null)
  const isSubmitting = submitPhase !== null
  const passwordInputRef = useRef<HTMLInputElement | null>(null)

  useEffect(() => {
    cleanupBodyInteractivity()
    dispatchUiReset()
  }, [])

  function redirectToRoleHome(
    role: string,
    title?: string | null,
    permissions?: { canCreateWorkOrders?: boolean; canAssignWorkOrders?: boolean; canCompleteWorkOrders?: boolean } | null,
  ) {
    const targetPath = roleHomePath(role, { title, permissions })

    try {
      cleanupBodyInteractivity()
      dispatchUiReset()
      navigate(targetPath, { replace: true })
    } catch (error) {
      console.error('[auth] Failed to redirect after login.', { role, targetPath, error })
      setSubmitPhase(null)
      setError(`Signed in successfully, but we could not open your workspace automatically. Try visiting ${targetPath}.`)
    }
  }

  function redirectTo(path: string) {
    try {
      cleanupBodyInteractivity()
      dispatchUiReset()
      navigate(path, { replace: true })
    } catch (error) {
      console.error('[auth] Failed to redirect after login.', { path, error })
      setSubmitPhase(null)
      setError(`Signed in successfully, but we could not open ${path} automatically.`)
    }
  }

  async function submit() {
    if (loading || isSubmitting) return
    setError('')
    setSubmitPhase('signing-in')
    cleanupBodyInteractivity()
    dispatchUiReset()

    if (!form.email.trim() || !form.password) {
      setSubmitPhase(null)
      setError('Enter your email and password to continue.')
      return
    }

    try {
      const session = await login(form, (status) => setSubmitPhase(status))
      if (session.mustChangePassword) {
        redirectTo('/change-password')
        return
      }
      redirectToRoleHome(String(session.role), session.title, session.permissions)
    } catch (error) {
      setSubmitPhase(null)
      setForm((current) => ({ ...current, password: '' }))
      passwordInputRef.current?.focus()
      setError(error instanceof Error ? error.message : 'Login failed.')
    }
  }

  return (
    <div className="relative min-h-dvh overflow-y-auto bg-app">
      {isSubmitting ? (
        <div className="absolute inset-0 z-20 flex items-center justify-center bg-overlay/90 px-6 backdrop-blur-sm">
          <div className="w-full max-w-sm rounded-[24px] border border-app bg-[var(--app-surface)] px-8 py-10 text-center shadow-[var(--shadow-elevated)]">
            <div className="mx-auto flex h-20 w-20 items-center justify-center rounded-full border border-app bg-[var(--app-primary-soft)]">
              <div className="h-12 w-12 animate-spin rounded-full border-4 border-[var(--app-border-strong)] border-t-[var(--app-lime)] border-r-[var(--app-primary-strong)]" />
            </div>
            <p className="mt-6 font-heading text-lg font-semibold tracking-tight text-app">
              {submitPhase === 'loading-workspace' ? 'Loading your workspace...' : 'Signing you in...'}
            </p>
            <p className="mt-2 text-sm leading-6 text-muted">
              We&apos;re securing your session and preparing the right Ecosys workspace for you.
            </p>
          </div>
        </div>
      ) : null}
      <div className="absolute inset-0 bg-[radial-gradient(circle_at_top_left,rgba(183,226,109,0.14),transparent_30%),radial-gradient(circle_at_bottom_right,rgba(18,122,120,0.16),transparent_32%)]" />
      <div className="relative mx-auto flex min-h-dvh max-w-[1280px] items-start px-4 py-10 sm:px-6 sm:py-12 lg:items-center lg:px-8">
        <div className="grid w-full items-start gap-6 lg:grid-cols-[minmax(0,1fr)_minmax(360px,460px)] lg:items-center">
          <section className="auth-showcase hidden min-h-[680px] flex-col justify-center rounded-[32px] p-10 shadow-[var(--shadow-elevated)] lg:flex">
            <div data-testid="login-brand-logo">
              <EcosysLogo
                variant="darkPanel"
                size="lg"
                subtitle="ServiceOps Suite"
              />
            </div>
            <div className="mt-14 max-w-xl">
              <h1 className="font-heading text-[3.35rem] font-semibold leading-[1.02] tracking-[-0.06em] text-white">
                Service operations, simplified.
              </h1>
              <p className="mt-5 max-w-lg text-lg leading-8 text-[#F2F7F4]">
                Manage work orders, teams, clients, and assets from one workspace.
              </p>
              <p className="mt-6 text-sm font-medium leading-6 text-[#E0ECE6]">
                Built for field service, maintenance, and operations teams.
              </p>
            </div>
          </section>

          <section className="flex items-start lg:items-center">
            <div className="surface-card mx-auto w-full max-w-[460px] overflow-hidden rounded-[30px] !p-0">
              <div className="px-6 pb-2 pt-6 sm:px-8 sm:pt-8">
                <div className="mb-8 lg:hidden" data-testid="login-card-logo">
                  <EcosysLogo
                    variant={theme === 'light' ? 'lightPanel' : 'darkPanel'}
                    size="md"
                    subtitle="ServiceOps Suite"
                  />
                </div>
                <h2 className="mt-3 font-heading text-[2.15rem] font-semibold tracking-[-0.04em] text-app">Welcome back</h2>
                <p className="mt-3 text-sm leading-6 text-muted">Sign in to continue.</p>
              </div>

              <form
                className="space-y-5 px-6 py-6 sm:px-8 sm:pb-8"
                onSubmit={(event) => {
                  event.preventDefault()
                  void submit()
                }}
              >
                <label className="block space-y-2">
                  <span className="text-sm font-medium text-app">Email</span>
                  <input
                    type="email"
                    value={form.email}
                    onChange={(event) => setForm((current) => ({ ...current, email: event.target.value }))}
                    className="field-input"
                    placeholder="name@company.com"
                    autoComplete="email"
                    disabled={isSubmitting}
                  />
                </label>
                <label className="block space-y-2">
                  <span className="text-sm font-medium text-app">Password</span>
                  <input
                    type="password"
                    value={form.password}
                    onChange={(event) => setForm((current) => ({ ...current, password: event.target.value }))}
                    ref={passwordInputRef}
                    className="field-input"
                    placeholder="Enter your password"
                    autoComplete="current-password"
                    disabled={isSubmitting}
                  />
                </label>
                {error ? (
                  <div role="alert" className="rounded-[12px] border border-[var(--app-badge-danger-border)] bg-[var(--app-badge-danger-bg)] px-4 py-3 text-sm text-[var(--app-badge-danger-text)]">
                    {error}
                  </div>
                ) : null}
                <div className="flex items-center justify-end">
                  <Link to="/forgot-password" className="text-sm font-semibold text-accent-strong hover:opacity-80">
                    Forgot password?
                  </Link>
                </div>
                <button
                  type="submit"
                  className="button-primary inline-flex w-full items-center justify-center gap-2"
                  disabled={isSubmitting}
                >
                  {submitPhase === 'loading-workspace' ? 'Loading your workspace...' : isSubmitting ? 'Signing you in...' : 'Login'}
                  <ArrowRight className="h-4 w-4" />
                </button>
              </form>

              <div className="border-t border-app bg-[var(--app-surface-strong)] px-6 py-5 text-center sm:px-8">
                <p className="text-sm text-muted">
                  New to Ecosys?{' '}
                  <Link to="/get-started" className="font-semibold text-accent-strong hover:opacity-80">
                    Get started
                  </Link>
                </p>
              </div>
            </div>
          </section>
        </div>
      </div>
    </div>
  )
}
