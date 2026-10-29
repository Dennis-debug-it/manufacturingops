import { apiClient } from '../api/apiClient'
import { endpoints } from '../api/endpoints'
import type { ImportCommitResponse, ImportPreviewResponse } from '../types/import'

function buildFormData(file: File) {
  const formData = new FormData()
  formData.append('file', file)
  return formData
}

function triggerDownload(blob: Blob, fileName: string) {
  const url = window.URL.createObjectURL(blob)
  const anchor = document.createElement('a')
  anchor.href = url
  anchor.download = fileName
  anchor.style.display = 'none'
  document.body.append(anchor)
  anchor.click()
  anchor.remove()
  window.URL.revokeObjectURL(url)
}

export const importService = {
  async downloadDealTemplate() {
    const response = await apiClient.get(endpoints.crm.dealImportTemplate, { responseType: 'blob' })
    triggerDownload(response.data as Blob, 'deals_import_template.csv')
  },
  previewDeals(file: File) {
    return apiClient.post<ImportPreviewResponse>(endpoints.crm.previewDealImport, buildFormData(file)).then((response) => response.data)
  },
  commitDeals(file: File) {
    return apiClient.post<ImportCommitResponse>(endpoints.crm.commitDealImport, buildFormData(file)).then((response) => response.data)
  },
  /** Deals (optionally one stage or owner) or contacts, as the person may see them. */
  async exportSales(kind: 'deals' | 'contacts', params?: { stage?: string | undefined; ownerUserId?: string | undefined }) {
    const response = await apiClient.get(kind === 'deals' ? endpoints.crm.exportDeals : endpoints.crm.exportContacts, { params, responseType: 'blob' })
    triggerDownload(response.data as Blob, `${kind}.csv`)
  },
  async downloadMaterialTemplate() {
    const response = await apiClient.get(endpoints.imports.materialTemplate, {
      responseType: 'blob',
    })
    triggerDownload(response.data as Blob, 'materials_import_template.csv')
  },
  previewMaterials(file: File) {
    return apiClient.post<ImportPreviewResponse>(endpoints.imports.previewMaterials, buildFormData(file)).then((response) => response.data)
  },
  commitMaterials(file: File) {
    return apiClient.post<ImportCommitResponse>(endpoints.imports.commitMaterials, buildFormData(file)).then((response) => response.data)
  },
  async downloadClientTemplate() {
    const response = await apiClient.get(endpoints.imports.clientTemplate, {
      responseType: 'blob',
    })
    triggerDownload(response.data as Blob, 'clients_import_template.csv')
  },
  previewClients(file: File) {
    return apiClient.post<ImportPreviewResponse>(endpoints.imports.previewClients, buildFormData(file)).then((response) => response.data)
  },
  commitClients(file: File) {
    return apiClient.post<ImportCommitResponse>(endpoints.imports.commitClients, buildFormData(file)).then((response) => response.data)
  },
  async downloadSiteTemplate() {
    const response = await apiClient.get(endpoints.imports.siteTemplate, {
      responseType: 'blob',
    })
    triggerDownload(response.data as Blob, 'sites_import_template.csv')
  },
  previewSites(file: File) {
    return apiClient.post<ImportPreviewResponse>(endpoints.imports.previewSites, buildFormData(file)).then((response) => response.data)
  },
  commitSites(file: File) {
    return apiClient.post<ImportCommitResponse>(endpoints.imports.commitSites, buildFormData(file)).then((response) => response.data)
  },
}
