import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Search, ArrowRight } from 'lucide-react'
import { Spinner } from '../../shared/components/Spinner'
import { EmptyState } from '../../shared/components/EmptyState'
import { resolveKnowledgeIcon } from '../../features/knowledge/knowledgeIcons'
import { usePortalKnowledgeCategories } from '../api'

export function PortalKnowledgeHomePage() {
  const navigate = useNavigate()
  const { data: categories, isLoading, isError } = usePortalKnowledgeCategories()
  const [query, setQuery] = useState('')

  const submitSearch = () => {
    const trimmed = query.trim()
    navigate(trimmed ? `/portal/knowledge/browse?q=${encodeURIComponent(trimmed)}` : '/portal/knowledge/browse')
  }

  return (
    <div className="space-y-8">
      <div className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-8 text-center shadow-sm sm:p-12">
        <h1 className="text-2xl font-semibold text-slate-900 sm:text-3xl">How can we help you?</h1>
        <p className="mt-2 text-sm text-slate-600">Search our help center for guides, FAQs, and troubleshooting tips.</p>
        <form
          className="mx-auto mt-6 flex max-w-xl items-center gap-2"
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
              className="h-12 w-full rounded-xl border border-slate-300 pl-10 pr-3 text-sm outline-none focus:border-[var(--app-primary)]"
            />
          </div>
          <button type="submit" className="inline-flex h-12 items-center justify-center rounded-xl bg-[var(--app-primary)] px-5 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)]">
            Search
          </button>
        </form>
      </div>

      {isLoading ? (
        <Spinner label="Loading help center" />
      ) : isError ? (
        <EmptyState title="Help center unavailable" description="Categories could not be loaded right now." />
      ) : (categories ?? []).length === 0 ? (
        <EmptyState title="No help articles yet" description="Check back soon — we're adding help center content." />
      ) : (
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {(categories ?? []).map((category) => {
            const Icon = resolveKnowledgeIcon(category.icon)
            return (
              <button
                key={category.id}
                type="button"
                onClick={() => navigate(`/portal/knowledge/browse?category=${category.id}`)}
                className="flex items-start gap-4 rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 text-left shadow-sm transition hover:border-slate-300 hover:shadow-md"
              >
                <div className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-slate-100 text-slate-700">
                  <Icon className="h-5 w-5" />
                </div>
                <div className="min-w-0 flex-1">
                  <div className="font-medium text-slate-900">{category.name}</div>
                  {category.description ? <p className="mt-1 line-clamp-2 text-xs text-slate-500">{category.description}</p> : null}
                  <div className="mt-2 flex items-center gap-1 text-xs font-medium text-slate-500">
                    {category.articleCount} article{category.articleCount === 1 ? '' : 's'}
                    <ArrowRight className="h-3 w-3" />
                  </div>
                </div>
              </button>
            )
          })}
        </div>
      )}
    </div>
  )
}
