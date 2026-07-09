import { Bot, Send, X } from 'lucide-react'
import { useState } from 'react'
import { kipService } from '../../services/kipService'
import type { KipContext } from '../../types/api'

export function KipPanel({
  open,
  context,
  title,
  onClose,
}: {
  open: boolean
  context: KipContext
  title: string
  onClose: () => void
}) {
  const [message, setMessage] = useState('')
  const [response, setResponse] = useState('Fundi is coming soon. This is where your assistant will help you here.')
  const [loading, setLoading] = useState(false)

  async function submit() {
    if (!message.trim() || loading) return
    setLoading(true)
    try {
      const result = await kipService.query({ context, message: message.trim() })
      setResponse(result.response)
    } finally {
      setLoading(false)
    }
  }

  return (
    <>
      {open ? <button type="button" className="fixed inset-0 z-40 bg-slate-950/35" onClick={onClose} aria-label="Close Fundi panel" /> : null}
      <aside className={`fixed right-0 top-0 z-50 h-full w-full max-w-[380px] border-l border-app bg-[var(--app-card)] shadow-[var(--app-shadow-soft)] transition-transform duration-200 ${open ? 'translate-x-0' : 'translate-x-full'}`}>
        <div className="flex h-full flex-col">
          <div className="flex items-center justify-between border-b border-app px-5 py-4">
            <div className="min-w-0">
              <p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted">Fundi Assistant</p>
              <h2 className="mt-1 truncate text-lg font-semibold text-app">{title}</h2>
            </div>
            <button type="button" className="button-secondary px-3 py-2" onClick={onClose}>
              <X className="h-4 w-4" />
            </button>
          </div>

          <div className="flex-1 space-y-4 overflow-y-auto px-5 py-5">
            <div className="rounded-[24px] border border-dashed border-app bg-[var(--app-surface)] p-4">
              <div className="flex items-start gap-3">
                <div className="rounded-2xl bg-emerald-500/10 p-2 text-emerald-700">
                  <Bot className="h-4 w-4" />
                </div>
                <div>
                  <p className="text-sm font-semibold text-app">Fundi is live</p>
                  <p className="mt-1 text-sm leading-6 text-muted">{response}</p>
                </div>
              </div>
            </div>

            <div className="rounded-[24px] border border-app bg-[var(--app-surface)] p-4">
              <p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted">Context</p>
              <p className="mt-2 text-sm text-app">{context.screen}</p>
              {context.entityType ? <p className="mt-1 text-xs text-muted">{context.entityType} {context.entityId || ''}</p> : null}
            </div>
          </div>

          <div className="border-t border-app px-5 py-4">
            <label className="block space-y-2">
              <span className="text-sm font-medium text-app">Ask a question</span>
              <textarea
                value={message}
                onChange={(event) => setMessage(event.target.value)}
                className="field-input min-h-[110px]"
                placeholder="Ask about this screen or record..."
              />
            </label>
            <div className="mt-3 flex justify-end">
              <button type="button" className="button-primary" onClick={() => void submit()} disabled={loading || !message.trim()}>
                <Send className="h-4 w-4" />
                Send
              </button>
            </div>
          </div>
        </div>
      </aside>
    </>
  )
}
