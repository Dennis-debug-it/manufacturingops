/**
 * ServiceOps modules. An edition is a fixed list of them (the backend's ServiceOpsPlans.EditionModules):
 * Essentials = operations + finance; Professional adds maintenance, inventory, projects and advanced;
 * Enterprise adds enterprise_tools. The WhatsApp Business add-on adds whatsapp_business on any edition.
 */
export type ServiceOpsModule =
  | 'operations'
  | 'finance'
  | 'maintenance'
  | 'inventory'
  | 'projects'
  | 'advanced'
  | 'enterprise_tools'
  | 'whatsapp_business'

// Checked in order, so a more specific prefix (/finance/expenses) wins over a broader one (/finance).
const modulePaths: ReadonlyArray<[ServiceOpsModule, readonly string[]]> = [
  ['maintenance', ['/maintenance', '/assets', '/systems', '/contracts', '/settings/pm-templates', '/settings/running-checks', '/settings/asset-categories', '/settings/escalation-rules', '/settings/sla-policies']],
  ['inventory', ['/inventory', '/settings/inventory']],
  ['advanced', ['/approvals', '/team/time-attendance', '/settings/time-attendance', '/settings/email-intake', '/settings/workflows', '/settings/approval-matrices', '/settings/approval-matrix']],
  ['enterprise_tools', ['/settings/integrations', '/settings/monitoring-intake', '/settings/monitoring']],
  ['whatsapp_business', ['/whatsapp', '/settings/whatsapp']],
  ['operations', ['/dashboard', '/work-orders', '/sites', '/reports', '/team/technicians', '/team/skills', '/team/groups', '/settings/notification-templates', '/settings/wo-templates', '/settings/executive-dashboard']],
  ['finance', ['/finance', '/settings/payments']],
]
/** The sales CRM joins an RFQ's quote (Finance) to its job (Operations), so it needs both; every edition has them. */

export function requiredModulesForPath(path: string): ServiceOpsModule[] {
  const matches = (prefix: string) => path === prefix || path.startsWith(`${prefix}/`)
  if (matches('/finance/billable-work')) return ['operations', 'finance']
  const match = modulePaths.find(([, prefixes]) => prefixes.some(matches))
  return match ? [match[0]] : []
}

export function canAccessModulePath(path: string, modules: readonly string[]): boolean {
  return requiredModulesForPath(path).every((module) => modules.includes(module))
}
