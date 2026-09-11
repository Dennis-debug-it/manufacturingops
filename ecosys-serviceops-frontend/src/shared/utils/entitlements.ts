export type ServiceOpsModule = 'operations' | 'finance'

const operationsPaths = ['/dashboard', '/work-orders', '/dispatch', '/maintenance', '/inventory', '/assets', '/sites', '/systems', '/contracts', '/reports', '/whatsapp', '/team/technicians', '/team/skills', '/team/groups', '/team/time-attendance', '/settings/pm-templates', '/settings/inventory', '/settings/time-attendance', '/settings/email-intake', '/settings/monitoring-intake', '/settings/monitoring', '/settings/notification-templates', '/settings/whatsapp', '/settings/workflows', '/settings/escalation-rules', '/settings/wo-templates', '/settings/sla-policies', '/settings/executive-dashboard']
const financePaths = ['/finance', '/approvals', '/settings/payments', '/settings/credit-control', '/settings/approval-matrices', '/settings/approval-matrix']

export function requiredModulesForPath(path: string): ServiceOpsModule[] {
  const matches = (prefix: string) => path === prefix || path.startsWith(`${prefix}/`)
  if (matches('/finance/billable-work')) return ['operations', 'finance']
  if (operationsPaths.some(matches)) return ['operations']
  if (financePaths.some(matches)) return ['finance']
  return []
}

export function canAccessModulePath(path: string, modules: readonly string[]): boolean {
  return requiredModulesForPath(path).every((module) => modules.includes(module))
}

