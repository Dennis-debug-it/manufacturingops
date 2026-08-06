import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../../shared/api/apiClient'
import { endpoints } from '../../shared/api/endpoints'

/** The 23 tenant capabilities the backend stores per role. */
export interface RoleCapabilities {
  canViewWorkOrders: boolean
  canCreateWorkOrders: boolean
  canAssignWorkOrders: boolean
  canCompleteWorkOrders: boolean
  canViewKnowledge: boolean
  canManageKnowledge: boolean
  canViewMaterials: boolean
  canRequestMaterials: boolean
  canManageMaterials: boolean
  canApproveMaterials: boolean
  canIssueMaterials: boolean
  canManageAssets: boolean
  canManageSettings: boolean
  canViewReports: boolean
  canViewFinance: boolean
  canManageFinance: boolean
  canViewFinanceRates: boolean
  canManageFinanceRates: boolean
  canViewFinanceApprovals: boolean
  canManageFinanceApprovals: boolean
  canViewBillableWork: boolean
  canManageBillableWork: boolean
  canOverrideMaterialStock: boolean
}

export type CapabilityKey = keyof RoleCapabilities

/** All 23 capabilities set to false — a safe starting point for a brand-new user. */
export const EMPTY_CAPABILITIES: RoleCapabilities = {
  canViewWorkOrders: false,
  canCreateWorkOrders: false,
  canAssignWorkOrders: false,
  canCompleteWorkOrders: false,
  canViewKnowledge: false,
  canManageKnowledge: false,
  canViewMaterials: false,
  canRequestMaterials: false,
  canManageMaterials: false,
  canApproveMaterials: false,
  canIssueMaterials: false,
  canManageAssets: false,
  canManageSettings: false,
  canViewReports: false,
  canViewFinance: false,
  canManageFinance: false,
  canViewFinanceRates: false,
  canManageFinanceRates: false,
  canViewFinanceApprovals: false,
  canManageFinanceApprovals: false,
  canViewBillableWork: false,
  canManageBillableWork: false,
  canOverrideMaterialStock: false,
}

/** All 23 capabilities set to true — used to show Admin's implicit full access. */
export const FULL_CAPABILITIES: RoleCapabilities = (Object.keys(EMPTY_CAPABILITIES) as CapabilityKey[]).reduce(
  (acc, key) => {
    acc[key] = true
    return acc
  },
  { ...EMPTY_CAPABILITIES },
)

export interface RolePermissionRow {
  role: string
  permissions: RoleCapabilities
}

export interface RolePermissionMatrix {
  roles: RolePermissionRow[]
  /** Role that implicitly has every capability and is not editable (Admin). */
  implicitAllAccessRole: string
}

export function useRolePermissions() {
  return useQuery({
    queryKey: ['role-permissions'],
    queryFn: () => api.get<RolePermissionMatrix>(endpoints.rolePermissions.matrix),
    staleTime: 30_000,
  })
}

export function useSaveRolePermissions() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (roles: RolePermissionRow[]) =>
      api.put<RolePermissionMatrix>(endpoints.rolePermissions.matrix, { roles }),
    onSuccess: (data) => {
      queryClient.setQueryData(['role-permissions'], data)
      // A user's effective permissions come from their role, so refresh the session.
      void queryClient.invalidateQueries({ queryKey: ['auth', 'me'] })
    },
  })
}

/** Capability rows, grouped for display. Order defines the matrix rows. */
export const capabilityGroups: Array<{ group: string; items: Array<{ key: CapabilityKey; label: string }> }> = [
  {
    group: 'Work Orders',
    items: [
      { key: 'canViewWorkOrders', label: 'View work orders' },
      { key: 'canCreateWorkOrders', label: 'Create work orders' },
      { key: 'canAssignWorkOrders', label: 'Assign work orders' },
      { key: 'canCompleteWorkOrders', label: 'Complete work orders' },
    ],
  },
  {
    group: 'Knowledge',
    items: [
      { key: 'canViewKnowledge', label: 'View knowledge base' },
      { key: 'canManageKnowledge', label: 'Manage knowledge base' },
    ],
  },
  {
    group: 'Inventory & Materials',
    items: [
      { key: 'canViewMaterials', label: 'View materials' },
      { key: 'canRequestMaterials', label: 'Request materials' },
      { key: 'canManageMaterials', label: 'Manage materials' },
      { key: 'canApproveMaterials', label: 'Approve material requests' },
      { key: 'canIssueMaterials', label: 'Issue materials' },
      { key: 'canOverrideMaterialStock', label: 'Override stock levels' },
      { key: 'canManageAssets', label: 'Manage assets' },
    ],
  },
  {
    group: 'Finance',
    items: [
      { key: 'canViewFinance', label: 'View finance' },
      { key: 'canManageFinance', label: 'Manage finance' },
      { key: 'canViewFinanceRates', label: 'View rate cards' },
      { key: 'canManageFinanceRates', label: 'Manage rate cards' },
      { key: 'canViewFinanceApprovals', label: 'View invoice approvals' },
      { key: 'canManageFinanceApprovals', label: 'Manage invoice approvals' },
      { key: 'canViewBillableWork', label: 'View billable work' },
      { key: 'canManageBillableWork', label: 'Manage billable work' },
    ],
  },
  {
    group: 'Administration',
    items: [
      { key: 'canViewReports', label: 'View reports' },
      { key: 'canManageSettings', label: 'Manage settings' },
    ],
  },
]

/** Human label for a stored role string. */
export function roleLabel(role: string): string {
  const normalized = role.toLowerCase()
  if (normalized === 'finance') return 'Finance'
  return role.charAt(0).toUpperCase() + role.slice(1)
}
