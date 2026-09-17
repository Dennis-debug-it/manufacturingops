import { useState } from 'react'
import toast from 'react-hot-toast'
import { Send, MessageCircle, Plus } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { EmptyState } from '../../shared/components/EmptyState'
import { FormDrawer } from '../../shared/components/FormDrawer'
import { formatDateTime } from '../../shared/utils/dates'
import { useCreateWhatsAppConversation, useSendWhatsAppMessage, useWhatsAppConversation, useWhatsAppConversations } from './api'

export function WhatsAppInboxPage() {
  const { data: conversations, isLoading } = useWhatsAppConversations()
  const [selectedId, setSelectedId] = useState<string | undefined>(undefined)
  const { data: detail } = useWhatsAppConversation(selectedId)
  const sendMessage = useSendWhatsAppMessage(selectedId ?? '')
  const createConversation = useCreateWhatsAppConversation()
  const [draft, setDraft] = useState('')
  const [showNew, setShowNew] = useState(false)
  const [contactName, setContactName] = useState('')
  const [contactPhone, setContactPhone] = useState('')

  const send = async () => {
    await sendMessage.mutateAsync(draft)
    setDraft('')
  }

  const startConversation = async () => {
    if (!contactPhone.trim()) return toast.error('Contact phone is required')
    const created = await createConversation.mutateAsync({ contactName: contactName || contactPhone, contactPhone })
    toast.success('Conversation started')
    setShowNew(false)
    setContactName('')
    setContactPhone('')
    setSelectedId(created.id)
  }

  return (
    <div className="space-y-4">
      <PageHeader
        title="WhatsApp Inbox"
        subtitle="Two-way WhatsApp Business conversations with clients and technicians."
        primaryAction={
          <button type="button" onClick={() => setShowNew(true)} className="inline-flex h-11 items-center gap-2 rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)]">
            <Plus className="h-4 w-4" />New Conversation
          </button>
        }
      />

      <div className="grid grid-cols-1 gap-4 lg:grid-cols-[320px_minmax(0,1fr)]">
        <div className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] shadow-sm">
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

        <div className="flex min-h-[420px] flex-col rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] shadow-sm">
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
                    <div className={`max-w-[70%] rounded-2xl px-4 py-2 text-sm ${message.direction === 'Outbound' ? 'bg-[var(--app-primary)] text-white' : 'bg-slate-100 text-slate-800'}`}>
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
                  className="h-11 flex-1 rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]"
                />
                <button
                  type="button"
                  disabled={!draft.trim() || sendMessage.isPending}
                  onClick={() => void send()}
                  className="inline-flex h-11 items-center gap-2 rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white hover:bg-[var(--app-secondary)] disabled:opacity-60"
                >
                  <Send className="h-4 w-4" />Send
                </button>
              </div>
            </>
          )}
        </div>
      </div>

      <FormDrawer open={showNew} title="New Conversation" onClose={() => setShowNew(false)}>
        <div className="space-y-4">
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Contact Name</label>
            <input value={contactName} onChange={(event) => setContactName(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]" />
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Contact Phone</label>
            <input value={contactPhone} onChange={(event) => setContactPhone(event.target.value)} placeholder="+2547…" className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]" />
          </div>
          <button
            type="button"
            disabled={!contactPhone.trim() || createConversation.isPending}
            onClick={() => void startConversation()}
            className="inline-flex h-11 w-full items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white hover:bg-[var(--app-secondary)] disabled:opacity-60"
          >
            {createConversation.isPending ? 'Starting…' : 'Start Conversation'}
          </button>
        </div>
      </FormDrawer>
    </div>
  )
}
