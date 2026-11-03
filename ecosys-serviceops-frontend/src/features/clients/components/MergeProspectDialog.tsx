import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import toast from 'react-hot-toast'
import { ConfirmDialog } from '../../../shared/components/ConfirmDialog'
import { useClients, useMergeClient, usePossibleDuplicates } from '../api'
import type { Client } from '../types'

/**
 * Folds a prospect entered twice into the real record: its deals, RFPs, quotes, sites and contacts
 * move across and the prospect is removed. Likely matches are offered first.
 */
export function MergeProspectDialog({ prospect, open, onClose }: { prospect: Client; open: boolean; onClose: () => void }) {
  const navigate = useNavigate()
  const merge = useMergeClient(prospect.id)
  const { data: likely } = usePossibleDuplicates({ name: prospect.clientName, email: prospect.email ?? undefined, phone: prospect.phone ?? undefined, excludeId: prospect.id }, open)
  const { data: everyone } = useClients({ status: 'active', kind: 'all' })
  const [targetId, setTargetId] = useState('')
  const others = (everyone ?? []).filter((client) => client.id !== prospect.id && !(likely ?? []).some((match) => match.id === client.id))
  const targetName = (likely ?? []).find((x) => x.id === targetId)?.clientName ?? others.find((x) => x.id === targetId)?.clientName

  const confirm = async () => {
    try {
      await merge.mutateAsync(targetId)
      toast.success(`${prospect.clientName} merged into ${targetName}`)
      onClose()
      navigate(`/clients/${targetId}`, { replace: true })
    } catch {
      // interceptor surfaces the error toast
    }
  }

  return (
    <ConfirmDialog
      open={open}
      title="Merge prospect"
      message={`Move everything from ${prospect.clientName} (deals, RFPs, quotes, sites and contacts) into another client, then remove ${prospect.clientName}.`}
      confirmLabel="Merge"
      confirmDisabled={!targetId || merge.isPending}
      onCancel={onClose}
      onConfirm={() => void confirm()}
    >
      <label className="block text-sm">
        <span className="font-medium text-slate-700">Merge into</span>
        <select value={targetId} onChange={(event) => setTargetId(event.target.value)} className="mt-1 h-10 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]">
          <option value="">Choose a client…</option>
          {(likely ?? []).length > 0 ? (
            <optgroup label="Likely the same">
              {(likely ?? []).map((match) => <option key={match.id} value={match.id}>{match.clientName} ({match.reason.toLowerCase()})</option>)}
            </optgroup>
          ) : null}
          <optgroup label="All clients">
            {others.map((client) => <option key={client.id} value={client.id}>{client.clientName}{client.isProspect ? ' (prospect)' : ''}</option>)}
          </optgroup>
        </select>
      </label>
    </ConfirmDialog>
  )
}
