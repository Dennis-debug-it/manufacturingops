import { PermissionGroupsPage } from './PermissionGroupsPage'
import { AccessScopesPage } from './AccessScopesPage'
import { ApprovalLimitsPage } from './ApprovalLimitsPage'
import { AccessAuditHistoryPage } from './AccessAuditHistoryPage'

/**
 * The four Users & Access sections the router mounts under /team. This used to render static
 * placeholder copy for all four; each section is now a real page against
 * `AccessControlController` (`api/access/...`), except Audit History which reads the
 * access-related slice of the tenant audit log — see `AccessAuditHistoryPage`.
 *
 * The `section` prop keeps the router's existing call sites (`<AccessSectionPage section="groups" />`)
 * compiling unchanged.
 */
export type AccessSection = 'groups' | 'scopes' | 'limits' | 'audit'

export function AccessSectionPage({ section }: { section: AccessSection }) {
  switch (section) {
    case 'groups':
      return <PermissionGroupsPage />
    case 'scopes':
      return <AccessScopesPage />
    case 'limits':
      return <ApprovalLimitsPage />
    case 'audit':
      return <AccessAuditHistoryPage />
  }
}
