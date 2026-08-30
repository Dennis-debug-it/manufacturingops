import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { zodResolver } from '@hookform/resolvers/zod'
import { Link } from 'react-router-dom'
import { CheckCircle2, Loader2 } from 'lucide-react'
import { api } from '../../shared/api/apiClient'
import { endpoints } from '../../shared/api/endpoints'
import { BrandWordmark } from '../../shared/components/BrandWordmark'
import { Crystal } from '../../shared/components/Crystal'
import { resolveErrorMessage } from '../../shared/api/apiError'

const forgotPasswordSchema = z.object({
  email: z.string().email('Enter a valid email address'),
})

type ForgotPasswordFormValues = z.infer<typeof forgotPasswordSchema>

export function ForgotPasswordPage() {
  const [submitError, setSubmitError] = useState<string | null>(null)
  const [message, setMessage] = useState<string | null>(null)

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<ForgotPasswordFormValues>({
    resolver: zodResolver(forgotPasswordSchema),
    defaultValues: { email: '' },
  })

  const onSubmit = handleSubmit(async (values) => {
    setSubmitError(null)
    try {
      const response = await api.post<{ message: string }>(endpoints.auth.forgotPassword, values)
      setMessage(response.message)
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
            <p className="mt-3 max-w-xs text-sm leading-6 text-white/70">Reset links expire after 45 minutes and revoke any active sessions once used.</p>
          </div>
        </div>
        <div className="flex flex-1 items-start justify-center px-6 py-8 lg:items-center lg:py-12">
          <div className="w-full max-w-md rounded-3xl border border-slate-200 bg-white p-8 shadow-xl">
            <BrandWordmark className="hidden lg:flex" />
            <h1 className="text-lg font-semibold tracking-tight text-slate-900 lg:mt-6">Reset your password</h1>

            {message ? (
              <div className="mt-8 space-y-4">
                <div className="flex items-start gap-3 rounded-xl border border-emerald-200 bg-emerald-50 px-4 py-3 text-sm text-emerald-800">
                  <CheckCircle2 className="mt-0.5 h-4 w-4 shrink-0" />
                  <span>{message}</span>
                </div>
                <Link to="/login" className="inline-flex h-12 w-full items-center justify-center rounded-xl border border-slate-200 bg-white px-4 text-sm font-semibold text-slate-700 transition hover:bg-[var(--app-primary-soft)]">
                  Back to sign in
                </Link>
              </div>
            ) : (
              <form className="mt-8 space-y-4" onSubmit={onSubmit}>
                <p className="text-sm text-slate-600">Enter the email address on your account and we'll send you a link to reset your password.</p>
                <div>
                  <label className="mb-2 block text-sm font-medium text-slate-700">Email</label>
                  <input
                    type="email"
                    autoComplete="email"
                    className="h-12 w-full rounded-xl border border-slate-300 bg-white px-4 text-sm text-slate-900 outline-none placeholder:text-slate-400 focus:border-[var(--app-primary)]"
                    {...register('email')}
                  />
                  {errors.email ? <p className="mt-2 text-sm text-rose-600">{errors.email.message}</p> : null}
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
                  Send reset link
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
          <h2 className="mt-4 text-4xl font-semibold tracking-tight">We'll get you back in.</h2>
          <p className="mt-4 text-base leading-7 text-white/70">
            Reset links expire after 45 minutes and any active sessions are revoked once your password is changed.
          </p>
        </div>
      </div>
    </div>
  )
}
