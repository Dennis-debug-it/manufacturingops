import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { zodResolver } from '@hookform/resolvers/zod'
import { Navigate, useNavigate } from 'react-router-dom'
import { api } from '../../shared/api/apiClient'
import { endpoints } from '../../shared/api/endpoints'
import { resolveErrorMessage } from '../../shared/api/apiError'
import { Logo } from '../../shared/components/Logo'
import { useAuth } from '../../shared/hooks/useAuth'

const changePasswordSchema = z
  .object({
    currentPassword: z.string().min(1, 'Enter the temporary password you signed in with'),
    newPassword: z.string().min(8, 'Password must be at least 8 characters'),
    confirmPassword: z.string().min(1, 'Please confirm your new password'),
  })
  .refine((values) => values.newPassword === values.confirmPassword, { message: 'Passwords do not match', path: ['confirmPassword'] })
  .refine((values) => values.newPassword !== values.currentPassword, { message: 'Choose a password different from the temporary one', path: ['newPassword'] })

type ChangePasswordFormValues = z.infer<typeof changePasswordSchema>

/**
 * First sign-in with a password an administrator set or generated. The login token only allows
 * POST /api/auth/change-password until this is done, so this page lives outside the app shell (whose
 * own requests would all be refused). No email is involved — it works for accounts without one —
 * and it signs straight back in with the new password to get an unrestricted session.
 */
export function ChangePasswordPage() {
  const { user, tenant, isReady, login, logout } = useAuth()
  const navigate = useNavigate()
  const [submitError, setSubmitError] = useState<string | null>(null)
  const { register, handleSubmit, formState: { errors, isSubmitting } } = useForm<ChangePasswordFormValues>({
    resolver: zodResolver(changePasswordSchema),
    defaultValues: { currentPassword: '', newPassword: '', confirmPassword: '' },
  })

  if (!isReady) return <div className="min-h-screen bg-slate-950" />
  if (!user) return <Navigate to="/login" replace />
  if (!user.mustChangePassword) return <Navigate to="/" replace />

  const signInHandle = user.hasEmail ? user.email : user.username

  const onSubmit = handleSubmit(async (values) => {
    setSubmitError(null)
    try {
      await api.post<{ message: string }>(endpoints.auth.changePassword, values)
    } catch (error) {
      setSubmitError(resolveErrorMessage(error))
      return
    }

    try {
      const session = await login({
        emailOrUsername: signInHandle ?? user.email,
        password: values.newPassword,
        ...(user.hasEmail ? {} : { workspace: tenant?.companyName ?? '' }),
      })
      navigate(session.user.role === 'super_admin' || session.user.role === 'platform_sales' ? '/platform/dashboard' : '/', { replace: true })
    } catch {
      // The password is changed either way; fall back to a normal sign-in.
      await logout()
      navigate('/login', { replace: true })
    }
  })

  const inputClass = 'h-12 w-full rounded-xl border border-white/10 bg-white/10 px-4 text-sm text-white outline-none focus:border-white/30'

  return (
    <div className="flex min-h-screen items-center justify-center bg-slate-950 px-6 py-12">
      <div className="w-full max-w-md rounded-3xl border border-white/10 bg-white/5 p-8 text-white shadow-2xl backdrop-blur">
        <Logo tone="dark" className="h-12 w-auto" />
        <h1 className="mt-6 text-lg font-semibold tracking-tight">Choose your own password</h1>
        <p className="mt-2 text-sm text-slate-300">
          You signed in{signInHandle ? ` as ${signInHandle}` : ''} with a temporary password. Set a new one to continue.
        </p>

        <form className="mt-8 space-y-4" onSubmit={onSubmit}>
          <div>
            <label htmlFor="current-password" className="mb-2 block text-sm font-medium text-slate-200">Temporary password</label>
            <input id="current-password" type="password" autoComplete="current-password" {...register('currentPassword')} className={inputClass} />
            {errors.currentPassword ? <p className="mt-2 text-sm text-rose-200">{errors.currentPassword.message}</p> : null}
          </div>
          <div>
            <label htmlFor="new-password" className="mb-2 block text-sm font-medium text-slate-200">New password</label>
            <input id="new-password" type="password" autoComplete="new-password" {...register('newPassword')} className={inputClass} />
            {errors.newPassword ? <p className="mt-2 text-sm text-rose-200">{errors.newPassword.message}</p> : null}
          </div>
          <div>
            <label htmlFor="confirm-password" className="mb-2 block text-sm font-medium text-slate-200">Confirm new password</label>
            <input id="confirm-password" type="password" autoComplete="new-password" {...register('confirmPassword')} className={inputClass} />
            {errors.confirmPassword ? <p className="mt-2 text-sm text-rose-200">{errors.confirmPassword.message}</p> : null}
          </div>
          {submitError ? <p role="alert" className="text-sm text-rose-200">{submitError}</p> : null}
          <button type="submit" disabled={isSubmitting} className="inline-flex h-12 w-full items-center justify-center rounded-xl bg-white px-4 text-sm font-semibold text-slate-950 transition hover:bg-slate-100 disabled:opacity-60">
            {isSubmitting ? 'Saving…' : 'Save password and continue'}
          </button>
          <button type="button" onClick={() => void logout().then(() => navigate('/login', { replace: true }))} className="block w-full text-center text-sm font-medium text-slate-300 hover:text-white">
            Sign out
          </button>
        </form>
      </div>
    </div>
  )
}
