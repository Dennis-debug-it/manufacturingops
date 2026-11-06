import { hasPermission } from '../hooks/usePermission'
import type { AuthUser } from '../types/auth'

type Can = (action: string) => boolean

interface RouteRule {
  prefix: string
  allow: (can: Can, user: AuthUser) => boolean
}

const everyone = () => true
const adminOnly = (_: Can, user: AuthUser) => user.role === 'admin'

/**
 * Who may open each office screen, most specific prefix first. These follow the services each
 * screen loads, so a page someone can't use shows a clear "no access" message instead of loading
 * half-way and failing (it used to pop up "Admin access is required" errors). The menu already
 * hides these; this covers typed addresses, bookmarks and links in emails.
 */
const rules: RouteRule[] = [
  { prefix: '/settings/account', allow: everyone },
  // Each person's own notification choices (the workspace-wide matrix stays admin-only).
  { prefix: '/settings/notifications', allow: everyone },
  { prefix: '/settings/audit-logs', allow: (can) => can('reports.view') },
  { prefix: '/settings', allow: adminOnly },
  { prefix: '/onboarding', allow: adminOnly },
  { prefix: '/team', allow: adminOnly },
  { prefix: '/dashboard/executive', allow: (can) => can('reports.view') && can('finance.view') },
  { prefix: '/finance', allow: (can) => can('finance.view') },
  { prefix: '/reports', allow: (can) => can('reports.view') },
  { prefix: '/dispatch', allow: (can) => can('dispatch.view') },
  { prefix: '/inventory', allow: (can) => can('materials.view') },
  { prefix: '/maintenance/compliance', allow: (can) => can('reports.view') },
  { prefix: '/maintenance/schedules', allow: (can) => can('settings.manage') || can('work-orders.create') || can('work-orders.assign') },
  { prefix: '/maintenance', allow: (_, user) => Boolean(user.permissions.canViewWorkOrders) },
  { prefix: '/work-orders', allow: (_, user) => Boolean(user.permissions.canViewWorkOrders) },
  { prefix: '/crm', allow: (can) => can('crm.view') },
  { prefix: '/clients', allow: (can) => can('clients.view') },
  { prefix: '/sites', allow: (can) => can('clients.view') },
  { prefix: '/assets', allow: (can) => can('clients.view') },
  { prefix: '/contracts', allow: (can) => can('contracts.view') },
  { prefix: '/projects/settings', allow: (can) => can('projects.settings') },
  { prefix: '/projects', allow: (can) => can('projects.view') },
  { prefix: '/whatsapp', allow: (can) => can('whatsapp.view') },
  { prefix: '/calendar', allow: (can) => can('activities.view') },
  { prefix: '/activities', allow: (can) => can('activities.view') },
  { prefix: '/action-items', allow: (can) => can('activities.view') },
]

const matches = (path: string, prefix: string) => path === prefix || path.startsWith(prefix + '/')

/** Whether this person may open the office screen at this path. Paths without a rule are open to all staff. */
export function canOpenPath(user: AuthUser | null | undefined, path: string): boolean {
  if (!user) return false
  if (user.role === 'admin' || user.role === 'super_admin') return true
  const rule = rules.find((candidate) => matches(path, candidate.prefix))
  return rule ? rule.allow((action) => hasPermission(user, action), user) : true
}
