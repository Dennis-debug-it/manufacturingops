import { useEffect, useRef, useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { zodResolver } from '@hookform/resolvers/zod'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { CheckCircle2, Eye, EyeOff, Loader2 } from 'lucide-react'
import { api } from '../../shared/api/apiClient'
import { endpoints } from '../../shared/api/endpoints'
import { Logo } from '../../shared/components/Logo'
import { resolveErrorMessage } from '../../shared/api/apiError'

const resetPasswordSchema = z
  .object({
    newPassword: z.string().min(8, 'Password must be at least 8 characters'),
    confirmPassword: z.string().min(1, 'Please confirm your new password'),
  })
  .refine((values) => values.newPassword === values.confirmPassword, {
    message: 'Passwords do not match',
    path: ['confirmPassword'],
  })

type ResetPasswordFormValues = z.infer<typeof resetPasswordSchema>

type InvitationStatus = 'VALID' | 'EXPIRED' | 'USED' | 'SUPERSEDED' | 'REVOKED' | 'INVALID'

const invitationMessages: Record<Exclude<InvitationStatus, 'VALID'>, string> = {
  EXPIRED: 'This invitation has expired. Request a new invitation to finish setting up your account.',
  USED: 'This invitation has already been used. Sign in with the password you chose.',
  SUPERSEDED: 'A newer invitation has replaced this link. Open the latest email or request a new invitation.',
  REVOKED: 'This invitation is no longer available. Contact your workspace administrator or request a new invitation.',
  INVALID: 'This invitation link is invalid or incomplete. Request a new invitation or contact your workspace administrator.',
}

export function ResetPasswordPage() {
  const [searchParams] = useSearchParams()
  const token = searchParams.get('token') ?? ''
  const isInvite = searchParams.get('mode') === 'invite'
  const navigate = useNavigate()
  const [submitError, setSubmitError] = useState<string | null>(null)
  const [success, setSuccess] = useState(false)
  const [invitationStatus, setInvitationStatus] = useState<InvitationStatus | null>(null)
  const [validationFailed, setValidationFailed] = useState(false)
  const [validationAttempt, setValidationAttempt] = useState(0)
  const [recoveryEmail, setRecoveryEmail] = useState('')
  const [recoveryMessage, setRecoveryMessage] = useState<string | null>(null)
  const [recovering, setRecovering] = useState(false)

  useEffect(() => {
    if (!isInvite) return
    if (!token) { setInvitationStatus('INVALID'); return }
    let active = true
    setInvitationStatus(null)
    setValidationFailed(false)
    void api.post<{ status: InvitationStatus }>(endpoints.auth.validateInvitation, { token })
      .then((response) => { if (active) setInvitationStatus(response.status) })
      .catch(() => { if (active) setValidationFailed(true) })
    return () => { active = false }
  }, [isInvite, token, validationAttempt])

  const requestInvitation = async (event: React.FormEvent) => {
    event.preventDefault()
    setRecovering(true)
    setSubmitError(null)
    try {
      const response = await api.post<{ message: string }>(endpoints.auth.resendInvitation, { email: recoveryEmail.trim() })
      setRecoveryMessage(response.message)
    } catch (error) {
      setSubmitError(resolveErrorMessage(error))
    } finally {
      setRecovering(false)
    }
  }
  const [showNewPassword, setShowNewPassword] = useState(false)
  const [showConfirmPassword, setShowConfirmPassword] = useState(false)
  const newPasswordVisibilityTimer = useRef<ReturnType<typeof setTimeout> | null>(null)
  const confirmPasswordVisibilityTimer = useRef<ReturnType<typeof setTimeout> | null>(null)

  const toggleTemporaryVisibility = (
    isVisible: boolean,
    setIsVisible: (visible: boolean) => void,
    timer: typeof newPasswordVisibilityTimer,
  ) => {
    if (timer.current) {
      clearTimeout(timer.current)
      timer.current = null
    }

    if (isVisible) {
      setIsVisible(false)
      return
    }

    setIsVisible(true)
    timer.current = setTimeout(() => {
      setIsVisible(false)
      timer.current = null
    }, 5000)
  }

  useEffect(() => () => {
    if (newPasswordVisibilityTimer.current) clearTimeout(newPasswordVisibilityTimer.current)
    if (confirmPasswordVisibilityTimer.current) clearTimeout(confirmPasswordVisibilityTimer.current)
  }, [])

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<ResetPasswordFormValues>({
    resolver: zodResolver(resetPasswordSchema),
    defaultValues: { newPassword: '', confirmPassword: '' },
  })

  const onSubmit = handleSubmit(async (values) => {
    setSubmitError(null)
    try {
      await api.post<{ message: string }>(endpoints.auth.resetPassword, {
        token,
        newPassword: values.newPassword,
        confirmPassword: values.confirmPassword,
      })
      setSuccess(true)
      setTimeout(() => navigate(isInvite ? '/login?welcome=1' : '/login', { replace: true }), 2000)
    } catch (error) {
      setSubmitError(resolveErrorMessage(error))
      if (isInvite) setValidationAttempt((attempt) => attempt + 1)
    }
  })

  return (
    <div className="grid min-h-screen bg-slate-950 lg:grid-cols-[1.1fr_0.9fr]">
      <div className="flex items-center justify-center px-6 py-12 lg:px-12">
        <div className="w-full max-w-md rounded-3xl border border-white/10 bg-white/5 p-8 text-white shadow-2xl backdrop-blur">
          <Logo tone="dark" className="h-12 w-auto" />
          <h1 className="mt-6 text-lg font-semibold tracking-tight">{isInvite ? 'Set your password' : 'Choose a new password'}</h1>

          {isInvite && !success && invitationStatus !== 'VALID' ? (
            <div className="mt-8 space-y-4">
              {!invitationStatus ? (
                validationFailed ? <div className="space-y-3"><p role="alert" className="text-sm text-rose-100">We could not check this invitation. Please try again.</p><button type="button" onClick={() => setValidationAttempt((attempt) => attempt + 1)} className="rounded-xl bg-white px-4 py-3 text-sm font-semibold text-slate-950">Try again</button></div>
                  : <p role="status" className="flex items-center gap-2 text-sm text-slate-200"><Loader2 className="h-4 w-4 animate-spin" />Checking your invitation…</p>
              ) : <p role="alert" className="rounded-xl border border-amber-400/30 bg-amber-500/10 px-4 py-3 text-sm text-amber-100">{invitationMessages[invitationStatus]}</p>}
              {invitationStatus && invitationStatus !== 'USED' ? (
                recoveryMessage ? <p role="status" className="text-sm text-emerald-100">{recoveryMessage}</p> : (
                  <form onSubmit={requestInvitation} className="space-y-3">
                    <label htmlFor="invitation-email" className="block text-sm font-medium text-slate-200">Your email address</label>
                    <input id="invitation-email" required type="email" autoComplete="email" value={recoveryEmail} onChange={(event) => setRecoveryEmail(event.target.value)} className="h-12 w-full rounded-xl border border-white/10 bg-white/10 px-4 text-sm text-white outline-none focus:border-white/30" />
                    <button disabled={recovering} className="inline-flex min-h-12 w-full items-center justify-center rounded-xl bg-white px-4 py-3 text-sm font-semibold text-slate-950 disabled:opacity-60">{recovering ? 'Requesting…' : 'Request new invitation'}</button>
                  </form>
                )
              ) : null}
              {submitError ? <p role="alert" className="text-sm text-rose-100">{submitError}</p> : null}
              <Link to="/login" className="block text-center text-sm font-medium text-slate-300 hover:text-white">Back to sign in</Link>
            </div>
          ) : !token ? (
            <div className="mt-8 space-y-4">
              <div className="rounded-xl border border-rose-400/30 bg-rose-500/10 px-4 py-3 text-sm text-rose-100">
                This link is missing its reset token. Please request a new link.
              </div>
              <Link to="/forgot-password" className="inline-flex h-12 w-full items-center justify-center rounded-xl bg-white px-4 text-sm font-semibold text-slate-950 transition hover:bg-slate-100">
                Request a new link
              </Link>
            </div>
          ) : success ? (
            <div className="mt-8 flex items-start gap-3 rounded-xl border border-emerald-400/30 bg-emerald-500/10 px-4 py-3 text-sm text-emerald-100">
              <CheckCircle2 className="mt-0.5 h-4 w-4 shrink-0" />
              <span>Your password has been reset. Redirecting you to sign inâ€¦</span>
            </div>
          ) : (
            <form className="mt-8 space-y-4" onSubmit={onSubmit}>
              <div>
                <label htmlFor="new-password" className="mb-2 block text-sm font-medium text-slate-200">New password</label>
                <div className="relative">
                  <input
                    id="new-password"
                    type={showNewPassword ? 'text' : 'password'}
                    autoComplete="new-password"
                    className="h-12 w-full rounded-xl border border-white/10 bg-white/10 py-0 pl-4 pr-12 text-sm text-white outline-none placeholder:text-slate-400 focus:border-white/30"
                    {...register('newPassword')}
                  />
                  <button
                    type="button"
                    onClick={() => toggleTemporaryVisibility(showNewPassword, setShowNewPassword, newPasswordVisibilityTimer)}
                    aria-label={showNewPassword ? 'Conceal entered characters' : 'Reveal entered characters'}
                    title={showNewPassword ? 'Hide new password' : 'Show new password'}
                    aria-pressed={showNewPassword}
                    className="absolute inset-y-0 right-0 flex w-12 items-center justify-center rounded-r-xl text-slate-300 transition hover:text-white focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-white"
                  >
                    {showNewPassword ? <EyeOff className="h-5 w-5" aria-hidden="true" /> : <Eye className="h-5 w-5" aria-hidden="true" />}
                  </button>
                </div>
                {errors.newPassword ? <p className="mt-2 text-sm text-rose-300">{errors.newPassword.message}</p> : null}
              </div>
              <div>
                <label htmlFor="confirm-new-password" className="mb-2 block text-sm font-medium text-slate-200">Confirm password</label>
                <div className="relative">
                  <input
                    id="confirm-new-password"
                    type={showConfirmPassword ? 'text' : 'password'}
                    autoComplete="new-password"
                    className="h-12 w-full rounded-xl border border-white/10 bg-white/10 py-0 pl-4 pr-12 text-sm text-white outline-none placeholder:text-slate-400 focus:border-white/30"
                    {...register('confirmPassword')}
                  />
                  <button
                    type="button"
                    onClick={() => toggleTemporaryVisibility(showConfirmPassword, setShowConfirmPassword, confirmPasswordVisibilityTimer)}
                    aria-label={showConfirmPassword ? 'Conceal entered characters' : 'Reveal entered characters'}
                    title={showConfirmPassword ? 'Hide password confirmation' : 'Show password confirmation'}
                    aria-pressed={showConfirmPassword}
                    className="absolute inset-y-0 right-0 flex w-12 items-center justify-center rounded-r-xl text-slate-300 transition hover:text-white focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-white"
                  >
                    {showConfirmPassword ? <EyeOff className="h-5 w-5" aria-hidden="true" /> : <Eye className="h-5 w-5" aria-hidden="true" />}
                  </button>
                </div>
                {errors.confirmPassword ? <p className="mt-2 text-sm text-rose-300">{errors.confirmPassword.message}</p> : null}
              </div>
              {submitError ? (
                <div className="rounded-xl border border-rose-400/30 bg-rose-500/10 px-4 py-3 text-sm text-rose-100">
                  {submitError}
                </div>
              ) : null}
              <button
                type="submit"
                disabled={isSubmitting}
                className="inline-flex h-12 w-full items-center justify-center gap-2 rounded-xl bg-white px-4 text-sm font-semibold text-slate-950 transition hover:bg-slate-100 disabled:cursor-not-allowed disabled:opacity-60"
              >
                {isSubmitting ? <Loader2 className="h-4 w-4 animate-spin" /> : null}
                {isInvite ? 'Set password' : 'Reset password'}
              </button>
              <Link to="/login" className="block text-center text-sm font-medium text-slate-300 hover:text-white">
                Back to sign in
              </Link>
            </form>
          )}
        </div>
      </div>
      <div className="hidden lg:flex lg:flex-col lg:justify-between lg:bg-[radial-gradient(circle_at_top_left,_rgba(255,255,255,0.18),_transparent_35%),linear-gradient(180deg,#15806A_0%,#0B1620_100%)] lg:p-12">
        <div />
        <div className="max-w-xl text-white">
          <p className="text-sm font-semibold uppercase tracking-[0.2em] text-emerald-300">{isInvite ? 'Welcome to ServiceOps' : 'Account recovery'}</p>
          <h2 className="mt-4 text-4xl font-semibold tracking-tight">Almost there.</h2>
          <p className="mt-4 text-base leading-7 text-slate-200">
            {isInvite ? 'Set your password, then start using your workspace. You can complete company settings after signing in.' : 'Choosing a new password will sign you out of every other active session.'}
          </p>
        </div>
      </div>
    </div>
  )
}
