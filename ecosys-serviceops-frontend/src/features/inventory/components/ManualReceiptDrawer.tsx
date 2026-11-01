import { useState } from 'react'
import { Link } from 'react-router-dom'
import toast from 'react-hot-toast'
import { FormDrawer } from '../../../shared/components/FormDrawer'
import { useBranches, useReplenishMaterial } from '../api'

const fieldClass = 'h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]'
const labelClass = 'mb-1.5 block text-sm font-medium text-slate-700'

export interface ManualReceiptPart {
  id: string
  itemName: string
  unitOfMeasure: string
  unitCost: number | null
  quantityOnHand: number
  reorderLevel: number
  branchId?: string | null
}

/**
 * Stock received without a purchase order: a found carton, a supplier delivery with no PO, stock
 * moved in from outside the system. It writes a receipt to the ledger like any other, but the
 * reason is required so the movement explains itself, and the cost can be recorded. Buying stock
 * should go through a purchase order, which keeps the supplier on record.
 */
export function ManualReceiptDrawer({ part, defaultQuantity, onClose }: { part: ManualReceiptPart; defaultQuantity?: number; onClose: () => void }) {
  const { data: branches } = useBranches()
  const activeBranches = (branches ?? []).filter((branch) => branch.isActive)
  const receive = useReplenishMaterial(part.id)
  const [branchId, setBranchId] = useState(part.branchId ?? '')
  const [quantity, setQuantity] = useState(String(defaultQuantity ?? 1))
  const [unitCost, setUnitCost] = useState(part.unitCost != null ? String(part.unitCost) : '')
  const [reason, setReason] = useState('')
  const [referenceNumber, setReferenceNumber] = useState('')
  const [batchOrSerial, setBatchOrSerial] = useState('')

  const submit = async () => {
    const qty = Number(quantity)
    if (!Number.isFinite(qty) || qty <= 0) return toast.error('Enter a quantity greater than zero')
    const cost = unitCost.trim() === '' ? null : Number(unitCost)
    if (cost != null && (!Number.isFinite(cost) || cost < 0)) return toast.error('Unit cost must be zero or more')
    if (!reason.trim()) return toast.error('Say why this stock is received without a purchase order')
    try {
      await receive.mutateAsync({
        quantity: qty,
        branchId: branchId || null,
        unitCost: cost,
        reason: reason.trim(),
        referenceNumber: referenceNumber.trim() || null,
        batchOrSerial: batchOrSerial.trim() || null,
      })
      toast.success(`Received ${qty} ${part.unitOfMeasure}`)
      onClose()
    } catch {
      // interceptor surfaces the error toast
    }
  }

  return (
    <FormDrawer
      open
      title={`Manual receipt · ${part.itemName}`}
      onClose={onClose}
      footer={
        <div className="flex items-center justify-end gap-2">
          <button type="button" onClick={onClose} className="inline-flex h-11 items-center justify-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">Cancel</button>
          <button type="button" onClick={() => void submit()} disabled={receive.isPending} className="inline-flex h-11 items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:opacity-60">
            {receive.isPending ? 'Saving…' : 'Receive stock'}
          </button>
        </div>
      }
    >
      <div className="space-y-4">
        <p className="rounded-lg border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-800">
          For stock you are buying, raise a <Link to="/inventory/purchase-orders" className="font-medium underline">purchase order</Link> instead — it keeps the supplier and price on record. Use a manual receipt for stock that arrives any other way.
        </p>
        {activeBranches.length > 0 ? (
          <div>
            <label className={labelClass} htmlFor="receipt-branch">Receive into</label>
            <select id="receipt-branch" value={branchId} onChange={(event) => setBranchId(event.target.value)} className={fieldClass}>
              <option value="">My default branch</option>
              {activeBranches.map((branch) => <option key={branch.id} value={branch.id}>{branch.name}</option>)}
            </select>
          </div>
        ) : null}
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className={labelClass} htmlFor="receipt-qty">Quantity ({part.unitOfMeasure})</label>
            <input id="receipt-qty" type="number" min="0" step="any" inputMode="decimal" value={quantity} onChange={(event) => setQuantity(event.target.value)} className={fieldClass} />
          </div>
          <div>
            <label className={labelClass} htmlFor="receipt-cost">Unit cost</label>
            <input id="receipt-cost" type="number" min="0" step="any" inputMode="decimal" value={unitCost} onChange={(event) => setUnitCost(event.target.value)} placeholder="Leave as is" className={fieldClass} />
          </div>
        </div>
        <p className="-mt-2 text-xs text-slate-500">{part.quantityOnHand} {part.unitOfMeasure} on hand now · reorder level {part.reorderLevel}.</p>
        <div>
          <label className={labelClass} htmlFor="receipt-reason">Reason (required)</label>
          <input id="receipt-reason" value={reason} onChange={(event) => setReason(event.target.value)} placeholder="e.g. Found in old store room, supplier sample" className={fieldClass} />
        </div>
        <div>
          <label className={labelClass} htmlFor="receipt-ref">Reference (optional)</label>
          <input id="receipt-ref" value={referenceNumber} onChange={(event) => setReferenceNumber(event.target.value)} placeholder="Delivery note, invoice number…" className={fieldClass} />
        </div>
        <div>
          <label className={labelClass} htmlFor="receipt-batch">Batch / serial numbers (optional)</label>
          <input id="receipt-batch" value={batchOrSerial} onChange={(event) => setBatchOrSerial(event.target.value)} placeholder="e.g. LOT-2291 or SN1001, SN1002" className={fieldClass} />
        </div>
      </div>
    </FormDrawer>
  )
}
