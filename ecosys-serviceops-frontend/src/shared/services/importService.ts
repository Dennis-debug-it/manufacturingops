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
}
