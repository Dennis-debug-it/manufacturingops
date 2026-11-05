import { useState } from 'react'
import toast from 'react-hot-toast'
import { Package, Trash2 } from 'lucide-react'
import { Badge } from '../../../shared/components/Badge'
import { EmptyState } from '../../../shared/components/EmptyState'
import { Spinner } from '../../../shared/components/Spinner'
import { Field, formatNumber, inputClass, primaryButton } from '../components/ui'
import { PartPicker, type PickedPart } from './PartPicker'
import { useAssetParts, useStoresAction } from './api'

/** An asset's parts list: what is fitted where, and how many are free in its factory and the group. */
export function AssetPartsPanel({ assetId, canManage }: { assetId: string; canManage: boolean }) {
  const parts = useAssetParts(assetId)
  const action = useStoresAction()
  const [part, setPart] = useState<PickedPart | null>(null)
  const [quantity, setQuantity] = useState('1')
  const [position, setPosition] = useState('')

  const add = async () => {
    if (!part) return
    try {
      await action.mutateAsync({ path: `/api/factory/assets/${assetId}/parts`, body: { materialItemId: part.id, quantityFitted: Number(quantity) || 1, position: position || null } })
      toast.success('Added to the parts list')
      setPart(null)
      setQuantity('1')
      setPosition('')
    } catch {
      // The API client shows the error.
    }
  }

  const remove = async (id: string) => {
    try {
      await action.mutateAsync({ method: 'delete', path: `/api/factory/assets/${assetId}/parts/${id}` })
    } catch {
      // The API client shows the error.
    }
  }

  if (parts.isLoading) return <Spinner />
  const rows = parts.data ?? []
  return (
    <div className="space-y-4">
      {rows.length === 0 ? <EmptyState icon={Package} title="No parts list" description="List the spares fitted to this asset so planners can reserve them and stores keep them in stock." /> : (
        <div className="overflow-x-auto rounded-[var(--radius-card)] border border-slate-200 bg-white">
          <table className="w-full min-w-[640px] text-sm">
            <thead className="bg-slate-50 text-left text-xs font-medium text-slate-500"><tr><th className="px-4 py-2">Part</th><th className="px-4 py-2">Where</th><th className="px-4 py-2 text-right">Fitted</th><th className="px-4 py-2 text-right">Free in factory</th><th className="px-4 py-2 text-right">Free in group</th><th /></tr></thead>
            <tbody className="divide-y divide-slate-100">
              {rows.map((x) => (
                <tr key={x.id}>
                  <td className="px-4 py-2"><span className="font-mono text-xs text-slate-500">{x.itemCode}</span> {x.itemName} {x.isCritical ? <Badge size="sm" tone="danger">Critical</Badge> : null}</td>
                  <td className="px-4 py-2 text-slate-600">{x.position ?? '—'}</td>
                  <td className="px-4 py-2 text-right tabular-nums">{formatNumber(x.quantityFitted)}</td>
                  <td className={`px-4 py-2 text-right tabular-nums ${x.availableInFactory < x.quantityFitted ? 'font-medium text-rose-700' : ''}`}>{formatNumber(x.availableInFactory)}</td>
                  <td className="px-4 py-2 text-right tabular-nums text-slate-600">{formatNumber(x.availableInGroup)}</td>
                  <td className="px-2 py-2 text-right">{canManage ? <button type="button" aria-label={`Remove ${x.itemName}`} className="text-slate-400 hover:text-rose-600" onClick={() => void remove(x.id)}><Trash2 className="h-4 w-4" /></button> : null}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      {canManage ? (
        <div className="grid gap-2 rounded-[var(--radius-card)] border border-slate-200 bg-white p-4 sm:grid-cols-[1fr_6rem_12rem_auto] sm:items-end">
          <Field label="Part"><PartPicker value={part} onChange={setPart} /></Field>
          <Field label="Fitted"><input type="number" min={0} step="any" className={inputClass} value={quantity} onChange={(e) => setQuantity(e.target.value)} /></Field>
          <Field label="Where on the asset"><input className={inputClass} value={position} placeholder="e.g. Drive end" onChange={(e) => setPosition(e.target.value)} /></Field>
          <button type="button" className={primaryButton} disabled={!part || action.isPending} onClick={() => void add()}>Add</button>
        </div>
      ) : null}
    </div>
  )
}
