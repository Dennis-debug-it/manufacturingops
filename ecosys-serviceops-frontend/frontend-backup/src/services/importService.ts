import { api } from '../lib/api'
import type { ImportCommitResponse, ImportPreviewResponse } from '../types/api'

function buildFormData(file: File) {
  const formData = new FormData()
  formData.append('file', file)
  return formData
}

export type ImportTemplateType = 'clients' | 'assets' | 'users' | 'branches' | 'materials'

const importTemplateNames: Record<ImportTemplateType, string> = {
  clients: 'clients_import_template.csv',
  assets: 'assets_import_template.csv',
  users: 'users_import_template.csv',
  branches: 'branches_import_template.csv',
  materials: 'materials_import_template.csv',
}

function triggerFileDownload(blob: Blob, fileName: string) {
  const url = window.URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = fileName
  link.style.display = 'none'
  document.body.append(link)
  link.click()
  link.remove()
  window.URL.revokeObjectURL(url)
}

export const importService = {
  async downloadTemplate(type: ImportTemplateType) {
    const { blob, fileName } = await api.download(`/api/import/templates/${type}`, {
      fallbackFileName: importTemplateNames[type],
    })
    triggerFileDownload(blob, fileName)
  },
  previewClients(file: File) {
    return api.postForm<ImportPreviewResponse>('/api/import/clients/preview', buildFormData(file))
  },
  commitClients(file: File) {
    return api.postForm<ImportCommitResponse>('/api/import/clients/commit', buildFormData(file))
  },
  previewAssets(file: File) {
    return api.postForm<ImportPreviewResponse>('/api/import/assets/preview', buildFormData(file))
  },
  commitAssets(file: File, options?: { allowCreateSystems?: boolean }) {
    const formData = buildFormData(file)
    formData.append('AllowCreateSystems', String(Boolean(options?.allowCreateSystems)))
    return api.postForm<ImportCommitResponse>('/api/import/assets/commit', formData)
  },
  previewUsers(file: File) {
    return api.postForm<ImportPreviewResponse>('/api/import/users/preview', buildFormData(file))
  },
  commitUsers(file: File) {
    return api.postForm<ImportCommitResponse>('/api/import/users/commit', buildFormData(file))
  },
  previewBranches(file: File) {
    return api.postForm<ImportPreviewResponse>('/api/import/branches/preview', buildFormData(file))
  },
  commitBranches(file: File) {
    return api.postForm<ImportCommitResponse>('/api/import/branches/commit', buildFormData(file))
  },
  previewMaterials(file: File) {
    return api.postForm<ImportPreviewResponse>('/api/import/materials/preview', buildFormData(file))
  },
  commitMaterials(file: File) {
    return api.postForm<ImportCommitResponse>('/api/import/materials/commit', buildFormData(file))
  },
}
