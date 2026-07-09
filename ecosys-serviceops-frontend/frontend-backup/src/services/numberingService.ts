import { tenantService } from './tenantService'
import type { NumberingRule } from '../types/app'

function padNumber(value: number) {
  return String(value).padStart(6, '0')
}

export const numberingService = {
  listRules(tenantId: string) {
    return tenantService.getTenantData(tenantId).settings.numberingRules
  },
  updateRule(tenantId: string, branchId: string, patch: Partial<NumberingRule>) {
    tenantService.updateTenantData(tenantId, (current) => ({
      ...current,
      settings: {
        ...current.settings,
        numberingRules: current.settings.numberingRules.map((rule) => (rule.branchId === branchId ? { ...rule, ...patch } : rule)),
      },
    }))
  },
  previewWorkOrderNumber(tenantId: string, branchId: string) {
    const rule = this.listRules(tenantId).find((item) => item.branchId === branchId)
    return rule ? `${rule.workOrderPrefix}-${padNumber(rule.nextWorkOrderNumber)}` : 'WO-UNKNOWN-000001'
  },
  previewAssetNumber(tenantId: string, branchId: string) {
    const rule = this.listRules(tenantId).find((item) => item.branchId === branchId)
    return rule ? `${rule.assetPrefix}-${padNumber(rule.nextAssetNumber)}` : 'AST-UNKNOWN-000001'
  },
  previewRequisitionNumber(tenantId: string, branchId: string) {
    const rule = this.listRules(tenantId).find((item) => item.branchId === branchId)
    return rule ? `${rule.requisitionPrefix}-${padNumber(rule.nextRequisitionNumber)}` : 'REQ-UNKNOWN-000001'
  },
  consumeWorkOrderNumber(tenantId: string, branchId: string) {
    const rule = this.listRules(tenantId).find((item) => item.branchId === branchId)
    const number = this.previewWorkOrderNumber(tenantId, branchId)
    this.updateRule(tenantId, branchId, { nextWorkOrderNumber: (rule?.nextWorkOrderNumber ?? 1) + 1 })
    return number
  },
  consumeAssetNumber(tenantId: string, branchId: string) {
    const rule = this.listRules(tenantId).find((item) => item.branchId === branchId)
    const number = this.previewAssetNumber(tenantId, branchId)
    this.updateRule(tenantId, branchId, { nextAssetNumber: (rule?.nextAssetNumber ?? 1) + 1 })
    return number
  },
  consumeRequisitionNumber(tenantId: string, branchId: string) {
    const rule = this.listRules(tenantId).find((item) => item.branchId === branchId)
    const number = this.previewRequisitionNumber(tenantId, branchId)
    this.updateRule(tenantId, branchId, { nextRequisitionNumber: (rule?.nextRequisitionNumber ?? 1) + 1 })
    return number
  },
}
