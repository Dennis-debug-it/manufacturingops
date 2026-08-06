import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../../shared/api/apiClient'
import { endpoints } from '../../shared/api/endpoints'
import type { PagedResult } from '../../shared/types/pagination'
import type { Asset, Client, ClientSummary, SiteDetail } from './types'

export interface ClientListFilters {
  search?: string
  status?: 'active' | 'inactive' | 'all'
}

export function useClients(filters?: ClientListFilters) {
  return useQuery({
    queryKey: ['clients', filters ?? {}],
    queryFn: () => api.get<Client[]>(endpoints.clients.list, { search: filters?.search, status: filters?.status }),
    staleTime: 30_000,
  })
}

/** Server-paginated clients for the list table (the bare {@link useClients} stays for pickers). */
export function useClientsPaged(filters: ClientListFilters, page: number, pageSize: number) {
  return useQuery({
    queryKey: ['clients', 'paged', filters, page, pageSize],
    queryFn: () => api.get<PagedResult<Client>>(endpoints.clients.list, { search: filters.search, status: filters.status, page, pageSize }),
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
    queryFn: () => api.get<PagedResult<Asset>>(endpoints.assets.list, { clientId: filters.clientId, siteId: filters.siteId, search: filters.search, page, pageSize }),
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
  warrantyExpiryDate?: string | null
  notes?: string | null
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

// ── Client Portal Contacts ───────────────────────────────────────────────────

export interface ClientPortalContact {
  id: string
  fullName: string
  email: string
  phone: string | null
  isActive: boolean
  lastLoginAt: string | null
  createdAt: string
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
