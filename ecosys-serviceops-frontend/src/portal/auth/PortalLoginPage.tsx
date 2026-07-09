import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { zodResolver } from '@hookform/resolvers/zod'
import { PageHeader } from '../../shared/components/PageHeader'
import { usePortalAuth } from './PortalAuthContext'

const portalLoginSchema = z.object({
  email: z.string().email('Enter a valid email address'),
})

type PortalLoginFormValues = z.infer<typeof portalLoginSchema>

export function PortalLoginPage() {
  const { requestLink } = usePortalAuth()
  const [sent, setSent] = useState<string | null>(null)
  const { register, handleSubmit, formState: { errors, isSubmitting } } = useForm<PortalLoginFormValues>({
    resolver: zodResolver(portalLoginSchema),
  })

  return (
    <div className="min-h-screen bg-slate-950 px-4 py-10 text-white sm:px-6 lg:px-8">
      <div className="mx-auto max-w-xl rounded-3xl border border-white/10 bg-white/5 p-8 shadow-2xl">
        <PageHeader title="Portal login" subtitle="Enter your email and we'll send you a one-time sign-in link." />
        {sent ? (
          <div className="mt-6 rounded-xl border border-emerald-400/30 bg-emerald-500/10 p-4 text-sm text-emerald-100">
            If <strong>{sent}</strong> is registered for portal access, a sign-in link has been sent. Check your inbox — the link expires in 30 minutes.
          </div>
        ) : (
          <form
            className="mt-6 space-y-4"
            onSubmit={handleSubmit(async (values) => {
              await requestLink(values.email)
              setSent(values.email)
            })}
          >
            <div>
              <label className="mb-2 block text-sm font-medium text-slate-200">Email</label>
              <input
                type="email"
                className="h-12 w-full rounded-xl border border-white/10 bg-white/10 px-4 text-sm text-white outline-none focus:border-white/30"
                {...register('email')}
              />
              {errors.email ? <p className="mt-2 text-sm text-rose-300">{errors.email.message}</p> : null}
            </div>
            <button type="submit" disabled={isSubmitting} className="inline-flex h-12 items-center justify-center rounded-xl bg-white px-4 text-sm font-semibold text-slate-950 transition hover:bg-slate-100 disabled:opacity-60">
              {isSubmitting ? 'Sending…' : 'Send sign-in link'}
            </button>
          </form>
        )}
      </div>
    </div>
  )
}
