import { useMemo, useState } from 'react'
import DOMPurify from 'dompurify'
import { marked } from 'marked'
import { Link, useParams } from 'react-router-dom'
import { BookOpen, ChevronLeft, ChevronRight } from 'lucide-react'
import { Spinner } from '../../shared/components/Spinner'
import { useDebounce } from '../../shared/hooks/useDebounce'
import { formatDate } from '../../shared/utils/dates'
import { useKnowledgeArticle } from '../knowledge/api'
import type { KnowledgeArticleListItem } from '../knowledge/types'
import { useGuideSearch, useGuideSuggestions } from './api'

function GuideList({ articles }: { articles: KnowledgeArticleListItem[] }) {
  return (
    <ul className="divide-y divide-slate-100 overflow-hidden rounded-xl border border-slate-200 bg-white">
      {articles.map((article) => (
        <li key={article.id}>
          <Link to={`/tech/guides/${article.id}`} className="flex items-center gap-3 px-4 py-3 hover:bg-slate-50">
            <BookOpen className="h-4 w-4 shrink-0 text-slate-400" />
            <span className="min-w-0 flex-1">
              <span className="block truncate text-sm font-medium text-slate-900">{article.title}</span>
              {article.summary || article.categoryName ? (
                <span className="block truncate text-xs text-slate-500">{article.categoryName ? `${article.categoryName} · ` : ''}{article.summary ?? ''}</span>
              ) : null}
            </span>
            <ChevronRight className="h-4 w-4 shrink-0 text-slate-400" />
          </Link>
        </li>
      ))}
    </ul>
  )
}

/** Searchable list of published knowledge-base guides. */
export function TechGuidesPage() {
  const [search, setSearch] = useState('')
  const q = useDebounce(search.trim(), 250)
  const { data, isLoading, isError } = useGuideSearch(q)

  return (
    <div className="space-y-3">
      <h1 className="text-lg font-semibold text-slate-900">Guides</h1>
      <Link to="/tech/help" className="flex items-center justify-between rounded-xl border border-slate-200 bg-white px-4 py-3 text-sm font-medium text-slate-900" data-testid="tech-app-help">
        How to use the technician app
        <span aria-hidden className="text-slate-400">›</span>
      </Link>
      <input
        type="search"
        value={search}
        onChange={(event) => setSearch(event.target.value)}
        placeholder="Search guides, fixes and manuals"
        aria-label="Search guides"
        className="h-11 w-full rounded-lg border border-slate-300 bg-white px-3 text-sm outline-none focus:border-[var(--app-primary)]"
      />
      {isLoading ? <Spinner label="Loading guides" /> : null}
      {isError ? <p className="rounded-xl border border-rose-200 bg-rose-50 p-4 text-sm text-rose-700">Guides could not be loaded.</p> : null}
      {data && data.length === 0 ? <p className="rounded-xl border border-slate-200 bg-white p-4 text-sm text-slate-500">{q ? 'No guides match that search.' : 'No guides have been published yet.'}</p> : null}
      {data && data.length > 0 ? <GuideList articles={data} /> : null}
    </div>
  )
}

export function TechGuidePage() {
  const { id = '' } = useParams<{ id: string }>()
  const { data: article, isLoading, isError } = useKnowledgeArticle(id)
  const html = useMemo(() => (article ? DOMPurify.sanitize(marked.parse(article.body, { async: false }) as string) : ''), [article])

  return (
    <div className="space-y-3">
      <Link to="/tech/guides" className="inline-flex items-center gap-1 text-sm text-slate-600"><ChevronLeft className="h-4 w-4" />Guides</Link>
      {isLoading ? <Spinner label="Loading guide" /> : null}
      {isError || (!isLoading && !article) ? <p className="rounded-xl border border-rose-200 bg-rose-50 p-4 text-sm text-rose-700">This guide could not be loaded.</p> : null}
      {article ? (
        <>
          <div>
            <h1 className="text-lg font-semibold leading-snug text-slate-900">{article.title}</h1>
            <p className="text-xs text-slate-500">{article.categoryName ? `${article.categoryName} · ` : ''}{article.updatedAt ? `Updated ${formatDate(article.updatedAt)}` : null}</p>
          </div>
          <article className="prose prose-slate max-w-none rounded-xl border border-slate-200 bg-white p-4 text-sm" dangerouslySetInnerHTML={{ __html: html }} />
          {article.relatedArticles.length > 0 ? (
            <section className="space-y-2">
              <h2 className="text-sm font-semibold text-slate-900">Related guides</h2>
              <GuideList articles={article.relatedArticles} />
            </section>
          ) : null}
        </>
      ) : null}
    </div>
  )
}

/** Guides the knowledge base matches to this job (asset type, title, site); hidden when there are none. */
export function TechJobGuides({ workOrderId }: { workOrderId: string }) {
  const { data } = useGuideSuggestions(workOrderId)
  if (!data || data.length === 0) return null
  return (
    <section className="space-y-2">
      <h2 className="text-sm font-semibold text-slate-900">Guides for this job</h2>
      <GuideList articles={data} />
    </section>
  )
}
