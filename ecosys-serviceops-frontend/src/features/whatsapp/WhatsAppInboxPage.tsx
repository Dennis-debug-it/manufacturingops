import { useState } from 'react'
import toast from 'react-hot-toast'
import { Send, MessageCircle } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { EmptyState } from '../../shared/components/EmptyState'
import { formatDateTime } from '../../shared/utils/dates'
import { useSendWhatsAppMessage, useWhatsAppConversation, useWhatsAppConversations } from './api'

const backendGapNote = 'No WhatsAppController exists in the current backend yet. This is the full intended inbox UI wired against typed stub data — messages will not send until a real WhatsApp Business API integration is connected.'

export function WhatsAppInboxPage() {
  const { data: conversations, isLoading } = useWhatsAppConversations()
  const [selectedId, setSelectedId] = useState<string | undefined>(undefined)
  const { data: detail } = useWhatsAppConversation(selectedId)
  const sendMessage = useSendWhatsAppMessage(selectedId ?? '')
  const [draft, setDraft] = useState('')

  const send = async () => {
    try {
      await sendMessage.mutateAsync(draft)
      setDraft('')
    } catch {
      toast.error('WhatsApp sending is not yet backed by a live API — this is a UI stub.')
    }
  }

  return (
    <div className="space-y-6">
      <PageHeader title="WhatsApp Inbox" subtitle="Two-way WhatsApp Business conversations with clients and technicians." />
      <div className="rounded-2xl border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-900">{backendGapNote}</div>

      <div className="grid grid-cols-1 gap-4 lg:grid-cols-[320px_minmax(0,1fr)]">
        <div className="rounded-2xl border border-slate-200 bg-white shadow-sm">
          <div className="border-b border-slate-200 px-4 py-3 text-sm font-semibold text-slate-700">Conversations</div>
          {isLoading ? null : (conversations ?? []).length === 0 ? (
            <div className="p-4">
              <EmptyState icon={MessageCircle} title="No conversations yet" description="Inbound and outbound WhatsApp threads will appear here once connected." />
            </div>
          ) : (
            <ul className="divide-y divide-slate-100">
              {(conversations ?? []).map((conversation) => (
                <li key={conversation.id}>
                  <button
                    type="button"
                    onClick={() => setSelectedId(conversation.id)}
                    className={`flex w-full flex-col items-start gap-1 px-4 py-3 text-left transition hover:bg-slate-50 ${selectedId === conversation.id ? 'bg-slate-50' : ''}`}
                  >
                    <span className="text-sm font-medium text-slate-900">{conversation.contactName}</span>
                    <span className="line-clamp-1 text-xs text-slate-500">{conversation.lastMessagePreview}</span>
                    <span className="text-xs text-slate-400">{formatDateTime(conversation.lastMessageAt)}</span>
                  </button>
                </li>
              ))}
            </ul>
          )}
        </div>

        <div className="flex min-h-[420px] flex-col rounded-2xl border border-slate-200 bg-white shadow-sm">
          {!detail ? (
            <div className="flex flex-1 items-center justify-center p-8">
              <EmptyState icon={MessageCircle} title="Select a conversation" description="Choose a thread from the left to view its timeline." />
            </div>
          ) : (
            <>
              <div className="border-b border-slate-200 px-5 py-3">
                <p className="text-sm font-semibold text-slate-900">{detail.contactName}</p>
                <p className="text-xs text-slate-500">{detail.contactPhone}</p>
              </div>
              <div className="flex-1 space-y-3 overflow-y-auto px-5 py-4">
                {detail.messages.map((message) => (
                  <div key={message.id} className={`flex ${message.direction === 'Outbound' ? 'justify-end' : 'justify-start'}`}>
                    <div className={`max-w-[70%] rounded-2xl px-4 py-2 text-sm ${message.direction === 'Outbound' ? 'bg-slate-900 text-white' : 'bg-slate-100 text-slate-800'}`}>
                      <p>{message.body}</p>
                      <p className={`mt-1 text-[11px] ${message.direction === 'Outbound' ? 'text-slate-300' : 'text-slate-400'}`}>{formatDateTime(message.sentAt)} · {message.status}</p>
                    </div>
                  </div>
                ))}
              </div>
              <div className="flex items-center gap-2 border-t border-slate-200 px-4 py-3">
                <input
                  value={draft}
                  onChange={(event) => setDraft(event.target.value)}
                  placeholder="Type a message"
                  className="h-11 flex-1 rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900"
                />
                <button
                  type="button"
                  disabled={!draft.trim() || sendMessage.isPending}
                  onClick={() => void send()}
                  className="inline-flex h-11 items-center gap-2 rounded-lg bg-slate-900 px-4 text-sm font-medium text-white hover:bg-slate-800 disabled:opacity-60"
                >
                  <Send className="h-4 w-4" />Send
                </button>
              </div>
            </>
          )}
        </div>
      </div>
    </div>
  )
}
