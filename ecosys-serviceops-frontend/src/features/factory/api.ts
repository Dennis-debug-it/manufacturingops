import { keepPreviousData, useMutation, useQuery, useQueryClient, type QueryClient } from '@tanstack/react-query'
import { api, apiClient } from '../../shared/api/apiClient'
import type { PagedResult } from '../../shared/types/pagination'
import type {
  AssetClass,
  AssetComponent,
  AssetDocument,
  AssetTag,
  FactoryAsset,
  FactorySettings,
  ImportPreview,
  IndustryTemplate,
  LifecycleEvent,
  Meter,
  MeterReading,
  MeterType,
  OfflineScanEntry,
  OrgNode,
  ScanResult,
  UpdateFactorySettingsPayload,
  UpsertFactoryAssetPayload,
  UpsertMeterPayload,
  UpsertOrgNodePayload,
} from './types'

/* ── Organisation ─────────────────────────────────────────────────────────── */

export function useOrgNodes(includeInactive = false) {
  return useQuery({
    queryKey: ['factory', 'org-nodes', includeInactive],
    queryFn: () => api.get<OrgNode[]>('/api/org-nodes', { includeInactive }),
    staleTime: 60_000,
  })
}

export function useIndustryTemplates() {
  return useQuery({
    queryKey: ['factory', 'industry-templates'],
    queryFn: () => api.get<IndustryTemplate[]>('/api/org-nodes/templates'),
    staleTime: Infinity,
  })
}

const invalidateOrg = (queryClient: QueryClient) => queryClient.invalidateQueries({ queryKey: ['factory', 'org-nodes'] })

export function useSaveOrgNode() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, payload }: { id?: string | undefined; payload: UpsertOrgNodePayload }) =>
      id ? api.put<OrgNode>(`/api/org-nodes/${id}`, payload) : api.post<OrgNode>('/api/org-nodes', payload),
    onSuccess: () => void invalidateOrg(queryClient),
  })
}

export function useSetOrgNodeActive() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, active }: { id: string; active: boolean }) => api.post<OrgNode>(`/api/org-nodes/${id}/${active ? 'reactivate' : 'deactivate'}`),
    onSuccess: () => void invalidateOrg(queryClient),
  })
}

export function useApplySectionTemplate() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ factoryId, templateKey }: { factoryId: string; templateKey?: string | undefined }) =>
      api.post<{ created: number; skipped: number }>(`/api/org-nodes/${factoryId}/apply-template`, { templateKey: templateKey ?? null }),
    onSuccess: () => void invalidateOrg(queryClient),
  })
}

export function useFactorySettings() {
  return useQuery({
    queryKey: ['factory', 'settings'],
    queryFn: () => api.get<FactorySettings>('/api/factory-settings'),
    staleTime: 5 * 60_000,
  })
}

export function useUpdateFactorySettings() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: UpdateFactorySettingsPayload) => api.put<FactorySettings>('/api/factory-settings', payload),
    onSuccess: (data) => queryClient.setQueryData(['factory', 'settings'], data),
  })
}

export function useUserOrgScope(userId: string | undefined) {
  return useQuery({
    queryKey: ['factory', 'user-scope', userId],
    queryFn: () => api.get<{ userId: string; orgNodeIds: string[] }>(`/api/users/${userId}/org-scope`),
    enabled: Boolean(userId),
  })
}

export function useSetUserOrgScope(userId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (orgNodeIds: string[]) => api.put<{ userId: string; orgNodeIds: string[] }>(`/api/users/${userId}/org-scope`, { orgNodeIds }),
    onSuccess: (data) => queryClient.setQueryData(['factory', 'user-scope', userId], data),
  })
}

/* ── Assets ───────────────────────────────────────────────────────────────── */

export interface FactoryAssetFilters {
  search?: string | undefined
  status?: 'active' | 'inactive' | 'all' | undefined
  orgNodeId?: string | undefined
  criticality?: string | undefined
  categoryId?: string | undefined
  topLevelOnly?: boolean | undefined
}

export function useFactoryAssets(filters: FactoryAssetFilters, page: number, pageSize: number) {
  return useQuery({
    queryKey: ['factory', 'assets', filters, page, pageSize],
    queryFn: () => api.get<PagedResult<FactoryAsset>>('/api/assets', { ...filters, page, pageSize }),
    placeholderData: keepPreviousData,
    staleTime: 30_000,
  })
}

/** Small unpaged search for pickers (parent asset). */
export function useAssetSearch(search: string, enabled: boolean) {
  return useQuery({
    queryKey: ['factory', 'asset-search', search],
    queryFn: () => api.get<PagedResult<FactoryAsset>>('/api/assets', { search, page: 1, pageSize: 20, status: 'active' }),
    enabled,
    staleTime: 30_000,
  })
}

export function useFactoryAsset(id: string | undefined) {
  return useQuery({
    queryKey: ['factory', 'asset', id],
    queryFn: () => api.get<FactoryAsset>(`/api/assets/${id}`),
    enabled: Boolean(id),
  })
}

const invalidateAssets = (queryClient: QueryClient, id?: string) => {
  void queryClient.invalidateQueries({ queryKey: ['factory', 'assets'] })
  void queryClient.invalidateQueries({ queryKey: ['factory', 'org-nodes'] })
  void queryClient.invalidateQueries({ queryKey: ['factory', 'scan-index'] })
  if (id) void queryClient.invalidateQueries({ queryKey: ['factory', 'asset', id] })
}

export function useSaveFactoryAsset() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, payload }: { id?: string | undefined; payload: UpsertFactoryAssetPayload }) =>
      id ? api.put<FactoryAsset>(`/api/assets/${id}`, payload) : api.post<FactoryAsset>('/api/assets', payload),
    onSuccess: (asset) => invalidateAssets(queryClient, asset.id),
  })
}

export function useRetireAsset(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: { decommissionDate: string | null; reason: string }) => api.post<FactoryAsset>(`/api/assets/${id}/retire`, payload),
    onSuccess: () => invalidateAssets(queryClient, id),
  })
}

export function useReactivateAsset(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () => api.post<FactoryAsset>(`/api/assets/${id}/reactivate`),
    onSuccess: () => invalidateAssets(queryClient, id),
  })
}

export function useAssetClasses() {
  return useQuery({
    queryKey: ['factory', 'asset-classes'],
    queryFn: () => api.get<AssetClass[]>('/api/asset-templates'),
    staleTime: 5 * 60_000,
  })
}

/* ── Lifecycle ────────────────────────────────────────────────────────────── */

export function useLifecycle(assetId: string) {
  return useQuery({
    queryKey: ['factory', 'asset', assetId, 'lifecycle'],
    queryFn: () => api.get<LifecycleEvent[]>(`/api/assets/${assetId}/lifecycle`),
  })
}

export function useComponents(assetId: string) {
  return useQuery({
    queryKey: ['factory', 'asset', assetId, 'components'],
    queryFn: () => api.get<AssetComponent[]>(`/api/assets/${assetId}/components`),
  })
}

export function useLifecycleAction(assetId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ action, payload }: { action: 'transfer' | 'commission' | 'dispose'; payload: unknown }) =>
      api.post<unknown>(`/api/assets/${assetId}/${action}`, payload),
    onSuccess: () => {
      invalidateAssets(queryClient, assetId)
      void queryClient.invalidateQueries({ queryKey: ['factory', 'asset', assetId] })
    },
  })
}

/* ── Meters ───────────────────────────────────────────────────────────────── */

export function useMeterTypes() {
  return useQuery({ queryKey: ['factory', 'meter-types'], queryFn: () => api.get<MeterType[]>('/api/meter-types'), staleTime: Infinity })
}

export function useMeters(assetId: string) {
  return useQuery({
    queryKey: ['factory', 'asset', assetId, 'meters'],
    queryFn: () => api.get<Meter[]>(`/api/assets/${assetId}/meters`),
  })
}

export function useSaveMeter(assetId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, payload }: { id?: string | undefined; payload: UpsertMeterPayload }) =>
      id ? api.put<Meter>(`/api/meters/${id}`, payload) : api.post<Meter>(`/api/assets/${assetId}/meters`, payload),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['factory', 'asset', assetId, 'meters'] }),
  })
}

export function useReadings(meterId: string | undefined, limit = 200) {
  return useQuery({
    queryKey: ['factory', 'readings', meterId, limit],
    queryFn: () => api.get<MeterReading[]>(`/api/meters/${meterId}/readings`, { limit }),
    enabled: Boolean(meterId),
  })
}

export function useRecordReading(assetId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ meterId, value, readAt, isReset, notes }: { meterId: string; value: number; readAt?: string | null; isReset?: boolean; notes?: string | null }) =>
      api.post<MeterReading>(`/api/meters/${meterId}/readings`, { value, readAt: readAt ?? null, isReset: isReset ?? false, notes: notes ?? null }),
    onSuccess: (reading) => {
      void queryClient.invalidateQueries({ queryKey: ['factory', 'asset', assetId, 'meters'] })
      void queryClient.invalidateQueries({ queryKey: ['factory', 'readings', reading.meterId] })
      void queryClient.invalidateQueries({ queryKey: ['factory', 'scan-index'] })
    },
  })
}

/* ── Tags and scanning ────────────────────────────────────────────────────── */

export function useAssetTags(assetId: string) {
  return useQuery({
    queryKey: ['factory', 'asset', assetId, 'tags'],
    queryFn: () => api.get<AssetTag[]>(`/api/assets/${assetId}/tags`),
  })
}

export function useTagAction(assetId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (request:
      | { action: 'issue'; tagNumber?: string | null; symbology?: string | null }
      | { action: 'replace'; tagId: string; reason: string }
      | { action: 'retire'; tagId: string; reason: string | null }) => {
      switch (request.action) {
        case 'issue': return api.post<AssetTag>(`/api/assets/${assetId}/tags`, { tagNumber: request.tagNumber ?? null, symbology: request.symbology ?? null })
        case 'replace': return api.post<AssetTag>(`/api/asset-tags/${request.tagId}/replace`, { reason: request.reason })
        case 'retire': return api.post<AssetTag>(`/api/asset-tags/${request.tagId}/retire`, { reason: request.reason })
      }
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['factory', 'asset', assetId, 'tags'] })
      void queryClient.invalidateQueries({ queryKey: ['factory', 'asset', assetId, 'lifecycle'] })
      void queryClient.invalidateQueries({ queryKey: ['factory', 'scan-index'] })
    },
  })
}

export function useBulkIssueTags() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: { assetIds?: string[] | null | undefined; orgNodeId?: string | null | undefined }) =>
      api.post<{ issued: number; skipped: number; tagIds: string[] }>('/api/asset-tags/bulk-issue', { assetIds: payload.assetIds ?? null, orgNodeId: payload.orgNodeId ?? null }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['factory'] })
    },
  })
}

/** Downloads a PDF sheet of QR labels. */
export async function downloadLabels(payload: { tagIds?: string[] | undefined; assetIds?: string[] | undefined; orgNodeId?: string | null | undefined; layout: string }) {
  const response = await apiClient.post('/api/asset-tags/labels', {
    tagIds: payload.tagIds ?? null,
    assetIds: payload.assetIds ?? null,
    orgNodeId: payload.orgNodeId ?? null,
    layout: payload.layout,
  }, { responseType: 'blob' })
  saveBlob(response.data as Blob, `asset-labels-${new Date().toISOString().slice(0, 10)}.pdf`)
}

export function useScan(code: string | undefined) {
  return useQuery({
    queryKey: ['factory', 'scan', code],
    queryFn: () => api.get<ScanResult>(`/api/scan/${encodeURIComponent(code ?? '')}`),
    enabled: Boolean(code),
    retry: false,
  })
}

/** Every asset the user can see, kept on the phone so scanning works with no signal. */
export function useOfflineScanIndex() {
  return useQuery({
    queryKey: ['factory', 'scan-index'],
    queryFn: () => api.get<OfflineScanEntry[]>('/api/scan/offline-index'),
    staleTime: 10 * 60_000,
  })
}

/* ── Documents ────────────────────────────────────────────────────────────── */

export function useAssetDocuments(assetId: string, includeArchived: boolean) {
  return useQuery({
    queryKey: ['factory', 'asset', assetId, 'documents', includeArchived],
    queryFn: () => api.get<AssetDocument[]>(`/api/assets/${assetId}/documents`, { includeArchived }),
  })
}

export function useUploadDocument(assetId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ documentId, form }: { documentId?: string | undefined; form: FormData }) =>
      documentId
        ? api.post<AssetDocument>(`/api/asset-documents/${documentId}/versions`, form)
        : api.post<AssetDocument>(`/api/assets/${assetId}/documents`, form),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['factory', 'asset', assetId, 'documents'] }),
  })
}

export function useArchiveDocument(assetId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, archived }: { id: string; archived: boolean }) => api.post<AssetDocument>(`/api/asset-documents/${id}/${archived ? 'archive' : 'restore'}`),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['factory', 'asset', assetId, 'documents'] }),
  })
}

export async function downloadDocument(url: string, fileName: string) {
  const response = await apiClient.get(url, { responseType: 'blob' })
  saveBlob(response.data as Blob, fileName)
}

/* ── Import ───────────────────────────────────────────────────────────────── */

export async function downloadImportTemplate() {
  const response = await apiClient.get('/api/import/factory-assets/template', { responseType: 'blob' })
  saveBlob(response.data as Blob, 'factory-assets-import.xlsx')
}

export type ImportResult = ImportPreview | { created: number; tagged: number }

export function useImportAssets() {
  const queryClient = useQueryClient()
  return useMutation<ImportResult, unknown, { file: File; commit: boolean }>({
    mutationFn: ({ file, commit }: { file: File; commit: boolean }) => {
      const form = new FormData()
      form.append('file', file)
      return commit
        ? api.post<ImportResult>('/api/import/factory-assets/commit', form)
        : api.post<ImportResult>('/api/import/factory-assets/preview', form)
    },
    onSuccess: (_, variables) => {
      if (variables.commit) void queryClient.invalidateQueries({ queryKey: ['factory'] })
    },
  })
}

function saveBlob(blob: Blob, fileName: string) {
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = fileName
  link.click()
  URL.revokeObjectURL(url)
}
