import { useState } from 'react'
import { useParams, useSearchParams } from 'react-router-dom'
import { useMutation, useQuery } from '@tanstack/react-query'
import { CheckCircle2 } from 'lucide-react'
import { api } from '../../shared/api/apiClient'
import { resolveErrorMessage } from '../../shared/api/apiError'
import { endpoints } from '../../shared/api/endpoints'
import { Spinner } from '../../shared/components/Spinner'
import { cn } from '../../shared/utils/cn'

interface EnquiryFormInfo {
  companyName: string
}

const fieldClass = 'h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none transition focus:border-slate-500'
const labelClass = 'mb-1.5 block text-sm font-medium text-slate-700'
const empty = { name: '', company: '', email: '', phone: '', location: '', message: '', website: '' }

/**
 * A workspace's public enquiry form (Settings › Sales › Lead capture), linked to or embedded on its
 * website. With ?embed=1 it drops the page frame so it sits cleanly in an iframe.
 */
export function EnquiryFormPage() {
  const { key = '' } = useParams<{ key: string }>()
  const [searchParams] = useSearchParams()
  const embedded = searchParams.get('embed') === '1'
  const [form, setForm] = useState(empty)
  const [sent, setSent] = useState(false)
  const [error, setError] = useState('')

  const { data, isLoading, isError } = useQuery({
    queryKey: ['public-enquiry', key],
    queryFn: () => api.get<EnquiryFormInfo>(endpoints.crm.leadForm(key)),
    enabled: Boolean(key),
    retry: false,
  })

  const submit = useMutation({
    mutationFn: () => api.post(endpoints.crm.leadForm(key), {
      name: form.name.trim(),
      company: form.company.trim() || null,
      email: form.email.trim() || null,
      phone: form.phone.trim() || null,
      location: form.location.trim() || null,
      message: form.message.trim(),
      website: form.website || null,
    }),
    onSuccess: () => setSent(true),
    onError: (err: unknown) => setError(resolveErrorMessage(err)),
  })

  const set = (patch: Partial<typeof empty>) => {
    setError('')
    setForm((current) => ({ ...current, ...patch }))
  }

  const shell = (children: React.ReactNode) => (
    <div className={cn('bg-slate-50', embedded ? 'p-3' : 'min-h-screen px-4 py-10 sm:px-6')}>
      <div className={cn('mx-auto max-w-xl rounded-2xl border border-slate-200 bg-white shadow-sm', embedded ? 'p-5' : 'p-8')}>{children}</div>
    </div>
  )

  if (isLoading) return shell(<Spinner label="Loading" />)
  if (isError || !data)
    return shell(
      <>
        <h1 className="text-xl font-semibold text-slate-900">Form unavailable</h1>
        <p className="mt-2 text-sm text-slate-600">This enquiry form is not available. Please contact the company directly.</p>
      </>,
    )

  if (sent)
    return shell(
      <div className="text-center">
        <CheckCircle2 className="mx-auto h-10 w-10 text-emerald-600" />
        <h1 className="mt-3 text-xl font-semibold text-slate-900">Thank you</h1>
        <p className="mt-2 text-sm text-slate-600">{data.companyName} has your enquiry and will be in touch shortly.</p>
      </div>,
    )

  return shell(
    <form
      onSubmit={(event) => {
        event.preventDefault()
        if (!form.name.trim()) return setError('Please tell us your name.')
        if (!form.email.trim() && !form.phone.trim()) return setError('Please give an email address or a phone number so we can reach you.')
        if (!form.message.trim()) return setError('Please tell us what you need.')
        submit.mutate()
      }}
      className="space-y-4"
      noValidate
    >
      <div>
        <h1 className="text-xl font-semibold text-slate-900">Contact {data.companyName}</h1>
        <p className="mt-1 text-sm text-slate-600">Tell us what you need and we will get back to you.</p>
      </div>
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
        <div>
          <label className={labelClass} htmlFor="enq-name">Your name</label>
          <input id="enq-name" autoComplete="name" value={form.name} onChange={(event) => set({ name: event.target.value })} className={fieldClass} maxLength={150} />
        </div>
        <div>
          <label className={labelClass} htmlFor="enq-company">Company (optional)</label>
          <input id="enq-company" autoComplete="organization" value={form.company} onChange={(event) => set({ company: event.target.value })} className={fieldClass} maxLength={200} />
        </div>
        <div>
          <label className={labelClass} htmlFor="enq-email">Email</label>
          <input id="enq-email" type="email" autoComplete="email" value={form.email} onChange={(event) => set({ email: event.target.value })} className={fieldClass} maxLength={256} />
        </div>
        <div>
          <label className={labelClass} htmlFor="enq-phone">Phone</label>
          <input id="enq-phone" type="tel" autoComplete="tel" value={form.phone} onChange={(event) => set({ phone: event.target.value })} className={fieldClass} maxLength={50} />
        </div>
      </div>
      <div>
        <label className={labelClass} htmlFor="enq-location">Location (optional)</label>
        <input id="enq-location" value={form.location} onChange={(event) => set({ location: event.target.value })} className={fieldClass} maxLength={250} placeholder="Town or site" />
      </div>
      <div>
        <label className={labelClass} htmlFor="enq-message">What do you need?</label>
        <textarea id="enq-message" rows={5} value={form.message} onChange={(event) => set({ message: event.target.value })} maxLength={3000} className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-slate-500" />
      </div>
      {/* People never see this field; bots fill it in. */}
      <div aria-hidden="true" className="absolute -left-[9999px] h-0 w-0 overflow-hidden">
        <label htmlFor="enq-website">Website</label>
        <input id="enq-website" tabIndex={-1} autoComplete="off" value={form.website} onChange={(event) => set({ website: event.target.value })} />
      </div>
      {error ? <p className="text-sm text-rose-600" role="alert">{error}</p> : null}
      <button type="submit" disabled={submit.isPending} className="inline-flex h-11 w-full items-center justify-center rounded-lg bg-slate-900 px-4 text-sm font-medium text-white transition hover:bg-slate-800 disabled:opacity-60">
        {submit.isPending ? 'Sending…' : 'Send enquiry'}
      </button>
    </form>,
  )
}
