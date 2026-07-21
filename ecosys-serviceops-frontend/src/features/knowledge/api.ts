import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../../shared/api/apiClient'
import { endpoints } from '../../shared/api/endpoints'
import type {
  KnowledgeArticleDetail,
  KnowledgeArticleListItem,
  KnowledgeCategory,
  UpsertKnowledgeArticlePayload,
  UpsertKnowledgeCategoryPayload,
} from './types'

export function useKnowledgeCategories() {
  return useQuery({
    queryKey: ['knowledge-categories'],
    queryFn: () => api.get<KnowledgeCategory[]>(endpoints.knowledge.categories),
    staleTime: 30_000,
  })
}

export function useCreateKnowledgeCategory() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: UpsertKnowledgeCategoryPayload) => api.post<KnowledgeCategory>(endpoints.knowledge.categories, payload),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['knowledge-categories'] }),
  })
}

export function useUpdateKnowledgeCategory(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: UpsertKnowledgeCategoryPayload) => api.put<KnowledgeCategory>(endpoints.knowledge.category(id), payload),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['knowledge-categories'] }),
  })
}

export function useDeleteKnowledgeCategory() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api.delete<void>(endpoints.knowledge.category(id)),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['knowledge-categories'] }),
  })
}

export function useKnowledgeArticles(filters?: { categoryId?: string | undefined; status?: string | undefined; q?: string | undefined }) {
  return useQuery({
    queryKey: ['knowledge-articles', filters?.categoryId ?? '', filters?.status ?? '', filters?.q ?? ''],
    queryFn: () =>
      api.get<KnowledgeArticleListItem[]>(endpoints.knowledge.articles, {
        categoryId: filters?.categoryId,
        status: filters?.status,
        q: filters?.q,
      }),
    staleTime: 15_000,
  })
}

export function useKnowledgeArticle(id: string | undefined) {
  return useQuery({
    queryKey: ['knowledge-articles', id],
    queryFn: () => api.get<KnowledgeArticleDetail>(endpoints.knowledge.article(id ?? '')),
    enabled: Boolean(id),
  })
}

function invalidateArticles(queryClient: ReturnType<typeof useQueryClient>) {
  void queryClient.invalidateQueries({ queryKey: ['knowledge-articles'] })
}

export function useCreateKnowledgeArticle() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: UpsertKnowledgeArticlePayload) => api.post<KnowledgeArticleDetail>(endpoints.knowledge.articles, payload),
    onSuccess: () => invalidateArticles(queryClient),
  })
}

export function useUpdateKnowledgeArticle(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: UpsertKnowledgeArticlePayload) => api.put<KnowledgeArticleDetail>(endpoints.knowledge.article(id), payload),
    onSuccess: () => {
      invalidateArticles(queryClient)
      void queryClient.invalidateQueries({ queryKey: ['knowledge-articles', id] })
    },
  })
}

export function useDeleteKnowledgeArticle() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api.delete<void>(endpoints.knowledge.article(id)),
    onSuccess: () => invalidateArticles(queryClient),
  })
}

export function usePublishKnowledgeArticle() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api.post<KnowledgeArticleDetail>(endpoints.knowledge.publish(id)),
    onSuccess: (_data, id) => {
      invalidateArticles(queryClient)
      void queryClient.invalidateQueries({ queryKey: ['knowledge-articles', id] })
    },
  })
}

export function useArchiveKnowledgeArticle() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api.post<KnowledgeArticleDetail>(endpoints.knowledge.archive(id)),
    onSuccess: (_data, id) => {
      invalidateArticles(queryClient)
      void queryClient.invalidateQueries({ queryKey: ['knowledge-articles', id] })
    },
  })
}
