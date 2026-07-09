import { useEffect, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { useAuth } from '../../auth/AuthContext'
import { ErrorState } from '../../components/ui/ErrorState'
import { LoadingState } from '../../components/ui/LoadingState'
import { useToast } from '../../components/ui/ToastProvider'
import { PageScaffold, SectionCard, StickyActionFooter } from '../../components/ui/Workspace'
import { useAsyncData } from '../../hooks/useAsyncData'
import { knowledgeService } from '../../services/knowledgeService'
import type { KnowledgeArticleDetail, KnowledgeCategoryRecord, UpsertKnowledgeArticleInput } from '../../types/api'

type EditorData = {
  article: KnowledgeArticleDetail | null
  categories: KnowledgeCategoryRecord[]
}

const emptyData: EditorData = {
  article: null,
  categories: [],
}

export function KnowledgeArticleEditorPage() {
  const { id } = useParams()
  const navigate = useNavigate()
  const { permissions } = useAuth()
  const { pushToast } = useToast()
  const isNew = !id || id === 'new'
  const canManageKnowledge = Boolean(permissions?.canManageKnowledge)
  const [form, setForm] = useState<UpsertKnowledgeArticleInput>({
    title: '',
    summary: '',
    body: '',
    categoryId: '',
    status: 'Draft',
    visibility: 'Internal',
    tags: [],
  })
  const [tagInput, setTagInput] = useState('')
  const [saving, setSaving] = useState(false)

  const { data, loading, error } = useAsyncData<EditorData>(
    async (signal) => {
      const categories = await knowledgeService.listCategories(signal)
      if (isNew) {
        return { article: null, categories }
      }
      const article = await knowledgeService.getArticle(id!, signal)
      return { article, categories }
    },
    emptyData,
    [id],
  )

  useEffect(() => {
    if (!data.article) {
      return
    }

    setForm({
      title: data.article.title,
      summary: data.article.summary || '',
      body: data.article.body,
      categoryId: data.article.categoryId || '',
      status: data.article.status,
      visibility: data.article.visibility,
      tags: data.article.tags,
    })
    setTagInput(data.article.tags.join(', '))
  }, [data.article])

  async function save() {
    if (!canManageKnowledge) return
    if (!form.title.trim() || !form.body.trim()) {
      pushToast({ title: 'Title and body required', description: 'Add a title and article body before saving.', tone: 'warning' })
      return
    }

    setSaving(true)
    try {
      const payload: UpsertKnowledgeArticleInput = {
        ...form,
        title: form.title.trim(),
        summary: form.summary?.trim() || null,
        body: form.body.trim(),
        categoryId: form.categoryId || null,
        tags: tagInput.split(',').map((item) => item.trim()).filter(Boolean),
      }

      const saved = isNew
        ? await knowledgeService.createArticle(payload)
        : await knowledgeService.updateArticle(id!, payload)

      pushToast({ title: isNew ? 'Article created' : 'Article updated', description: 'Knowledge article changes were saved.', tone: 'success' })
      navigate(`/knowledge/${saved.id}`)
    } catch (nextError) {
      pushToast({ title: 'Save failed', description: nextError instanceof Error ? nextError.message : 'Unable to save article.', tone: 'danger' })
    } finally {
      setSaving(false)
    }
  }

  if (loading) {
    return <LoadingState label="Loading article editor" />
  }

  if (!canManageKnowledge) {
    return <ErrorState title="Permission required" description="Your account does not currently have rights to create or edit knowledge articles." />
  }

  if (error) {
    return <ErrorState title="Unable to load article editor" description={error} />
  }

  return (
    <PageScaffold
      eyebrow="Knowledge Centre"
      title={isNew ? 'Create article' : 'Edit article'}
      description="Capture troubleshooting steps in a format that can be reused by dispatchers, admins, and technicians."
      actions={<Link to="/knowledge" className="button-secondary">Back to library</Link>}
    >
      <SectionCard title="Article content" description="Keep the summary short, then use the body for procedural detail and lessons learned.">
        <div className="grid gap-4 md:grid-cols-2">
          <label className="block space-y-2 md:col-span-2">
            <span className="text-sm font-medium text-app">Title</span>
            <input value={form.title} onChange={(event) => setForm((current) => ({ ...current, title: event.target.value }))} className="field-input" />
          </label>
          <label className="block space-y-2 md:col-span-2">
            <span className="text-sm font-medium text-app">Summary</span>
            <textarea value={form.summary || ''} onChange={(event) => setForm((current) => ({ ...current, summary: event.target.value }))} className="field-input min-h-[110px]" />
          </label>
          <label className="block space-y-2">
            <span className="text-sm font-medium text-app">Category</span>
            <select value={form.categoryId || ''} onChange={(event) => setForm((current) => ({ ...current, categoryId: event.target.value }))} className="field-input">
              <option value="">No category</option>
              {data.categories.map((category) => (
                <option key={category.id} value={category.id}>{category.name}</option>
              ))}
            </select>
          </label>
          <label className="block space-y-2">
            <span className="text-sm font-medium text-app">Status</span>
            <select value={form.status} onChange={(event) => setForm((current) => ({ ...current, status: event.target.value }))} className="field-input">
              <option value="Draft">Draft</option>
              <option value="Published">Published</option>
              <option value="Archived">Archived</option>
            </select>
          </label>
          <label className="block space-y-2">
            <span className="text-sm font-medium text-app">Visibility</span>
            <select value={form.visibility} onChange={(event) => setForm((current) => ({ ...current, visibility: event.target.value }))} className="field-input">
              <option value="Internal">Internal</option>
              <option value="TechnicianOnly">Technician only</option>
              <option value="AdminOnly">Admin only</option>
            </select>
          </label>
          <label className="block space-y-2">
            <span className="text-sm font-medium text-app">Tags</span>
            <input value={tagInput} onChange={(event) => setTagInput(event.target.value)} className="field-input" placeholder="Generator, ATS, relay" />
          </label>
          <label className="block space-y-2 md:col-span-2">
            <span className="text-sm font-medium text-app">Body</span>
            <textarea value={form.body} onChange={(event) => setForm((current) => ({ ...current, body: event.target.value }))} className="field-input min-h-[360px]" />
          </label>
        </div>
        <StickyActionFooter>
          <button type="button" className="button-primary w-full sm:w-auto" disabled={saving} onClick={() => void save()}>
            {saving ? 'Saving...' : 'Save article'}
          </button>
        </StickyActionFooter>
      </SectionCard>
    </PageScaffold>
  )
}
