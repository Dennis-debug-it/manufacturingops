import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import toast from 'react-hot-toast'
import { PageHeader } from '../../shared/components/PageHeader'
import { useCreatePortalWorkOrder, usePortalAssets } from '../api'
import { portalFeatures, usePortalFeature } from '../components/PortalFeatureGate'

const priorities = ['Low', 'Medium', 'High', 'Urgent']

export function PortalNewRequestPage() {
  const navigate = useNavigate()
  const showAssets = usePortalFeature(portalFeatures.assetRegister)
  const { data: assets } = usePortalAssets(showAssets)
  const createRequest = useCreatePortalWorkOrder()

  const [title, setTitle] = useState('')
  const [description, setDescription] = useState('')
  const [priority, setPriority] = useState('Medium')
  const [assetId, setAssetId] = useState('')

  const submit = async () => {
    try {
      const created = await createRequest.mutateAsync({
        title,
        description: description || undefined,
        priority,
        assetId: assetId || undefined,
      })
      toast.success(`Request ${created.workOrderNumber} submitted`)
      navigate(`/portal/requests/${created.id}`)
    } catch {
      // interceptor surfaces the error toast
    }
  }

  return (
    <div className="space-y-4">
      <PageHeader title="New Service Request" subtitle="Tell us what needs attention and we'll get it scheduled." breadcrumbs={[{ label: 'Requests', to: '/portal/requests' }, { label: 'New' }]} />
      <div className="max-w-xl space-y-4 rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 shadow-sm">
        <div>
          <label className="mb-1.5 block text-sm font-medium text-slate-700">What's the issue? *</label>
          <input value={title} onChange={(event) => setTitle(event.target.value)} placeholder="e.g. AC not cooling in Ward A" className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]" />
        </div>
        <div>
          <label className="mb-1.5 block text-sm font-medium text-slate-700">Details</label>
          <textarea value={description} onChange={(event) => setDescription(event.target.value)} rows={4} placeholder="Any additional detail that helps our technicians" className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[var(--app-primary)]" />
        </div>
        <div className={showAssets ? 'grid grid-cols-2 gap-4' : 'grid grid-cols-1 gap-4'}>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Priority</label>
            <select value={priority} onChange={(event) => setPriority(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]">
              {priorities.map((p) => <option key={p} value={p}>{p}</option>)}
            </select>
          </div>
          {showAssets ? (
            <div>
              <label className="mb-1.5 block text-sm font-medium text-slate-700">Related Asset (optional)</label>
              <select value={assetId} onChange={(event) => setAssetId(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]">
                <option value="">None</option>
                {(assets ?? []).map((asset) => <option key={asset.id} value={asset.id}>{asset.assetName}</option>)}
              </select>
            </div>
          ) : null}
        </div>
        <button
          type="button"
          disabled={!title.trim() || createRequest.isPending}
          onClick={() => void submit()}
          className="inline-flex h-11 w-full items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white hover:bg-[var(--app-secondary)] disabled:opacity-60"
        >
          {createRequest.isPending ? 'Submitting…' : 'Submit Request'}
        </button>
      </div>
    </div>
  )
}
