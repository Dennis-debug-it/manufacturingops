import { useState } from 'react'
import toast from 'react-hot-toast'
import { Bookmark, Trash2 } from 'lucide-react'
import { usePermission } from '../../../shared/hooks/usePermission'
import { useDeleteView, useSavedViews, useSaveView } from '../api'

/**
 * Saved pipeline views: a name for the current filters (stored as the page's query string). Managers
 * can share a view with everyone; picking one applies its filters.
 */
export function SavedViewsMenu({ currentQuery, onApply }: { currentQuery: string; onApply: (query: string) => void }) {
  const { data: views } = useSavedViews()
  const save = useSaveView()
  const remove = useDeleteView()
  const canShare = usePermission('crm.view_all')
  const [name, setName] = useState('')
  const [shared, setShared] = useState(false)

  const submit = async () => {
    if (!name.trim()) return void toast.error('Name the view')
    try {
      await save.mutateAsync({ name: name.trim(), query: currentQuery, isShared: shared })
      toast.success('View saved')
      setName('')
      setShared(false)
    } catch {
      // interceptor surfaces the error toast
    }
  }

  return (
    <details className="relative self-start sm:self-auto">
      <summary className="inline-flex h-10 cursor-pointer list-none items-center gap-1.5 rounded-lg border border-slate-200 px-3 text-sm font-medium text-slate-600 transition hover:bg-slate-50">
        <Bookmark className="h-4 w-4" />Views
      </summary>
      <div className="absolute right-0 z-20 mt-1 w-72 rounded-lg border border-slate-200 bg-white p-2 shadow-lg">
        {(views ?? []).length === 0 ? <p className="px-2 py-1.5 text-sm text-slate-500">No saved views yet.</p> : null}
        <ul>
          {(views ?? []).map((view) => (
            <li key={view.id} className="flex items-center gap-1">
              <button
                type="button"
                onClick={(event) => {
                  onApply(view.query)
                  event.currentTarget.closest('details')?.removeAttribute('open')
                }}
                className="min-w-0 flex-1 truncate rounded-md px-2 py-1.5 text-left text-sm text-slate-700 hover:bg-slate-50"
              >
                {view.name}{view.isShared ? <span className="ml-1 text-xs text-slate-400">(shared)</span> : null}
              </button>
              {view.isMine ? (
                <button type="button" aria-label={`Delete ${view.name}`} disabled={remove.isPending} onClick={() => remove.mutate(view.id)} className="rounded p-1.5 text-slate-400 hover:bg-rose-50 hover:text-rose-600">
                  <Trash2 className="h-3.5 w-3.5" />
                </button>
              ) : null}
            </li>
          ))}
        </ul>
        <div className="mt-2 space-y-2 border-t border-slate-100 pt-2">
          <input aria-label="View name" value={name} onChange={(event) => setName(event.target.value)} placeholder="Save these filters as…" className="h-9 w-full rounded-lg border border-slate-300 px-2.5 text-sm outline-none focus:border-[var(--app-primary)]" />
          <div className="flex items-center justify-between gap-2">
            {canShare ? (
              <label className="flex items-center gap-1.5 text-xs text-slate-600">
                <input type="checkbox" checked={shared} onChange={(event) => setShared(event.target.checked)} />Share with everyone
              </label>
            ) : <span />}
            <button type="button" disabled={save.isPending} onClick={() => void submit()} className="inline-flex h-8 items-center rounded-lg bg-[var(--app-primary)] px-3 text-xs font-medium text-white disabled:opacity-60">Save</button>
          </div>
        </div>
      </div>
    </details>
  )
}
