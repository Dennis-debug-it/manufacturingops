import { useState } from 'react'
import toast from 'react-hot-toast'
import { CheckCircle2, FileDown } from 'lucide-react'
import { SignaturePad } from '../../shared/components/SignaturePad'
import { useAuth } from '../../shared/hooks/useAuth'
import { cn } from '../../shared/utils/cn'
import { formatDateTime } from '../../shared/utils/dates'
import { useCaptureSignature, useExecutionBundle } from '../work-orders/api'
import { downloadServiceReport } from './api'

const inputClass = 'w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]'

export interface TechFinishProps {
  workOrderId: string
  workOrderNumber: string
  editable: boolean
  workDone: string
  onWorkDoneChange: (value: string) => void
}

export function TechFinish({ workOrderId, workOrderNumber, editable, workDone, onWorkDoneChange }: TechFinishProps) {
  const { user } = useAuth()
  const { data: bundle } = useExecutionBundle(workOrderId)
  const capture = useCaptureSignature(workOrderId)
  const signatures = bundle?.signatures ?? []
  const clientSigned = signatures.some((signature) => signature.signatureType === 'Client')
  const techSigned = signatures.some((signature) => signature.signatureType === 'Technician')

  const [signer, setSigner] = useState<'Client' | 'Technician'>('Client')
  const [clientName, setClientName] = useState('')
  const [clientRole, setClientRole] = useState('')
  const [dataUrl, setDataUrl] = useState<string | null>(null)
  const [padKey, setPadKey] = useState(0)
  const [downloading, setDownloading] = useState(false)

  const download = async () => {
    setDownloading(true)
    try {
      await downloadServiceReport(workOrderId, workOrderNumber)
    } catch {
      toast.error('Could not download the service report')
    } finally {
      setDownloading(false)
    }
  }

  const name = signer === 'Client' ? clientName.trim() : user?.fullName ?? 'Technician'
  const saveSignature = async () => {
    if (!dataUrl || !name) return
    try {
      await capture.mutateAsync({
        signatureType: signer,
        signerName: name,
        signerRole: signer === 'Client' ? clientRole.trim() || null : 'Technician',
        signatureDataUrl: dataUrl,
      })
      toast.success(signer === 'Client' ? 'Client sign-off saved' : 'Your signature saved')
      setDataUrl(null)
      setPadKey((key) => key + 1)
      if (signer === 'Client' && !techSigned) setSigner('Technician')
    } catch {
      // interceptor surfaces the error toast
    }
  }

  return (
    <div className="space-y-3">
      <section className="rounded-xl border border-slate-200 bg-white p-4">
        <label htmlFor="tech-work-done" className="mb-2 block text-sm font-semibold text-slate-900">Work done <span className="font-normal text-rose-600">*</span></label>
        <textarea
          id="tech-work-done"
          value={workDone}
          disabled={!editable}
          onChange={(event) => onWorkDoneChange(event.target.value)}
          rows={4}
          placeholder="What was the fault and what did you do?"
          className={cn(inputClass, 'py-2')}
        />
        <p className="mt-1 text-xs text-slate-500">Goes on the service report. Required to complete the job.</p>
      </section>

      <section className="space-y-3 rounded-xl border border-slate-200 bg-white p-4">
        <h2 className="text-sm font-semibold text-slate-900">Sign-off</h2>
        <ul className="space-y-1 text-sm">
          {[
            { label: 'Client signature', done: clientSigned },
            { label: 'Your signature', done: techSigned },
          ].map((item) => (
            <li key={item.label} className={cn('flex items-center gap-2', item.done ? 'text-emerald-700' : 'text-slate-500')}>
              {item.done ? <CheckCircle2 className="h-4 w-4" /> : <span className="h-4 w-4 rounded-full border border-slate-300" />}
              {item.label}
            </li>
          ))}
        </ul>

        {editable ? (
          <>
            <div className="grid grid-cols-2 gap-1 rounded-lg border border-slate-200 p-1" role="radiogroup" aria-label="Who is signing">
              {(['Client', 'Technician'] as const).map((option) => (
                <button
                  key={option}
                  type="button"
                  role="radio"
                  aria-checked={signer === option}
                  onClick={() => { setSigner(option); setDataUrl(null); setPadKey((key) => key + 1) }}
                  className={cn('h-9 rounded-md text-sm font-medium', signer === option ? 'bg-[var(--app-primary)] text-white' : 'text-slate-600')}
                >
                  {option === 'Client' ? 'Client' : 'Me'}
                </button>
              ))}
            </div>
            {signer === 'Client' ? (
              <div className="grid grid-cols-2 gap-2">
                <input value={clientName} onChange={(event) => setClientName(event.target.value)} placeholder="Client name" aria-label="Client name" className={cn(inputClass, 'h-10')} />
                <input value={clientRole} onChange={(event) => setClientRole(event.target.value)} placeholder="Role" aria-label="Client role" className={cn(inputClass, 'h-10')} />
              </div>
            ) : (
              <p className="text-sm text-slate-600">Signing as <span className="font-medium text-slate-900">{name}</span></p>
            )}
            <div className="overflow-hidden rounded-lg border border-slate-300 bg-white">
              <SignaturePad key={padKey} onChange={setDataUrl} />
            </div>
            <p className={cn('text-xs', dataUrl ? 'text-emerald-700' : 'text-slate-500')}>{dataUrl ? 'Signature captured' : 'Sign in the box above'}</p>
            <button type="button" onClick={() => void saveSignature()} disabled={!dataUrl || !name || capture.isPending} className="inline-flex h-11 w-full items-center justify-center rounded-lg bg-slate-900 text-sm font-medium text-white disabled:opacity-50">
              {capture.isPending ? 'Saving…' : 'Save signature'}
            </button>
          </>
        ) : null}

        {signatures.length > 0 ? (
          <ul className="space-y-2 border-t border-slate-100 pt-3">
            {signatures.map((signature) => (
              <li key={signature.id} className="flex items-center gap-3">
                <img src={signature.signatureDataUrl} alt={`${signature.signerName} signature`} className="h-10 w-20 rounded border border-slate-200 bg-white object-contain" />
                <div className="min-w-0 text-sm">
                  <p className="truncate font-medium text-slate-900">{signature.signerName}</p>
                  <p className="text-xs text-slate-500">{signature.signatureType} · {formatDateTime(signature.capturedAt)}</p>
                </div>
              </li>
            ))}
          </ul>
        ) : null}
      </section>

      <section className="rounded-xl border border-slate-200 bg-white p-4">
        <h2 className="text-sm font-semibold text-slate-900">Service report</h2>
        <p className="mt-1 text-xs text-slate-500">Work done, checklist, materials, photos and signatures as the client will see them.</p>
        <button type="button" onClick={() => void download()} disabled={downloading} className="mt-3 inline-flex h-11 w-full items-center justify-center gap-2 rounded-lg border border-slate-300 bg-white text-sm font-medium text-slate-800 disabled:opacity-60">
          <FileDown className="h-4 w-4" />{downloading ? 'Preparing PDF…' : 'Download service report (PDF)'}
        </button>
      </section>
    </div>
  )
}
