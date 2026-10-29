import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../../shared/api/apiClient'
import { endpoints } from '../../shared/api/endpoints'
import type { PagedResult } from '../../shared/types/pagination'
import type { Asset, Client, ClientSummary, SiteDetail } from './types'

export interface ClientListFilters {
  search?: string
  status?: 'active' | 'inactive' | 'all'
  /** Prospects are left out unless asked for: 'prospects' lists only them, 'all' lists both. */
  kind?: 'clients' | 'prospects' | 'all'
}

export function useClients(filters?: ClientListFilters) {
  return useQuery({
    queryKey: ['clients', filters ?? {}],
    queryFn: () => api.get<Client[]>(endpoints.clients.list, { search: filters?.search, status: filters?.status, kind: filters?.kind }),
    staleTime: 30_000,
  })
}

/** Server-paginated clients for the list table (the bare {@link useClients} stays for pickers). */
export function useClientsPaged(filters: ClientListFilters, page: number, pageSize: number) {
  return useQuery({
    queryKey: ['clients', 'paged', filters, page, pageSize],
    queryFn: () => api.get<PagedResult<Client>>(endpoints.clients.list, { search: filters.search, status: filters.status, kind: filters.kind, page, pageSize }),
    staleTime: 30_000,
    placeholderData: keepPreviousData,
  })
}

export function useClient(id: string | undefined) {
  return useQuery({
    queryKey: ['clients', id],
    queryFn: () => api.get<Client>(endpoints.clients.detail(id ?? '')),
    enabled: Boolean(id),
  })
}

export function useClientSummary(id: string | undefined) {
  return useQuery({
    queryKey: ['clients', id, 'summary'],
    queryFn: () => api.get<ClientSummary>(endpoints.clients.summary(id ?? '')),
    enabled: Boolean(id),
  })
}

export interface UpsertClientPayload {
  clientName: string
  clientType?: string | null
  email?: string | null
  phone?: string | null
  location?: string | null
  contactPerson?: string | null
  contactPhone?: string | null
  notes?: string | null
  isActive?: boolean
}

export function useCreateClient() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: UpsertClientPayload) => api.post<Client>(endpoints.clients.create, payload),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['clients'] }),
  })
}

export function useUpdateClient(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: UpsertClientPayload) => api.put<Client>(endpoints.clients.update(id), payload),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['clients'] })
      void queryClient.invalidateQueries({ queryKey: ['clients', id] })
    },
  })
}

export function useSetClientActive(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (active: boolean) =>
      active ? api.patch<Client>(endpoints.clients.activate(id)) : api.patch<Client>(endpoints.clients.deactivateAction(id)),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['clients'] })
      void queryClient.invalidateQueries({ queryKey: ['clients', id] })
    },
  })
}

/** Toggle activation for an arbitrary client (id supplied per-call), for list-row actions. */
export function useToggleClientActive() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, active }: { id: string; active: boolean }) =>
      active ? api.patch<Client>(endpoints.clients.activate(id)) : api.patch<Client>(endpoints.clients.deactivateAction(id)),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['clients'] }),
  })
}

// Sites
export function useSites(clientId: string | undefined) {
  return useQuery({
    queryKey: ['sites', clientId],
    queryFn: () => api.get<Array<{ id: string; siteName: string; siteCode: string; status: string; townCity: string | null; contactPerson: string | null }>>(endpoints.sites.list(clientId ?? '')),
    enabled: Boolean(clientId),
    staleTime: 30_000,
  })
}

export function useSiteDetail(clientId: string | undefined, siteId: string | undefined) {
  return useQuery({
    queryKey: ['sites', clientId, siteId],
    queryFn: () => api.get<SiteDetail>(endpoints.sites.detail(clientId ?? '', siteId ?? '')),
    enabled: Boolean(clientId) && Boolean(siteId),
  })
}

export interface UpsertSitePayload {
  siteName: string
  siteType?: string | null
  status?: string | null
  streetAddress?: string | null
  townCity?: string | null
  county?: string | null
  country?: string | null
  contactPerson?: string | null
  contactPhone?: string | null
  contactEmail?: string | null
  operatingHours?: string | null
  accessNotes?: string | null
}

export function useCreateSite(clientId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: UpsertSitePayload) => api.post(endpoints.sites.create(clientId), payload),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['sites', clientId] })
      void queryClient.invalidateQueries({ queryKey: ['clients', clientId, 'summary'] })
    },
  })
}

// Assets
export interface AssetListFilters {
  clientId?: string
  siteId?: string
  search?: string
  /** Defaults to active on the server. */
  status?: 'active' | 'inactive' | 'all'
  /** 'out' = in service but with no warranty or contract cover. */
  support?: 'out'
  categoryId?: string
}

export function useAssets(filters?: AssetListFilters) {
  return useQuery({
    queryKey: ['assets', filters ?? {}],
    queryFn: () => api.get<Asset[]>(endpoints.assets.list, { clientId: filters?.clientId, siteId: filters?.siteId, search: filters?.search }),
    staleTime: 30_000,
  })
}

/** Server-paginated assets for the list table (the bare {@link useAssets} stays for pickers). */
export function useAssetsPaged(filters: AssetListFilters, page: number, pageSize: number) {
  return useQuery({
    queryKey: ['assets', 'paged', filters, page, pageSize],
    queryFn: () => api.get<PagedResult<Asset>>(endpoints.assets.list, { clientId: filters.clientId, siteId: filters.siteId, search: filters.search, status: filters.status, support: filters.support, categoryId: filters.categoryId, page, pageSize }),
    staleTime: 30_000,
    placeholderData: keepPreviousData,
  })
}

export function useAsset(id: string | undefined) {
  return useQuery({
    queryKey: ['assets', id],
    queryFn: () => api.get<Asset>(endpoints.assets.detail(id ?? '')),
    enabled: Boolean(id),
  })
}

export interface UpsertAssetPayload {
  clientId: string
  siteId?: string | null
  assetName: string
  assetCode?: string | null
  assetType?: string | null
  location?: string | null
  serialNumber?: string | null
  manufacturer?: string | null
  model?: string | null
  installationDate?: string | null
  warrantyStartDate?: string | null
  warrantyPeriodMonths?: number | null
  warrantyExpiryDate?: string | null
  recommendedPmFrequency?: string | null
  pmFrequencyValue?: number | null
  pmFrequencyUnit?: string | null
  autoSchedulePm?: boolean
  lastPmDate?: string | null
  nextPmDate?: string | null
  notes?: string | null
  // Not edited by the asset form, but sent back on update so saving the form does not clear them.
  branchId?: string | null
  systemId?: string | null
  assetCategoryId?: string | null
  ownershipType?: string | null
  status?: string | null
  customFieldValues?: { fieldDefinitionId: string; value: string }[]
}

export function useCreateAsset() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: UpsertAssetPayload) => api.post<Asset>(endpoints.assets.create, payload),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['assets'] }),
  })
}

export function useUpdateAsset(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: UpsertAssetPayload) => api.put<Asset>(endpoints.assets.update(id), payload),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['assets'] })
      void queryClient.invalidateQueries({ queryKey: ['assets', id] })
    },
  })
}

export interface RetireAssetPayload {
  decommissionDate?: string | null
  reason: string
}

function invalidateAssets(queryClient: ReturnType<typeof useQueryClient>) {
  void queryClient.invalidateQueries({ queryKey: ['assets'] })
  // Retiring stops an asset's auto PM plan.
  void queryClient.invalidateQueries({ queryKey: ['pm-plans'] })
}

export function useRetireAsset(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: RetireAssetPayload) => api.post<Asset>(endpoints.assets.retire(id), payload),
    onSuccess: () => invalidateAssets(queryClient),
  })
}

export function useBulkRetireAssets() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: RetireAssetPayload & { assetIds: string[] }) =>
      api.post<{ retired: number; skipped: number }>(endpoints.assets.bulkRetire, payload),
    onSuccess: () => invalidateAssets(queryClient),
  })
}

export function useReactivateAsset(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () => api.post<Asset>(endpoints.assets.reactivate(id), {}),
    onSuccess: () => invalidateAssets(queryClient),
  })
}

// ── Client Portal Contacts ───────────────────────────────────────────────────

export interface ClientPortalContact {
  id: string
  fullName: string
  email: string
  phone: string | null
  isActive: boolean
  /** Invited by non-admin staff while approval is required; cannot sign in until an admin activates it. */
  isPendingApproval: boolean
  lastLoginAt: string | null
  createdAt: string
  /** Gets the client WhatsApp messages: consent recorded by the office or confirmed from the portal. */
  receivesWhatsApp?: boolean
  whatsAppNumber?: string | null
  whatsAppConsentAt?: string | null
  whatsAppConsentMethod?: string | null
}

export function useClientPortalContacts(clientId: string | undefined) {
  return useQuery({
    queryKey: ['clients', clientId, 'portal-contacts'],
    queryFn: () => api.get<ClientPortalContact[]>(endpoints.clientPortalContacts.list(clientId ?? '')),
    enabled: Boolean(clientId),
  })
}

export interface CreateClientPortalContactPayload {
  fullName: string
  email: string
  phone?: string | undefined
}

export function useCreateClientPortalContact(clientId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: CreateClientPortalContactPayload) => api.post<ClientPortalContact>(endpoints.clientPortalContacts.create(clientId), payload),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['clients', clientId, 'portal-contacts'] }),
  })
}

export function useUpdateClientPortalContactStatus(clientId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) => api.patch<ClientPortalContact>(endpoints.clientPortalContacts.status(clientId, id), { isActive }),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['clients', clientId, 'portal-contacts'] }),
  })
}

export function useResendClientPortalLink(clientId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api.post<ClientPortalContact>(endpoints.clientPortalContacts.resendLink(clientId, id)),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['clients', clientId, 'portal-contacts'] }),
  })
}

export interface PortalAccessLink {
  link: string
  portalLoginUrl: string
}

/**
 * Admin-only: the contact's permanent, full-access portal link (never expires). The same link is
 * returned each time until it is reset; resetting makes the previous link stop working.
 */
export function usePortalAccessLink(clientId: string) {
  return useMutation({
    mutationFn: ({ id, reset }: { id: string; reset: boolean }) =>
      api.post<PortalAccessLink>(reset ? endpoints.clientPortalContacts.resetAccessLink(clientId, id) : endpoints.clientPortalContacts.accessLink(clientId, id), {}),
  })
}

/** Where clients sign in; they enter their email and get a one-time link. */
export const portalLoginUrl = () => `${window.location.origin}/portal/login`

// ── Client Archive/Delete ────────────────────────────────────────────────────

export interface ClientDeletionCheck {
  canDelete: boolean
  blockingReason: string | null
  workOrderCount: number
  invoiceCount: number
  contractCount: number
  assetCount: number
  siteCount: number
}

export function useValidateClientDeletion(id: string) {
  return useQuery({
    queryKey: ['clients', id, 'validate-deletion'],
    queryFn: () => api.post<ClientDeletionCheck>(endpoints.clients.validateDeletion(id)),
    enabled: Boolean(id),
  })
}

export function useDeleteClientPermanent(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () => api.delete(endpoints.clients.deletePermanent(id)),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['clients'] })
      void queryClient.removeQueries({ queryKey: ['clients', id] })
    },
  })
}
