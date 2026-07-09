import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { zodResolver } from '@hookform/resolvers/zod'
import { Link, useNavigate } from 'react-router-dom'
import { Building2, Loader2 } from 'lucide-react'
import { useAuth } from '../../shared/hooks/useAuth'
import { brand } from '../../shared/config/brand'

const loginSchema = z.object({
  email: z.string().email('Enter a valid email address'),
  password: z.string().min(1, 'Password is required'),
})

type LoginFormValues = z.infer<typeof loginSchema>

export function LoginPage() {
  const { login, isAuthenticated, isReady } = useAuth()
  const navigate = useNavigate()
  const [submitError, setSubmitError] = useState<string | null>(null)

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<LoginFormValues>({
    resolver: zodResolver(loginSchema),
    defaultValues: {
      email: '',
      password: '',
    },
  })

  useEffect(() => {
    if (!isReady || !isAuthenticated) {
      return
    }
    navigate('/dashboard', { replace: true })
  }, [isAuthenticated, isReady, navigate])

  const onSubmit = handleSubmit(async (values) => {
    setSubmitError(null)
    try {
      const session = await login(values)
      navigate(session.user.role === 'super_admin' || session.user.role === 'platform_sales' ? '/platform/dashboard' : '/dashboard', { replace: true })
    } catch (error) {
      setSubmitError(error instanceof Error ? error.message : 'Unable to sign in.')
    }
  })

  return (
    <div className="grid min-h-screen bg-slate-950 lg:grid-cols-[1.1fr_0.9fr]">
      <div className="flex items-center justify-center px-6 py-12 lg:px-12">
        <div className="w-full max-w-md rounded-3xl border border-white/10 bg-white/5 p-8 text-white shadow-2xl backdrop-blur">
          <div className="flex items-center gap-3">
            <div className="flex h-12 w-12 items-center justify-center rounded-2xl bg-white text-slate-950">
              <Building2 className="h-6 w-6" />
            </div>
            <div>
              <div className="text-xs font-semibold uppercase tracking-[0.2em] text-slate-300">{brand.appName}</div>
              <h1 className="text-2xl font-semibold tracking-tight">{brand.tagline}</h1>
            </div>
          </div>

          <form className="mt-8 space-y-4" onSubmit={onSubmit}>
            <div>
              <label className="mb-2 block text-sm font-medium text-slate-200">Email</label>
              <input
                type="email"
                autoComplete="email"
                className="h-12 w-full rounded-xl border border-white/10 bg-white/10 px-4 text-sm text-white outline-none placeholder:text-slate-400 focus:border-white/30"
                {...register('email')}
              />
              {errors.email ? <p className="mt-2 text-sm text-rose-300">{errors.email.message}</p> : null}
            </div>
            <div>
              <div className="mb-2 flex items-center justify-between">
                <label className="block text-sm font-medium text-slate-200">Password</label>
                <Link to="/forgot-password" className="text-sm font-medium text-slate-300 hover:text-white">
                  Forgot password?
                </Link>
              </div>
              <input
                type="password"
                autoComplete="current-password"
                className="h-12 w-full rounded-xl border border-white/10 bg-white/10 px-4 text-sm text-white outline-none placeholder:text-slate-400 focus:border-white/30"
                {...register('password')}
              />
              {errors.password ? <p className="mt-2 text-sm text-rose-300">{errors.password.message}</p> : null}
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
              Sign in
            </button>
            <p className="text-center text-sm text-slate-400">
              New to {brand.appName}?{' '}
              <Link to="/request-demo" className="font-medium text-white hover:underline">
                Request a demo
              </Link>
            </p>
          </form>
        </div>
      </div>
      <div className="hidden lg:flex lg:flex-col lg:justify-between lg:bg-[radial-gradient(circle_at_top_left,_rgba(255,255,255,0.18),_transparent_35%),linear-gradient(180deg,#0F3B5E_0%,#0B1620_100%)] lg:p-12">
        <div />
        <div className="max-w-xl text-white">
          <p className="text-sm font-semibold uppercase tracking-[0.2em] text-slate-300">Operational control</p>
          <h2 className="mt-4 text-4xl font-semibold tracking-tight">Command field service, finance, and platform operations from one system.</h2>
          <p className="mt-4 text-base leading-7 text-slate-200">
            Fresh shell, strict role awareness, offline-capable technician flows, and a dedicated platform admin console.
          </p>
        </div>
      </div>
    </div>
  )
}
