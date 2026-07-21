import { useState } from 'react'
import toast from 'react-hot-toast'
import { Eye, Lock, Send } from 'lucide-react'
import { EmptyState } from '../../../shared/components/EmptyState'
import { Spinner } from '../../../shared/components/Spinner'
import { formatDateTime, formatRelative } from '../../../shared/utils/dates'
import { cn } from '../../../shared/utils/cn'
import { useAddWorkOrderNote, useWorkOrderNotes } from '../api'

export function NotesTab({ workOrderId }: { workOrderId: string }) {
  const { data, isLoading, isError } = useWorkOrderNotes(workOrderId)
  const addNote = useAddWorkOrderNote(workOrderId)
  const [content, setContent] = useState('')
  const [isClientVisible, setIsClientVisible] = useState(false)

  const submit = async () => {
    if (!content.trim()) return
    try {
      await addNote.mutateAsync({ content: content.trim(), isClientVisible })
      toast.success(isClientVisible ? 'Client-visible note added' : 'Internal note added')
      setContent('')
      setIsClientVisible(false)
    } catch {
      // interceptor surfaces the error toast
    }
  }

  if (isLoading) {
    return <Spinner label="Loading notes" />
  }

  if (isError) {
    return <EmptyState title="Notes unavailable" description="Notes could not be loaded for this work order." />
  }

  const notes = data ?? []

  return (
    <div className="max-w-2xl space-y-6">
      <div className="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm">
        <label htmlFor="wo-note" className="mb-2 block text-sm font-medium text-slate-700">Add Note</label>
        <textarea
          id="wo-note"
          value={content}
          onChange={(event) => setContent(event.target.value)}
          rows={3}
          placeholder="Write a note…"
          className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none transition focus:border-slate-900"
        />
        <div className="mt-3 flex flex-wrap items-center justify-between gap-3">
          <div className="inline-flex items-center rounded-lg border border-slate-200 p-1">
            <button
              type="button"
              onClick={() => setIsClientVisible(false)}
              className={cn(
                'inline-flex items-center gap-1.5 rounded-md px-3 py-1.5 text-xs font-medium transition',
                !isClientVisible ? 'bg-slate-900 text-white' : 'text-slate-600 hover:bg-slate-50',
              )}
            >
              <Lock className="h-3.5 w-3.5" />
              Internal
            </button>
            <button
              type="button"
              onClick={() => setIsClientVisible(true)}
              className={cn(
                'inline-flex items-center gap-1.5 rounded-md px-3 py-1.5 text-xs font-medium transition',
                isClientVisible ? 'bg-sky-600 text-white' : 'text-slate-600 hover:bg-slate-50',
              )}
            >
              <Eye className="h-3.5 w-3.5" />
              Client-visible
            </button>
          </div>
          <button
            type="button"
            onClick={submit}
            disabled={addNote.isPending || !content.trim()}
            className="inline-flex h-10 items-center justify-center gap-2 rounded-lg bg-slate-900 px-4 text-sm font-medium text-white transition hover:bg-slate-800 disabled:cursor-not-allowed disabled:opacity-60"
          >
            <Send className="h-4 w-4" />
            Post Note
          </button>
        </div>
      </div>

      {notes.length === 0 ? (
        <EmptyState title="No notes yet" description="Internal and client-visible notes for this work order will appear here." />
      ) : (
        <ul className="space-y-3">
          {notes.map((note) => (
            <li key={note.id} className="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm">
              <div className="flex flex-wrap items-center justify-between gap-2">
                <span className="flex items-center gap-1.5 text-sm font-medium text-slate-900">
                  {note.isClientVisible ? (
                    <Eye className="h-3.5 w-3.5 text-sky-600" />
                  ) : (
                    <Lock className="h-3.5 w-3.5 text-slate-400" />
                  )}
                  {note.createdByUserName ?? 'Unknown user'}
                  <span
                    className={cn(
                      'ml-1 rounded-full px-2 py-0.5 text-[10px] font-medium ring-1 ring-inset',
                      note.isClientVisible
                        ? 'bg-sky-50 text-sky-700 ring-sky-200'
                        : 'bg-slate-100 text-slate-600 ring-slate-200',
                    )}
                  >
                    {note.isClientVisible ? 'Client-visible' : 'Internal'}
                  </span>
                </span>
                <span className="text-xs text-slate-500" title={formatDateTime(note.createdAt)}>
                  {formatRelative(note.createdAt)}
                </span>
              </div>
              <p className="mt-2 whitespace-pre-wrap text-sm leading-6 text-slate-700">{note.content}</p>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
