import { FileUp, MessageSquareText, Plus, RefreshCw, Search, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useAuth } from '../../auth/AuthContext'
import { EmptyState } from '../../components/ui/EmptyState'
import { ErrorState } from '../../components/ui/ErrorState'
import { LoadingState } from '../../components/ui/LoadingState'
import { useToast } from '../../components/ui/ToastProvider'
import { PageScaffold, SearchToolbar, SectionCard } from '../../components/ui/Workspace'
import { useAsyncData } from '../../hooks/useAsyncData'
import { knowledgeService } from '../../services/knowledgeService'
import type { AskKnowledgeResponse, KnowledgeArticleListItem, KnowledgeCategoryRecord, KnowledgeDocumentRecord } from '../../types/api'
import { formatDateTime } from '../../utils/date'

type KnowledgePageData = {
  articles: KnowledgeArticleListItem[]
  categories: KnowledgeCategoryRecord[]
  documents: KnowledgeDocumentRecord[]
}

const emptyData: KnowledgePageData = {
  articles: [],
  categories: [],
  documents: [],
}

export function KnowledgeCentrePage() {
  const { permissions } = useAuth()
  const { pushToast } = useToast()
  const canViewKnowledge = Boolean(permissions?.canViewKnowledge || permissions?.canManageKnowledge)
  const canManageKnowledge = Boolean(permissions?.canManageKnowledge)
  const [search, setSearch] = useState('')
  const [categoryId, setCategoryId] = useState('all')
  const [status, setStatus] = useState('all')
  const [showCategoryForm, setShowCategoryForm] = useState(false)
  const [categoryName, setCategoryName] = useState('')
  const [categoryDescription, setCategoryDescription] = useState('')
  const [documentTitle, setDocumentTitle] = useState('')
  const [documentDescription, setDocumentDescription] = useState('')
  const [documentCategoryId, setDocumentCategoryId] = useState('')
  const [documentTags, setDocumentTags] = useState('')
  const [documentFile, setDocumentFile] = useState<File | null>(null)
  const [question, setQuestion] = useState('')
  const [asking, setAsking] = useState(false)
  const [answer, setAnswer] = useState<AskKnowledgeResponse | null>(null)
  const [documentActionId, setDocumentActionId] = useState<string | null>(null)

  const { data, loading, error, reload } = useAsyncData<KnowledgePageData>(
    async (signal) => {
      const articleResults = search.trim()
        ? await knowledgeService.search(search, categoryId === 'all' ? null : categoryId, signal)
        : await knowledgeService.listArticles({
            q: search,
            categoryId: categoryId === 'all' ? null : categoryId,
            status: status === 'all' ? null : status,
            signal,
          })

      const [articles, categories, documents] = await Promise.all([
        Promise.resolve(articleResults),
        knowledgeService.listCategories(signal),
        knowledgeService.listDocuments(signal),
      ])
      return { articles, categories, documents }
    },
    emptyData,
    [search, categoryId, status],
  )

  async function createCategory() {
    if (!canManageKnowledge) return
    if (!categoryName.trim()) {
      pushToast({ title: 'Category name required', description: 'Enter a category name before saving.', tone: 'warning' })
      return
    }

    try {
      await knowledgeService.createCategory({
        name: categoryName.trim(),
        description: categoryDescription.trim() || null,
        displayOrder: data.categories.length + 1,
      })
      pushToast({ title: 'Category created', description: 'The Knowledge Centre category is ready to use.', tone: 'success' })
      setCategoryName('')
      setCategoryDescription('')
      setShowCategoryForm(false)
      await reload()
    } catch (nextError) {
      pushToast({ title: 'Save failed', description: nextError instanceof Error ? nextError.message : 'Unable to save category.', tone: 'danger' })
    }
  }

  async function uploadDocument() {
    if (!canManageKnowledge) return
    if (!documentFile || !documentTitle.trim()) {
      pushToast({ title: 'Document details required', description: 'Choose a file and enter a document title before uploading.', tone: 'warning' })
      return
    }

    try {
      await knowledgeService.uploadDocument({
        file: documentFile,
        title: documentTitle.trim(),
        description: documentDescription.trim() || null,
        categoryId: documentCategoryId || null,
        tags: documentTags.split(',').map((item) => item.trim()).filter(Boolean),
        status: 'published',
      })
      pushToast({ title: 'Document uploaded', description: 'The document has been indexed for Knowledge Centre search and Ask.', tone: 'success' })
      setDocumentTitle('')
      setDocumentDescription('')
      setDocumentCategoryId('')
      setDocumentTags('')
      setDocumentFile(null)
      await reload()
    } catch (nextError) {
      pushToast({ title: 'Upload failed', description: nextError instanceof Error ? nextError.message : 'Unable to upload document.', tone: 'danger' })
    }
  }

  async function askKnowledge() {
    if (!question.trim() || asking) return
    setAsking(true)
    try {
      const result = await knowledgeService.ask({
        question: question.trim(),
        contextType: 'knowledge',
        includeDocuments: true,
      })
      setAnswer(result)
    } catch (nextError) {
      pushToast({ title: 'Ask Fundi failed', description: nextError instanceof Error ? nextError.message : 'Unable to query Fundi.', tone: 'danger' })
    } finally {
      setAsking(false)
    }
  }

  async function reindexDocument(document: KnowledgeDocumentRecord) {
    if (!canManageKnowledge || documentActionId) return

    setDocumentActionId(document.id)
    try {
      await knowledgeService.reindexDocument(document.id)
      pushToast({ title: 'Document reindexed', description: 'Search chunks were refreshed for this document.', tone: 'success' })
      await reload()
    } catch (nextError) {
      pushToast({ title: 'Reindex failed', description: nextError instanceof Error ? nextError.message : 'Unable to reindex document.', tone: 'danger' })
    } finally {
      setDocumentActionId(null)
    }
  }

  async function deleteDocument(document: KnowledgeDocumentRecord) {
    if (!canManageKnowledge || documentActionId) return
    const confirmed = window.confirm(`Delete "${document.title}" from the Knowledge Centre?`)
    if (!confirmed) return

    setDocumentActionId(document.id)
    try {
      await knowledgeService.deleteDocument(document.id)
      pushToast({ title: 'Document deleted', description: 'The document was removed from the Knowledge Centre.', tone: 'success' })
      await reload()
    } catch (nextError) {
      pushToast({ title: 'Delete failed', description: nextError instanceof Error ? nextError.message : 'Unable to delete document.', tone: 'danger' })
    } finally {
      setDocumentActionId(null)
    }
  }

  if (!canViewKnowledge) {
    return <ErrorState title="Permission required" description="Your account does not currently have access to the Knowledge Centre." />
  }

  if (loading) {
    return <LoadingState label="Loading Knowledge Centre" />
  }

  if (error) {
    return <ErrorState title="Unable to load Knowledge Centre" description={error} />
  }

  return (
    <PageScaffold
      eyebrow="Knowledge Centre"
      title="Operational knowledge that teams can actually reuse"
      description="Store SOPs, troubleshooting guides, and lessons learned, then surface them again where the work is happening."
      actions={(
        <div className="flex flex-wrap gap-2">
          {canManageKnowledge ? (
            <button type="button" className="button-secondary" onClick={() => setShowCategoryForm((current) => !current)}>
              {showCategoryForm ? 'Hide category form' : 'New category'}
            </button>
          ) : null}
          {canManageKnowledge ? (
            <Link to="/knowledge/new" className="button-primary">
              <Plus className="h-4 w-4" />
              New article
            </Link>
          ) : null}
        </div>
      )}
    >
      <SearchToolbar
        searchSlot={(
          <label className="block space-y-2">
            <span className="text-sm font-medium text-app">Search knowledge</span>
            <div className="relative">
              <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted" />
              <input
                value={search}
                onChange={(event) => setSearch(event.target.value)}
                className="field-input pl-10"
                placeholder="Search titles, tags, categories, or troubleshooting notes"
              />
            </div>
          </label>
        )}
        filters={(
          <>
            <label className="block space-y-2">
              <span className="text-sm font-medium text-app">Category</span>
              <select value={categoryId} onChange={(event) => setCategoryId(event.target.value)} className="field-input">
                <option value="all">All categories</option>
                {data.categories.map((category) => (
                  <option key={category.id} value={category.id}>{category.name}</option>
                ))}
              </select>
            </label>
            <label className="block space-y-2">
              <span className="text-sm font-medium text-app">Status</span>
              <select value={status} onChange={(event) => setStatus(event.target.value)} className="field-input">
                <option value="all">All statuses</option>
                <option value="Draft">Draft</option>
                <option value="Published">Published</option>
                <option value="Archived">Archived</option>
              </select>
            </label>
          </>
        )}
      />

      {showCategoryForm && canManageKnowledge ? (
        <SectionCard title="Create category" description="Keep the knowledge library organized so related guides are easier to discover.">
          <div className="grid gap-4 md:grid-cols-2">
            <label className="block space-y-2">
              <span className="text-sm font-medium text-app">Category name</span>
              <input value={categoryName} onChange={(event) => setCategoryName(event.target.value)} className="field-input" />
            </label>
            <label className="block space-y-2 md:col-span-2">
              <span className="text-sm font-medium text-app">Document description</span>
              <textarea value={categoryDescription} onChange={(event) => setCategoryDescription(event.target.value)} className="field-input min-h-[110px]" />
            </label>
          </div>
          <div className="mt-4 flex justify-end">
            <button type="button" className="button-primary" onClick={() => void createCategory()}>Save category</button>
          </div>
        </SectionCard>
      ) : null}

      <div className={`grid gap-4 ${canManageKnowledge ? 'xl:grid-cols-2' : ''}`}>
        {canManageKnowledge ? (
        <SectionCard title="Document upload" description="Upload manuals, SOPs, guides, and tenant operational references so they become searchable and available in Ask.">
          <div className="grid gap-4 md:grid-cols-2">
            <label className="block space-y-2 md:col-span-2">
              <span className="text-sm font-medium text-app">Document file</span>
              <input type="file" accept=".pdf,.docx,.txt,.md,.png,.jpg,.jpeg,.webp,.xlsx" onChange={(event) => setDocumentFile(event.target.files?.[0] ?? null)} className="field-input" />
            </label>
            <label className="block space-y-2">
              <span className="text-sm font-medium text-app">Title</span>
              <input value={documentTitle} onChange={(event) => setDocumentTitle(event.target.value)} className="field-input" />
            </label>
            <label className="block space-y-2">
              <span className="text-sm font-medium text-app">Category</span>
              <select value={documentCategoryId} onChange={(event) => setDocumentCategoryId(event.target.value)} className="field-input">
                <option value="">No category</option>
                {data.categories.map((item) => (
                  <option key={item.id} value={item.id}>{item.name}</option>
                ))}
              </select>
            </label>
            <label className="block space-y-2 md:col-span-2">
              <span className="text-sm font-medium text-app">Tags</span>
              <input value={documentTags} onChange={(event) => setDocumentTags(event.target.value)} className="field-input" placeholder="Manual, Generator, SOP" />
            </label>
            <label className="block space-y-2 md:col-span-2">
              <span className="text-sm font-medium text-app">Description</span>
              <textarea value={documentDescription} onChange={(event) => setDocumentDescription(event.target.value)} className="field-input min-h-[110px]" />
            </label>
          </div>
          <div className="mt-4 flex justify-end">
            <button type="button" className="button-primary" onClick={() => void uploadDocument()}>
              <FileUp className="h-4 w-4" />
              Upload document
            </button>
          </div>
        </SectionCard>
        ) : null}

        <SectionCard title="Ask Fundi" description="Ask Fundi questions against articles and uploaded internal documents only.">
          <label className="block space-y-2">
            <span className="text-sm font-medium text-app">Question</span>
            <textarea value={question} onChange={(event) => setQuestion(event.target.value)} className="field-input min-h-[120px]" placeholder="What does the manual say about alarm E104?" />
          </label>
          <div className="mt-4 flex justify-end">
            <button type="button" className="button-primary" onClick={() => void askKnowledge()} disabled={asking || !question.trim()}>
              <MessageSquareText className="h-4 w-4" />
              {asking ? 'Searching...' : 'Ask Fundi'}
            </button>
          </div>
          {answer ? (
            <div className="mt-4 space-y-4">
              <div className="rounded-[24px] border border-app bg-subtle p-4">
                <p className="text-sm font-semibold text-app">Answer</p>
                <p className="mt-2 text-sm leading-6 text-muted">{answer.answer}</p>
              </div>
              <div className="space-y-3">
                {answer.sources.map((source) => (
                  <a key={`${source.sourceType}-${source.sourceId}`} href={source.path} className="block rounded-[24px] border border-app bg-[var(--app-card)] p-4">
                    <div className="flex flex-wrap items-center justify-between gap-3">
                      <p className="text-sm font-semibold text-app">{source.title}</p>
                      <span className="text-xs uppercase tracking-[0.16em] text-muted">{source.sourceType}{source.pageNumber ? ` • page ${source.pageNumber}` : ''}</span>
                    </div>
                    <p className="mt-2 text-sm text-muted">{source.excerpt}</p>
                  </a>
                ))}
              </div>
            </div>
          ) : null}
        </SectionCard>
      </div>

      <SectionCard title="Articles" description="Published guides, active drafts, and archived institutional memory in one place.">
        {data.articles.length === 0 ? (
          <EmptyState title="No knowledge articles yet" description="Create the first article or broaden the search filters." />
        ) : (
          <div className="grid gap-4 xl:grid-cols-2">
            {data.articles.map((article) => (
              <Link key={article.id} to={`/knowledge/${article.id}`} className="rounded-[24px] border border-app bg-[var(--app-card)] p-5 shadow-[var(--app-shadow-soft)] transition hover:-translate-y-0.5">
                <div className="flex flex-wrap items-center justify-between gap-3">
                  <p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted">{article.categoryName || 'General guide'}</p>
                  <span className="rounded-full bg-[var(--app-surface-strong)] px-3 py-1 text-[11px] font-semibold uppercase tracking-[0.16em] text-app">{article.status}</span>
                </div>
                <h2 className="mt-3 text-xl font-semibold text-app">{article.title}</h2>
                <p className="mt-2 text-sm leading-6 text-muted">{article.summary || 'No summary provided yet.'}</p>
                <div className="mt-4 flex flex-wrap gap-2">
                  {article.tags.map((tag) => (
                    <span key={tag} className="rounded-full bg-[var(--app-primary-soft)] px-3 py-1 text-[11px] font-semibold uppercase tracking-[0.14em] text-app">{tag}</span>
                  ))}
                </div>
                <p className="mt-4 text-xs text-muted">Updated {formatDateTime(article.updatedAt)}</p>
              </Link>
            ))}
          </div>
        )}
      </SectionCard>

      <SectionCard title="Documents" description="Uploaded operational files currently indexed for Ask and tenant search.">
        {data.documents.length === 0 ? (
          <EmptyState title="No knowledge documents yet" description="Upload a manual, SOP, or guide to make it searchable." />
        ) : (
          <div className="grid gap-4 xl:grid-cols-2">
            {data.documents.map((document) => (
              <div key={document.id} className="rounded-[24px] border border-app bg-[var(--app-card)] p-5 shadow-[var(--app-shadow-soft)]">
                <div className="flex flex-wrap items-center justify-between gap-3">
                  <p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted">{document.categoryName || document.fileType}</p>
                  <span className="rounded-full bg-[var(--app-surface-strong)] px-3 py-1 text-[11px] font-semibold uppercase tracking-[0.16em] text-app">{document.status}</span>
                </div>
                <a href={document.downloadPath} className="mt-3 block text-xl font-semibold text-app hover:underline">{document.title}</a>
                <p className="mt-2 text-sm leading-6 text-muted">{document.description || document.fileName}</p>
                <p className="mt-4 text-xs text-muted">{document.chunkCount} indexed chunks • uploaded {formatDateTime(document.uploadedAt)}</p>
                {canManageKnowledge ? (
                  <div className="mt-4 flex flex-wrap justify-end gap-2">
                    <button type="button" className="button-secondary" onClick={() => void reindexDocument(document)} disabled={documentActionId === document.id}>
                      <RefreshCw className="h-4 w-4" />
                      {documentActionId === document.id ? 'Working...' : 'Reindex'}
                    </button>
                    <button type="button" className="button-secondary" onClick={() => void deleteDocument(document)} disabled={documentActionId === document.id}>
                      <Trash2 className="h-4 w-4" />
                      Delete
                    </button>
                  </div>
                ) : null}
              </div>
            ))}
          </div>
        )}
      </SectionCard>
    </PageScaffold>
  )
}
