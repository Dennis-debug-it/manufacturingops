import { api } from '../lib/api'
import type {
  AskKnowledgeInput,
  AskKnowledgeResponse,
  KnowledgeArticleDetail,
  KnowledgeArticleListItem,
  KnowledgeCategoryRecord,
  KnowledgeDocumentRecord,
  UpsertKnowledgeArticleInput,
  UpsertKnowledgeCategoryInput,
} from '../types/api'
import { asArray } from '../utils/apiDefaults'

export const knowledgeService = {
  async listArticles(options?: { q?: string; categoryId?: string | null; status?: string | null; signal?: AbortSignal }): Promise<KnowledgeArticleListItem[]> {
    const response = await api.get<unknown>('/api/knowledge/articles', {
      query: {
        q: options?.q?.trim() || undefined,
        categoryId: options?.categoryId || undefined,
        status: options?.status || undefined,
      },
      signal: options?.signal,
    })
    return asArray<KnowledgeArticleListItem>(response)
  },

  createArticle(input: UpsertKnowledgeArticleInput) {
    return api.post<KnowledgeArticleDetail>('/api/knowledge/articles', input)
  },

  getArticle(id: string, signal?: AbortSignal) {
    return api.get<KnowledgeArticleDetail>(`/api/knowledge/articles/${id}`, { signal })
  },

  updateArticle(id: string, input: UpsertKnowledgeArticleInput) {
    return api.put<KnowledgeArticleDetail>(`/api/knowledge/articles/${id}`, input)
  },

  deleteArticle(id: string) {
    return api.delete<void>(`/api/knowledge/articles/${id}`)
  },

  publishArticle(id: string) {
    return api.post<KnowledgeArticleDetail>(`/api/knowledge/articles/${id}/publish`)
  },

  archiveArticle(id: string) {
    return api.post<KnowledgeArticleDetail>(`/api/knowledge/articles/${id}/archive`)
  },

  async listCategories(signal?: AbortSignal): Promise<KnowledgeCategoryRecord[]> {
    const response = await api.get<unknown>('/api/knowledge/categories', { signal })
    return asArray<KnowledgeCategoryRecord>(response)
  },

  createCategory(input: UpsertKnowledgeCategoryInput) {
    return api.post<KnowledgeCategoryRecord>('/api/knowledge/categories', input)
  },

  async search(query: string, categoryId?: string | null, signal?: AbortSignal): Promise<KnowledgeArticleListItem[]> {
    const response = await api.get<unknown>('/api/knowledge/search', {
      query: {
        q: query.trim() || undefined,
        categoryId: categoryId || undefined,
      },
      signal,
    })
    return asArray<KnowledgeArticleListItem>(response)
  },

  async getSuggestionsForWorkOrder(workOrderId: string, signal?: AbortSignal): Promise<KnowledgeArticleListItem[]> {
    const response = await api.get<unknown>(`/api/knowledge/suggestions/work-order/${workOrderId}`, { signal })
    return asArray<KnowledgeArticleListItem>(response)
  },

  async getRelatedArticles(id: string, signal?: AbortSignal): Promise<KnowledgeArticleListItem[]> {
    const response = await api.get<unknown>(`/api/knowledge/articles/${id}/related`, { signal })
    return asArray<KnowledgeArticleListItem>(response)
  },

  draftFromWorkOrder(workOrderId: string) {
    return api.post<KnowledgeArticleDetail>(`/api/knowledge/articles/from-work-order/${workOrderId}`)
  },

  async listDocuments(signal?: AbortSignal): Promise<KnowledgeDocumentRecord[]> {
    const response = await api.get<unknown>('/api/knowledge/documents', { signal })
    return asArray<KnowledgeDocumentRecord>(response)
  },

  getDocument(id: string, signal?: AbortSignal) {
    return api.get<KnowledgeDocumentRecord>(`/api/knowledge/documents/${id}`, { signal })
  },

  async uploadDocument(input: {
    file: File
    title: string
    description?: string | null
    categoryId?: string | null
    tags?: string[] | null
    linkedClientId?: string | null
    linkedSiteId?: string | null
    linkedSystemId?: string | null
    linkedAssetId?: string | null
    linkedWorkOrderId?: string | null
    status?: string | null
  }) {
    const form = new FormData()
    form.append('file', input.file)
    form.append('title', input.title)
    if (input.description) form.append('description', input.description)
    if (input.categoryId) form.append('categoryId', input.categoryId)
    if (input.tags?.length) form.append('tags', input.tags.join(', '))
    if (input.linkedClientId) form.append('linkedClientId', input.linkedClientId)
    if (input.linkedSiteId) form.append('linkedSiteId', input.linkedSiteId)
    if (input.linkedSystemId) form.append('linkedSystemId', input.linkedSystemId)
    if (input.linkedAssetId) form.append('linkedAssetId', input.linkedAssetId)
    if (input.linkedWorkOrderId) form.append('linkedWorkOrderId', input.linkedWorkOrderId)
    if (input.status) form.append('status', input.status)
    return api.postForm<KnowledgeDocumentRecord>('/api/knowledge/documents/upload', form)
  },

  deleteDocument(id: string) {
    return api.delete<void>(`/api/knowledge/documents/${id}`)
  },

  reindexDocument(id: string) {
    return api.post<KnowledgeDocumentRecord>(`/api/knowledge/documents/${id}/reindex`)
  },

  ask(input: AskKnowledgeInput) {
    return api.post<AskKnowledgeResponse>('/api/knowledge/ask', input)
  },
}
