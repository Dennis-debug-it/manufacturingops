/**
 * @deprecated This service is dead code — it is not imported anywhere in the UI.
 * The real inventory / materials API is served by `materialService.ts` which calls
 * the backend at /api/materials. This file remains only because the file system is
 * read-only in the current environment; it should be deleted.
 */
import { auditService } from './auditService'
import { tenantService } from './tenantService'
import { nowIso } from '../utils/date'
import { createId } from '../utils/id'
import type { InventoryAlert, InventoryItem, RequisitionRecord } from '../types/app'

function deriveAlert(item: InventoryItem): InventoryAlert {
  if (item.quantity <= 0 || item.quantity <= Math.max(1, Math.floor(item.reorderLevel / 2))) return 'Critical'
  if (item.quantity <= item.reorderLevel) return 'Low'
  return 'Healthy'
}

export const inventoryService = {
  listItems(tenantId: string) {
    return tenantService.getTenantData(tenantId).inventoryItems.map((item) => ({ ...item, alert: deriveAlert(item) }))
  },
  listRequisitions(tenantId: string) {
    return tenantService.getTenantData(tenantId).requisitions.sort((a, b) => b.requestedAt.localeCompare(a.requestedAt))
  },
  addItem(tenantId: string, input: Omit<InventoryItem, 'id' | 'linkedWorkOrderIds' | 'lastUpdatedAt'>) {
    const item: InventoryItem = { ...input, id: createId('inv'), linkedWorkOrderIds: [], lastUpdatedAt: nowIso() }
    tenantService.updateTenantData(tenantId, (current) => ({ ...current, inventoryItems: [item, ...current.inventoryItems] }))
    auditService.addTenantAudit(tenantId, 'Inventory Desk', 'create', 'inventory-item', item.id, `Added ${item.name}.`)
    return item
  },
  updateItem(tenantId: string, itemId: string, patch: Partial<InventoryItem>) {
    tenantService.updateTenantData(tenantId, (current) => ({
      ...current,
      inventoryItems: current.inventoryItems.map((item) => (item.id === itemId ? { ...item, ...patch, lastUpdatedAt: nowIso() } : item)),
    }))
  },
  replenish(tenantId: string, itemId: string, quantity: number) {
    this.adjustStock(tenantId, itemId, Math.abs(quantity), 'Stock-in')
  },
  adjustStock(tenantId: string, itemId: string, delta: number, actor = 'Inventory Desk') {
    tenantService.updateTenantData(tenantId, (current) => ({
      ...current,
      inventoryItems: current.inventoryItems.map((item) =>
        item.id === itemId ? { ...item, quantity: Math.max(0, item.quantity + delta), lastUpdatedAt: nowIso() } : item,
      ),
    }))
    auditService.addTenantAudit(tenantId, actor, 'adjust', 'inventory-item', itemId, `Adjusted stock by ${delta}.`)
  },
  updateRequisition(tenantId: string, requisitionId: string, patch: Partial<RequisitionRecord>) {
    tenantService.updateTenantData(tenantId, (current) => ({
      ...current,
      requisitions: current.requisitions.map((req) => (req.id === requisitionId ? { ...req, ...patch } : req)),
    }))
  },
  issueRequisition(tenantId: string, requisitionId: string, quantity: number) {
    const data = tenantService.getTenantData(tenantId)
    const requisition = data.requisitions.find((item) => item.id === requisitionId)
    const item = data.inventoryItems.find((inventoryItem) => inventoryItem.id === requisition?.inventoryItemId)
    if (!requisition || !item) throw new Error('Requisition or inventory item not found.')
    if (quantity > item.quantity) throw new Error('Cannot issue more than available stock.')

    tenantService.updateTenantData(tenantId, (current) => ({
      ...current,
      inventoryItems: current.inventoryItems.map((inventoryItem) =>
        inventoryItem.id === item.id
          ? {
              ...inventoryItem,
              quantity: inventoryItem.quantity - quantity,
              linkedWorkOrderIds: inventoryItem.linkedWorkOrderIds.includes(requisition.workOrderId)
                ? inventoryItem.linkedWorkOrderIds
                : [requisition.workOrderId, ...inventoryItem.linkedWorkOrderIds],
              lastUpdatedAt: nowIso(),
            }
          : inventoryItem,
      ),
      requisitions: current.requisitions.map((record) =>
        record.id === requisitionId
          ? { ...record, status: 'Issued', quantityIssued: quantity, issuedAt: nowIso(), approvedAt: record.approvedAt ?? nowIso() }
          : record,
      ),
      workOrders: current.workOrders.map((workOrder) =>
        workOrder.id === requisition.workOrderId
          ? {
              ...workOrder,
              updatedAt: nowIso(),
              activity: [
                { id: createId('act'), type: 'material', actor: 'Inventory Desk', title: 'Material issued', detail: `${recordItemName(item.name)} x${quantity} issued.`, createdAt: nowIso() },
                ...workOrder.activity,
              ],
            }
          : workOrder,
      ),
    }))
    auditService.addTenantAudit(tenantId, 'Inventory Desk', 'issue', 'requisition', requisitionId, `Issued ${quantity} of ${item.name}.`)
  },
}

function recordItemName(name: string) {
  return name
}
