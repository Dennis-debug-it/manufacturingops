import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { zodResolver } from '@hookform/resolvers/zod'
import { Link } from 'react-router-dom'
import { MailCheck } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { api } from '../../shared/api/apiClient'
import { endpoints } from '../../shared/api/endpoints'
import { resolveErrorMessage } from '../../shared/api/apiError'

const signupSchema = z.object({
  companyName: z.string().trim().min(2, 'Company name is required'),
  fullName: z.string().trim().min(2, 'Your name is required'),
  email: z.string().trim().email('Enter a valid email'),
  phone: z.string().trim().min(7, 'Phone is required'),
  country: z.string().trim().min(2, 'Country is required'),
  industry: z.string().optional(),
  // The server's default password policy: 8+ characters with upper and lower case and a number.
  password: z.string()
    .min(8, 'At least 8 characters')
    .regex(/[A-Z]/, 'Include an uppercase letter')
    .regex(/[a-z]/, 'Include a lowercase letter')
    .regex(/[0-9]/, 'Include a number'),
})

type SignupFormValues = z.infer<typeof signupSchema>

const fields: Array<{ key: keyof SignupFormValues; label: string; type?: string; autoComplete?: string }> = [
  { key: 'companyName', label: 'Company name', autoComplete: 'organization' },
  { key: 'fullName', label: 'Your name', autoComplete: 'name' },
  { key: 'email', label: 'Work email', type: 'email', autoComplete: 'email' },
  { key: 'phone', label: 'Phone', type: 'tel', autoComplete: 'tel' },
  { key: 'country', label: 'Country', autoComplete: 'country-name' },
  { key: 'industry', label: 'Industry (optional)' },
  { key: 'password', label: 'Choose a password', type: 'password', autoComplete: 'new-password' },
]

/** "Start free trial": the company confirms its email and gets a 14-day trial workspace. */
export function SignupPage() {
  const [submitError, setSubmitError] = useState<string | null>(null)
  const [sentTo, setSentTo] = useState<string | null>(null)
  const { register, handleSubmit, formState: { errors, isSubmitting } } = useForm<SignupFormValues>({
    resolver: zodResolver(signupSchema),
    defaultValues: { country: 'Kenya' },
  })

  if (sentTo) {
    return (
      <div className="min-h-screen bg-slate-50 px-4 py-8 text-slate-900 sm:px-6 lg:px-10">
        <div className="mx-auto max-w-xl rounded-3xl border border-slate-200 bg-white p-6 text-center shadow-sm" data-testid="signup-sent">
          <MailCheck className="mx-auto h-10 w-10 text-[var(--app-primary)]" />
          <h1 className="mt-3 text-xl font-semibold">Check your email</h1>
          <p className="mt-2 text-sm text-slate-600">
            We've sent a link to <strong>{sentTo}</strong>. Open it to confirm your address and go straight into your workspace.
            Your 14-day free trial starts then.
          </p>
          <p className="mt-4 text-sm text-slate-500">
            Nothing arrived after a few minutes? Check spam, or <button type="button" onClick={() => setSentTo(null)} className="font-medium text-slate-900 underline">sign up again</button>.
            Already have an account? <Link to="/login" className="font-medium text-slate-900 underline">Sign in</Link>.
          </p>
        </div>
      </div>
    )
  }

  return (
    <div className="min-h-screen bg-slate-50 px-4 py-8 text-slate-900 sm:px-6 lg:px-10">
      <div className="mx-auto max-w-3xl">
        <PageHeader title="Start your free trial" subtitle="14 days of Ecosys Factory Maintenance, every module included. No card needed." />
        <form
          className="mt-8 space-y-4 rounded-3xl border border-slate-200 bg-white p-6 shadow-sm"
          noValidate
          onSubmit={handleSubmit(async (values) => {
            setSubmitError(null)
            try {
              await api.post(endpoints.auth.signupStart, {
                companyName: values.companyName,
                fullName: values.fullName,
                email: values.email,
                phone: values.phone,
                country: values.country,
                industry: values.industry || null,
                password: values.password,
              }, { suppressGlobalErrorToast: true })
              setSentTo(values.email.trim())
            } catch (error) {
              setSubmitError(resolveErrorMessage(error))
            }
          })}
        >
          {submitError ? (
            <div role="alert" className="rounded-xl border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-700">{submitError}</div>
          ) : null}
          {fields.map(({ key, label, type, autoComplete }) => (
            <div key={key}>
              <label htmlFor={`signup-${key}`} className="mb-2 block text-sm font-medium text-slate-700">{label}</label>
              <input
                id={`signup-${key}`}
                type={type ?? 'text'}
                autoComplete={autoComplete}
                className="h-12 w-full rounded-xl border border-slate-300 bg-white px-4 text-sm text-slate-900 outline-none focus:border-[var(--app-primary)]"
                {...register(key)}
              />
              {errors[key] ? <p className="mt-2 text-sm text-rose-600">{errors[key]?.message}</p> : null}
            </div>
          ))}
          <button
            type="submit"
            disabled={isSubmitting}
            className="inline-flex h-12 w-full items-center justify-center rounded-xl bg-[var(--app-primary)] px-4 text-sm font-semibold text-white transition hover:bg-[var(--app-secondary)] disabled:opacity-60"
          >
            {isSubmitting ? 'Sending…' : 'Start free trial'}
          </button>
          <p className="text-center text-sm text-slate-500">
            Already have an account? <Link to="/login" className="font-medium text-slate-900 hover:underline">Sign in</Link>
          </p>
        </form>
      </div>
    </div>
  )
}
