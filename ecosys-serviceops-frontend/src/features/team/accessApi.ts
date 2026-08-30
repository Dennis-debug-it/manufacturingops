import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../../shared/api/apiClient'
import { endpoints } from '../../shared/api/endpoints'

/**
 * Dynamic access control — the tenant-scoped counterpart to the static role matrix in
 * `rolePermissionsApi.ts`. Contract: `backend/src/Ecosys.Api/Controllers/AccessControlController.cs`.
 *
 * Every mutating route on that controller calls `EnsureCanAssign()`, which is
 * `EnsureAdminOrPermission(PermissionNames.CanManageSettings)` — mirror it in the UI with
 * `usePermission('settings.manage')` so we never render a control that is guaranteed to 403.
 * The three read routes used here (`scopes`, `approval-limits`) also require that permission;
 * `capabilities` and `permission-groups` only require an authenticated tenant user.
 */

/** `GET /api/access/capabilities` — the capability vocabulary a group may grant. */
export interface AccessCapability {
  /** Stored value, e.g. `can_view_workorders` or `finance.view`. */
  key: string
  /** Server-humanised label, e.g. "Can View Workorders". */
  name: string
}

/** `GET /api/access/permission-groups` — projection, not the raw entity. */
export interface PermissionGroupSummary {
  id: string
  name: string
  description: string | null
  isActive: boolean
  capabilities: string[]
  userCount: number
}

/** Request body for POST and PUT `/api/access/permission-groups` (`GroupRequest`). */
export interface PermissionGroupPayload {
  name: string
  description: string | null
  isActive: boolean
  capabilities: string[]
}

/**
 * `GET /api/access/scopes` returns raw `AccessScope` rows. `userId` and `permissionGroupId` are
 * both nullable — a row with neither set applies tenant-wide.
 */
export interface AccessScopeRow {
  id: string
  userId: string | null
  permissionGroupId: string | null
  capability: string
  scopeType: string
  scopeEntityId: string | null
  createdAt: string
  updatedAt: string | null
}

/** `GET /api/access/approval-limits` returns raw `PermissionThreshold` rows. */
export interface ApprovalLimitRow {
  id: string
  userId: string | null
  permissionGroupId: string | null
  capability: string
  maximumAmount: number
  currency: string
  createdAt: string
  updatedAt: string | null
}

const capabilitiesKey = ['access', 'capabilities'] as const
const groupsKey = ['access', 'permission-groups'] as const
const scopesKey = ['access', 'scopes'] as const
const limitsKey = ['access', 'approval-limits'] as const

export function useAccessCapabilities(enabled = true) {
  return useQuery({
    queryKey: capabilitiesKey,
    queryFn: () => api.get<AccessCapability[]>(endpoints.access.capabilities),
    // The capability vocabulary is a compile-time constant server-side (`PermissionNames.All`).
    staleTime: 10 * 60_000,
    enabled,
  })
}

export function usePermissionGroups(enabled = true) {
  return useQuery({
    queryKey: groupsKey,
    queryFn: () => api.get<PermissionGroupSummary[]>(endpoints.access.permissionGroups),
    staleTime: 30_000,
    enabled,
  })
}

export function useCreatePermissionGroup() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: PermissionGroupPayload) =>
      api.post<{ id: string }>(endpoints.access.permissionGroups, payload),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: groupsKey })
    },
  })
}

export function useUpdatePermissionGroup(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    // Responds 204 No Content.
    mutationFn: (payload: PermissionGroupPayload) =>
      api.put<void>(endpoints.access.permissionGroup(id), payload),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: groupsKey })
      // Editing a group revokes the sessions of its members server-side, and the signed-in admin
      // may be one of them — refresh the session so the shell reflects the new capabilities.
      void queryClient.invalidateQueries({ queryKey: ['auth', 'me'] })
    },
  })
}

export function useDeletePermissionGroup() {
  const queryClient = useQueryClient()
  return useMutation({
    // Responds 204 No Content.
    mutationFn: (id: string) => api.delete<void>(endpoints.access.permissionGroup(id)),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: groupsKey })
      void queryClient.invalidateQueries({ queryKey: ['auth', 'me'] })
    },
  })
}

export function useAccessScopes(enabled = true) {
  return useQuery({
    queryKey: scopesKey,
    queryFn: () => api.get<AccessScopeRow[]>(endpoints.access.scopes),
    staleTime: 30_000,
    enabled,
  })
}

export function useApprovalLimits(enabled = true) {
  return useQuery({
    queryKey: limitsKey,
    queryFn: () => api.get<ApprovalLimitRow[]>(endpoints.access.approvalLimits),
    staleTime: 30_000,
    enabled,
  })
}

/**
 * Falls back to the raw key when the capability is not in the server vocabulary — a group can
 * still hold a capability that a later backend build removed, and hiding it would misrepresent
 * what the group actually grants.
 */
export function capabilityLabel(key: string, capabilities: AccessCapability[] | undefined): string {
  return capabilities?.find((item) => item.key === key)?.name ?? key
}

// ── Per-user access ────────────────────────────────────────────────────────
//
// Group membership, overrides, scopes and limits are all set per user, and all four PUTs call
// `RevokeAllForUserAsync` server-side: saving any of them signs that user out of every session.
// That is deliberate (stale claims in a live JWT would outlive the change), but it is a real
// consequence and the UI has to say so before the click, not after.

/** One row of `GET /api/access/users/{id}/effective` (`EffectiveCapability`). */
export interface EffectiveCapability {
  capability: string
  allowed: boolean
  /** Why: "Tenant owner" | "Role permission" | "Permission group" | "Direct allow" | "Explicit deny" | "Not granted". */
  source: string
  scopes: AccessScopeRow[]
  maximumAmount: number | null
  currency: string | null
}

/** `OverrideItem` — `effect` is compared case-insensitively against "allow" server-side. */
export interface UserOverride {
  capability: string
  effect: 'allow' | 'deny'
}

/** `ScopeItem`. `scopeType` is free text on the server; see SCOPE_TYPES for the intended vocabulary. */
export interface UserScope {
  capability: string
  scopeType: string
  scopeEntityId: string | null
}

/** `ThresholdItem`. The server upper-cases the currency. */
export interface UserApprovalLimit {
  capability: string
  maximumAmount: number
  currency: string
}

/**
 * The server does not validate `ScopeType` — it stores whatever string it is given, and an
 * unrecognised value simply never matches, silently narrowing access rather than erroring. Offering
 * a fixed list here is what keeps that from happening by typo.
 */
export const SCOPE_TYPES = ['Organization', 'Branch', 'Client', 'Site'] as const

const userAccessKey = (userId: string) => ['access', 'user', userId] as const

export function useUserEffectiveAccess(userId: string | undefined) {
  return useQuery({
    queryKey: userAccessKey(userId ?? ''),
    queryFn: () => api.get<EffectiveCapability[]>(endpoints.access.userEffective(userId ?? '')),
    enabled: Boolean(userId),
    staleTime: 15_000,
  })
}

/**
 * Shared post-save handling for the four per-user PUTs: re-read this user's effective access (the
 * whole point of changing it), the group list (membership counts move), and the signed-in session,
 * since an admin editing their own access is revoked too.
 */
function useUserAccessMutation<TPayload>(
  userId: string,
  send: (payload: TPayload) => Promise<void>,
) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: send,
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: userAccessKey(userId) })
      void queryClient.invalidateQueries({ queryKey: groupsKey })
      void queryClient.invalidateQueries({ queryKey: scopesKey })
      void queryClient.invalidateQueries({ queryKey: limitsKey })
      void queryClient.invalidateQueries({ queryKey: ['auth', 'me'] })
    },
  })
}

/** `GET /api/access/users/{id}/groups` — the ids this user currently belongs to. */
export function useUserGroups(userId: string | undefined) {
  return useQuery({
    queryKey: [...userAccessKey(userId ?? ''), 'groups'] as const,
    queryFn: () => api.get<string[]>(endpoints.access.userGroups(userId ?? '')),
    enabled: Boolean(userId),
    staleTime: 15_000,
  })
}

export function useSetUserGroups(userId: string) {
  return useUserAccessMutation<string[]>(userId, (groupIds) =>
    api.put<void>(endpoints.access.userGroups(userId), { groupIds }))
}

export function useSetUserOverrides(userId: string) {
  return useUserAccessMutation<UserOverride[]>(userId, (overrides) =>
    api.put<void>(endpoints.access.userOverrides(userId), { overrides }))
}

export function useSetUserScopes(userId: string) {
  return useUserAccessMutation<UserScope[]>(userId, (scopes) =>
    api.put<void>(endpoints.access.userScopes(userId), { scopes }))
}

export function useSetUserApprovalLimits(userId: string) {
  return useUserAccessMutation<UserApprovalLimit[]>(userId, (limits) =>
    api.put<void>(endpoints.access.userApprovalLimits(userId), { limits }))
}
