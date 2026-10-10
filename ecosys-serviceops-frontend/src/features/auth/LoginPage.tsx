import { useEffect, useRef, useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { zodResolver } from '@hookform/resolvers/zod'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { Eye, EyeOff, Loader2 } from 'lucide-react'
import { useAuth } from '../../shared/hooks/useAuth'
import { brand } from '../../shared/config/brand'
import { resolveErrorMessage } from '../../shared/api/apiError'
import { Logo } from '../../shared/components/Logo'

const loginSchema = z
  .object({
    emailOrUsername: z.string().min(1, 'Enter your email or username'),
    workspace: z.string().optional(),
    password: z.string().min(1, 'Password is required'),
  })
  .superRefine((values, ctx) => {
    if (!values.emailOrUsername.includes('@') && !values.workspace?.trim()) {
      ctx.addIssue({ code: z.ZodIssueCode.custom, path: ['workspace'], message: 'Enter your company name to sign in with a username' })
    }
  })

type LoginFormValues = z.infer<typeof loginSchema>

export function LoginPage() {
  const { login, isAuthenticated, isReady, user } = useAuth()
  const navigate = useNavigate()
  const [searchParams] = useSearchParams()
  // The first-sign-in setup page is for the workspace admin. Anyone else invited to the workspace
  // starts on their normal home screen; the setup page calls admin-only services.
  const welcome = searchParams.get('welcome') === '1'
  const destinationFor = (role: string | undefined) => (welcome && role === 'admin' ? '/onboarding' : '/dashboard')
  const [submitError, setSubmitError] = useState<string | null>(null)
  const [showPassword, setShowPassword] = useState(false)
  const passwordVisibilityTimer = useRef<ReturnType<typeof setTimeout> | null>(null)

  const togglePasswordVisibility = () => {
    if (passwordVisibilityTimer.current) {
      clearTimeout(passwordVisibilityTimer.current)
      passwordVisibilityTimer.current = null
    }

    if (showPassword) {
      setShowPassword(false)
      return
    }

    setShowPassword(true)
    passwordVisibilityTimer.current = setTimeout(() => {
      setShowPassword(false)
      passwordVisibilityTimer.current = null
    }, 5000)
  }

  useEffect(() => () => {
    if (passwordVisibilityTimer.current) clearTimeout(passwordVisibilityTimer.current)
  }, [])

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<LoginFormValues>({
    resolver: zodResolver(loginSchema),
    defaultValues: {
      emailOrUsername: '',
      workspace: '',
      password: '',
    },
  })

  useEffect(() => {
    if (!isReady || !isAuthenticated) {
      return
    }
    navigate(destinationFor(user?.role), { replace: true })
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isAuthenticated, isReady, navigate, welcome, user?.role])

  const onSubmit = handleSubmit(async (values) => {
    setSubmitError(null)
    try {
      const session = await login({
        emailOrUsername: values.emailOrUsername,
        password: values.password,
        ...(values.workspace?.trim() ? { workspace: values.workspace.trim() } : {}),
      })
      if (session.user.mustChangePassword) {
        navigate('/change-password', { replace: true })
        return
      }
      navigate(session.user.role === 'super_admin' || session.user.role === 'platform_sales' ? '/platform/dashboard' : destinationFor(session.user.role), { replace: true })
    } catch (error) {
      setSubmitError(resolveErrorMessage(error))
    }
  })

  return (
    <div className="grid min-h-screen bg-white lg:grid-cols-[1fr_1fr]">
      <div className="flex items-center justify-center px-6 py-12 lg:px-12">
        <div className="w-full max-w-md rounded-3xl border border-slate-200 bg-white p-8 shadow-xl">
          <Logo className="h-12 w-auto" />
          <h1 className="mt-6 text-lg font-semibold tracking-tight text-slate-900">Sign in to your workspace</h1>

          <form className="mt-8 space-y-4" onSubmit={onSubmit}>
            <div>
              <label htmlFor="login-workspace" className="mb-2 block text-sm font-medium text-slate-700">Company Name</label>
              <input
                id="login-workspace"
                type="text"
                autoComplete="organization"
                placeholder="e.g. Acme Corp"
                className="h-12 w-full rounded-xl border border-slate-300 bg-white px-4 text-sm text-slate-900 outline-none placeholder:text-slate-400 focus:border-slate-900"
                {...register('workspace')}
              />
              <p className="mt-1.5 text-xs text-slate-500">Only required when signing in with a username instead of an email.</p>
              {errors.workspace ? <p className="mt-2 text-sm text-rose-600">{errors.workspace.message}</p> : null}
            </div>
            <div>
              <label htmlFor="login-identifier" className="mb-2 block text-sm font-medium text-slate-700">Email or username</label>
              <input
                id="login-identifier"
                type="text"
                autoComplete="username"
                className="h-12 w-full rounded-xl border border-slate-300 bg-white px-4 text-sm text-slate-900 outline-none placeholder:text-slate-400 focus:border-slate-900"
                {...register('emailOrUsername')}
              />
              {errors.emailOrUsername ? <p className="mt-2 text-sm text-rose-600">{errors.emailOrUsername.message}</p> : null}
            </div>
            <div>
              <div className="mb-2 flex items-center justify-between">
                <label htmlFor="login-password" className="block text-sm font-medium text-slate-700">Password</label>
                <Link to="/forgot-password" className="text-sm font-medium text-slate-500 hover:text-slate-900">
                  Forgot password?
                </Link>
              </div>
              <div className="relative">
                <input
                  id="login-password"
                  type={showPassword ? 'text' : 'password'}
                  autoComplete="current-password"
                  className="h-12 w-full rounded-xl border border-slate-300 bg-white py-0 pl-4 pr-12 text-sm text-slate-900 outline-none placeholder:text-slate-400 focus:border-slate-900"
                  {...register('password')}
                />
                <button
                  type="button"
                  onClick={togglePasswordVisibility}
                  aria-label={showPassword ? 'Conceal entered characters' : 'Reveal entered characters'}
                  title={showPassword ? 'Hide password' : 'Show password'}
                  aria-pressed={showPassword}
                  className="absolute inset-y-0 right-0 flex w-12 items-center justify-center rounded-r-xl text-slate-500 transition hover:text-slate-900 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-slate-900"
                >
                  {showPassword ? <EyeOff className="h-5 w-5" aria-hidden="true" /> : <Eye className="h-5 w-5" aria-hidden="true" />}
                </button>
              </div>
              {errors.password ? <p className="mt-2 text-sm text-rose-600">{errors.password.message}</p> : null}
            </div>
            {submitError ? (
              <div role="alert" className="rounded-xl border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-700">
                {submitError}
              </div>
            ) : null}
            <button
              type="submit"
              disabled={isSubmitting}
              className="inline-flex h-12 w-full items-center justify-center gap-2 rounded-xl bg-slate-900 px-4 text-sm font-semibold text-white transition hover:bg-slate-800 disabled:cursor-not-allowed disabled:opacity-60"
            >
              {isSubmitting ? <Loader2 className="h-4 w-4 animate-spin" /> : null}
              Sign in
            </button>
            <p className="text-center text-sm text-slate-500">
              New to {brand.appName}?{' '}
              <Link to="/signup" className="font-medium text-slate-900 hover:underline">
                Start a free trial
              </Link>
              {' or '}
              <Link to="/request-demo" className="font-medium text-slate-900 hover:underline">
                request a demo
              </Link>
            </p>
          </form>
        </div>
      </div>

      {/* Operational control — real technicians photo (chamfered) with tagline overlay */}
      <div className="relative hidden bg-white p-6 lg:block">
        <div className="relative h-full w-full overflow-hidden rounded-[2rem] bg-gradient-to-br from-slate-800 to-slate-950">
          <img
            src="/ecosys-technicians.jpg"
            alt="ECOSYS technicians reviewing operations on a tablet"
            className="absolute inset-0 h-full w-full object-cover"
            onError={(event) => { event.currentTarget.style.display = 'none' }}
          />
          <div className="absolute inset-0 bg-gradient-to-t from-slate-950/85 via-slate-950/30 to-transparent" />
          <div className="absolute inset-x-0 bottom-0 p-12">
            <p className="text-sm font-semibold uppercase tracking-[0.2em] text-emerald-300">Operational control</p>
            <h2 className="mt-4 max-w-xl text-4xl font-semibold tracking-tight text-white">
              Command field service, finance, and platform operations from one system.
            </h2>
          </div>
        </div>
      </div>
    </div>
  )
}
