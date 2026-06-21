import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '../../auth/AuthContext'
import { useToast } from '../../components/ui/ToastProvider'
import { ApiError } from '../../lib/api'
import { roleHomePath } from '../../utils/roles'

const PASSWORD_POLICY_HELPER = 'Use at least 8 characters with uppercase, lowercase, and a number. Some workspaces may also require a special character.'

function toFriendlyError(error: unknown) {
  if (error instanceof ApiError) {
    const message = error.message.toLowerCase()

    if (message.includes('current password')) {
      return 'The current password is incorrect.'
    }

    if (message.includes('password must') || message.includes('strong')) {
      return 'Please choose a stronger password.'
    }

    return 'Unable to change password. Please try again.'
  }

  return error instanceof Error && error.message ? error.message : 'Unable to change password. Please try again.'
}

export function ChangePasswordPage() {
  const navigate = useNavigate()
  const { session, changePassword, logout, loading } = useAuth()
  const { pushToast } = useToast()
  const [form, setForm] = useState({
    currentPassword: '',
    newPassword: '',
    confirmPassword: '',
  })
  const [error, setError] = useState('')
  const [submitting, setSubmitting] = useState(false)

  async function handleSubmit() {
    if (!session || loading || submitting) return

    setError('')

    if (!form.currentPassword) {
      setError('Current password required')
      return
    }

    if (!form.newPassword) {
      setError('New password required')
      return
    }

    if (!form.confirmPassword) {
      setError('Confirm password required')
      return
    }

    if (form.newPassword !== form.confirmPassword) {
      setError('Passwords do not match.')
      return
    }

    setSubmitting(true)
    try {
      const message = await changePassword(form)
      pushToast({
        title: 'Password updated',
        description: message,
        tone: 'success',
      })
      navigate(roleHomePath(session.role, { title: session.title, permissions: session.permissions }), { replace: true })
    } catch (error) {
      setError(toFriendlyError(error))
    } finally {
      setSubmitting(false)
    }
  }

  async function handleSignOut() {
    await logout()
    navigate('/login', { replace: true })
  }

  return (
    <div className="relative min-h-dvh overflow-y-auto bg-app">
      <div className="absolute inset-0 bg-[radial-gradient(circle_at_top_left,rgba(183,226,109,0.14),transparent_30%),radial-gradient(circle_at_bottom_right,rgba(18,122,120,0.16),transparent_32%)]" />
      <div className="relative mx-auto flex min-h-dvh max-w-[720px] items-start px-4 py-10 sm:px-6 sm:py-12 lg:items-center lg:px-8">
        <div className="surface-card mx-auto w-full max-w-[520px] rounded-[30px]">
          <p className="eyebrow-accent text-xs font-semibold uppercase tracking-[0.2em]">Security Check</p>
          <h1 className="mt-3 font-heading text-[2.15rem] font-semibold tracking-[-0.04em] text-app">Change your temporary password</h1>
          <p className="mt-3 text-sm leading-6 text-muted">
            For your security, please change your temporary password before continuing.
          </p>

          <form
            className="mt-8 space-y-5"
            onSubmit={(event) => {
              event.preventDefault()
              void handleSubmit()
            }}
          >
            <label className="block space-y-2">
              <span className="text-sm font-medium text-app">Current password</span>
              <input
                type="password"
                value={form.currentPassword}
                onChange={(event) => setForm((current) => ({ ...current, currentPassword: event.target.value }))}
                className="field-input"
                autoComplete="current-password"
                disabled={submitting}
              />
            </label>

            <label className="block space-y-2">
              <span className="text-sm font-medium text-app">New password</span>
              <input
                type="password"
                value={form.newPassword}
                onChange={(event) => setForm((current) => ({ ...current, newPassword: event.target.value }))}
                className="field-input"
                autoComplete="new-password"
                disabled={submitting}
              />
            </label>

            <label className="block space-y-2">
              <span className="text-sm font-medium text-app">Confirm new password</span>
              <input
                type="password"
                value={form.confirmPassword}
                onChange={(event) => setForm((current) => ({ ...current, confirmPassword: event.target.value }))}
                className="field-input"
                autoComplete="new-password"
                disabled={submitting}
              />
            </label>

            <p className="rounded-[14px] border border-app bg-[var(--app-surface-strong)] px-4 py-3 text-sm text-muted">
              {PASSWORD_POLICY_HELPER}
            </p>

            {error ? (
              <div role="alert" className="rounded-[12px] border border-[var(--app-badge-danger-border)] bg-[var(--app-badge-danger-bg)] px-4 py-3 text-sm text-[var(--app-badge-danger-text)]">
                {error}
              </div>
            ) : null}

            <div className="flex flex-col gap-3 sm:flex-row">
              <button type="submit" className="button-primary flex-1" disabled={submitting}>
                {submitting ? 'Changing password...' : 'Change password'}
              </button>
              <button type="button" className="button-secondary flex-1" onClick={() => void handleSignOut()} disabled={submitting}>
                Sign out
              </button>
            </div>
          </form>
        </div>
      </div>
    </div>
  )
}
