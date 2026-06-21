import { ArrowLeft, Mail } from 'lucide-react'
import { useState } from 'react'
import { Link } from 'react-router-dom'
import { EcosysLogo } from '../../components/brand'
import { authService } from '../../services/authService'

export function ForgotPasswordPage() {
  const [email, setEmail] = useState('')
  const [message, setMessage] = useState('')
  const [error, setError] = useState('')
  const [submitting, setSubmitting] = useState(false)

  async function submit() {
    if (submitting) return
    setError('')
    setMessage('')

    if (!email.trim()) {
      setError('Enter your email address to continue.')
      return
    }

    setSubmitting(true)
    try {
      const response = await authService.forgotPassword(email)
      setMessage(response.message)
    } catch (submitError) {
      setError(submitError instanceof Error ? submitError.message : 'We could not submit your request right now.')
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
                subtitle="Password Assistance"
                subtitleClassName="text-[0.72rem] font-semibold normal-case tracking-[0.08em] auth-showcase-muted"
              />
              <div className="mt-8 max-w-xl">
                <p className="text-xs font-semibold uppercase tracking-[0.18em] text-[#dded49]">Password Recovery</p>
                <h1 className="mt-4 font-heading text-[2.8rem] font-semibold tracking-[-0.05em] text-white">
                  Reset access without exposing whether an account exists.
                </h1>
                <p className="auth-showcase-muted mt-4 text-sm leading-7">
                  Enter the email address linked to your Ecosys workspace and we&apos;ll send reset instructions if the account is active.
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
                  subtitle="Password Assistance"
                  subtitleClassName="text-[0.72rem] font-semibold normal-case tracking-[0.08em] text-muted"
                />
              </div>
              <p className="mt-6 text-xs font-semibold uppercase tracking-[0.18em] text-accent">Forgot Password</p>
              <h2 className="mt-3 font-heading text-[2rem] font-semibold tracking-[-0.04em] text-app">Request a reset link</h2>
              <p className="mt-3 text-sm leading-6 text-muted">
                  We&apos;ll always show the same response for privacy and account security.
              </p>

              <form
                className="mt-8 space-y-4"
                onSubmit={(event) => {
                  event.preventDefault()
                  void submit()
                }}
              >
                <label className="block space-y-2">
                  <span className="text-sm font-medium text-app">Email address</span>
                  <div className="relative">
                    <Mail className="pointer-events-none absolute left-4 top-1/2 h-4 w-4 -translate-y-1/2 text-muted" />
                    <input
                      type="email"
                      value={email}
                      onChange={(event) => setEmail(event.target.value)}
                      className="field-input pl-11"
                      placeholder="name@example.com"
                      autoComplete="email"
                      disabled={submitting}
                    />
                  </div>
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
                  {submitting ? 'Sending reset link...' : 'Send reset link'}
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
