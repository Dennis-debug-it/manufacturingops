export interface PmPlan {
  id: string
  branchId: string | null
  branchName: string | null
  assetId: string
  assetName: string | null
  clientId: string | null
  clientName: string | null
  pmTemplateId: string | null
  pmTemplateName: string | null
  frequency: string
  serviceIntervalMonths: number | null
  autoSchedule: boolean
  lastPmDate: string | null
  nextPmDate: string | null
  status: string
  createdAt: string
}

export interface PmTemplateChecklistItem {
  id: string
  sectionId: string | null
  sectionName: string | null
  sectionOrder: number
  question: string
  helperText: string | null
  type: string
  unit: string | null
  minValue: number | null
  maxValue: number | null
  defaultValue: string | null
  required: boolean
  evidenceRequired: boolean
  technicianRemarksAllowed: boolean
  requiresNoteOnFail: boolean
  order: number
  options: string[]
}

export interface PmTemplate {
  id: string
  name: string
  category: string
  description: string | null
  code: string | null
  workType: string
  defaultIntervalMonths: number | null
  assetCategoryId: string | null
  assetCategoryName: string | null
  version: number
  isPlatformSeed: boolean
  checklist: PmTemplateChecklistItem[]
  isActive: boolean
}
