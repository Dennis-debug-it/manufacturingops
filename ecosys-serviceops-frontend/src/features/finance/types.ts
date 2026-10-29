export interface InvoiceListItem {
  id: string
  clientId: string
  clientName: string | null
  invoiceNumber: string
  documentType: string
  status: string
  invoiceDate: string
  dueDate: string
  subtotal: number
  taxAmount: number
  totalAmount: number
  amountPaid: number
  balance: number
  currency: string
  createdAt: string
  creditTermId: string | null
  creditTermName: string | null
}

export interface InvoiceLine {
  id: string
  invoiceId: string
  billableItemId: string | null
  workOrderId: string | null
  description: string
  itemType: string
  quantity: number
  unit: string
  unitRate: number
  lineTotal: number
  taxRate: number
  taxAmount: number
  grandTotal: number
}

export interface Payment {
  id: string
  tenantId: string
  invoiceId: string | null
  clientId: string
  paymentNumber: string
  amount: number
  paymentDate: string
  method: string
  reference: string | null
  notes: string | null
  status: string
  createdAt: string
}

export interface CreditNoteListItem {
  id: string
  clientId: string
  clientName: string | null
  creditNoteNumber: string
  invoiceId: string | null
  invoiceNumber: string | null
  reason: string
  status: string
  issueDate: string
  totalAmount: number
  amountApplied: number
  balance: number
  currency: string
  createdAt: string
}

export interface CreditNoteLine {
  id: string
  description: string
  itemType: string
  quantity: number
  unit: string
  unitRate: number
  lineTotal: number
  taxRate: number
  taxAmount: number
  grandTotal: number
}

export interface CreditNoteApplication {
  invoiceId: string
  invoiceNumber: string | null
  amount: number
  allocatedAt: string
}

export interface CreditNoteDetail extends CreditNoteListItem {
  notes: string | null
  subtotal: number
  taxAmount: number
  lines: CreditNoteLine[]
  applications: CreditNoteApplication[]
}

export interface ClientCreditBalance {
  clientId: string
  clientName: string | null
  currentBalance: number
  availableCredit: number
  currency: string
}

export interface InvoiceDetail extends InvoiceListItem {
  notes: string | null
  termsAndConditions: string | null
  lines: InvoiceLine[]
  payments: Payment[]
  depositAmount: number | null
  depositPaid: boolean
  clientCurrentBalance: number
  clientCreditLimit: number | null
  sourceQuotations: SourceQuotation[]
}

export interface SourceQuotation {
  id: string
  quoteNumber: string
  status: string
  subject: string | null
  quoteDate: string
  totalAmount: number
  currency: string
}

export interface QuotationListItem {
  id: string
  clientId: string
  clientName: string | null
  quoteNumber: string
  status: string
  subject: string | null
  quoteDate: string
  validUntil: string
  subtotal: number
  taxAmount: number
  totalAmount: number
  currency: string
  convertedToInvoiceId: string | null
  convertedToWorkOrderId: string | null
  createdAt: string
}

export interface QuotationLine {
  id: string
  quotationId: string
  rateCardId: string | null
  description: string
  itemType: string
  quantity: number
  unit: string
  unitRate: number
  lineTotal: number
  taxRate: number
  grandTotal: number
}

export interface QuotationDetail extends QuotationListItem {
  siteId: string | null
  notes: string | null
  rejectionReason: string | null
  lines: QuotationLine[]
  /** The latest approval request's status, when the workspace approves quotes before sending (Settings › Approvals). */
  approvalStatus?: 'Pending' | 'Approved' | 'Rejected' | 'Cancelled' | null
  /** The total that was approved; changing the total asks for approval again. */
  approvedAmount?: number | null
}

export interface AgingRow {
  clientId: string
  clientName: string
  current: number
  days1To30: number
  days31To60: number
  days61To90: number
  days90Plus: number
  total: number
}

export interface RateCard {
  id: string
  tenantId: string
  name: string
  category: string
  description: string | null
  unitRate: number
  unit: string
  skillLevel: string | null
  timeType: string
  isDefault: boolean
  taxRate: number | null
  isActive: boolean
  createdAt: string
}

export interface PreferentialRate {
  id: string
  tenantId: string
  clientId: string
  clientName: string | null
  rateName: string
  rateType: string
  itemType: string
  unit: string
  defaultRate: number
  preferentialRate: number
  currency: string
  effectiveFrom: string
  effectiveTo: string | null
  status: string
  requiresApproval: boolean
  createdBy: string | null
  approvedBy: string | null
  approvedAt: string | null
  createdAt: string
}

export interface FinanceApprovalRule {
  id: string
  tenantId: string
  approvalArea: string
  thresholdAmount: number
  currency: string
  requiredRole: string
  requiredApproverUserId: string | null
  requiredApproverUserName: string | null
  requiredAssignmentGroupId: string | null
  requiredAssignmentGroupName: string | null
  sequence: number
  isActive: boolean
  createdAt: string
}

export interface CreditTerm {
  id: string
  tenantId: string
  name: string
  code: string
  netDays: number
  isDepositRequired: boolean
  depositPercent: number
  creditLimit: number | null
  description: string | null
  isDefault: boolean
  isActive: boolean
  createdAt: string
}

export interface FinanceDashboard {
  totalInvoicedYtd: number
  totalCollectedYtd: number
  outstandingBalance: number
  overdueBalance: number
  quoteConversionRate: number
  totalQuotations: number
  convertedQuotations: number
  currency: string
}
