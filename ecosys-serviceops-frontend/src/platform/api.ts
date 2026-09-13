import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../shared/api/apiClient'
import { endpoints } from '../shared/api/endpoints'
import type { PagedResult } from '../shared/types/pagination'
import type {
  PlatformAuditLog,
  PlatformDashboardSummary,
  PlatformExpense,
  PlatformFinanceDashboard,
  PlatformInvoice,
  PlatformLicensePlan,
  PlatformLeadDetail,
  PlatformLeadListItem,
  PlatformQuotation,
  PlatformSummary,
  PlatformSubscription,
  PlatformTenantAuditLog,
  PlatformTenantDetail,
  PlatformTenantListItem,
  PlatformUser,
  PlatformReportsAuditRow,
  PlatformReportsRevenueRow,
  PlatformReportsSubscriptionRow,
  PlatformReportsSummary,
  PlatformReportsTenantRow,
  PlatformPayment,
  TenantCommunicationSettings,
} from './types'

export function usePlatformSummary() {
  return useQuery({
    queryKey: ['platform', 'summary'],
    queryFn: () => api.get<PlatformSummary>(endpoints.platform.summary),
    staleTime: 30_000,
    refetchInterval: 30_000,
  })
}

export function usePlatformDashboardSummary() {
  return useQuery({
    queryKey: ['platform', 'dashboard-summary'],
    queryFn: () => api.get<PlatformDashboardSummary>(endpoints.platform.dashboardSummary),
    staleTime: 30_000,
    refetchInterval: 30_000,
  })
}

export interface TenantListFilters { search?: string | undefined; status?: string | undefined; trial?: string | undefined; sortBy?: string | undefined; sortDir?: string | undefined }

/** Server-paginated tenants for the list table (the bare {@link useTenants} stays for dropdowns). */
export function useTenantsPaged(filters: TenantListFilters, page: number, pageSize: number, options?: { refetchInterval?: number }) {
  return useQuery({
    queryKey: ['platform', 'tenants', 'paged', filters, page, pageSize],
    queryFn: () => api.get<PagedResult<PlatformTenantListItem>>(endpoints.platform.tenants, {
      search: filters.search, status: filters.status, trial: filters.trial, sortBy: filters.sortBy, sortDir: filters.sortDir, page, pageSize,
    }),
    staleTime: 30_000,
    placeholderData: keepPreviousData,
    ...(options?.refetchInterval ? { refetchInterval: options.refetchInterval } : {}),
  })
}

/** `refetchInterval` lets operational pages opt into polling; dropdown consumers omit it. */
export function useTenants(options?: { refetchInterval?: number }) {
  return useQuery({
    queryKey: ['platform', 'tenants'],
    queryFn: () => api.get<PlatformTenantListItem[]>(endpoints.platform.tenants),
    staleTime: 30_000,
    ...(options?.refetchInterval ? { refetchInterval: options.refetchInterval } : {}),
  })
}

export function useTenantDetail(id: string | undefined) {
  return useQuery({
    queryKey: ['platform', 'tenants', id],
    queryFn: () => api.get<PlatformTenantDetail>(endpoints.platform.tenant(id ?? '')),
    enabled: Boolean(id),
  })
}

export function useTenantAuditLogs(id: string | undefined) {
  return useQuery({
    queryKey: ['platform', 'tenants', id, 'audit-logs'],
    queryFn: () => api.get<PlatformTenantAuditLog[]>(endpoints.platform.tenantAuditLogs(id ?? '')),
    enabled: Boolean(id),
  })
}

export interface PlatformTenantBranding {
  companyName: string
  logoUrl: string | null
  primaryColor: string
  secondaryColor: string
  showPoweredByEcosys: boolean
}

export function useTenantBranding(id: string | undefined) {
  return useQuery({
    queryKey: ['platform', 'tenants', id, 'branding'],
    queryFn: () => api.get<PlatformTenantBranding>(endpoints.platform.tenantBranding(id ?? '')),
    enabled: Boolean(id),
  })
}

export interface PlatformTenantUser {
  id: string
  fullName: string
  email: string
  role: string
  isActive: boolean
  jobTitle: string | null
  createdAt: string
  lockedOutUntil: string | null
  mustChangePassword: boolean
  lastCredentialSentAt: string | null
  /** Null means the account has never been signed into — its setup link is still pending. */
  lastLoginAt: string | null
}

export interface PlatformTenantCredentialDelivery {
  userId: string
  email: string
  success: boolean
  status: string
  message: string
  lastCredentialSentAt: string | null
}

export function useTenantUsers(id: string | undefined) {
  return useQuery({
    queryKey: ['platform', 'tenants', id, 'users'],
    queryFn: () => api.get<PlatformTenantUser[]>(endpoints.platform.tenantUsers(id ?? '')),
    enabled: Boolean(id),
  })
}

export interface CreateWorkspaceUserPayload {
  fullName: string
  email: string
  phone: string | null
  role: string
  jobTitle: string | null
}

export interface CreatedWorkspaceUser {
  user: PlatformTenantUser
  invitationSent: boolean
  invitationStatus: string
  message: string
}

/**
 * Creates an extra user inside a tenant workspace — most often a second tenant admin, since tenant
 * creation only ever seeds one. Tenant roles only; platform staff are created under Platform → Users.
 */
export function useCreateTenantWorkspaceUser(tenantId: string | undefined) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: CreateWorkspaceUserPayload) =>
      api.post<CreatedWorkspaceUser>(endpoints.platform.tenantUsers(tenantId ?? ''), payload),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['platform', 'tenants', tenantId, 'users'] })
      void queryClient.invalidateQueries({ queryKey: ['platform', 'tenants', tenantId] })
    },
  })
}

/**
 * Re-sends a workspace account-setup link. This is the recovery path for a tenant admin whose
 * first-login invitation lapsed — they cannot request a new one without being able to sign in.
 */
export function useResendTenantUserInvitation(tenantId: string | undefined) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (userId: string) =>
      api.post<PlatformTenantCredentialDelivery>(endpoints.platform.tenantUserResendInvitation(tenantId ?? '', userId), {}),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['platform', 'tenants', tenantId, 'users'] })
    },
  })
}

/**
 * Forces a password reset for a workspace user who has already completed account setup. This is
 * the recovery path for a locked-out or forgetful tenant admin when the tenant has no other admin
 * available to trigger the tenant-side reset — resend-invitation only covers a user still pending
 * their first sign-in.
 */
export function useResetTenantUserPassword(tenantId: string | undefined) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (userId: string) =>
      api.post<PlatformTenantCredentialDelivery>(endpoints.platform.tenantUserResetPassword(tenantId ?? '', userId), {}),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['platform', 'tenants', tenantId, 'users'] })
    },
  })
}

export interface PlatformTenantNumbering {
  id: string
  documentType: string
  prefix: string
  suffix: string | null
  nextNumber: number
  paddingLength: number
  resetFrequency: string
  isActive: boolean
  lastResetAt: string | null
}

export function useTenantNumbering(id: string | undefined) {
  return useQuery({
    queryKey: ['platform', 'tenants', id, 'numbering'],
    queryFn: () => api.get<PlatformTenantNumbering[]>(endpoints.platform.tenantNumbering(id ?? '')),
    enabled: Boolean(id),
  })
}

export interface PlatformTenantDocumentDesign {
  documentType: string
  isCustomised: boolean
}

export function useTenantDocumentDesigns(id: string | undefined) {
  return useQuery({
    queryKey: ['platform', 'tenants', id, 'document-designs'],
    queryFn: () => api.get<PlatformTenantDocumentDesign[]>(endpoints.platform.tenantDocumentDesigns(id ?? '')),
    enabled: Boolean(id),
  })
}

export interface PlatformTenantFinanceSummary {
  invoiceCount: number
  totalInvoiced: number
  totalPaid: number
  outstandingBalance: number
  overdueCount: number
}

export function useTenantFinanceSummary(id: string | undefined) {
  return useQuery({
    queryKey: ['platform', 'tenants', id, 'finance-summary'],
    queryFn: () => api.get<PlatformTenantFinanceSummary>(endpoints.platform.tenantFinanceSummary(id ?? '')),
    enabled: Boolean(id),
  })
}

export interface PlatformTenantModuleSetting {
  moduleKey: string
  updatedAt: string
}

export function useTenantModuleSettings(id: string | undefined) {
  return useQuery({
    queryKey: ['platform', 'tenants', id, 'module-settings'],
    queryFn: () => api.get<PlatformTenantModuleSetting[]>(endpoints.platform.tenantModuleSettings(id ?? '')),
    enabled: Boolean(id),
  })
}

export interface CreateTenantPayload {
  planCode?: 'operations' | 'finance' | 'suite'
  timeZoneId?: string
  companyName: string
  slug?: string
  companyEmail?: string
  companyPhone?: string
  industry?: string
  contactName?: string
  contactEmail?: string
  contactPhone?: string
  primaryContactName?: string
  primaryContactEmail?: string
  primaryContactPhone?: string
  country?: string
  county?: string
  city?: string
  address?: string
  taxPin?: string
  planName?: string
  maxUsers?: number | null
  maxBranches?: number | null
  trialEndsAt?: string | null
  subscriptionEndsAt?: string | null
  startsAt?: string | null
  expiresAt?: string | null
  status?: string
  licenseStatus?: string
  createDefaultAdmin?: boolean
  usePrimaryContactAsWorkspaceAdmin?: boolean
  adminFullName?: string
  adminEmail?: string
  adminPhone?: string
}

export function useCreateTenant() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: CreateTenantPayload) => api.post<PlatformTenantDetail>(endpoints.platform.tenants, payload),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['platform', 'tenants'] }),
  })
}

function useTenantAction(id: string, url: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (reason?: string) => api.post<PlatformTenantDetail>(url, reason ? { reason } : undefined),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['platform', 'tenants'] })
      void queryClient.invalidateQueries({ queryKey: ['platform', 'tenants', id] })
      void queryClient.invalidateQueries({ queryKey: ['platform', 'dashboard-summary'] })
    },
  })
}

export const useActivateTenant = (id: string) => useTenantAction(id, endpoints.platform.tenantActivate(id))
export const useSuspendTenant = (id: string) => useTenantAction(id, endpoints.platform.tenantSuspend(id))
export const useDeactivateTenant = (id: string) => useTenantAction(id, endpoints.platform.tenantDeactivate(id))
export const useExtendTenantTrial = (id: string) => useTenantAction(id, endpoints.platform.tenantExtendTrial(id))

export function useDeleteTenant() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api.delete<void>(endpoints.platform.tenant(id)),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['platform', 'tenants'] }),
  })
}

export function useImpersonateTenant(id: string) {
  return useMutation({
    mutationFn: () => api.post(endpoints.platform.tenantImpersonate(id)),
  })
}

export function useTenantCommunicationSettings(id: string | undefined) {
  return useQuery({
    queryKey: ['platform', 'tenants', id, 'communication'],
    queryFn: () => api.get<TenantCommunicationSettings>(`${endpoints.platform.tenant(id ?? '')}/communication-settings`),
    enabled: Boolean(id),
  })
}

export function useUpdateTenantCommunicationSettings(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: TenantCommunicationSettings) =>
      api.put<TenantCommunicationSettings>(`${endpoints.platform.tenant(id)}/communication-settings`, payload),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['platform', 'tenants', id, 'communication'] })
      void queryClient.invalidateQueries({ queryKey: ['platform', 'tenants', id] })
    },
  })
}

/** `refetchInterval` lets operational pages opt into polling; dropdown consumers omit it. */
export function useLeads(options?: { refetchInterval?: number }) {
  return useQuery({
    queryKey: ['platform', 'leads'],
    queryFn: () => api.get<PlatformLeadListItem[]>(endpoints.platform.leads),
    staleTime: 30_000,
    ...(options?.refetchInterval ? { refetchInterval: options.refetchInterval } : {}),
  })
}

export interface LeadListFilters { search?: string | undefined; status?: string | undefined }

/** Server-paginated leads for the list table (the bare {@link useLeads} stays for other callers). */
export function useLeadsPaged(filters: LeadListFilters, page: number, pageSize: number) {
  return useQuery({
    queryKey: ['platform', 'leads', 'paged', filters, page, pageSize],
    queryFn: () => api.get<PagedResult<PlatformLeadListItem>>(endpoints.platform.leads, { search: filters.search, status: filters.status, page, pageSize }),
    staleTime: 30_000,
    placeholderData: keepPreviousData,
  })
}

export function useLead(id: string | undefined) {
  return useQuery({
    queryKey: ['platform', 'leads', id],
    queryFn: () => api.get<PlatformLeadDetail>(endpoints.platform.lead(id ?? '')),
    enabled: Boolean(id),
  })
}

export function useUpdateLeadStatus(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (status: string) => api.put<PlatformLeadDetail>(endpoints.platform.leadStatus(id), { status }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['platform', 'leads'] })
      void queryClient.invalidateQueries({ queryKey: ['platform', 'leads', id] })
    },
  })
}

export function useUpdateLeadNotes(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (notes: string) => api.put<PlatformLeadDetail>(endpoints.platform.leadNotes(id), { notes }),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['platform', 'leads', id] }),
  })
}

export function usePlatformUsers() {
  return useQuery({
    queryKey: ['platform', 'users'],
    queryFn: () => api.get<PlatformUser[]>(endpoints.platformUsers.list),
    staleTime: 30_000,
  })
}

export interface PlatformUserListFilters { search?: string | undefined; role?: string | undefined; status?: string | undefined }

/** Server-paginated platform users for the list table. */
export function usePlatformUsersPaged(filters: PlatformUserListFilters, page: number, pageSize: number) {
  return useQuery({
    queryKey: ['platform', 'users', 'paged', filters, page, pageSize],
    queryFn: () => api.get<PagedResult<PlatformUser>>(endpoints.platformUsers.list, { search: filters.search, role: filters.role, status: filters.status, page, pageSize }),
    staleTime: 30_000,
    placeholderData: keepPreviousData,
  })
}

export interface UpsertPlatformUserPayload {
  fullName: string
  email: string
  phone?: string | null
  role: string
  status?: string | null
  password?: string | null
}

export function useCreatePlatformUser() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: UpsertPlatformUserPayload) => api.post<PlatformUser>(endpoints.platformUsers.list, payload),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['platform', 'users'] }),
  })
}

export function useUpdatePlatformUser(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: UpsertPlatformUserPayload) => api.put<PlatformUser>(endpoints.platformUsers.detail(id), payload),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['platform', 'users'] }),
  })
}

export function useUpdatePlatformUserStatus(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (status: 'Active' | 'Inactive') => api.post<PlatformUser>(status === 'Active' ? endpoints.platformUsers.activate(id) : endpoints.platformUsers.deactivate(id)),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['platform', 'users'] }),
  })
}

export function useResetPlatformUserPassword(id: string) {
  return useMutation({
    mutationFn: (temporaryPassword?: string) => api.post(endpoints.platformUsers.resetPassword(id), temporaryPassword ? { temporaryPassword } : undefined),
  })
}

export function useResendPlatformUserCredentials(id: string) {
  return useMutation({
    mutationFn: () => api.post(endpoints.platformUsers.resendCredentials(id)),
  })
}

export function useAssignPlatformUserRole(id: string) {
  return useMutation({
    mutationFn: (role: string) => api.post<PlatformUser>(endpoints.platformUsers.assignRole(id), { role }),
  })
}

export function useLicensingUsage() {
  return useQuery({
    queryKey: ['platform', 'licensing', 'usage'],
    queryFn: () => api.get<LicenseUsageSnapshot[]>(endpoints.platformLicensing.usage),
    staleTime: 30_000,
    refetchInterval: 30_000,
  })
}

export interface LicenseUsageSnapshot {
  id?: string
  tenantId: string
  tenantName: string
  planName: string | null
  planCode: string
  modules: string[]
  startsAt: string
  billingCycle: string
  status: string
  licenseStatus: string
  maxUsers: number | null
  maxBranches: number | null
  maxAssets: number | null
  monthlyWorkOrders: number
  monthlyWorkOrdersLimit: number | null
  trialEndsAt: string | null
  expiresAt: string | null
  daysRemaining: number | null
}

export function useLicensePlans() {
  return useQuery({
    queryKey: ['platform', 'licensing', 'plans'],
    queryFn: () => api.get<PlatformLicensePlan[]>(endpoints.platformLicensing.plans),
    staleTime: 30_000,
  })
}

export function useSubscriptions() {
  return useQuery({
    queryKey: ['platform', 'licensing', 'subscriptions'],
    queryFn: () => api.get<PlatformSubscription[]>(endpoints.platformLicensing.subscriptions),
    staleTime: 30_000,
    refetchInterval: 30_000,
  })
}

export function useFinanceDashboard() {
  return useQuery({
    queryKey: ['platform', 'finance', 'dashboard'],
    queryFn: () => api.get<PlatformFinanceDashboard>(endpoints.platformFinance.dashboard),
    staleTime: 30_000,
    refetchInterval: 30_000,
  })
}

export function useFinanceQuotations() {
  return useQuery({
    queryKey: ['platform', 'finance', 'quotations'],
    queryFn: () => api.get<PlatformQuotation[]>(endpoints.platformFinance.quotations),
    staleTime: 30_000,
  })
}

export function useFinanceInvoices() {
  return useQuery({
    queryKey: ['platform', 'finance', 'invoices'],
    queryFn: () => api.get<PlatformInvoice[]>(endpoints.platformFinance.invoices),
    staleTime: 30_000,
  })
}

export function useFinancePayments() {
  return useQuery({
    queryKey: ['platform', 'finance', 'payments'],
    queryFn: () => api.get<PlatformPayment[]>(endpoints.platformFinance.payments),
    staleTime: 30_000,
  })
}

export function useFinanceExpenses() {
  return useQuery({
    queryKey: ['platform', 'finance', 'expenses'],
    queryFn: () => api.get<PlatformExpense[]>(endpoints.platformFinance.expenses),
    staleTime: 30_000,
  })
}

export function usePlatformReportsSummary() {
  return useQuery({
    queryKey: ['platform', 'reports', 'summary'],
    queryFn: () => api.get<PlatformReportsSummary>(endpoints.platformReports.summary),
    staleTime: 30_000,
  })
}

export function usePlatformReportsTenants() {
  return useQuery({
    queryKey: ['platform', 'reports', 'tenants'],
    queryFn: () => api.get<PlatformReportsTenantRow[]>(endpoints.platformReports.tenants),
    staleTime: 30_000,
  })
}

export function usePlatformReportsRevenue() {
  return useQuery({
    queryKey: ['platform', 'reports', 'revenue'],
    queryFn: () => api.get<PlatformReportsRevenueRow[]>(endpoints.platformReports.revenue),
    staleTime: 30_000,
  })
}

export function usePlatformReportsSubscriptions() {
  return useQuery({
    queryKey: ['platform', 'reports', 'subscriptions'],
    queryFn: () => api.get<PlatformReportsSubscriptionRow[]>(endpoints.platformReports.subscriptions),
    staleTime: 30_000,
  })
}

export function usePlatformReportsAudit() {
  return useQuery({
    queryKey: ['platform', 'reports', 'audit'],
    queryFn: () => api.get<PlatformReportsAuditRow[]>(endpoints.platformReports.audit),
    staleTime: 30_000,
  })
}

export function usePlatformAuditLogs(params?: {
  from?: string
  to?: string
  actorUserId?: string
  tenantId?: string
  action?: string
  entityType?: string
  severity?: string
}) {
  return useQuery({
    queryKey: ['platform', 'audit-logs', params ?? {}],
    queryFn: () => api.get<PlatformAuditLog[]>(endpoints.platformAuditLogs.list, params as Record<string, string | number | boolean | null | undefined> | undefined),
    staleTime: 30_000,
  })
}
