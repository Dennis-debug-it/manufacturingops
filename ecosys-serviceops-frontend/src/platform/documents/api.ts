import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, apiClient } from '../../shared/api/apiClient'

const BASE = '/api/platform/settings/templates'
const KEY = ['platform', 'document-templates'] as const

export const DOCUMENT_TYPES = ['Invoice', 'Quotation', 'Receipt'] as const
export type DocumentType = (typeof DOCUMENT_TYPES)[number]

export const DOCUMENT_LAYOUTS = [
  { id: 'Classic', label: 'Classic', hint: 'Logo and company on the left, title on the right, a coloured rule beneath.' },
  { id: 'Banner', label: 'Banner', hint: 'A solid band of your colour across the top.' },
  { id: 'Minimal', label: 'Minimal', hint: 'Plain and compact, no colour fills.' },
] as const

export interface DocumentTemplate {
  id: string
  name: string
  type: string
  previewText: string
  isDefault: boolean
  isActive: boolean
  subject: string | null
  headerHtml: string | null
  bodyHtml: string | null
  footerHtml: string | null
  termsHtml: string | null
  signatureHtml: string | null
  pageSize: string
  orientation: string
  showLogo: boolean
  showTenantBranding: boolean
  showPoweredByEcosys: boolean
  accentColor: string
  layout: string
  createdAt: string
  updatedAt: string | null
}

/** What the designer edits and the server saves; the text fields are plain text with {{placeholders}}. */
export interface DocumentTemplateDraft {
  name: string
  type: string
  isDefault: boolean
  isActive: boolean
  /** Document title, e.g. TAX INVOICE. */
  subject: string
  /** Company details under the name: address, PIN, contacts. */
  headerHtml: string
  /** Opening message above the lines. */
  bodyHtml: string
  /** Terms and payment details. */
  termsHtml: string
  /** Sign-off at the end. */
  signatureHtml: string
  /** Page footer. */
  footerHtml: string
  pageSize: string
  orientation: string
  showLogo: boolean
  showTenantBranding: boolean
  showPoweredByEcosys: boolean
  accentColor: string
  layout: string
}

export interface TemplatePlaceholder { key: string; meaning: string }

export const DEFAULT_TITLES: Record<DocumentType, string> = { Invoice: 'INVOICE', Quotation: 'QUOTATION', Receipt: 'RECEIPT' }

export function blankDraft(type: DocumentType): DocumentTemplateDraft {
  return {
    name: `Standard ${type.toLowerCase()}`,
    type,
    isDefault: true,
    isActive: true,
    subject: DEFAULT_TITLES[type],
    headerHtml: '',
    bodyHtml: type === 'Quotation' ? 'Thank you for your interest. Here is our quotation for {{customer.name}}.' : type === 'Receipt' ? 'Thank you for your payment.' : '',
    termsHtml: type === 'Invoice'
      ? 'Payment is due by {{document.dueDate}}. Please quote {{document.number}} with your payment.'
      : type === 'Quotation' ? 'This quotation is valid until {{document.dueDate}}.' : '',
    signatureHtml: '',
    footerHtml: '',
    pageSize: 'A4',
    orientation: 'Portrait',
    showLogo: true,
    showTenantBranding: true,
    showPoweredByEcosys: true,
    accentColor: '#0f3d3e',
    layout: 'Classic',
  }
}

export function draftOf(x: DocumentTemplate): DocumentTemplateDraft {
  return {
    name: x.name,
    type: x.type,
    isDefault: x.isDefault,
    isActive: x.isActive,
    subject: x.subject ?? '',
    headerHtml: x.headerHtml ?? '',
    bodyHtml: x.bodyHtml ?? '',
    termsHtml: x.termsHtml ?? '',
    signatureHtml: x.signatureHtml ?? '',
    footerHtml: x.footerHtml ?? '',
    pageSize: x.pageSize || 'A4',
    orientation: x.orientation || 'Portrait',
    showLogo: x.showLogo,
    showTenantBranding: x.showTenantBranding,
    showPoweredByEcosys: x.showPoweredByEcosys,
    accentColor: x.accentColor || '#0f3d3e',
    layout: x.layout || 'Classic',
  }
}

const toRequest = (d: DocumentTemplateDraft) => ({ ...d, previewText: null })

export const useDocumentTemplates = () => useQuery({ queryKey: KEY, queryFn: () => api.get<DocumentTemplate[]>(BASE) })

export const useDocumentTemplate = (id: string | null) =>
  useQuery({ queryKey: [...KEY, id], queryFn: () => api.get<DocumentTemplate>(`${BASE}/${id}`), enabled: Boolean(id) })

export const usePlaceholders = () =>
  useQuery({ queryKey: [...KEY, 'placeholders'], queryFn: () => api.get<TemplatePlaceholder[]>(`${BASE}/placeholders`), staleTime: Infinity })

export function useSaveDocumentTemplate(id: string | null) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (draft: DocumentTemplateDraft) => (id ? api.put<DocumentTemplate>(`${BASE}/${id}`, toRequest(draft)) : api.post<DocumentTemplate>(BASE, toRequest(draft))),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: KEY }),
  })
}

export function useTemplateAction() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, action }: { id: string; action: 'duplicate' | 'make-default' | 'activate' | 'deactivate' | 'delete' }) =>
      action === 'delete' ? api.delete(`${BASE}/${id}`) : api.post(`${BASE}/${id}/${action}`),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: KEY }),
  })
}

/** What the draft draws on a made-up document of its type: the PDF, or an image of its first page for the preview. */
export async function previewDocument(draft: DocumentTemplateDraft, format: 'png' | 'pdf', signal?: AbortSignal): Promise<Blob> {
  const response = await apiClient.post(`${BASE}/preview-pdf`, toRequest(draft), {
    params: format === 'png' ? { format } : undefined, responseType: 'blob', suppressGlobalErrorToast: true, ...(signal ? { signal } : {}),
  })
  return response.data as Blob
}

export function saveBlob(blob: Blob, fileName: string) {
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = fileName
  link.click()
  setTimeout(() => URL.revokeObjectURL(url), 1000)
}

/** Downloads a quotation, invoice or receipt PDF from platform finance. */
export async function downloadFinancePdf(kind: 'quotations' | 'invoices' | 'payments', id: string, fileName: string) {
  const response = await apiClient.get(`/api/platform/finance/${kind}/${id}/pdf`, { responseType: 'blob' })
  saveBlob(response.data as Blob, `${fileName}.pdf`)
}
