import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { zodResolver } from '@hookform/resolvers/zod'
import { useNavigate } from 'react-router-dom'
import { PageHeader } from '../../shared/components/PageHeader'
import { api } from '../../shared/api/apiClient'
import { endpoints } from '../../shared/api/endpoints'

const demoSchema = z.object({
  companyName: z.string().min(2, 'Company name is required'),
  name: z.string().min(2, 'Your name is required'),
  email: z.string().email('Enter a valid email'),
  phone: z.string().min(7, 'Phone is required'),
  country: z.string().min(2, 'Country is required'),
  notes: z.string().optional(),
})

type DemoFormValues = z.infer<typeof demoSchema>

export function RequestDemoPage() {
  const navigate = useNavigate()
  const [submitError, setSubmitError] = useState<string | null>(null)
  const { register, handleSubmit, formState: { errors, isSubmitting } } = useForm<DemoFormValues>({
    resolver: zodResolver(demoSchema),
  })

  return (
    <div className="min-h-screen bg-slate-950 px-4 py-8 text-white sm:px-6 lg:px-10">
      <div className="mx-auto max-w-3xl">
        <PageHeader
          title="Request a Demo"
          subtitle="See ECOSYS in action for your service operation."
        />
        <form
          className="mt-8 space-y-4 rounded-3xl border border-white/10 bg-white/5 p-6 shadow-2xl"
          onSubmit={handleSubmit(async (values) => {
            setSubmitError(null)
            try {
              await api.post(endpoints.platform.publicLeads, {
                companyName: values.companyName,
                contactPersonName: values.name,
                email: values.email,
                phone: values.phone,
                country: values.country,
                message: values.notes || null,
              })
              navigate('/demo-confirmation')
            } catch (error) {
              setSubmitError(error instanceof Error ? error.message : 'Unable to submit your request. Please try again.')
            }
          })}
        >
          {submitError ? (
            <div className="rounded-xl border border-rose-400/30 bg-rose-500/10 px-4 py-3 text-sm text-rose-100">{submitError}</div>
          ) : null}
          {[
            ['Company name', 'companyName'],
            ['Your name', 'name'],
            ['Work email', 'email'],
            ['Phone', 'phone'],
            ['Country', 'country'],
          ].map(([label, key]) => (
            <div key={key}>
              <label className="mb-2 block text-sm font-medium text-slate-200">{label}</label>
              <input
                className="h-12 w-full rounded-xl border border-white/10 bg-white/10 px-4 text-sm text-white outline-none focus:border-white/30"
                {...register(key as keyof DemoFormValues)}
              />
              {errors[key as keyof DemoFormValues] ? <p className="mt-2 text-sm text-rose-300">{errors[key as keyof DemoFormValues]?.message}</p> : null}
            </div>
          ))}
          <div>
            <label className="mb-2 block text-sm font-medium text-slate-200">Additional notes</label>
            <textarea className="min-h-32 w-full rounded-xl border border-white/10 bg-white/10 px-4 py-3 text-sm text-white outline-none focus:border-white/30" {...register('notes')} />
          </div>
          <button
            type="submit"
            disabled={isSubmitting}
            className="inline-flex h-12 items-center justify-center rounded-xl bg-white px-4 text-sm font-semibold text-slate-950 transition hover:bg-slate-100 disabled:opacity-60"
          >
            Submit request
          </button>
        </form>
      </div>
    </div>
  )
}
