import { useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { useAuth } from '../../auth/AuthContext'
import { EmptyState } from '../../components/ui/EmptyState'
import { ErrorState } from '../../components/ui/ErrorState'
import { LoadingState } from '../../components/ui/LoadingState'
import { useToast } from '../../components/ui/ToastProvider'
import { PageScaffold, SectionCard } from '../../components/ui/Workspace'
import { useAsyncData } from '../../hooks/useAsyncData'
import { knowledgeService } from '../../services/knowledgeService'
import type { KnowledgeArticleDetail } from '../../types/api'
import { formatDateTime } from '../../utils/date'

export function KnowledgeArticleDetailPage() {
  const { id = '' } = useParams()
  const navigate = useNavigate()
  const { permissions } = useAuth()
  const { pushToast } = useToast()
  const canViewKnowledge = Boolean(permissions?.canViewKnowledge || permissions?.canManageKnowledge)
  const canManageKnowledge = Boolean(permissions?.canManageKnowledge)
  const [acting, setActing] = useState(false)
  const { data, loading, error, reload } = useAsyncData<KnowledgeArticleDetail | null>(
    async (signal) => knowledgeService.getArticle(id, signal),
    null,
    [id],
  )

  async function runAction(action: 'publish' | 'archive' | 'delete') {
    if (!data || !canManageKnowledge) return
    setActing(true)
    try {
      if (action === 'publish') {
        await knowledgeService.publishArticle(data.id)
        pushToast({ title: 'Article published', description: 'The guide is now visible in smart suggestions and search.', tone: 'success' })
        await reload()
      } else if (action === 'archive') {
        await knowledgeService.archiveArticle(data.id)
        pushToast({ title: 'Article archived', description: 'The guide was archived and removed from active suggestions.', tone: 'success' })
        await reload()
      } else {
        await knowledgeService.deleteArticle(data.id)
        pushToast({ title: 'Article deleted', description: 'The knowledge article was removed.', tone: 'warning' })
        navigate('/knowledge')
      }
    } catch (nextError) {
      pushToast({ title: 'Action failed', description: nextError instanceof Error ? nextError.message : 'Unable to complete action.', tone: 'danger' })
    } finally {
      setActing(false)
    }
  }

  if (loading) {
    return <LoadingState label="Loading knowledge article" />
  }

  if (!canViewKnowledge) {
    return <ErrorState title="Permission required" description="Your account does not currently have access to this knowledge article." />
  }

  if (error) {
    return <ErrorState title="Unable to load knowledge article" description={error} />
  }

  if (!data) {
    return <EmptyState title="Knowledge article not found" description="The article may have been deleted or you may not have access to it." />
  }

  return (
    <PageScaffold
      eyebrow="Knowledge Centre"
      title={data.title}
      description={data.summary || 'Operational guidance, lessons learned, and reusable troubleshooting context.'}
      actions={(
        <div className="flex flex-wrap gap-2">
          <Link to="/knowledge" className="button-secondary">Back to library</Link>
          {canManageKnowledge ? <Link to={`/knowledge/${data.id}/edit`} className="button-secondary">Edit</Link> : null}
          {canManageKnowledge && data.status !== 'Published' ? (
            <button type="button" className="button-primary" disabled={acting} onClick={() => void runAction('publish')}>Publish</button>
          ) : null}
          {canManageKnowledge && data.status !== 'Archived' ? (
            <button type="button" className="button-secondary" disabled={acting} onClick={() => void runAction('archive')}>Archive</button>
          ) : null}
          {canManageKnowledge ? <button type="button" className="button-secondary" disabled={acting} onClick={() => void runAction('delete')}>Delete</button> : null}
        </div>
      )}
    >
      <div className="grid gap-4 xl:grid-cols-[1.1fr_0.9fr]">
        <div className="space-y-4">
          <SectionCard title="Article body" description="Use this as the team’s shared operational reference.">
            <div className="whitespace-pre-wrap text-sm leading-7 text-app">{data.body}</div>
          </SectionCard>

          <SectionCard title="Version history" description="Track how the guide evolved over time.">
            {data.versions.length === 0 ? (
              <EmptyState title="No saved versions" description="Versions will appear after the article is created and updated." />
            ) : (
              <div className="space-y-3">
                {data.versions.map((version) => (
                  <div key={version.id} className="rounded-2xl border border-app bg-[var(--app-surface)] p-4">
                    <div className="flex items-center justify-between gap-3">
                      <p className="text-sm font-semibold text-app">Version {version.versionNumber}</p>
                      <p className="text-xs text-muted">{formatDateTime(version.createdAt)}</p>
                    </div>
                    <p className="mt-3 line-clamp-4 whitespace-pre-wrap text-sm text-muted">{version.body}</p>
                  </div>
                ))}
              </div>
            )}
          </SectionCard>
        </div>

        <div className="space-y-4">
          <SectionCard title="Article metadata" description="Useful for search ranking, reuse, and access control.">
            <div className="grid gap-3 sm:grid-cols-2">
              <MetaCard label="Status" value={data.status} />
              <MetaCard label="Visibility" value={data.visibility} />
              <MetaCard label="Category" value={data.categoryName || 'Uncategorized'} />
              <MetaCard label="Published" value={formatDateTime(data.publishedAt || undefined)} />
              <MetaCard label="Created by" value={data.createdByName || 'Unknown'} />
              <MetaCard label="Updated by" value={data.updatedByName || data.createdByName || 'Unknown'} />
            </div>
            <div className="mt-4 flex flex-wrap gap-2">
              {data.tags.map((tag) => (
                <span key={tag} className="rounded-full bg-[var(--app-primary-soft)] px-3 py-1 text-[11px] font-semibold uppercase tracking-[0.14em] text-app">{tag}</span>
              ))}
            </div>
          </SectionCard>

          <SectionCard title="Related guides" description="Articles that share category or tag context with this one.">
            {data.relatedArticles.length === 0 ? (
              <EmptyState title="No related guides yet" description="Related articles will appear as the library grows." />
            ) : (
              <div className="space-y-3">
                {data.relatedArticles.map((article) => (
                  <Link key={article.id} to={`/knowledge/${article.id}`} className="block rounded-2xl border border-app bg-[var(--app-surface)] p-4">
                    <p className="text-sm font-semibold text-app">{article.title}</p>
                    <p className="mt-2 text-sm text-muted">{article.summary || 'No summary provided yet.'}</p>
                  </Link>
                ))}
              </div>
            )}
          </SectionCard>
        </div>
      </div>
    </PageScaffold>
  )
}

function MetaCard({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-2xl bg-[var(--app-surface)] p-4">
      <p className="text-xs font-semibold uppercase tracking-[0.16em] text-muted">{label}</p>
      <p className="mt-2 text-sm font-medium text-app">{value}</p>
    </div>
  )
}
