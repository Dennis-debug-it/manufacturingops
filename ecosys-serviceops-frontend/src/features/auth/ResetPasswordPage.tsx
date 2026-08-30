import { useEffect, useRef, useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { zodResolver } from '@hookform/resolvers/zod'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { CheckCircle2, Eye, EyeOff, Loader2 } from 'lucide-react'
import { api } from '../../shared/api/apiClient'
import { endpoints } from '../../shared/api/endpoints'
import { BrandWordmark } from '../../shared/components/BrandWordmark'
import { Crystal } from '../../shared/components/Crystal'
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

export function ResetPasswordPage() {
  const [searchParams] = useSearchParams()
  const token = searchParams.get('token') ?? ''
  const isInvite = searchParams.get('mode') === 'invite'
  const navigate = useNavigate()
  const [submitError, setSubmitError] = useState<string | null>(null)
  const [success, setSuccess] = useState(false)
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
      setTimeout(() => navigate('/login', { replace: true }), 2000)
    } catch (error) {
      setSubmitError(resolveErrorMessage(error))
    }
  })

  return (
    <div className="grid min-h-screen bg-white lg:grid-cols-[1fr_1fr]">
      <div className="flex flex-col">
        {/* Mobile-only brand header — desktop gets the marketing panel instead (see below),
            so without this the form would sit centered in a mostly-empty full-height column. */}
        <div className="relative overflow-hidden bg-gradient-to-br from-[#073F43] to-[#052D30] px-6 pb-8 pt-10 lg:hidden">
          <Crystal variant="analytics" className="absolute inset-0 opacity-60" />
          <div className="relative">
            <BrandWordmark tone="dark" />
            <p className="mt-3 max-w-xs text-sm leading-6 text-white/70">Choosing a new password signs you out of every other active session.</p>
          </div>
        </div>
        <div className="flex flex-1 items-start justify-center px-6 py-8 lg:items-center lg:py-12">
          <div className="w-full max-w-md rounded-3xl border border-slate-200 bg-white p-8 shadow-xl">
            <BrandWordmark className="hidden lg:flex" />
            <h1 className="text-lg font-semibold tracking-tight text-slate-900 lg:mt-6">{isInvite ? 'Set your password' : 'Choose a new password'}</h1>

            {!token ? (
              <div className="mt-8 space-y-4">
                <div className="rounded-xl border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-700">
                  This link is missing its reset token. Please request a new link.
                </div>
                <Link to="/forgot-password" className="inline-flex h-12 w-full items-center justify-center rounded-xl bg-[var(--app-primary)] px-4 text-sm font-semibold text-white transition hover:bg-[var(--app-secondary)]">
                  Request a new link
                </Link>
              </div>
            ) : success ? (
              <div className="mt-8 flex items-start gap-3 rounded-xl border border-emerald-200 bg-emerald-50 px-4 py-3 text-sm text-emerald-800">
                <CheckCircle2 className="mt-0.5 h-4 w-4 shrink-0" />
                <span>Your password has been reset. Redirecting you to sign in…</span>
              </div>
            ) : (
              <form className="mt-8 space-y-4" onSubmit={onSubmit}>
                <div>
                  <label htmlFor="new-password" className="mb-2 block text-sm font-medium text-slate-700">New password</label>
                  <div className="relative">
                    <input
                      id="new-password"
                      type={showNewPassword ? 'text' : 'password'}
                      autoComplete="new-password"
                      className="h-12 w-full rounded-xl border border-slate-300 bg-white py-0 pl-4 pr-12 text-sm text-slate-900 outline-none placeholder:text-slate-400 focus:border-[var(--app-primary)]"
                      {...register('newPassword')}
                    />
                    <button
                      type="button"
                      onClick={() => toggleTemporaryVisibility(showNewPassword, setShowNewPassword, newPasswordVisibilityTimer)}
                      aria-label={showNewPassword ? 'Conceal entered characters' : 'Reveal entered characters'}
                      title={showNewPassword ? 'Hide new password' : 'Show new password'}
                      aria-pressed={showNewPassword}
                      className="absolute inset-y-0 right-0 flex w-12 items-center justify-center rounded-r-xl text-slate-500 transition hover:text-slate-900 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-[var(--app-primary)]"
                    >
                      {showNewPassword ? <EyeOff className="h-5 w-5" aria-hidden="true" /> : <Eye className="h-5 w-5" aria-hidden="true" />}
                    </button>
                  </div>
                  {errors.newPassword ? <p className="mt-2 text-sm text-rose-600">{errors.newPassword.message}</p> : null}
                </div>
                <div>
                  <label htmlFor="confirm-new-password" className="mb-2 block text-sm font-medium text-slate-700">Confirm password</label>
                  <div className="relative">
                    <input
                      id="confirm-new-password"
                      type={showConfirmPassword ? 'text' : 'password'}
                      autoComplete="new-password"
                      className="h-12 w-full rounded-xl border border-slate-300 bg-white py-0 pl-4 pr-12 text-sm text-slate-900 outline-none placeholder:text-slate-400 focus:border-[var(--app-primary)]"
                      {...register('confirmPassword')}
                    />
                    <button
                      type="button"
                      onClick={() => toggleTemporaryVisibility(showConfirmPassword, setShowConfirmPassword, confirmPasswordVisibilityTimer)}
                      aria-label={showConfirmPassword ? 'Conceal entered characters' : 'Reveal entered characters'}
                      title={showConfirmPassword ? 'Hide password confirmation' : 'Show password confirmation'}
                      aria-pressed={showConfirmPassword}
                      className="absolute inset-y-0 right-0 flex w-12 items-center justify-center rounded-r-xl text-slate-500 transition hover:text-slate-900 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-[var(--app-primary)]"
                    >
                      {showConfirmPassword ? <EyeOff className="h-5 w-5" aria-hidden="true" /> : <Eye className="h-5 w-5" aria-hidden="true" />}
                    </button>
                  </div>
                  {errors.confirmPassword ? <p className="mt-2 text-sm text-rose-600">{errors.confirmPassword.message}</p> : null}
                </div>
                {submitError ? (
                  <div className="rounded-xl border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-700">
                    {submitError}
                  </div>
                ) : null}
                <button
                  type="submit"
                  disabled={isSubmitting}
                  className="inline-flex h-12 w-full items-center justify-center gap-2 rounded-xl bg-[var(--app-primary)] px-4 text-sm font-semibold text-white transition hover:bg-[var(--app-secondary)] disabled:cursor-not-allowed disabled:opacity-60"
                >
                  {isSubmitting ? <Loader2 className="h-4 w-4 animate-spin" /> : null}
                  {isInvite ? 'Set password' : 'Reset password'}
                </button>
                <Link to="/login" className="block text-center text-sm font-medium text-slate-500 hover:text-slate-900">
                  Back to sign in
                </Link>
              </form>
            )}
          </div>
        </div>
      </div>
      <div className="relative hidden overflow-hidden bg-gradient-to-br from-[#073F43] to-[#052D30] lg:flex lg:flex-col lg:justify-between lg:p-12">
        <Crystal variant="analytics" className="absolute inset-0 opacity-70" />
        <div className="relative" />
        <div className="relative max-w-xl text-white">
          <p className="text-sm font-semibold uppercase tracking-[0.2em] text-[var(--app-lime)]">Account recovery</p>
          <h2 className="mt-4 text-4xl font-semibold tracking-tight">Almost there.</h2>
          <p className="mt-4 text-base leading-7 text-white/70">
            Choosing a new password will sign you out of every other active session.
          </p>
        </div>
      </div>
    </div>
  )
}
