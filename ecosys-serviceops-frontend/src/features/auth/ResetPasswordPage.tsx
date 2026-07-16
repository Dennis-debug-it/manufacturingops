import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { zodResolver } from '@hookform/resolvers/zod'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { CheckCircle2, Loader2 } from 'lucide-react'
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

export function ResetPasswordPage() {
  const [searchParams] = useSearchParams()
  const token = searchParams.get('token') ?? ''
  const isInvite = searchParams.get('mode') === 'invite'
  const navigate = useNavigate()
  const [submitError, setSubmitError] = useState<string | null>(null)
  const [success, setSuccess] = useState(false)

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
    <div className="grid min-h-screen bg-slate-950 lg:grid-cols-[1.1fr_0.9fr]">
      <div className="flex items-center justify-center px-6 py-12 lg:px-12">
        <div className="w-full max-w-md rounded-3xl border border-white/10 bg-white/5 p-8 text-white shadow-2xl backdrop-blur">
          <Logo tone="dark" className="h-12 w-auto" />
          <h1 className="mt-6 text-lg font-semibold tracking-tight">{isInvite ? 'Set your password' : 'Choose a new password'}</h1>

          {!token ? (
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
              <span>Your password has been reset. Redirecting you to sign in…</span>
            </div>
          ) : (
            <form className="mt-8 space-y-4" onSubmit={onSubmit}>
              <div>
                <label className="mb-2 block text-sm font-medium text-slate-200">New password</label>
                <input
                  type="password"
                  autoComplete="new-password"
                  className="h-12 w-full rounded-xl border border-white/10 bg-white/10 px-4 text-sm text-white outline-none placeholder:text-slate-400 focus:border-white/30"
                  {...register('newPassword')}
                />
                {errors.newPassword ? <p className="mt-2 text-sm text-rose-300">{errors.newPassword.message}</p> : null}
              </div>
              <div>
                <label className="mb-2 block text-sm font-medium text-slate-200">Confirm new password</label>
                <input
                  type="password"
                  autoComplete="new-password"
                  className="h-12 w-full rounded-xl border border-white/10 bg-white/10 px-4 text-sm text-white outline-none placeholder:text-slate-400 focus:border-white/30"
                  {...register('confirmPassword')}
                />
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
      <div className="hidden lg:flex lg:flex-col lg:justify-between lg:bg-[radial-gradient(circle_at_top_left,_rgba(255,255,255,0.18),_transparent_35%),linear-gradient(180deg,#0F3B5E_0%,#0B1620_100%)] lg:p-12">
        <div />
        <div className="max-w-xl text-white">
          <p className="text-sm font-semibold uppercase tracking-[0.2em] text-slate-300">Account recovery</p>
          <h2 className="mt-4 text-4xl font-semibold tracking-tight">Almost there.</h2>
          <p className="mt-4 text-base leading-7 text-slate-200">
            Choosing a new password will sign you out of every other active session.
          </p>
        </div>
      </div>
    </div>
  )
}
