import { ArrowLeft, KeyRound } from 'lucide-react'
import { useMemo, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { EcosysLogo } from '../../components/brand'
import { authService } from '../../services/authService'

export function ResetPasswordPage() {
  const [searchParams] = useSearchParams()
  const token = useMemo(() => searchParams.get('token')?.trim() ?? '', [searchParams])
  const mode = useMemo(() => searchParams.get('mode')?.trim().toLowerCase() ?? '', [searchParams])
  const isInviteMode = mode === 'invite' || mode === 'setup' || mode === 'activate'
  const [form, setForm] = useState({ newPassword: '', confirmPassword: '' })
  const [message, setMessage] = useState('')
  const [error, setError] = useState('')
  const [submitting, setSubmitting] = useState(false)

  async function submit() {
    if (submitting) return
    setError('')
    setMessage('')

    if (!token) {
      setError(isInviteMode
        ? 'This account setup link is invalid or has expired. Ask your administrator to resend the invitation.'
        : 'This password reset link is invalid or has expired. Please request a new one.')
      return
    }

    if (!form.newPassword || !form.confirmPassword) {
      setError('Enter and confirm your new password to continue.')
      return
    }

    if (form.newPassword !== form.confirmPassword) {
      setError('New password and confirmation password must match.')
      return
    }

    setSubmitting(true)
    try {
      const response = await authService.resetPassword({
        token,
        newPassword: form.newPassword,
        confirmPassword: form.confirmPassword,
      })
      setMessage(isInviteMode ? 'Your password is set. You can now sign in.' : response.message)
      setForm({ newPassword: '', confirmPassword: '' })
    } catch (submitError) {
      const nextMessage = submitError instanceof Error ? submitError.message : 'We could not reset your password.'
      if (/invalid or has expired/i.test(nextMessage)) {
        setError(isInviteMode
          ? 'This account setup link is invalid or has expired. Ask your administrator to resend the invitation.'
          : 'This password reset link is invalid or has expired. Please request a new one.')
      } else {
        setError(nextMessage)
      }
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="relative min-h-dvh overflow-y-auto bg-app">
      <div className="relative mx-auto flex min-h-dvh max-w-container items-start px-4 py-10 sm:px-6 sm:py-12 lg:px-8">
        <div className="grid w-full items-start gap-6 lg:grid-cols-[minmax(0,0.92fr)_minmax(320px,460px)] lg:items-center">
          <section className="auth-showcase hidden rounded-[28px] p-8 shadow-[var(--shadow-elevated)] lg:flex lg:min-h-[680px] lg:flex-col lg:justify-between">
            <div>
              <EcosysLogo
                variant="light"
                size="lg"
                subtitle={isInviteMode ? 'Secure Account Setup' : 'Secure Password Reset'}
                subtitleClassName="text-[0.72rem] font-semibold normal-case tracking-[0.08em] auth-showcase-muted"
              />
              <div className="mt-8 max-w-xl">
                <p className="text-xs font-semibold uppercase tracking-[0.18em] text-[#dded49]">{isInviteMode ? 'Activate Access' : 'Reset Credentials'}</p>
                <h1 className="mt-4 font-heading text-[2.8rem] font-semibold tracking-[-0.05em] text-white">
                  {isInviteMode ? 'Set your password to activate your Ecosys account.' : 'Choose a new password and sign back in securely.'}
                </h1>
                <p className="auth-showcase-muted mt-4 text-sm leading-7">
                  {isInviteMode
                    ? 'Your setup link is single-use and expires automatically. Choose a strong password with uppercase, lowercase, numbers, and symbols.'
                    : 'Reset links are single-use and expire automatically. Use a strong password with uppercase, lowercase, numbers, and symbols.'}
                </p>
              </div>
            </div>
          </section>

          <section className="flex items-start lg:items-center">
            <div className="surface-card mx-auto w-full max-w-[460px]">
              <div className="lg:hidden">
                <EcosysLogo
                  variant="dark"
                  size="md"
                  subtitle={isInviteMode ? 'Secure Account Setup' : 'Secure Password Reset'}
                  subtitleClassName="text-[0.72rem] font-semibold normal-case tracking-[0.08em] text-muted"
                />
              </div>
              <p className="mt-6 text-xs font-semibold uppercase tracking-[0.18em] text-accent">{isInviteMode ? 'Account Setup' : 'Reset Password'}</p>
              <h2 className="mt-3 font-heading text-[2rem] font-semibold tracking-[-0.04em] text-app">{isInviteMode ? 'Set up your password' : 'Set a new password'}</h2>
              <p className="mt-3 text-sm leading-6 text-muted">
                {isInviteMode
                  ? 'Set your password to activate your account and complete access to Ecosys.'
                  : 'Your new password will replace the current one immediately after a valid reset.'}
              </p>

              <form
                className="mt-8 space-y-4"
                onSubmit={(event) => {
                  event.preventDefault()
                  void submit()
                }}
              >
                <label className="block space-y-2">
                  <span className="text-sm font-medium text-app">New password</span>
                  <div className="relative">
                    <KeyRound className="pointer-events-none absolute left-4 top-1/2 h-4 w-4 -translate-y-1/2 text-muted" />
                    <input
                      type="password"
                      value={form.newPassword}
                      onChange={(event) => setForm((current) => ({ ...current, newPassword: event.target.value }))}
                      className="field-input pl-11"
                      placeholder="Enter a strong password"
                      autoComplete="new-password"
                      disabled={submitting}
                    />
                  </div>
                </label>

                <label className="block space-y-2">
                  <span className="text-sm font-medium text-app">Confirm password</span>
                  <input
                    type="password"
                    value={form.confirmPassword}
                    onChange={(event) => setForm((current) => ({ ...current, confirmPassword: event.target.value }))}
                    className="field-input"
                    placeholder="Re-enter your new password"
                    autoComplete="new-password"
                    disabled={submitting}
                  />
                </label>

                {message ? (
                  <div role="status" className="rounded-[12px] border border-[var(--app-badge-success-border)] bg-[var(--app-badge-success-bg)] px-4 py-3 text-sm text-[var(--app-badge-success-text)]">
                    {message}
                  </div>
                ) : null}

                {error ? (
                  <div role="alert" className="rounded-[12px] border border-[var(--app-badge-danger-border)] bg-[var(--app-badge-danger-bg)] px-4 py-3 text-sm text-[var(--app-badge-danger-text)]">
                    {error}
                  </div>
                ) : null}

                <button type="submit" className="button-primary inline-flex w-full items-center justify-center" disabled={submitting}>
                  {submitting ? (isInviteMode ? 'Setting password...' : 'Resetting password...') : isInviteMode ? 'Set password' : 'Reset password'}
                </button>
              </form>

              <Link to="/login" className="mt-6 inline-flex items-center gap-2 text-sm font-semibold text-accent-strong hover:opacity-80">
                <ArrowLeft className="h-4 w-4" />
                Back to login
              </Link>
            </div>
          </section>
        </div>
      </div>
    </div>
  )
}
