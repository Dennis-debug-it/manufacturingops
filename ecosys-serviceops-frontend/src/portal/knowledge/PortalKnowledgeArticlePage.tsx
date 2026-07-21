import { useMemo, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import toast from 'react-hot-toast'
import DOMPurify from 'dompurify'
import { marked } from 'marked'
import { ArrowLeft, Eye, ThumbsUp, ThumbsDown, ArrowRight } from 'lucide-react'
import { Spinner } from '../../shared/components/Spinner'
import { EmptyState } from '../../shared/components/EmptyState'
import { usePortalKnowledgeArticle, useSubmitPortalKnowledgeFeedback } from '../api'

export function PortalKnowledgeArticlePage() {
  const { slug } = useParams<{ slug: string }>()
  const { data: article, isLoading, isError } = usePortalKnowledgeArticle(slug)
  const submitFeedback = useSubmitPortalKnowledgeFeedback(article?.id ?? '')
  const [feedbackGiven, setFeedbackGiven] = useState<boolean | null>(null)

  const html = useMemo(() => (article ? DOMPurify.sanitize(marked.parse(article.body, { async: false }) as string) : ''), [article])

  const giveFeedback = async (isHelpful: boolean) => {
    try {
      await submitFeedback.mutateAsync({ isHelpful })
      setFeedbackGiven(isHelpful)
      toast.success('Thanks for letting us know!')
    } catch {
      // interceptor surfaces the error toast
    }
  }

  if (isLoading) {
    return <Spinner label="Loading article" />
  }

  if (isError || !article) {
    return <EmptyState title="Article not found" description="This article may have been moved or unpublished." />
  }

  return (
    <div className="space-y-6">
      <Link to="/portal/knowledge" className="inline-flex items-center gap-1 text-xs font-medium text-slate-500 hover:text-slate-900">
        <ArrowLeft className="h-3.5 w-3.5" />
        Help Center
      </Link>

      <article className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm sm:p-8">
        {article.categoryName ? <div className="text-xs font-medium uppercase tracking-wide text-slate-500">{article.categoryName}</div> : null}
        <h1 className="mt-1 text-2xl font-semibold text-slate-900 sm:text-3xl">{article.title}</h1>
        <div className="mt-2 flex items-center gap-4 text-xs text-slate-500">
          <span className="inline-flex items-center gap-1"><Eye className="h-3.5 w-3.5" />{article.viewCount} views</span>
          {article.publishedAt ? <span>Updated {new Date(article.publishedAt).toLocaleDateString()}</span> : null}
        </div>

        <div className="prose prose-slate mt-6 max-w-none text-sm" dangerouslySetInnerHTML={{ __html: html }} />

        {article.tags.length > 0 ? (
          <div className="mt-6 flex flex-wrap gap-2">
            {article.tags.map((tag) => (
              <span key={tag} className="rounded-full bg-slate-100 px-2.5 py-1 text-xs font-medium text-slate-600">{tag}</span>
            ))}
          </div>
        ) : null}
      </article>

      <div className="rounded-2xl border border-slate-200 bg-white p-6 text-center shadow-sm">
        {feedbackGiven === null ? (
          <>
            <p className="text-sm font-medium text-slate-900">Was this helpful?</p>
            <div className="mt-3 flex items-center justify-center gap-3">
              <button
                type="button"
                onClick={() => void giveFeedback(true)}
                disabled={submitFeedback.isPending}
                className="inline-flex h-10 items-center gap-2 rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-emerald-50 hover:text-emerald-700"
              >
                <ThumbsUp className="h-4 w-4" />
                Yes
              </button>
              <button
                type="button"
                onClick={() => void giveFeedback(false)}
                disabled={submitFeedback.isPending}
                className="inline-flex h-10 items-center gap-2 rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-rose-50 hover:text-rose-700"
              >
                <ThumbsDown className="h-4 w-4" />
                No
              </button>
            </div>
          </>
        ) : (
          <p className="text-sm text-slate-600">Thanks for your feedback!</p>
        )}
      </div>

      <div className="rounded-2xl border border-dashed border-slate-300 bg-slate-50 p-6 text-center">
        <p className="text-sm text-slate-600">Still need help?</p>
        <Link
          to="/portal/requests/new"
          className="mt-3 inline-flex h-11 items-center gap-2 rounded-lg bg-slate-900 px-4 text-sm font-medium text-white transition hover:bg-slate-800"
        >
          Raise a service request
          <ArrowRight className="h-4 w-4" />
        </Link>
      </div>
    </div>
  )
}
