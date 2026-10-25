export interface Material {
  id: string
  branchId: string | null
  branchName: string | null
  itemCode: string
  itemName: string
  category: string | null
  unitOfMeasure: string
  quantityOnHand: number
  reorderLevel: number
  unitCost: number | null
  isActive: boolean
  isLowStock: boolean
  /** At or below Settings → Inventory's critical share of the reorder level. */
  isCriticalStock: boolean
  createdAt: string
  barcode: string | null
  preferredSupplierId: string | null
  preferredSupplierName: string | null
  /** Usual order size; null means reorders top up to twice the reorder level. */
  reorderQuantity: number | null
}

export interface MaterialStockLocations {
  branches: Array<{ branchId: string; branchName: string; quantityOnHand: number; reorderLevel: number; unitCost: number | null; isLowStock: boolean }>
  vans: Array<{ vanId: string; vanName: string; quantityOnHand: number }>
}

export interface StockMovement {
  id: string
  branchId: string | null
  branchName: string | null
  materialId: string
  workOrderId: string | null
  materialRequestId: string | null
  movementType: string
  quantity: number
  balanceAfter: number
  reason: string | null
  referenceNumber: string | null
  createdByUserId: string | null
  createdAt: string
  /** Signed change to the balance; null on older adjustments of unknown direction. */
  quantityChange: number | null
  batchOrSerial: string | null
}

export interface BranchStockSummary {
  branchId: string
  partsStocked: number
  lowStockParts: number
  stockValue: number
}

export interface MaterialRequestLine {
  id: string
  materialItemId: string
  itemCode: string | null
  itemName: string | null
  quantityRequested: number
  quantityIssued: number
  quantityUsed: number
  quantityReturned: number
}

export interface MaterialRequest {
  id: string
  requestNumber: string
  branchId: string | null
  branchName: string | null
  workOrderId: string
  workOrderNumber: string | null
  requestedByUserId: string
  requestedByName: string | null
  status: 'Pending' | 'Approved' | 'Issued' | 'Closed'
  createdAt: string
  lines: MaterialRequestLine[]
}

export interface MaterialIssueLine {
  id: string
  materialItemId: string
  materialItemName: string | null
  itemCode: string | null
  quantityRequested: number
  quantityIssued: number
  quantityReturned: number
  unit: string
  unitCost: number | null
  notes: string | null
}

export interface MaterialIssue {
  id: string
  issueNumber: string
  branchId: string | null
  branchName: string | null
  workOrderId: string | null
  workOrderNumber: string | null
  assetId: string | null
  assetName: string | null
  clientId: string | null
  clientName: string | null
  siteId: string | null
  siteName: string | null
  systemId: string | null
  systemName: string | null
  issuedToUserId: string | null
  issuedToUserName: string | null
  requestedByUserId: string
  requestedByUserName: string | null
  issuedByUserId: string | null
  issuedByUserName: string | null
  approvedByUserId: string | null
  approvedByUserName: string | null
  issueStatus: 'draft' | 'pending_approval' | 'approved' | 'issued' | 'returned' | 'rejected' | 'cancelled'
  notes: string | null
  issuedAt: string | null
  approvedAt: string | null
  rejectedAt: string | null
  rejectionReason: string | null
  totalEstimatedCost: number
  totalIssuedCost: number
  createdAt: string
  lines: MaterialIssueLine[]
}

export interface StockTransfer {
  id: string
  fromBranchId: string
  fromBranchName: string | null
  toBranchId: string
  toBranchName: string | null
  materialId: string
  materialName: string | null
  quantity: number
  status: 'Submitted' | 'Approved' | 'Completed' | 'Cancelled'
  requestedByUserId: string
  approvedByUserId: string | null
  completedByUserId: string | null
  reason: string | null
  referenceNumber: string | null
  createdAt: string
  approvedAt: string | null
  completedAt: string | null
}

export interface Branch {
  id: string
  name: string
  code: string
  location: string | null
  isActive: boolean
}

/** One line of the tenant-wide stock ledger (`GET /api/stock-movements`). */
export interface StockLedgerEntry {
  id: string
  createdAt: string
  movementType: string
  materialId: string
  itemCode: string | null
  itemName: string | null
  unitOfMeasure: string | null
  quantity: number
  /** Signed change to the location's balance; null on older adjustments of unknown direction. */
  quantityChange: number | null
  balanceAfter: number
  branchId: string | null
  branchName: string | null
  vanId: string | null
  vanName: string | null
  workOrderId: string | null
  workOrderNumber: string | null
  referenceNumber: string | null
  reason: string | null
  createdByName: string | null
  batchOrSerial: string | null
}
