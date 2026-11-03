export type KnowledgeArticleStatus = 'Draft' | 'Published' | 'Archived'
export type KnowledgeArticleVisibility = 'Internal' | 'TechnicianOnly' | 'AdminOnly' | 'Public'

export interface KnowledgeCategory {
  id: string
  name: string
  description: string | null
  icon: string | null
  displayOrder: number
  isActive: boolean
  isPublic: boolean
}

export interface KnowledgeArticleListItem {
  id: string
  title: string
  slug: string
  summary: string | null
  categoryId: string | null
  categoryName: string | null
  status: KnowledgeArticleStatus
  visibility: KnowledgeArticleVisibility
  publishedAt: string | null
  updatedAt: string
  viewCount: number
  helpfulCount: number
  notHelpfulCount: number
  tags: string[]
}

export interface KnowledgeArticleVersion {
  id: string
  versionNumber: number
  title: string
  body: string
  updatedByUserId: string | null
  createdAt: string
}

export interface KnowledgeArticleDetail {
  id: string
  title: string
  slug: string
  summary: string | null
  body: string
  categoryId: string | null
  categoryName: string | null
  status: KnowledgeArticleStatus
  visibility: KnowledgeArticleVisibility
  createdByUserId: string
  createdByName: string | null
  updatedByUserId: string | null
  updatedByName: string | null
  createdAt: string
  updatedAt: string | null
  publishedAt: string | null
  sourceWorkOrderId: string | null
  viewCount: number
  helpfulCount: number
  notHelpfulCount: number
  tags: string[]
  versions: KnowledgeArticleVersion[]
  relatedArticles: KnowledgeArticleListItem[]
}

export interface UpsertKnowledgeCategoryPayload {
  name: string
  description?: string | null
  icon?: string | null
  displayOrder: number
  isPublic: boolean
}

export interface UpsertKnowledgeArticlePayload {
  title: string
  summary?: string | null
  body: string
  categoryId?: string | null
  status: KnowledgeArticleStatus
  visibility: KnowledgeArticleVisibility
  tags?: string[]
}
