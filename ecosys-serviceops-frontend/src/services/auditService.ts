import { updateDatabase } from './storage'
import { nowIso } from '../utils/date'
import { createId } from '../utils/id'

export const auditService = {
  addTenantAudit(tenantId: string, actor: string, action: string, entityType: string, entityId: string, detail: string) {
    updateDatabase((database) => {
      database.tenantData[tenantId].auditLog.unshift({
        id: createId('audit'),
        tenantId,
        actor,
        action,
        entityType,
        entityId,
        detail,
        createdAt: nowIso(),
      })
      return database
    })
  },
  addPlatformAudit(actor: string, action: string, detail: string) {
    updateDatabase((database) => {
      database.platformAuditLog.unshift({
        id: createId('paudit'),
        actor,
        action,
        detail,
        createdAt: nowIso(),
      })
      return database
    })
  },
}
