import { useState } from 'react'
import toast from 'react-hot-toast'
import { PenLine, Trash2 } from 'lucide-react'
import { ConfirmDialog } from '../../../shared/components/ConfirmDialog'
import { EmptyState } from '../../../shared/components/EmptyState'
import { FormDrawer } from '../../../shared/components/FormDrawer'
import { Spinner } from '../../../shared/components/Spinner'
import { SignaturePad } from '../../../shared/components/SignaturePad'
import { usePermission } from '../../../shared/hooks/usePermission'
import { formatDateTime } from '../../../shared/utils/dates'
import { useCaptureSignature, useDeleteSignature, useExecutionBundle } from '../api'

const signatureTypes = [
  { value: 'Technician', label: 'Technician Signature' },
  { value: 'Client', label: 'Client Signature' },
]

export function SignaturesTab({ workOrderId }: { workOrderId: string }) {
  const canCapture = usePermission('work-orders.signatures.capture')
  const { data, isLoading, isError } = useExecutionBundle(workOrderId)
  const capture = useCaptureSignature(workOrderId)
  const deleteSignature = useDeleteSignature(workOrderId)

  const [open, setOpen] = useState(false)
  const [signatureType, setSignatureType] = useState('Technician')
  const [signerName, setSignerName] = useState('')
  const [signerRole, setSignerRole] = useState('')
  const [comment, setComment] = useState('')
  const [dataUrl, setDataUrl] = useState<string | null>(null)
  const [pendingDeleteId, setPendingDeleteId] = useState<string | null>(null)

  const closeDrawer = () => {
    setOpen(false)
    setSignerName('')
    setSignerRole('')
    setComment('')
    setDataUrl(null)
  }

  const submit = async () => {
    if (!signerName.trim() || !dataUrl) {
      toast.error('Draw a signature and enter the signer name')
      return
    }
    try {
      await capture.mutateAsync({ signatureType, signerName: signerName.trim(), signerRole: signerRole.trim() || null, signatureDataUrl: dataUrl, comment: comment.trim() || null })
      toast.success('Signature captured')
      closeDrawer()
    } catch {
      // interceptor surfaces the error toast
    }
  }

  const confirmDelete = async () => {
    if (!pendingDeleteId) return
    try {
      await deleteSignature.mutateAsync(pendingDeleteId)
      toast.success('Signature removed')
    } catch {
      // interceptor surfaces the error toast
    } finally {
      setPendingDeleteId(null)
    }
  }

  if (isLoading) {
    return <Spinner label="Loading signatures" />
  }

  if (isError || !data) {
    return <EmptyState title="Signatures unavailable" description="Captured signatures could not be loaded." />
  }

  return (
    <div className="space-y-4">
      {canCapture ? (
        <div className="flex justify-end">
          <button
            type="button"
            onClick={() => setOpen(true)}
            className="inline-flex h-11 items-center gap-2 rounded-lg bg-slate-900 px-4 text-sm font-medium text-white transition hover:bg-slate-800"
          >
            <PenLine className="h-4 w-4" />
            Capture Signature
          </button>
        </div>
      ) : null}

      {data.signatures.length === 0 ? (
        <EmptyState title="No signatures captured" description="Technician and client sign-off will appear here." />
      ) : (
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          {data.signatures.map((signature) => (
            <div key={signature.id} className="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm">
              <div className="flex items-start justify-between gap-2">
                <div>
                  <div className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">{signature.signatureType}</div>
                  <div className="mt-1 text-sm font-medium text-slate-900">{signature.signerName}</div>
                  {signature.signerRole ? <div className="text-xs text-slate-500">{signature.signerRole}</div> : null}
                </div>
                {canCapture ? (
                  <button type="button" onClick={() => setPendingDeleteId(signature.id)} className="rounded-lg p-1.5 text-slate-400 transition hover:bg-rose-50 hover:text-rose-600">
                    <Trash2 className="h-4 w-4" />
                  </button>
                ) : null}
              </div>
              <img src={signature.signatureDataUrl} alt={`${signature.signerName} signature`} className="mt-3 h-24 w-full rounded-lg border border-slate-100 bg-slate-50 object-contain" />
              {signature.comment ? <p className="mt-2 text-xs text-slate-600">{signature.comment}</p> : null}
              <p className="mt-2 text-[11px] text-slate-400">{formatDateTime(signature.capturedAt)}</p>
            </div>
          ))}
        </div>
      )}

      <FormDrawer
        open={open}
        title="Capture Signature"
        onClose={closeDrawer}
        footer={
          <div className="flex items-center justify-end gap-2">
            <button type="button" onClick={closeDrawer} className="inline-flex h-11 items-center justify-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">
              Cancel
            </button>
            <button
              type="button"
              onClick={submit}
              disabled={capture.isPending}
              className="inline-flex h-11 items-center justify-center rounded-lg bg-slate-900 px-4 text-sm font-medium text-white transition hover:bg-slate-800 disabled:cursor-not-allowed disabled:opacity-60"
            >
              {capture.isPending ? 'Saving…' : 'Save Signature'}
            </button>
          </div>
        }
      >
        <div className="space-y-4">
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="signature-type">Type</label>
            <select id="signature-type" value={signatureType} onChange={(event) => setSignatureType(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900">
              {signatureTypes.map((type) => (
                <option key={type.value} value={type.value}>{type.label}</option>
              ))}
            </select>
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="signer-name">Signer Name</label>
            <input id="signer-name" value={signerName} onChange={(event) => setSignerName(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900" />
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="signer-role">Role (optional)</label>
            <input id="signer-role" value={signerRole} onChange={(event) => setSignerRole(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900" />
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Signature</label>
            <SignaturePad onChange={setDataUrl} />
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="signature-comment">Comment (optional)</label>
            <textarea id="signature-comment" value={comment} onChange={(event) => setComment(event.target.value)} rows={2} className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-slate-900" />
          </div>
        </div>
      </FormDrawer>

      <ConfirmDialog
        open={Boolean(pendingDeleteId)}
        title="Remove Signature"
        message="This signature will be permanently removed from the work order."
        confirmLabel="Remove"
        danger
        onCancel={() => setPendingDeleteId(null)}
        onConfirm={confirmDelete}
      />
    </div>
  )
}
