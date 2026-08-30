import { useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { Search, ArrowLeft, Eye, ThumbsUp } from 'lucide-react'
import { Spinner } from '../../shared/components/Spinner'
import { EmptyState } from '../../shared/components/EmptyState'
import { usePortalKnowledgeArticles, usePortalKnowledgeCategories } from '../api'

export function PortalKnowledgeBrowsePage() {
  const [searchParams, setSearchParams] = useSearchParams()
  const categoryId = searchParams.get('category') ?? undefined
  const initialQuery = searchParams.get('q') ?? ''
  const [query, setQuery] = useState(initialQuery)

  const { data: categories } = usePortalKnowledgeCategories()
  const { data: articles, isLoading, isError } = usePortalKnowledgeArticles({ categoryId, search: initialQuery || undefined })

  const activeCategory = categories?.find((c) => c.id === categoryId)

  const submitSearch = () => {
    const next = new URLSearchParams()
    if (categoryId) next.set('category', categoryId)
    if (query.trim()) next.set('q', query.trim())
    setSearchParams(next)
  }

  return (
    <div className="space-y-6">
      <div>
        <Link to="/portal/knowledge" className="inline-flex items-center gap-1 text-xs font-medium text-slate-500 hover:text-slate-900">
          <ArrowLeft className="h-3.5 w-3.5" />
          Help Center
        </Link>
        <h1 className="mt-2 text-2xl font-semibold text-slate-900">{activeCategory ? activeCategory.name : initialQuery ? `Results for "${initialQuery}"` : 'All Articles'}</h1>
      </div>

      <form
        className="flex max-w-xl items-center gap-2"
        onSubmit={(event) => {
          event.preventDefault()
          submitSearch()
        }}
      >
        <div className="relative flex-1">
          <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
          <input
            value={query}
            onChange={(event) => setQuery(event.target.value)}
            placeholder="Search articles…"
            className="h-11 w-full rounded-lg border border-slate-300 pl-10 pr-3 text-sm outline-none focus:border-[var(--app-primary)]"
          />
        </div>
        <button type="submit" className="inline-flex h-11 items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)]">
          Search
        </button>
      </form>

      {isLoading ? (
        <Spinner label="Loading articles" />
      ) : isError ? (
        <EmptyState title="Articles unavailable" description="Articles could not be loaded right now." />
      ) : (articles ?? []).length === 0 ? (
        <EmptyState title="No articles found" description="Try a different search term or browse another category." />
      ) : (
        <div className="space-y-3">
          {(articles ?? []).map((article) => (
            <Link
              key={article.id}
              to={`/portal/knowledge/articles/${article.slug}`}
              className="block rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-5 shadow-sm transition hover:border-slate-300 hover:shadow-md"
            >
              <div className="font-medium text-slate-900">{article.title}</div>
              {article.summary ? <p className="mt-1 text-sm text-slate-600">{article.summary}</p> : null}
              <div className="mt-3 flex items-center gap-4 text-xs text-slate-500">
                {article.categoryName ? <span>{article.categoryName}</span> : null}
                <span className="inline-flex items-center gap-1"><Eye className="h-3.5 w-3.5" />{article.viewCount}</span>
                <span className="inline-flex items-center gap-1"><ThumbsUp className="h-3.5 w-3.5" />{article.helpfulCount}</span>
              </div>
            </Link>
          ))}
        </div>
      )}
    </div>
  )
}
