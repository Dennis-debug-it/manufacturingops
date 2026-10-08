export interface WorkOrderSlaStatus {
  workOrderId: string
  priority: string
  policyName: string | null
  responseDeadline: string | null
  resolutionDeadline: string | null
  responseBreached: boolean
  resolutionBreached: boolean
  responseBreachedAt: string | null
  resolutionBreachedAt: string | null
  responseMinutesRemaining: number | null
  resolutionMinutesRemaining: number | null
  status: 'OnTrack' | 'AtRisk' | 'Breached' | 'NoSla'
  isPaused: boolean
  pauseReason: string | null
}

export interface WorkOrderChecklistItem {
  id: string
  pmTemplateQuestionId: string | null
  sectionName: string | null
  questionText: string
  inputType: string
  isRequired: boolean
  sortOrder: number
  responseValue: string | null
  remarks: string | null
  isCompleted: boolean
  completedByUserId: string | null
  completedAt: string | null
  options: string[]
  sectionOrder: number
  helperText: string | null
  unit: string | null
  minValue: number | null
  maxValue: number | null
  defaultValue: string | null
  evidenceRequired: boolean
  attachmentId: string | null
  technicianRemarksAllowed: boolean
  requiresNoteOnFail: boolean
}

export interface WorkOrderTechnicianAssignment {
  id: string
  technicianId: string
  technicianName: string | null
  isLead: boolean
  status: string
  assignedAt: string
  acceptedAt: string | null
  arrivalAt: string | null
  departureAt: string | null
  notes: string | null
}

export interface WorkOrder {
  id: string
  branchId: string | null
  branchName: string | null
  clientId: string
  clientName: string | null
  siteId: string | null
  siteName: string | null
  assetId: string | null
  assetName: string | null
  assignmentGroupId: string | null
  assignmentGroupName: string | null
  workOrderNumber: string
  title: string
  description: string | null
  priority: string
  status: string
  workType: string
  slaStatus: string
  assignmentType: string
  assignedTechnicianId: string | null
  assignedTechnicianName: string | null
  assignedTechnicianIds: string[]
  leadTechnicianId: string | null
  leadTechnicianName: string | null
  dueDate: string | null
  createdAt: string
  workStartedAt: string | null
  arrivalAt: string | null
  departureAt: string | null
  completedAt: string | null
  workDoneNotes: string | null
  jobCardNotes: string | null
  slaResponseDeadline: string | null
  slaResolutionDeadline: string | null
  slaResponseBreached: boolean
  slaResolutionBreached: boolean
  slaResponseBreachedAt: string | null
  slaResolutionBreachedAt: string | null
  acknowledgedByName: string | null
  acknowledgementComments: string | null
  acknowledgementDate: string | null
  isPreventiveMaintenance: boolean
  holdReason: string | null
  holdNote: string | null
  assignmentStatus: string
  assignmentNotes: string | null
  technicianAssignments: WorkOrderTechnicianAssignment[]
  assignmentSummary: string
  isUnassigned: boolean
  pmTemplateId: string | null
  pmTemplateName: string | null
  preventiveMaintenancePlanId: string | null
  systemId: string | null
  systemName: string | null
  checklistItems: WorkOrderChecklistItem[]
  pmTemplateVersionSnapshot: number | null
  scheduledStartAt: string | null
  scheduledEndAt: string | null
  notifyClient: boolean
  clientContactEmail: string | null
  billingStatus: string | null
  isBillable: boolean
  clientContactPerson: string | null
  clientContactPhone: string | null
  clientSlaPlan: string | null
  siteCode: string | null
  siteContactPerson: string | null
  siteContactPhone: string | null
  siteOperatingHours: string | null
  siteAccessNotes: string | null
  siteLocation: string | null
  siteLatitude: number | null
  siteLongitude: number | null
  estimatedLabourHours: number | null
  assetCode: string | null
  assetType: string | null
  assetCategoryName: string | null
  assetSerialNumber: string | null
  assetManufacturer: string | null
  assetModel: string | null
  assetLocation: string | null
  assetWarrantyExpiryDate: string | null
  assetLastServiceDate: string | null
  assetNextServiceDate: string | null
  assetWarrantyStatus: string | null
  availableTransitions: string[]
  workflowStatusId: string | null
  contractId: string | null
  contractNumber: string | null
  contractStatus: string | null
  coverageStatus: string | null
  billingModel: string | null
  rateCardName: string | null
  contractEndDate: string | null
  contractRenewalRisk: string | null
  pricingNotes: string | null
  estimatedCharge: number | null
  finalCharge: number | null
  appliedSlaSource: string | null
  appliedRateSource: string | null
  trackingToken: string | null
  workOrderTemplateId: string | null
  quoteId: string | null
  quoteNumber: string | null
  parentWorkOrderId: string | null
  childWorkOrderCount: number
  holdType: string | null
  holdSince: string | null
  plannedMaterials: PlannedMaterial[]
  isRepeatVisit: boolean
  repeatVisitCount: number
  /** The finished job this one is a return visit for. */
  followUpOfWorkOrderId?: string | null
  followUpOfWorkOrderNumber?: string | null
  /** Return visits raised from this job. */
  followUps?: WorkOrderLink[]
  /** The technician answered "Follow-up required" with yes on the checklist. */
  followUpRequested?: boolean
}

export interface WorkOrderLink {
  id: string
  workOrderNumber: string
  status: string
  createdAt: string
}

export interface PlannedMaterial {
  materialId: string
  materialName: string
  quantity: number
}

export interface WorkOrderTemplateMaterial {
  materialId: string
  materialName: string | null
  quantity: number
}

export interface WorkOrderTemplate {
  id: string
  name: string
  jobType: string
  description: string | null
  estimatedDurationMins: number | null
  defaultPriority: string
  requiredSkills: string[]
  checklistTemplateId: string | null
  checklistTemplateName: string | null
  defaultMaterials: WorkOrderTemplateMaterial[]
  isActive: boolean
  createdAt: string
}

export interface WorkOrderEvent {
  id: string
  eventType: string
  status: string | null
  message: string | null
  actorUserId: string | null
  actorUserName: string | null
  oldStatus: string | null
  newStatus: string | null
  reason: string | null
  note: string | null
  latitude: number | null
  longitude: number | null
  occurredAt: string
  metadataJson: string | null
}

export interface WorkOrderNote {
  id: string
  workOrderId: string
  content: string
  isClientVisible: boolean
  createdByUserId: string
  createdByUserName: string | null
  createdAt: string
}

export interface RelatedWorkOrder {
  id: string
  workOrderNumber: string
  status: string
  completedAt: string | null
  description: string
  technicianName: string | null
}

export interface RelatedWorkOrders {
  sameSite: RelatedWorkOrder[]
  sameAsset: RelatedWorkOrder[]
}

export interface WorkOrderDocument {
  documentType: 'Quote' | 'ServiceReport' | 'Invoice' | 'MaterialRequest'
  id: string
  number: string
  status: string
  pdfUrl: string | null
}

export interface RepeatVisitCheck {
  isRepeatVisit: boolean
  repeatVisitCount: number
}

export interface WorkOrderAssignmentHistoryItem {
  id: string
  action: string
  fromGroupId: string | null
  fromGroupName: string | null
  toGroupId: string | null
  toGroupName: string | null
  fromTechnicianId: string | null
  fromTechnicianName: string | null
  toTechnicianId: string | null
  toTechnicianName: string | null
  performedByUserId: string | null
  performedByUserName: string | null
  performedAt: string
  notes: string | null
}

export interface WorkOrderPhoto {
  id: string
  attachmentId: string
  fileName: string
  publicUrl: string | null
  caption: string
  category: string
  includeInReport: boolean
  uploadedByUserId: string
  uploadedByName: string | null
  uploadedAt: string
}

export interface WorkOrderMaterialUsage {
  id: string
  materialItemId: string
  materialName: string | null
  unitOfMeasure: string | null
  assetId: string | null
  assetName: string | null
  quantityUsed: number
  unitCost: number | null
  chargeable: boolean
  notes: string | null
  usedByUserId: string
  usedByName: string | null
  usedAt: string
}

export interface WorkOrderSignature {
  id: string
  signatureType: string
  signerName: string
  signerRole: string | null
  signatureDataUrl: string
  comment: string | null
  capturedByUserId: string
  capturedAt: string
}

export interface WorkOrderExecutionProgressUpdate {
  id: string
  message: string
  actorUserId: string | null
  actorUserName: string | null
  occurredAt: string
}

export interface WorkOrderReportChecklistItem {
  question: string
  inputType: string
  responseValue: string | null
  unit: string | null
  remarks: string | null
  isRequired: boolean
  isCompleted: boolean
  evidenceRequired: boolean
  evidenceUrl: string | null
}

export interface WorkOrderReportChecklistSection {
  sectionName: string
  items: WorkOrderReportChecklistItem[]
}

export interface WorkOrderReportMaterial {
  name: string
  quantityUsed: number
  unitOfMeasure: string
  unitCost: number | null
  chargeable: boolean
  notes: string | null
}

export interface WorkOrderReportPhoto {
  caption: string
  publicUrl: string | null
}

export interface WorkOrderReportPhotoGroup {
  category: string
  photos: WorkOrderReportPhoto[]
}

export interface WorkOrderReportSignature {
  signatureType: string
  signerName: string
  signerRole: string | null
  comment: string | null
  capturedAtLabel: string
}

export interface WorkOrderReportTimestamp {
  label: string
  value: string | null
}

export interface WorkOrderServiceReportPreview {
  companyName: string
  workOrderNumber: string
  title: string
  clientName: string | null
  siteLabel: string | null
  assetName: string | null
  assetDetails: string | null
  technicianTeam: string | null
  templateName: string | null
  templateVersion: number | null
  reportedProblem: string | null
  findings: string | null
  workDone: string | null
  generatedAtLabel: string
  timestamps: WorkOrderReportTimestamp[]
  checklistSections: WorkOrderReportChecklistSection[]
  materials: WorkOrderReportMaterial[]
  photoGroups: WorkOrderReportPhotoGroup[]
  signatures: WorkOrderReportSignature[]
  showPoweredByEcosys: boolean
}

export interface WorkOrderExecutionBundle {
  notes: { findings: string | null; workDone: string | null }
  photos: WorkOrderPhoto[]
  materialUsages: WorkOrderMaterialUsage[]
  signatures: WorkOrderSignature[]
  progressUpdates: WorkOrderExecutionProgressUpdate[]
  reportPreview: WorkOrderServiceReportPreview
}
