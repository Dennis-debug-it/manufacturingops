export type OrgNodeLevel = 'Group' | 'Factory' | 'Section' | 'SubSection'

export interface OrgNode {
  id: string
  parentId: string | null
  level: OrgNodeLevel
  name: string
  code: string
  description: string | null
  location: string | null
  displayOrder: number
  isActive: boolean
  templateKey: string | null
  externalId: string | null
  externalSystem: string | null
  /** False for ancestors shown only so a scoped user's tree has a root. */
  canAccess: boolean
  childCount: number
  assetCount: number
}

export interface UpsertOrgNodePayload {
  parentId: string | null
  level: OrgNodeLevel
  name: string
  code: string
  description?: string | null
  location?: string | null
  displayOrder?: number | null
  isActive?: boolean | null
  externalId?: string | null
  externalSystem?: string | null
}

export interface IndustryTemplate {
  key: string
  name: string
  productionUnit: string
  sections: { code: string; name: string; description: string }[]
}

export interface FactorySettings {
  industryTemplate: string
  productionUnit: string
  productionUnitShort: string
  useGroupLevel: boolean
  useSubSectionLevel: boolean
  weeklyWindowEnabled: boolean
  weeklyWindowDay: string
  weeklyWindowStart: string
  weeklyWindowEnd: string
  lowCropStartMonth: number | null
  lowCropEndMonth: number | null
  tagPrefix: string
  nextTagNumber: number
}

export type UpdateFactorySettingsPayload = Omit<FactorySettings, 'nextTagNumber'>

export type Criticality = 'A' | 'B' | 'C'
export type OperationalStatus = 'Operational' | 'Standby' | 'Down'

export interface CustomFieldValue {
  fieldDefinitionId: string
  fieldName: string
  fieldLabel: string
  fieldType: string
  value: string
}

/** The asset as the API returns it, factory fields included. */
export interface FactoryAsset {
  id: string
  assetName: string
  assetCode: string
  assetType: string | null
  assetCategoryId: string | null
  assetCategoryName: string | null
  serialNumber: string | null
  manufacturer: string | null
  model: string | null
  installationDate: string | null
  warrantyExpiryDate: string | null
  warrantyStartDate: string | null
  warrantyPeriodMonths: number | null
  isUnderWarranty: boolean
  warrantyStatus: string
  notes: string | null
  status: string
  createdAt: string
  decommissionDate: string | null
  decommissionReason: string | null
  recommendedPmFrequency: string | null
  nextPmDate: string | null
  lastPmDate: string | null
  customFieldValues: CustomFieldValue[]
  orgNodeId: string | null
  orgNodeName: string | null
  orgNodeLevel: OrgNodeLevel | null
  parentAssetId: string | null
  parentAssetCode: string | null
  parentAssetName: string | null
  criticality: Criticality | null
  operationalStatus: OperationalStatus
  lostProductionPerHour: number | null
  commissioningDate: string | null
  disposalDate: string | null
  disposalMethod: string | null
  externalId: string | null
  externalSystem: string | null
}

export interface UpsertFactoryAssetPayload {
  clientId: null
  branchId: null
  siteId: null
  systemId: null
  assetCategoryId: string | null
  assetName: string
  assetCode: string | null
  assetType: string | null
  ownershipType: string | null
  location: string | null
  serialNumber: string | null
  manufacturer: string | null
  model: string | null
  installationDate: string | null
  warrantyExpiryDate: string | null
  recommendedPmFrequency: string | null
  pmFrequencyValue: number | null
  pmFrequencyUnit: string | null
  autoSchedulePm: boolean
  lastPmDate: string | null
  nextPmDate: string | null
  notes: string | null
  status: string | null
  customFieldValues: { fieldDefinitionId: string; value: string | null }[]
  warrantyStartDate: string | null
  warrantyPeriodMonths: number | null
  orgNodeId: string | null
  parentAssetId: string | null
  criticality: Criticality | null
  operationalStatus: OperationalStatus | null
  lostProductionPerHour: number | null
  commissioningDate: string | null
  externalId: string | null
  externalSystem: string | null
}

export interface AssetClassField {
  id: string
  fieldName: string
  fieldLabel: string
  fieldType: 'Text' | 'Number' | 'Dropdown' | 'Date' | string
  dropdownOptions: string | null
  unit: string | null
  isRequired: boolean
  isActive: boolean
  displayOrder: number
}

export interface AssetClass {
  id: string
  name: string
  code: string | null
  parentCategoryName: string | null
  isActive: boolean
  defaultCriticality: Criticality | null
  defaultMeterTypes: string[]
  fields: AssetClassField[]
}

export interface LifecycleEvent {
  id: string
  eventType: string
  occurredAt: string
  fromOrgNodeId: string | null
  toOrgNodeId: string | null
  fromValue: string | null
  toValue: string | null
  notes: string | null
  performedByName: string | null
}

export interface AssetComponent {
  id: string
  parentAssetId: string | null
  assetCode: string
  assetName: string
  criticality: Criticality | null
  status: string
  operationalStatus: OperationalStatus
}

export interface MeterType {
  meterType: string
  defaultUnit: string
  isCumulative: boolean
}

export interface Meter {
  id: string
  assetId: string
  name: string
  meterType: string
  unit: string
  isCumulative: boolean
  isPrimary: boolean
  isActive: boolean
  currentValue: number | null
  lastReadingAt: string | null
  minThreshold: number | null
  maxThreshold: number | null
  externalId: string | null
  externalSystem: string | null
  warningMin: number | null
  warningMax: number | null
  /** Alert, or WorkOrder: also raise a job when the alarm limit is crossed. */
  conditionAction: ConditionAction
  triggerAfterReadings: number
  alarmPriority: string
  alarmJobPlanId: string | null
  conditionState: ConditionLevel
  conditionSince: string | null
}

export type ConditionLevel = 'Normal' | 'Warning' | 'Alarm'
export type ConditionAction = 'Alert' | 'WorkOrder'

export interface UpsertMeterPayload {
  name: string
  meterType: string
  unit?: string | null
  isCumulative?: boolean | null
  isPrimary?: boolean | null
  minThreshold?: number | null
  maxThreshold?: number | null
  initialValue?: number | null
  warningMin?: number | null
  warningMax?: number | null
  conditionAction?: ConditionAction | null
  triggerAfterReadings?: number | null
  alarmPriority?: string | null
  alarmJobPlanId?: string | null
  externalId?: string | null
  externalSystem?: string | null
}

export interface MeterReading {
  id: string
  meterId: string
  value: number
  delta: number | null
  readAt: string
  source: string
  isReset: boolean
  isOutOfRange: boolean
  notes: string | null
  recordedByName: string | null
  condition: ConditionLevel
}

export interface AssetTag {
  id: string
  assetId: string
  tagNumber: string
  symbology: string
  status: 'Active' | 'Replaced' | 'Retired'
  scanUrl: string
  issuedAt: string
  retiredAt: string | null
  retiredReason: string | null
  replacedByTagId: string | null
  printCount: number
  lastPrintedAt: string | null
}

export interface ScanResult {
  code: string
  matchedBy: 'Tag' | 'AssetCode'
  warning: string | null
  activeTagNumber: string | null
  assetId: string
  assetCode: string
  assetName: string
  status: string
  operationalStatus: OperationalStatus
  criticality: Criticality | null
  manufacturer: string | null
  model: string | null
  serialNumber: string | null
  orgNodeId: string | null
  location: string | null
  parent: { id: string; assetCode: string; assetName: string } | null
  components: { id: string; assetCode: string; assetName: string }[]
  meters: { id: string; name: string; meterType: string; unit: string; currentValue: number | null; lastReadingAt: string | null }[]
  openWorkOrders: { id: string; workOrderNumber: string; title: string; status: string; priority: string; dueDate: string | null }[]
  recentHistory: { eventType: string; occurredAt: string; fromValue: string | null; toValue: string | null; notes: string | null }[]
}

export interface OfflineScanEntry {
  assetId: string
  assetCode: string
  assetName: string
  location: string | null
  parentAssetCode: string | null
  criticality: Criticality | null
  operationalStatus: OperationalStatus
  manufacturer: string | null
  model: string | null
  serialNumber: string | null
  tagNumber: string | null
  oldTagNumbers: string[]
  meters: { id: string; name: string; unit: string; isCumulative: boolean; currentValue: number | null; lastReadingAt: string | null }[]
}

export interface AssetDocumentVersion {
  versionNumber: number
  fileName: string
  fileSize: number
  mimeType: string
  downloadUrl: string
  notes: string | null
  uploadedByName: string | null
  uploadedAt: string
}

export interface AssetDocument {
  id: string
  assetId: string
  title: string
  documentType: string
  description: string | null
  currentVersion: number
  expiryDate: string | null
  isExpired: boolean
  isArchived: boolean
  versions: AssetDocumentVersion[]
}

export interface ImportPreview {
  totalRows: number
  validRows: number
  rows: { rowNumber: number; assetCode: string | null; assetName: string | null; errors: string[] }[]
}

export const DOCUMENT_TYPES = ['Manual', 'Drawing', 'Datasheet', 'Certificate', 'Procedure', 'Photo', 'Other'] as const
export const CRITICALITIES: Criticality[] = ['A', 'B', 'C']
export const OPERATIONAL_STATUSES: OperationalStatus[] = ['Operational', 'Standby', 'Down']
export const LEVEL_LABELS: Record<OrgNodeLevel, string> = { Group: 'Group', Factory: 'Factory', Section: 'Section', SubSection: 'Sub-section' }
