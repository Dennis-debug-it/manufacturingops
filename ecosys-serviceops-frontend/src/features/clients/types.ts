export interface Client {
  id: string
  clientName: string
  clientType: string | null
  email: string | null
  phone: string | null
  location: string | null
  contactPerson: string | null
  contactPhone: string | null
  slaDefinitionId: string | null
  slaDefinitionName: string | null
  activeStartDate: string | null
  activeDurationValue: number | null
  activeDurationUnit: string | null
  activeEndDate: string | null
  activeStatus: string
  notes: string | null
  isActive: boolean
  createdAt: string
  siteCount: number
  systemCount: number
  assetCount: number
  openWorkOrderCount: number
  /** A company sales is still talking to; kept out of client pickers until a deal is won or work is raised. */
  isProspect?: boolean
}

export const CONTACT_ROLES = ['DecisionMaker', 'Procurement', 'Technical', 'Finance', 'Other'] as const
export type ContactRole = (typeof CONTACT_ROLES)[number]
export const contactRoleLabels: Record<ContactRole, string> = {
  DecisionMaker: 'Decision maker',
  Procurement: 'Procurement',
  Technical: 'Technical',
  Finance: 'Finance',
  Other: 'Other',
}

/** A person at a client or prospect (not a portal login). */
export interface ClientContact {
  id: string
  clientId: string
  fullName: string
  jobTitle: string | null
  email: string | null
  phone: string | null
  role: ContactRole
  isPrimary: boolean
  notes: string | null
}

export type UpsertClientContactPayload = Omit<ClientContact, 'id' | 'clientId'>

export interface PossibleDuplicateClient {
  id: string
  clientName: string
  isProspect: boolean
  reason: string
}

export interface ClientSiteSummary {
  siteId: string
  siteCode: string
  siteName: string
  siteType: string | null
  location: string | null
  contactPerson: string | null
  systemCount: number
  assetCount: number
  openWorkOrderCount: number
  status: string
  createdAt: string
  updatedAt: string
}

export interface ClientSummary {
  client: Client
  siteCount: number
  systemCount: number
  assetCount: number
  openWorkOrderCount: number
  upcomingPmCount: number
  sites: ClientSiteSummary[]
}

export interface Site {
  id: string
  tenantId: string
  clientId: string
  siteCode: string
  siteName: string
  siteType: string | null
  status: string
  streetAddress: string | null
  areaEstate: string | null
  townCity: string | null
  county: string | null
  country: string | null
  region: string | null
  contactPerson: string | null
  contactPhone: string | null
  contactEmail: string | null
  alternateContact: string | null
  operatingHours: string | null
  accessNotes: string | null
  specialInstructions: string | null
  createdAt: string
  updatedAt: string
}

export interface SiteSystemSummary {
  systemId: string
  systemName: string
  systemType: string
  status: string
  manufacturer: string | null
  model: string | null
}

export interface SiteAssetSummary {
  assetId: string
  assetName: string
  assetCode: string
  assetType: string | null
  status: string | null
}

export interface SiteWorkOrderSummary {
  workOrderId: string
  workOrderNumber: string
  title: string
  status: string
  priority: string
  dueDate: string | null
}

export interface SiteDetail {
  site: Site
  clientName: string | null
  systemCount: number
  assetCount: number
  openWorkOrders: number
  upcomingPmCount: number
  systems: SiteSystemSummary[]
  assets: SiteAssetSummary[]
  workOrders: SiteWorkOrderSummary[]
}

export interface Asset {
  id: string
  branchId: string | null
  branchName: string | null
  clientId: string
  clientName: string | null
  siteId: string | null
  siteName: string | null
  systemId: string | null
  systemName: string | null
  systemType: string | null
  assetCategoryId: string | null
  assetCategoryName: string | null
  assetName: string
  assetCode: string
  assetType: string | null
  ownershipType: string | null
  location: string | null
  serialNumber: string | null
  manufacturer: string | null
  model: string | null
  installationDate: string | null
  warrantyExpiryDate: string | null
  warrantyStartDate: string | null
  warrantyPeriodMonths: number | null
  isUnderWarranty: boolean
  /** 'Under Warranty' | 'Warranty Expiring Soon' | 'Warranty Expired' | 'No Warranty' */
  warrantyStatus: string
  recommendedPmFrequency: string | null
  pmFrequencyValue: number | null
  pmFrequencyUnit: string | null
  autoSchedulePm: boolean
  lastPmDate: string | null
  nextPmDate: string | null
  notes: string | null
  status: string | null
  decommissionDate?: string | null
  decommissionReason?: string | null
  hasContractCover?: boolean
  /** The contract covering this asset (detail only). Scope: Asset (listed), Category, Site or Client (everything). */
  coveredBy?: AssetContractCover | null
  /** 'Under Warranty' | 'Under Contract' | 'Out of Support' | 'Retired' */
  supportStatus?: string
  /** Datasheet values for the asset's category (labels come with the value). */
  customFieldValues?: { fieldDefinitionId: string; value: string; fieldLabel?: string; fieldType?: string; unit?: string | null }[]
}

export interface AssetContractCover {
  contractId: string
  contractNumber: string
  contractName: string
  status: string
  endDate: string
  scope: 'Asset' | 'Category' | 'Site' | 'Client'
}

/** One of the client's open contracts, seen from an asset: does it cover it, and can the asset be added. */
export interface AssetContractOption {
  contractId: string
  contractNumber: string
  contractName: string
  status: string
  startDate: string
  endDate: string
  scope: 'Asset' | 'Category' | 'Site' | 'Client'
  scopeLabel: string
  scopeCoversAsset: boolean
  canAdd: boolean
  reason: string | null
}

