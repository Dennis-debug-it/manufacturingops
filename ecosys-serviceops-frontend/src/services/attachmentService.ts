import { api } from '../lib/api'
import type { AttachmentRecord } from '../types/api'

const MAX_FILE_SIZE = 5 * 1024 * 1024 // 5 MB

const ALLOWED_MIME_TYPES = new Set([
  'image/jpeg',
  'image/png',
  'image/webp',
  'application/pdf',
  'application/msword',
  'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
  'application/vnd.ms-excel',
  'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
  'text/csv',
])

export const ATTACHMENT_ENTITY_TYPES = {
  WorkOrder: 'WorkOrder',
  Asset: 'Asset',
  Site: 'Site',
  Client: 'Client',
} as const

export type AttachmentEntityType = (typeof ATTACHMENT_ENTITY_TYPES)[keyof typeof ATTACHMENT_ENTITY_TYPES]

export function validateAttachmentFile(file: File): string | null {
  if (file.size > MAX_FILE_SIZE) return `File "${file.name}" exceeds the 5 MB size limit.`
  if (!ALLOWED_MIME_TYPES.has(file.type)) return `File type "${file.type}" is not permitted.`
  return null
}

export const attachmentService = {
  list(entityType: string, entityId: string, signal?: AbortSignal): Promise<AttachmentRecord[]> {
    return api.get<AttachmentRecord[]>(`/api/attachments/entity/${entityType}/${entityId}`, { signal })
  },

  get(id: string, signal?: AbortSignal): Promise<AttachmentRecord> {
    return api.get<AttachmentRecord>(`/api/attachments/${id}`, { signal })
  },

  upload(entityType: string, entityId: string, file: File, signal?: AbortSignal): Promise<AttachmentRecord> {
    const form = new FormData()
    form.append('entityType', entityType)
    form.append('entityId', entityId)
    form.append('file', file)
    return api.postForm<AttachmentRecord>('/api/attachments/upload', form, { signal })
  },

  delete(id: string, signal?: AbortSignal): Promise<void> {
    return api.delete<void>(`/api/attachments/${id}`, { signal })
  },
}
