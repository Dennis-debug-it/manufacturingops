import { useState } from 'react'
import { Link, useParams, useSearchParams } from 'react-router-dom'
import { useMutation, useQuery } from '@tanstack/react-query'
import { AlertOctagon, CheckCircle2, Wrench } from 'lucide-react'
import { api } from '../../shared/api/apiClient'
import { resolveErrorMessage } from '../../shared/api/apiError'
import { Spinner } from '../../shared/components/Spinner'
import { cn } from '../../shared/utils/cn'

interface OperatorAsset {
  companyName: string
  assetCode: string
  assetName: string
  location: string | null
  openJobNumber: string | null
  openJobStatus: string | null
}

interface OperatorReportResult { workOrderNumber: string; addedToOpenJob: boolean }

const fieldClass = 'h-12 w-full rounded-lg border border-slate-300 px-3 text-base outline-none transition focus:border-slate-500'
const labelClass = 'mb-1.5 block text-sm font-medium text-slate-700'
const NAME_KEY = 'operator-report-name'

const remembered = () => {
  try { return localStorage.getItem(NAME_KEY) ?? '' } catch { return '' }
}

/**
 * What an asset label's QR code opens: an operator reports a fault on that machine without
 * signing in. Staff who are signed in can jump to the asset instead.
 */
export function OperatorReportPage() {
  const { tag = '' } = useParams<{ tag: string }>()
  const [params] = useSearchParams()
  const key = params.get('r') ?? ''
  const [form, setForm] = useState({ description: '', reporterName: remembered(), phone: '', machineStopped: false })
  const [photo, setPhoto] = useState<File | null>(null)
  const [error, setError] = useState('')
  const base = `/api/public/fault-reports/${encodeURIComponent(key)}/${encodeURIComponent(tag)}`

  const asset = useQuery({
    queryKey: ['operator-report', key, tag],
    queryFn: () => api.get<OperatorAsset>(base),
    enabled: Boolean(key && tag),
    retry: false,
  })

  const submit = useMutation({
    mutationFn: () => {
      const data = new FormData()
      data.append('description', form.description.trim())
      data.append('reporterName', form.reporterName.trim())
      data.append('machineStopped', String(form.machineStopped))
      if (form.phone.trim()) data.append('phone', form.phone.trim())
      if (photo) data.append('photo', photo)
      return api.post<OperatorReportResult>(base, data)
    },
    onSuccess: () => {
      try { localStorage.setItem(NAME_KEY, form.reporterName.trim()) } catch { /* private mode */ }
    },
    onError: (err: unknown) => setError(resolveErrorMessage(err)),
  })

  const staffLink = <Link to={`/scan/${encodeURIComponent(tag)}`} className="font-medium text-slate-700 underline underline-offset-4">Staff: open this asset</Link>

  const shell = (children: React.ReactNode) => (
    <div className="min-h-screen bg-slate-50 px-4 py-6 sm:py-10">
      <div className="mx-auto max-w-md space-y-4">
        <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm sm:p-7">{children}</div>
        <p className="text-center text-sm text-slate-500">{staffLink}</p>
      </div>
    </div>
  )

  if (!key) return shell(<p className="text-sm text-slate-700">This label can't take fault reports. Tell your supervisor about the fault.</p>)
  if (asset.isLoading) return shell(<Spinner label="Loading" />)
  if (asset.isError || !asset.data) return shell(
    <div className="space-y-2">
      <h1 className="text-lg font-semibold text-slate-900">Label not recognised</h1>
      <p className="text-sm text-slate-600">{resolveErrorMessage(asset.error)} Tell your supervisor about the fault.</p>
    </div>,
  )

  const machine = asset.data
  const header = (
    <div className="mb-5">
      <p className="text-xs font-medium uppercase tracking-wide text-slate-500">{machine.companyName}</p>
      <h1 className="mt-1 text-xl font-semibold text-slate-900">{machine.assetName}</h1>
      <p className="text-sm text-slate-500"><span className="font-mono">{machine.assetCode}</span>{machine.location ? ` · ${machine.location}` : ''}</p>
    </div>
  )

  if (submit.data) return shell(
    <>
      {header}
      <div className="flex items-start gap-3 rounded-xl bg-emerald-50 p-4 text-emerald-900">
        <CheckCircle2 className="mt-0.5 h-5 w-5 shrink-0" />
        <div>
          <p className="font-medium">Thank you. Maintenance has been told.</p>
          <p className="mt-1 text-sm">{submit.data.addedToOpenJob ? `Added to job ${submit.data.workOrderNumber}, which is already open for this machine.` : `Your report is job ${submit.data.workOrderNumber}.`}</p>
        </div>
      </div>
    </>,
  )

  const ready = form.description.trim().length >= 3 && form.reporterName.trim().length > 0

  return shell(
    <>
      {header}
      {machine.openJobNumber ? (
        <div className="mb-4 flex items-start gap-2 rounded-lg border border-amber-200 bg-amber-50 p-3 text-sm text-amber-900">
          <Wrench className="mt-0.5 h-4 w-4 shrink-0" />
          <span>Job {machine.openJobNumber} is already open ({machine.openJobStatus}). A new report is added to it.</span>
        </div>
      ) : null}
      <form className="space-y-4" onSubmit={(event) => { event.preventDefault(); setError(''); if (ready) submit.mutate() }}>
        <button type="button" aria-pressed={form.machineStopped} onClick={() => setForm({ ...form, machineStopped: !form.machineStopped })}
          className={cn('flex h-14 w-full items-center justify-center gap-2 rounded-xl border-2 text-base font-semibold transition',
            form.machineStopped ? 'border-rose-600 bg-rose-600 text-white' : 'border-rose-200 bg-rose-50 text-rose-800')}>
          <AlertOctagon className="h-5 w-5" />{form.machineStopped ? 'The machine has stopped' : 'Has the machine stopped? Tap here'}
        </button>
        <div>
          <label className={labelClass} htmlFor="op-description">What is wrong?</label>
          <textarea id="op-description" className={cn(fieldClass, 'h-28 py-2')} value={form.description} onChange={(event) => setForm({ ...form, description: event.target.value })} maxLength={2000} />
        </div>
        <div>
          <label className={labelClass} htmlFor="op-name">Your name</label>
          <input id="op-name" className={fieldClass} value={form.reporterName} onChange={(event) => setForm({ ...form, reporterName: event.target.value })} maxLength={120} autoComplete="name" />
        </div>
        <div>
          <label className={labelClass} htmlFor="op-phone">Phone (optional)</label>
          <input id="op-phone" type="tel" className={fieldClass} value={form.phone} onChange={(event) => setForm({ ...form, phone: event.target.value })} maxLength={30} autoComplete="tel" />
        </div>
        <div>
          <label className={labelClass} htmlFor="op-photo">Photo (optional)</label>
          <input id="op-photo" type="file" accept="image/jpeg,image/png,image/webp" capture="environment" className="block w-full text-sm text-slate-600" onChange={(event) => setPhoto(event.target.files?.[0] ?? null)} />
        </div>
        {error ? <p className="rounded-lg bg-rose-50 p-3 text-sm text-rose-800">{error}</p> : null}
        <button type="submit" disabled={!ready || submit.isPending} className="h-12 w-full rounded-xl bg-slate-900 text-base font-semibold text-white disabled:opacity-50">
          {submit.isPending ? 'Sending…' : 'Report the fault'}
        </button>
      </form>
    </>,
  )
}
