import { useEffect, useMemo, useState } from 'react'
import toast from 'react-hot-toast'
import DOMPurify from 'dompurify'
import { marked } from 'marked'
import { BookOpen, Eye, Pencil, Plus, Trash2, Globe, Lock } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { Spinner } from '../../shared/components/Spinner'
import { EmptyState } from '../../shared/components/EmptyState'
import { FormDrawer } from '../../shared/components/FormDrawer'
import { ConfirmDialog } from '../../shared/components/ConfirmDialog'
import { usePermission } from '../../shared/hooks/usePermission'
import { cn } from '../../shared/utils/cn'
import {
  useArchiveKnowledgeArticle,
  useCreateKnowledgeArticle,
  useCreateKnowledgeCategory,
  useDeleteKnowledgeArticle,
  useDeleteKnowledgeCategory,
  useKnowledgeArticle,
  useKnowledgeArticles,
  useKnowledgeCategories,
  usePublishKnowledgeArticle,
  useUpdateKnowledgeArticle,
  useUpdateKnowledgeCategory,
} from './api'
import { knowledgeIconOptions, resolveKnowledgeIcon } from './knowledgeIcons'
import type {
  KnowledgeArticleListItem,
  KnowledgeArticleStatus,
  KnowledgeArticleVisibility,
  KnowledgeCategory,
  UpsertKnowledgeArticlePayload,
} from './types'

const statusStyles: Record<KnowledgeArticleStatus, string> = {
  Draft: 'bg-slate-100 text-slate-700 ring-slate-200',
  Published: 'bg-emerald-50 text-emerald-700 ring-emerald-200',
  Archived: 'bg-amber-50 text-amber-700 ring-amber-200',
}

const visibilityOptions: { value: KnowledgeArticleVisibility; label: string }[] = [
  { value: 'Internal', label: 'Internal — all staff' },
  { value: 'TechnicianOnly', label: 'Technicians only' },
  { value: 'AdminOnly', label: 'Admins only' },
  { value: 'Public', label: 'Public — visible in client portal' },
]

function CategoryEditor({ category, onClose }: { category: KnowledgeCategory | null; onClose: () => void }) {
  const canManage = usePermission('knowledge.manage')
  const create = useCreateKnowledgeCategory()
  const update = useUpdateKnowledgeCategory(category?.id ?? '')

  const [name, setName] = useState(category?.name ?? '')
  const [description, setDescription] = useState(category?.description ?? '')
  const [icon, setIcon] = useState(category?.icon ?? knowledgeIconOptions[0]!.name)
  const [displayOrder, setDisplayOrder] = useState(category?.displayOrder ?? 0)
  const [isPublic, setIsPublic] = useState(category?.isPublic ?? false)

  const isPending = create.isPending || update.isPending

  const save = async () => {
    if (!name.trim()) {
      toast.error('Category name is required')
      return
    }

    const payload = { name: name.trim(), description: description.trim() || null, icon, displayOrder, isPublic }
    try {
      if (category) {
        await update.mutateAsync(payload)
      } else {
        await create.mutateAsync(payload)
      }
      toast.success('Category saved')
      onClose()
    } catch {
      // interceptor surfaces the error toast
    }
  }

  return (
    <FormDrawer
      open
      title={category ? 'Edit Category' : 'New Category'}
      onClose={onClose}
      footer={
        canManage ? (
          <div className="flex items-center justify-end gap-2">
            <button type="button" onClick={onClose} className="inline-flex h-11 items-center justify-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">Cancel</button>
            <button type="button" onClick={save} disabled={isPending} className="inline-flex h-11 items-center justify-center rounded-lg bg-slate-900 px-4 text-sm font-medium text-white transition hover:bg-slate-800 disabled:opacity-60">
              {isPending ? 'Saving…' : 'Save'}
            </button>
          </div>
        ) : null
      }
    >
      <div className="space-y-5">
        <div>
          <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="kb-cat-name">Name</label>
          <input id="kb-cat-name" value={name} disabled={!canManage} onChange={(event) => setName(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900 disabled:bg-slate-50" />
        </div>

        <div>
          <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="kb-cat-description">Description</label>
          <textarea id="kb-cat-description" value={description ?? ''} disabled={!canManage} onChange={(event) => setDescription(event.target.value)} rows={3} className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-slate-900 disabled:bg-slate-50" />
        </div>

        <div>
          <label className="mb-1.5 block text-sm font-medium text-slate-700">Icon</label>
          <div className="grid grid-cols-5 gap-2">
            {knowledgeIconOptions.map((option) => {
              const Icon = option.icon
              const active = icon === option.name
              return (
                <button
                  key={option.name}
                  type="button"
                  disabled={!canManage}
                  onClick={() => setIcon(option.name)}
                  className={cn(
                    'flex h-12 items-center justify-center rounded-lg border transition',
                    active ? 'border-slate-900 bg-slate-900 text-white' : 'border-slate-200 bg-white text-slate-600 hover:bg-slate-50',
                  )}
                >
                  <Icon className="h-5 w-5" />
                </button>
              )
            })}
          </div>
        </div>

        <div>
          <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="kb-cat-order">Display order</label>
          <input id="kb-cat-order" type="number" value={displayOrder} disabled={!canManage} onChange={(event) => setDisplayOrder(Number(event.target.value))} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900 disabled:bg-slate-50" />
        </div>

        <label className="flex items-center gap-2 text-sm text-slate-700">
          <input type="checkbox" checked={isPublic} disabled={!canManage} onChange={(event) => setIsPublic(event.target.checked)} className="h-4 w-4 rounded border-slate-300" />
          Show this category in the client portal Help Center
        </label>
      </div>
    </FormDrawer>
  )
}

function ArticleEditor({
  articleId,
  defaultCategoryId,
  categories,
  onClose,
}: {
  articleId: string | null
  defaultCategoryId: string | null
  categories: KnowledgeCategory[]
  onClose: () => void
}) {
  const canManage = usePermission('knowledge.manage')
  const { data: article, isLoading } = useKnowledgeArticle(articleId ?? undefined)
  const create = useCreateKnowledgeArticle()
  const update = useUpdateKnowledgeArticle(articleId ?? '')
  const publish = usePublishKnowledgeArticle()
  const archiveArticle = useArchiveKnowledgeArticle()

  const [title, setTitle] = useState('')
  const [summary, setSummary] = useState('')
  const [body, setBody] = useState('')
  const [categoryId, setCategoryId] = useState<string>(defaultCategoryId ?? '')
  const [status, setStatus] = useState<KnowledgeArticleStatus>('Draft')
  const [visibility, setVisibility] = useState<KnowledgeArticleVisibility>('Internal')
  const [tagsInput, setTagsInput] = useState('')
  const [previewMode, setPreviewMode] = useState(false)

  useEffect(() => {
    if (!article) return
    setTitle(article.title)
    setSummary(article.summary ?? '')
    setBody(article.body)
    setCategoryId(article.categoryId ?? '')
    setStatus(article.status)
    setVisibility(article.visibility)
    setTagsInput(article.tags.join(', '))
  }, [article])

  const isPending = create.isPending || update.isPending
  const renderedHtml = useMemo(() => (previewMode ? DOMPurify.sanitize(marked.parse(body, { async: false }) as string) : ''), [previewMode, body])

  const save = async () => {
    if (!title.trim()) {
      toast.error('Article title is required')
      return
    }
    if (!body.trim()) {
      toast.error('Article body is required')
      return
    }

    const payload: UpsertKnowledgeArticlePayload = {
      title: title.trim(),
      summary: summary.trim() || null,
      body,
      categoryId: categoryId || null,
      status,
      visibility,
      tags: tagsInput.split(',').map((tag) => tag.trim()).filter(Boolean),
    }

    try {
      if (articleId) {
        await update.mutateAsync(payload)
      } else {
        await create.mutateAsync(payload)
      }
      toast.success('Article saved')
      onClose()
    } catch {
      // interceptor surfaces the error toast
    }
  }

  const togglePublish = async () => {
    if (!articleId) return
    try {
      if (status === 'Published') {
        await archiveArticle.mutateAsync(articleId)
        toast.success('Article archived')
      } else {
        await publish.mutateAsync(articleId)
        toast.success('Article published')
      }
      onClose()
    } catch {
      // interceptor surfaces the error toast
    }
  }

  return (
    <FormDrawer
      open
      title={articleId ? 'Edit Article' : 'New Article'}
      onClose={onClose}
      footer={
        canManage ? (
          <div className="flex items-center justify-between gap-2">
            {articleId && status !== 'Draft' ? (
              <button type="button" onClick={() => void togglePublish()} className="inline-flex h-11 items-center justify-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">
                {status === 'Published' ? 'Archive' : 'Publish'}
              </button>
            ) : articleId ? (
              <button type="button" onClick={() => void togglePublish()} disabled={publish.isPending} className="inline-flex h-11 items-center justify-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">
                Publish
              </button>
            ) : <span />}
            <div className="flex items-center gap-2">
              <button type="button" onClick={onClose} className="inline-flex h-11 items-center justify-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">Cancel</button>
              <button type="button" onClick={save} disabled={isPending} className="inline-flex h-11 items-center justify-center rounded-lg bg-slate-900 px-4 text-sm font-medium text-white transition hover:bg-slate-800 disabled:opacity-60">
                {isPending ? 'Saving…' : 'Save'}
              </button>
            </div>
          </div>
        ) : null
      }
    >
      {isLoading ? (
        <Spinner label="Loading article" />
      ) : (
        <div className="space-y-5">
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="kb-art-title">Title</label>
            <input id="kb-art-title" value={title} disabled={!canManage} onChange={(event) => setTitle(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900 disabled:bg-slate-50" />
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="kb-art-category">Category</label>
              <select id="kb-art-category" value={categoryId} disabled={!canManage} onChange={(event) => setCategoryId(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900 disabled:bg-slate-50">
                <option value="">No category</option>
                {categories.map((c) => (
                  <option key={c.id} value={c.id}>{c.name}</option>
                ))}
              </select>
            </div>
            <div>
              <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="kb-art-visibility">Visibility</label>
              <select id="kb-art-visibility" value={visibility} disabled={!canManage} onChange={(event) => setVisibility(event.target.value as KnowledgeArticleVisibility)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900 disabled:bg-slate-50">
                {visibilityOptions.map((option) => (
                  <option key={option.value} value={option.value}>{option.label}</option>
                ))}
              </select>
            </div>
          </div>

          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="kb-art-tags">Tags (comma separated)</label>
            <input id="kb-art-tags" value={tagsInput} disabled={!canManage} onChange={(event) => setTagsInput(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900 disabled:bg-slate-50" />
          </div>

          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="kb-art-summary">Excerpt</label>
            <textarea id="kb-art-summary" value={summary ?? ''} disabled={!canManage} onChange={(event) => setSummary(event.target.value)} rows={2} className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-slate-900 disabled:bg-slate-50" />
          </div>

          <div>
            <div className="mb-1.5 flex items-center justify-between">
              <label className="block text-sm font-medium text-slate-700" htmlFor="kb-art-body">Body (Markdown)</label>
              <div className="flex overflow-hidden rounded-lg border border-slate-200">
                <button type="button" onClick={() => setPreviewMode(false)} className={cn('px-3 py-1 text-xs font-medium', !previewMode ? 'bg-slate-900 text-white' : 'bg-white text-slate-600')}>Write</button>
                <button type="button" onClick={() => setPreviewMode(true)} className={cn('px-3 py-1 text-xs font-medium', previewMode ? 'bg-slate-900 text-white' : 'bg-white text-slate-600')}>Preview</button>
              </div>
            </div>
            {previewMode ? (
              <div className="prose prose-slate min-h-[16rem] max-w-none rounded-lg border border-slate-200 px-3 py-2 text-sm" dangerouslySetInnerHTML={{ __html: renderedHtml }} />
            ) : (
              <textarea id="kb-art-body" value={body} disabled={!canManage} onChange={(event) => setBody(event.target.value)} rows={16} className="w-full rounded-lg border border-slate-300 px-3 py-2 font-mono text-sm outline-none focus:border-slate-900 disabled:bg-slate-50" />
            )}
          </div>
        </div>
      )}
    </FormDrawer>
  )
}

export function KnowledgeBasePage() {
  const canManage = usePermission('knowledge.manage')
  const { data: categories, isLoading: categoriesLoading, isError: categoriesError } = useKnowledgeCategories()
  const [activeCategoryId, setActiveCategoryId] = useState<string | 'all'>('all')
  const { data: articles, isLoading: articlesLoading, isError: articlesError } = useKnowledgeArticles({
    categoryId: activeCategoryId === 'all' ? undefined : activeCategoryId,
  })

  const [editingCategory, setEditingCategory] = useState<{ open: boolean; category: KnowledgeCategory | null }>({ open: false, category: null })
  const [confirmDeleteCategory, setConfirmDeleteCategory] = useState<KnowledgeCategory | null>(null)
  const [editingArticle, setEditingArticle] = useState<{ open: boolean; id: string | null } | null>(null)
  const [confirmDeleteArticle, setConfirmDeleteArticle] = useState<KnowledgeArticleListItem | null>(null)

  const deleteCategory = useDeleteKnowledgeCategory()
  const deleteArticle = useDeleteKnowledgeArticle()

  if (categoriesLoading) {
    return <Spinner label="Loading knowledge base" />
  }

  if (categoriesError) {
    return <EmptyState icon={BookOpen} title="Knowledge base unavailable" description="Categories could not be loaded." />
  }

  const categoryList = categories ?? []

  return (
    <div className="space-y-6">
      <PageHeader
        title="Knowledge Base"
        subtitle="Internal reference articles and client-facing help center content."
        primaryAction={
          canManage ? (
            <button type="button" onClick={() => setEditingArticle({ open: true, id: null })} className="inline-flex h-11 items-center gap-2 rounded-lg bg-slate-900 px-4 text-sm font-medium text-white transition hover:bg-slate-800">
              <Plus className="h-4 w-4" />
              New Article
            </button>
          ) : undefined
        }
        secondaryActions={
          canManage ? (
            <button type="button" onClick={() => setEditingCategory({ open: true, category: null })} className="inline-flex h-11 items-center gap-2 rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">
              <Plus className="h-4 w-4" />
              New Category
            </button>
          ) : undefined
        }
      />

      <div className="flex flex-wrap gap-2">
        <button
          type="button"
          onClick={() => setActiveCategoryId('all')}
          className={cn(
            'inline-flex items-center gap-2 rounded-full px-3.5 py-2 text-sm font-medium ring-1 ring-inset transition',
            activeCategoryId === 'all' ? 'bg-slate-900 text-white ring-slate-900' : 'bg-white text-slate-600 ring-slate-200 hover:bg-slate-50',
          )}
        >
          All articles
        </button>
        {categoryList.map((category) => {
          const Icon = resolveKnowledgeIcon(category.icon)
          const active = activeCategoryId === category.id
          return (
            <div key={category.id} className="group relative">
              <button
                type="button"
                onClick={() => setActiveCategoryId(category.id)}
                className={cn(
                  'inline-flex items-center gap-2 rounded-full px-3.5 py-2 text-sm font-medium ring-1 ring-inset transition',
                  active ? 'bg-slate-900 text-white ring-slate-900' : 'bg-white text-slate-600 ring-slate-200 hover:bg-slate-50',
                )}
              >
                <Icon className="h-4 w-4" />
                {category.name}
                {category.isPublic ? <Globe className="h-3 w-3 opacity-70" /> : <Lock className="h-3 w-3 opacity-40" />}
              </button>
              {canManage ? (
                <div className="absolute -top-2 -right-2 hidden gap-1 group-hover:flex">
                  <button type="button" onClick={() => setEditingCategory({ open: true, category })} className="rounded-full bg-white p-1 text-slate-500 shadow ring-1 ring-slate-200 hover:text-slate-900">
                    <Pencil className="h-3 w-3" />
                  </button>
                  <button type="button" onClick={() => setConfirmDeleteCategory(category)} className="rounded-full bg-white p-1 text-rose-500 shadow ring-1 ring-slate-200 hover:text-rose-700">
                    <Trash2 className="h-3 w-3" />
                  </button>
                </div>
              ) : null}
            </div>
          )
        })}
      </div>

      {articlesLoading ? (
        <Spinner label="Loading articles" />
      ) : articlesError ? (
        <EmptyState icon={BookOpen} title="Articles unavailable" description="Articles could not be loaded." />
      ) : (articles ?? []).length === 0 ? (
        <EmptyState
          icon={BookOpen}
          title="No articles yet"
          description="Create your first knowledge base article to get started."
          actionLabel={canManage ? 'New Article' : undefined}
          onAction={canManage ? () => setEditingArticle({ open: true, id: null }) : undefined}
        />
      ) : (
        <div className="overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm">
          <table className="w-full text-sm">
            <thead className="bg-slate-50 text-left text-xs font-medium uppercase tracking-wide text-slate-500">
              <tr>
                <th className="px-4 py-3">Title</th>
                <th className="px-4 py-3">Category</th>
                <th className="px-4 py-3">Status</th>
                <th className="px-4 py-3">Visibility</th>
                <th className="px-4 py-3">Views</th>
                <th className="px-4 py-3">Updated</th>
                <th className="px-4 py-3" />
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {(articles ?? []).map((article) => (
                <tr key={article.id} className="cursor-pointer hover:bg-slate-50" onClick={() => setEditingArticle({ open: true, id: article.id })}>
                  <td className="px-4 py-3">
                    <div className="font-medium text-slate-900">{article.title}</div>
                    {article.tags.length > 0 ? <div className="mt-1 text-xs text-slate-500">{article.tags.join(', ')}</div> : null}
                  </td>
                  <td className="px-4 py-3 text-slate-600">{article.categoryName ?? '—'}</td>
                  <td className="px-4 py-3">
                    <span className={cn('inline-flex items-center rounded-full px-2.5 py-1 text-xs font-medium ring-1 ring-inset', statusStyles[article.status])}>{article.status}</span>
                  </td>
                  <td className="px-4 py-3 text-slate-600">{article.visibility}</td>
                  <td className="px-4 py-3 text-slate-600">
                    <span className="inline-flex items-center gap-1"><Eye className="h-3.5 w-3.5" />{article.viewCount}</span>
                  </td>
                  <td className="px-4 py-3 text-slate-500">{new Date(article.updatedAt).toLocaleDateString()}</td>
                  <td className="px-4 py-3 text-right">
                    {canManage ? (
                      <button
                        type="button"
                        onClick={(event) => {
                          event.stopPropagation()
                          setConfirmDeleteArticle(article)
                        }}
                        className="rounded-lg p-1.5 text-slate-400 hover:bg-rose-50 hover:text-rose-600"
                      >
                        <Trash2 className="h-4 w-4" />
                      </button>
                    ) : null}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {editingCategory.open ? (
        <CategoryEditor category={editingCategory.category} onClose={() => setEditingCategory({ open: false, category: null })} />
      ) : null}

      {editingArticle?.open ? (
        <ArticleEditor
          articleId={editingArticle.id}
          defaultCategoryId={activeCategoryId === 'all' ? null : activeCategoryId}
          categories={categoryList}
          onClose={() => setEditingArticle(null)}
        />
      ) : null}

      <ConfirmDialog
        open={confirmDeleteCategory !== null}
        title="Delete Category"
        message={`This removes the "${confirmDeleteCategory?.name ?? ''}" category. Categories with articles cannot be deleted — move or delete those articles first.`}
        confirmLabel="Delete Category"
        danger
        onCancel={() => setConfirmDeleteCategory(null)}
        onConfirm={async () => {
          if (!confirmDeleteCategory) return
          try {
            await deleteCategory.mutateAsync(confirmDeleteCategory.id)
            toast.success('Category deleted')
            if (activeCategoryId === confirmDeleteCategory.id) {
              setActiveCategoryId('all')
            }
          } catch {
            // interceptor surfaces the error toast
          }
          setConfirmDeleteCategory(null)
        }}
      />

      <ConfirmDialog
        open={confirmDeleteArticle !== null}
        title="Delete Article"
        message={`This permanently removes "${confirmDeleteArticle?.title ?? ''}".`}
        confirmLabel="Delete Article"
        danger
        onCancel={() => setConfirmDeleteArticle(null)}
        onConfirm={async () => {
          if (!confirmDeleteArticle) return
          try {
            await deleteArticle.mutateAsync(confirmDeleteArticle.id)
            toast.success('Article deleted')
          } catch {
            // interceptor surfaces the error toast
          }
          setConfirmDeleteArticle(null)
        }}
      />
    </div>
  )
}
