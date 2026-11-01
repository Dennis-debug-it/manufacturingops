import { useState } from 'react'
import toast from 'react-hot-toast'
import { Trash2 } from 'lucide-react'
import { FormDrawer } from '../../../shared/components/FormDrawer'
import { MaterialAutocomplete } from './MaterialAutocomplete'
import {
  useEmailPurchaseOrder,
  useSuppliers,
  useUpdatePurchaseOrder,
  type PurchaseOrderDetail,
} from '../purchaseOrdersApi'

const fieldClass = 'h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]'
const labelClass = 'mb-1.5 block text-sm font-medium text-slate-700'
const cancelButton = 'inline-flex h-11 items-center justify-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50'
const primaryButton = 'inline-flex h-11 items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:opacity-60'

interface DraftLine {
  itemName: string
  materialItemId: string | null
  itemCode: string | null
  quantityOrdered: string
  unitCost: string
}

/** Edits a draft purchase order. Saving a changed total sends it for approval again when a matrix applies. */
export function EditPurchaseOrderDrawer({ order, onClose }: { order: PurchaseOrderDetail; onClose: () => void }) {
  const { data: suppliers } = useSuppliers()
  const update = useUpdatePurchaseOrder(order.id)
  const [supplierId, setSupplierId] = useState(order.supplierId)
  const [expectedAt, setExpectedAt] = useState(order.expectedAt ? order.expectedAt.slice(0, 10) : '')
  const [notes, setNotes] = useState(order.notes ?? '')
  const [lines, setLines] = useState<DraftLine[]>(order.lines.map((line) => ({
    itemName: line.itemName,
    materialItemId: line.materialItemId,
    itemCode: line.itemCode,
    quantityOrdered: String(line.quantityOrdered),
    unitCost: String(line.unitCost),
  })))
  const setLine = (index: number, patch: Partial<DraftLine>) => setLines((prev) => prev.map((line, i) => (i === index ? { ...line, ...patch } : line)))
  const total = lines.reduce((sum, line) => sum + (Number(line.quantityOrdered) || 0) * (Number(line.unitCost) || 0), 0)

  const submit = async () => {
    const valid = lines.filter((line) => line.itemName.trim() && Number(line.quantityOrdered) > 0)
    if (valid.length === 0) return void toast.error('Add at least one line with an item and quantity')
    try {
      await update.mutateAsync({
        supplierId,
        expectedAt: expectedAt || undefined,
        notes: notes.trim() || undefined,
        currency: order.currency,
        lines: valid.map((line) => ({
          itemName: line.itemName.trim(),
          materialItemId: line.materialItemId,
          quantityOrdered: Number(line.quantityOrdered),
          unitCost: Number(line.unitCost) || 0,
        })),
      })
      toast.success(total !== order.totalAmount ? `${order.poNumber} saved — the new total may need approval again` : `${order.poNumber} saved`)
      onClose()
    } catch {
      // interceptor surfaces the error toast
    }
  }

  return (
    <FormDrawer
      open
      title={`Edit ${order.poNumber}`}
      onClose={onClose}
      footer={
        <div className="flex items-center justify-between gap-2">
          <span className="text-sm text-slate-600">Total <strong className="tabular-nums text-slate-900">{total.toLocaleString()} {order.currency}</strong></span>
          <div className="flex gap-2">
            <button type="button" onClick={onClose} className={cancelButton}>Cancel</button>
            <button type="button" onClick={() => void submit()} disabled={update.isPending} className={primaryButton}>{update.isPending ? 'Saving…' : 'Save changes'}</button>
          </div>
        </div>
      }
    >
      <div className="space-y-4">
        <div>
          <label className={labelClass} htmlFor="po-edit-supplier">Supplier</label>
          <select id="po-edit-supplier" value={supplierId} onChange={(event) => setSupplierId(event.target.value)} className={fieldClass}>
            {(suppliers ?? []).filter((supplier) => supplier.status === 'Active' || supplier.id === order.supplierId).map((supplier) => (
              <option key={supplier.id} value={supplier.id}>{supplier.supplierName}</option>
            ))}
          </select>
        </div>
        <div>
          <label className={labelClass} htmlFor="po-edit-expected">Expected delivery date</label>
          <input id="po-edit-expected" type="date" value={expectedAt} onChange={(event) => setExpectedAt(event.target.value)} className={fieldClass} />
        </div>
        <div className="space-y-3">
          <div className="flex items-center justify-between">
            <span className={labelClass}>Lines</span>
            <button type="button" onClick={() => setLines((prev) => [...prev, { itemName: '', materialItemId: null, itemCode: null, quantityOrdered: '1', unitCost: '' }])} className="text-sm font-medium text-slate-700 underline underline-offset-4">+ Add line</button>
          </div>
          {lines.map((line, index) => (
            <div key={index} className="space-y-2 rounded-lg border border-slate-200 p-3">
              <MaterialAutocomplete
                value={line.itemName}
                onChange={(next) => setLine(index, { itemName: next, materialItemId: null, itemCode: null })}
                onSelectMaterial={(material) => setLine(index, {
                  itemName: material.itemName,
                  materialItemId: material.id,
                  itemCode: material.itemCode,
                  unitCost: line.unitCost || String(material.unitCost ?? ''),
                })}
                aria-label="Item"
                placeholder="Search or type an item…"
              />
              <p className="text-xs text-slate-500">{line.materialItemId ? `Stocked part ${line.itemCode ?? ''}` : 'Not a stocked part — receiving records the delivery only.'}</p>
              <div className="flex items-end gap-2">
                <label className="flex-1 text-xs text-slate-600">Qty
                  <input type="number" min="0" step="any" value={line.quantityOrdered} onChange={(event) => setLine(index, { quantityOrdered: event.target.value })} className={fieldClass} aria-label="Quantity" />
                </label>
                <label className="flex-1 text-xs text-slate-600">Unit cost
                  <input type="number" min="0" step="any" value={line.unitCost} onChange={(event) => setLine(index, { unitCost: event.target.value })} className={fieldClass} aria-label="Unit cost" />
                </label>
                <button type="button" disabled={lines.length === 1} onClick={() => setLines((prev) => prev.filter((_, i) => i !== index))} className="inline-flex h-11 w-11 items-center justify-center rounded-lg text-rose-500 hover:bg-rose-50 disabled:opacity-40" aria-label="Remove line">
                  <Trash2 className="h-4 w-4" />
                </button>
              </div>
            </div>
          ))}
        </div>
        <div>
          <label className={labelClass} htmlFor="po-edit-notes">Notes</label>
          <textarea id="po-edit-notes" value={notes} onChange={(event) => setNotes(event.target.value)} rows={2} className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[var(--app-primary)]" />
        </div>
      </div>
    </FormDrawer>
  )
}

/** Sends the PDF to the supplier by email; a draft is marked Sent. */
export function EmailPurchaseOrderDrawer({ order, onClose }: { order: PurchaseOrderDetail; onClose: () => void }) {
  const { data: suppliers } = useSuppliers()
  const supplier = (suppliers ?? []).find((item) => item.id === order.supplierId)
  const email = useEmailPurchaseOrder(order.id)
  const [to, setTo] = useState('')
  const [cc, setCc] = useState('')
  const [message, setMessage] = useState('')
  const [markSent, setMarkSent] = useState(true)
  const recipient = to.trim() || supplier?.email || ''

  const submit = async () => {
    if (!recipient) return void toast.error('Enter the address to send to — this supplier has no email on file')
    try {
      const result = await email.mutateAsync({
        to: to.trim() || null,
        cc: cc.split(/[,;\s]+/).map((value) => value.trim()).filter(Boolean),
        message: message.trim() || null,
        markSent,
      })
      toast.success(`${order.poNumber} emailed to ${result.to}`)
      onClose()
    } catch {
      // interceptor surfaces the error toast
    }
  }

  return (
    <FormDrawer
      open
      title={`Email ${order.poNumber} to supplier`}
      onClose={onClose}
      footer={
        <div className="flex items-center justify-end gap-2">
          <button type="button" onClick={onClose} className={cancelButton}>Cancel</button>
          <button type="button" onClick={() => void submit()} disabled={email.isPending} className={primaryButton}>{email.isPending ? 'Sending…' : 'Send'}</button>
        </div>
      }
    >
      <div className="space-y-4">
        <p className="text-sm text-slate-600">The purchase order PDF is attached, in your purchase order design (Settings → Document designs).</p>
        <div>
          <label className={labelClass} htmlFor="po-email-to">To</label>
          <input id="po-email-to" type="email" value={to} onChange={(event) => setTo(event.target.value)} placeholder={supplier?.email ?? 'supplier@example.com'} className={fieldClass} />
          <p className="mt-1 text-xs text-slate-500">{supplier?.email ? `Leave blank to send to ${supplier.email}.` : 'This supplier has no email on file.'}</p>
        </div>
        <div>
          <label className={labelClass} htmlFor="po-email-cc">Cc (optional)</label>
          <input id="po-email-cc" value={cc} onChange={(event) => setCc(event.target.value)} placeholder="buyer@yourcompany.com, accounts@yourcompany.com" className={fieldClass} />
        </div>
        <div>
          <label className={labelClass} htmlFor="po-email-message">Message (optional)</label>
          <textarea id="po-email-message" value={message} onChange={(event) => setMessage(event.target.value)} rows={4} placeholder="Please find attached our purchase order. Kindly confirm availability and the delivery date." className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[var(--app-primary)]" />
        </div>
        {order.status === 'Draft' ? (
          <label className="flex items-center gap-2 text-sm text-slate-700">
            <input type="checkbox" checked={markSent} onChange={(event) => setMarkSent(event.target.checked)} className="h-4 w-4 rounded border-slate-300" />
            Mark the purchase order as Sent
          </label>
        ) : null}
      </div>
    </FormDrawer>
  )
}
