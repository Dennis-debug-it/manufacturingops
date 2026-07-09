import { AlertCircle, Info, Loader2, PlusCircle, Trash2 } from 'lucide-react'
import { useNavigate } from 'react-router-dom'
import { useState } from 'react'
import { useAuth } from '../../auth/AuthContext'
import { Badge } from '../../components/ui/Badge'
import { ConfirmDialog } from '../../components/ui/ConfirmDialog'
import { Drawer } from '../../components/ui/Drawer'
import { EmptyState } from '../../components/ui/EmptyState'
import { ErrorState } from '../../components/ui/ErrorState'
import { LoadingState } from '../../components/ui/LoadingState'
import { useToast } from '../../components/ui/ToastProvider'
import { useAsyncData } from '../../hooks/useAsyncData'
import { workOrderBillingService } from '../../services/workOrderBillingService'
import { invoiceService } from '../../services/invoiceService'
import type {
  TechnicianRecord,
  UpsertBillableItemInput,
  WorkOrder,
  WorkOrderBillableItemRecord,
} from '../../types/api'
import { WorkOrderTimeLogsSection } from './WorkOrderTimeLogsSection'

const ITEM_TYPES = ['Labour', 'Material', 'Callout', 'Service', 'Transport', 'Other'] as const
const UNITS = ['hour', 'visit', 'km', 'unit', 'day', 'job', 'each'] as const

const emptyItemForm: UpsertBillableItemInput = {
  itemType: 'Material',
  description: '',
  quantity: 1,
  unit: 'unit',
  unitRate: 0,
  rateCardId: null,
  taxRate: 16,
  notes: null,
}

function billingStatusTone(status?: string | null): 'neutral' | 'warning' | 'success' | 'default' {
  switch (status) {
    case 'Billed': return 'success'
    case 'PartiallyBilled': return 'warning'
    case 'NotBillable': return 'neutral'
    default: return 'default'
  }
}

function itemTypeTone(type: string): 'info' | 'warning' | 'success' | 'neutral' | 'default' {
  switch (type) {
    case 'Labour': return 'info'
    case 'Callout': return 'warning'
    case 'Material': return 'success'
    case 'Service': return 'default'
    default: return 'neutral'
  }
}

interface Props {
  workOrder: WorkOrder
  technicians: TechnicianRecord[]
  onWorkOrderUpdated: () => void
}

export function WorkOrderBillingTab({ workOrder, technicians, onWorkOrderUpdated }: Props) {
  const { permissions } = useAuth()
  const { pushToast } = useToast()
  const canEdit = Boolean(permissions?.canManageFinance || permissions?.canManageBillableWork)

  const [drawerOpen, setDrawerOpen] = useState(false)
  const [editingItem, setEditingItem] = useState<WorkOrderBillableItemRecord | null>(null)
  const [itemForm, setItemForm] = useState<UpsertBillableItemInput>(emptyItemForm)
  const [deleteTarget, setDeleteTarget] = useState<WorkOrderBillableItemRecord | null>(null)
  const [autoPopulating, setAutoPopulating] = useState(false)
  const [previewItems, setPreviewItems] = useState<WorkOrderBillableItemRecord[] | null>(null)
  const [savingPreview, setSavingPreview] = useState(false)
  const [togglingBillable, setTogglingBillable] = useState(false)

  const { data: items, loading, error, reload } = useAsyncData<WorkOrderBillableItemRecord[]>(
    (signal) => workOrderBillingService.getBillableItems(workOrder.id, signal),
    [],
    [workOrder.id],
  )

  // Derived totals
  const subtotal = items.reduce((sum, i) => sum + i.lineTotal, 0)
  const totalTax = items.reduce((sum, i) => sum + i.taxAmount, 0)
  const grandTotal = items.reduce((sum, i) => sum + i.grandTotal, 0)
  const currency = 'KES'

  // Billing status
  const billingStatus = workOrder.billingStatus ?? 'Unbilled'
  const isBillable = workOrder.isBillable !== false

  function openAddDrawer() {
    setEditingItem(null)
    setItemForm(emptyItemForm)
    setDrawerOpen(true)
  }

  function openEditDrawer(item: WorkOrderBillableItemRecord) {
    setEditingItem(item)
    setItemForm({
      itemType: item.itemType,
      description: item.description,
      quantity: item.quantity,
      unit: item.unit,
      unitRate: item.unitRate,
      rateCardId: item.rateCardId ?? null,
      taxRate: item.taxRate,
      notes: item.notes ?? null,
    })
    setDrawerOpen(true)
  }

  function formField<K extends keyof UpsertBillableItemInput>(key: K, value: UpsertBillableItemInput[K]) {
    setItemForm((prev) => ({ ...prev, [key]: value }))
  }

  async function saveItem() {
    if (!itemForm.description.trim()) {
      pushToast({ title: 'Description required', tone: 'danger' })
      return
    }
    try {
      if (editingItem) {
        await workOrderBillingService.updateBillableItem(workOrder.id, editingItem.id, itemForm)
        pushToast({ title: 'Item updated', tone: 'success' })
      } else {
        await workOrderBillingService.addBillableItem(workOrder.id, itemForm)
        pushToast({ title: 'Item added', tone: 'success' })
      }
      setDrawerOpen(false)
      await reload()
    } catch (err) {
      pushToast({ title: 'Save failed', description: err instanceof Error ? err.message : 'Unable to save item.', tone: 'danger' })
    }
  }

  async function deleteItem() {
    if (!deleteTarget) return
    try {
      await workOrderBillingService.deleteBillableItem(workOrder.id, deleteTarget.id)
      pushToast({ title: 'Item removed', tone: 'success' })
      setDeleteTarget(null)
      await reload()
    } catch (err) {
      pushToast({ title: 'Delete failed', description: err instanceof Error ? err.message : 'Unable to delete.', tone: 'danger' })
    }
  }

  async function autoPopulate() {
    setAutoPopulating(true)
    try {
      const generated = await workOrderBillingService.autoPopulateBillableItems(workOrder.id)
      setPreviewItems(generated)
    } catch (err) {
      pushToast({ title: 'Auto-populate failed', description: err instanceof Error ? err.message : 'Unable to generate items.', tone: 'danger' })
    } finally {
      setAutoPopulating(false)
    }
  }

  async function savePreview() {
    if (!previewItems) return
    setSavingPreview(true)
    try {
      await Promise.all(
        previewItems.map((item) =>
          workOrderBillingService.addBillableItem(workOrder.id, {
            itemType: item.itemType,
            description: item.description,
            quantity: item.quantity,
            unit: item.unit,
            unitRate: item.unitRate,
            rateCardId: item.rateCardId ?? null,
            taxRate: item.taxRate,
            notes: item.notes ?? null,
          }),
        ),
      )
      pushToast({ title: `${previewItems.length} items added`, tone: 'success' })
      setPreviewItems(null)
      await reload()
    } catch (err) {
      pushToast({ title: 'Save failed', description: err instanceof Error ? err.message : 'Unable to save items.', tone: 'danger' })
    } finally {
      setSavingPreview(false)
    }
  }

  async function toggleNotBillable() {
    setTogglingBillable(true)
    try {
      const nextBillable = !isBillable
      await workOrderBillingService.updateBillingStatus(workOrder.id, {
        billingStatus: nextBillable ? 'Unbilled' : 'NotBillable',
        isBillable: nextBillable,
      })
      pushToast({
        title: nextBillable ? 'Marked as billable' : 'Marked as not billable',
        tone: 'success',
      })
      onWorkOrderUpdated()
    } catch (err) {
      pushToast({ title: 'Update failed', description: err instanceof Error ? err.message : 'Unable to update billing status.', tone: 'danger' })
    } finally {
      setTogglingBillable(false)
    }
  }

  if (loading) return <LoadingState label="Loading billing details" />
  if (error) return <ErrorState title="Unable to load billing items" description={error} />

  return (
    <div className="space-y-4">
      {/* Header row */}
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex items-center gap-3 flex-wrap">
          <Badge tone={billingStatusTone(billingStatus)}>{billingStatus}</Badge>
          {!isBillable && (
            <span className="flex items-center gap-1 text-xs text-muted">
              <Info className="h-3.5 w-3.5" />
              Not billable — billing section is locked
            </span>
          )}
        </div>
        <div className="flex items-center gap-2 flex-wrap">
          <button
            type="button"
            className="button-secondary py-1.5 text-xs"
            onClick={toggleNotBillable}
            disabled={togglingBillable}
          >
            {togglingBillable ? (
              <Loader2 className="h-3.5 w-3.5 animate-spin" />
            ) : isBillable ? 'Mark as not billable' : 'Mark as billable'}
          </button>
          {isBillable && (
            <>
              <button
                type="button"
                className="button-secondary py-1.5 text-xs"
                onClick={autoPopulate}
                disabled={autoPopulating}
              >
                {autoPopulating ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : 'Auto-populate from job'}
              </button>
              <button type="button" className="button-primary py-1.5 text-xs" onClick={openAddDrawer}>
                <PlusCircle className="mr-1 h-3.5 w-3.5" />
                Add item
              </button>
            </>
          )}
        </div>
      </div>

      {/* Auto-populate preview */}
      {previewItems !== null && (
        <div className="rounded-[18px] border border-info bg-info/5 p-4 space-y-3">
          <div className="flex items-start gap-2">
            <AlertCircle className="h-4 w-4 text-info shrink-0 mt-0.5" />
            <div>
              <p className="text-sm font-semibold text-app">Review auto-populated items</p>
              <p className="text-xs text-muted mt-0.5">
                {previewItems.length} item{previewItems.length !== 1 ? 's' : ''} generated from materials and time logs. Review and click Save to add them.
              </p>
            </div>
          </div>
          <div className="overflow-x-auto">
            <table className="w-full min-w-[600px] text-sm">
              <thead>
                <tr className="border-b border-app text-left text-xs text-muted">
                  <th className="pb-2 pr-3 font-medium">Type</th>
                  <th className="pb-2 pr-3 font-medium">Description</th>
                  <th className="pb-2 pr-3 font-medium text-right">Qty</th>
                  <th className="pb-2 pr-3 font-medium text-right">Rate</th>
                  <th className="pb-2 font-medium text-right">Total</th>
                </tr>
              </thead>
              <tbody>
                {previewItems.map((item, i) => (
                  <tr key={i} className="border-b border-app/50">
                    <td className="py-1.5 pr-3"><Badge tone={itemTypeTone(item.itemType)}>{item.itemType}</Badge></td>
                    <td className="py-1.5 pr-3 text-app">{item.description}</td>
                    <td className="py-1.5 pr-3 text-right">{item.quantity} {item.unit}</td>
                    <td className="py-1.5 pr-3 text-right">{currency} {item.unitRate.toLocaleString()}</td>
                    <td className="py-1.5 text-right font-semibold">{currency} {item.grandTotal.toLocaleString()}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <div className="flex gap-2">
            <button
              type="button"
              className="button-primary py-1.5 text-xs"
              onClick={savePreview}
              disabled={savingPreview}
            >
              {savingPreview ? 'Saving…' : `Save ${previewItems.length} items`}
            </button>
            <button type="button" className="button-secondary py-1.5 text-xs" onClick={() => setPreviewItems(null)}>
              Discard
            </button>
          </div>
        </div>
      )}

      {/* Billable items table */}
      {isBillable && (
        <>
          {items.length === 0 && previewItems === null ? (
            <EmptyState
              title="No billable items yet"
              description="Add items manually or use Auto-populate to generate from materials used and time logged."
            />
          ) : null}

          {items.length > 0 ? (
            <div className="overflow-x-auto">
              <table className="w-full min-w-[780px]">
                <thead className="table-head">
                  <tr>
                    <th className="table-cell text-left">Type</th>
                    <th className="table-cell text-left">Description</th>
                    <th className="table-cell text-right">Qty / Unit</th>
                    <th className="table-cell text-right">Unit Rate</th>
                    <th className="table-cell text-right">Line Total</th>
                    <th className="table-cell text-right">Tax</th>
                    <th className="table-cell text-right">Grand Total</th>
                    <th className="table-cell w-16"></th>
                  </tr>
                </thead>
                <tbody>
                  {items.map((item) => (
                    <tr key={item.id} className="table-row border-t border-app">
                      <td className="table-cell">
                        <Badge tone={itemTypeTone(item.itemType)}>{item.itemType}</Badge>
                      </td>
                      <td className="table-cell">
                        <p className="text-sm text-app">{item.description}</p>
                        {item.notes ? <p className="text-xs text-muted">{item.notes}</p> : null}
                      </td>
                      <td className="table-cell text-right text-sm">{item.quantity} {item.unit}</td>
                      <td className="table-cell text-right text-sm font-mono">{currency} {item.unitRate.toLocaleString()}</td>
                      <td className="table-cell text-right text-sm font-mono">{currency} {item.lineTotal.toLocaleString()}</td>
                      <td className="table-cell text-right text-sm text-muted">{item.taxRate}%</td>
                      <td className="table-cell text-right text-sm font-semibold font-mono">{currency} {item.grandTotal.toLocaleString()}</td>
                      <td className="table-cell">
                        {canEdit && !item.isBilled ? (
                          <div className="flex items-center gap-1">
                            <button
                              type="button"
                              className="icon-button h-7 w-7 text-muted hover:text-app"
                              onClick={() => openEditDrawer(item)}
                              aria-label="Edit item"
                            >
                              <span className="text-xs">Edit</span>
                            </button>
                            <button
                              type="button"
                              className="icon-button h-7 w-7 text-muted hover:text-danger"
                              onClick={() => setDeleteTarget(item)}
                              aria-label="Delete item"
                            >
                              <Trash2 className="h-3.5 w-3.5" />
                            </button>
                          </div>
                        ) : null}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : null}

          {/* Summary footer */}
          {items.length > 0 ? (
            <div className="ml-auto w-full max-w-xs rounded-[16px] border border-app bg-[var(--app-surface-strong)] p-4 space-y-2">
              <div className="flex justify-between text-sm text-muted">
                <span>Subtotal</span>
                <span className="font-mono">{currency} {subtotal.toLocaleString(undefined, { minimumFractionDigits: 2 })}</span>
              </div>
              <div className="flex justify-between text-sm text-muted">
                <span>Tax</span>
                <span className="font-mono">{currency} {totalTax.toLocaleString(undefined, { minimumFractionDigits: 2 })}</span>
              </div>
              <div className="border-t border-app pt-2 flex justify-between text-base font-semibold text-app">
                <span>Grand Total</span>
                <span className="font-mono">{currency} {grandTotal.toLocaleString(undefined, { minimumFractionDigits: 2 })}</span>
              </div>
              <GenerateInvoiceButton workOrderId={workOrder.id} />
            </div>
          ) : null}
        </>
      )}

      {/* Time logs */}
      {isBillable && (
        <WorkOrderTimeLogsSection
          workOrderId={workOrder.id}
          canEdit={canEdit}
          technicians={technicians}
        />
      )}

      {/* Add / Edit item drawer */}
      <Drawer
        open={drawerOpen}
        title={editingItem ? 'Edit billable item' : 'Add billable item'}
        description="Manually add or adjust a line item for this work order's bill."
        onClose={() => setDrawerOpen(false)}
      >
        <div className="space-y-4">
          <div className="grid grid-cols-2 gap-4">
            <div className="flex flex-col gap-1.5">
              <label className="text-sm font-medium text-app">Type</label>
              <select className="input-field" value={itemForm.itemType} onChange={(e) => formField('itemType', e.target.value)}>
                {ITEM_TYPES.map((t) => <option key={t} value={t}>{t}</option>)}
              </select>
            </div>
            <div className="flex flex-col gap-1.5">
              <label className="text-sm font-medium text-app">Unit</label>
              <select className="input-field" value={itemForm.unit} onChange={(e) => formField('unit', e.target.value)}>
                {UNITS.map((u) => <option key={u} value={u}>{u}</option>)}
              </select>
            </div>
          </div>
          <div className="flex flex-col gap-1.5">
            <label className="text-sm font-medium text-app">Description <span className="text-danger">*</span></label>
            <input
              type="text"
              className="input-field"
              value={itemForm.description}
              onChange={(e) => formField('description', e.target.value)}
              placeholder="e.g. Standard labour — 2h"
            />
          </div>
          <div className="grid grid-cols-3 gap-4">
            <div className="flex flex-col gap-1.5">
              <label className="text-sm font-medium text-app">Quantity</label>
              <input
                type="number"
                min="0"
                step="0.01"
                className="input-field"
                value={itemForm.quantity}
                onChange={(e) => formField('quantity', parseFloat(e.target.value) || 0)}
              />
            </div>
            <div className="flex flex-col gap-1.5">
              <label className="text-sm font-medium text-app">Unit Rate (KES)</label>
              <input
                type="number"
                min="0"
                step="0.01"
                className="input-field"
                value={itemForm.unitRate}
                onChange={(e) => formField('unitRate', parseFloat(e.target.value) || 0)}
              />
            </div>
            <div className="flex flex-col gap-1.5">
              <label className="text-sm font-medium text-app">Tax Rate (%)</label>
              <input
                type="number"
                min="0"
                max="100"
                step="0.01"
                className="input-field"
                value={itemForm.taxRate}
                onChange={(e) => formField('taxRate', parseFloat(e.target.value) || 0)}
              />
            </div>
          </div>
          {/* Live preview */}
          <div className="rounded-[12px] bg-[var(--app-surface-strong)] p-3 text-sm">
            <div className="flex justify-between text-muted">
              <span>Line total</span>
              <span className="font-mono">{currency} {(itemForm.quantity * itemForm.unitRate).toLocaleString(undefined, { minimumFractionDigits: 2 })}</span>
            </div>
            <div className="flex justify-between font-semibold text-app mt-1">
              <span>Grand total (inc. tax)</span>
              <span className="font-mono">{currency} {(itemForm.quantity * itemForm.unitRate * (1 + itemForm.taxRate / 100)).toLocaleString(undefined, { minimumFractionDigits: 2 })}</span>
            </div>
          </div>
          <div className="flex flex-col gap-1.5">
            <label className="text-sm font-medium text-app">Notes</label>
            <input
              type="text"
              className="input-field"
              placeholder="Optional…"
              value={itemForm.notes ?? ''}
              onChange={(e) => formField('notes', e.target.value || null)}
            />
          </div>
          <div className="flex gap-3 pt-1">
            <button type="button" className="button-primary flex-1" onClick={saveItem}>
              {editingItem ? 'Save changes' : 'Add item'}
            </button>
            <button type="button" className="button-secondary" onClick={() => setDrawerOpen(false)}>Cancel</button>
          </div>
        </div>
      </Drawer>

      {/* Delete confirm */}
      <ConfirmDialog
        open={deleteTarget !== null}
        title="Remove billable item"
        description={`Remove "${deleteTarget?.description}" from the bill? This cannot be undone.`}
        confirmLabel="Remove"
        onConfirm={deleteItem}
        onCancel={() => setDeleteTarget(null)}
      />
    </div>
  )
}


function GenerateInvoiceButton({ workOrderId }: { workOrderId: string }) {
  const navigate = useNavigate()
  const { pushToast } = useToast()
  const [generating, setGenerating] = useState(false)

  async function generate() {
    setGenerating(true)
    try {
      const invoice = await invoiceService.generateFromWorkOrder(workOrderId)
      pushToast({
        title: 'Invoice generated',
        description: `Invoice ${invoice.invoiceNumber} created.`,
        tone: 'success',
      })
      navigate(`/finance/invoices/${invoice.id}`)
    } catch (err) {
      pushToast({
        title: 'Invoice generation failed',
        description: err instanceof Error ? err.message : 'Unable to generate invoice.',
        tone: 'danger',
      })
    } finally {
      setGenerating(false)
    }
  }

  return (
    <button
      type="button"
      className="button-primary w-full py-2 text-sm"
      onClick={() => void generate()}
      disabled={generating}
    >
      {generating ? 'Generating…' : 'Generate Invoice'}
    </button>
  )
}
