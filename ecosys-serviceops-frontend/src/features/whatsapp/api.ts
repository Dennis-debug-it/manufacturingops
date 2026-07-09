import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

// TODO(backend): No WhatsAppController exists yet (confirmed against the 67-controller snapshot
// in FRONTEND_BUILD_PLAN.md §4.5 / backend-gap table). Every hook below is a typed stub returning
// empty/default data — swap the queryFn/mutationFn bodies for real `api.get`/`api.post` calls once
// a backend WhatsApp Business API integration exists.

export type WhatsAppMessageDirection = 'Inbound' | 'Outbound'
export type WhatsAppMessageStatus = 'Sent' | 'Delivered' | 'Read' | 'Failed'

export interface WhatsAppConversationListItem {
  id: string
  contactName: string
  contactPhone: string
  clientId: string | null
  lastMessagePreview: string
  lastMessageAt: string
  unreadCount: number
}

export function useWhatsAppConversations() {
  return useQuery({
    queryKey: ['whatsapp', 'conversations'],
    queryFn: async (): Promise<WhatsAppConversationListItem[]> => [],
    staleTime: Infinity,
  })
}

export interface WhatsAppTimelineMessage {
  id: string
  direction: WhatsAppMessageDirection
  body: string
  sentAt: string
  status: WhatsAppMessageStatus
  templateName: string | null
}

export interface WhatsAppConversationDetail {
  id: string
  contactName: string
  contactPhone: string
  clientId: string | null
  messages: WhatsAppTimelineMessage[]
}

export function useWhatsAppConversation(id: string | undefined) {
  return useQuery({
    queryKey: ['whatsapp', 'conversations', id],
    queryFn: async (): Promise<WhatsAppConversationDetail | null> => null,
    enabled: Boolean(id),
    staleTime: Infinity,
  })
}

export function useSendWhatsAppMessage(_conversationId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (_body: string): Promise<void> => {
      throw new Error('WhatsApp messages cannot be sent yet — no backend WhatsAppController exists.')
    },
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['whatsapp', 'conversations'] }),
  })
}

export interface WhatsAppSettings {
  businessAccountId: string
  phoneNumberId: string
  accessToken: string
  webhookVerifyToken: string
  webhookUrl: string
  isEnabled: boolean
  autoReplyEnabled: boolean
  autoReplyMessage: string
}

const defaultWhatsAppSettings: WhatsAppSettings = {
  businessAccountId: '',
  phoneNumberId: '',
  accessToken: '',
  webhookVerifyToken: '',
  webhookUrl: '',
  isEnabled: false,
  autoReplyEnabled: false,
  autoReplyMessage: 'Thanks for reaching out — a member of our team will respond shortly.',
}

export function useWhatsAppSettings() {
  return useQuery({
    queryKey: ['settings', 'whatsapp'],
    queryFn: async (): Promise<WhatsAppSettings> => defaultWhatsAppSettings,
    staleTime: Infinity,
  })
}

export function useSaveWhatsAppSettings() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (_settings: WhatsAppSettings): Promise<void> => {
      throw new Error('WhatsApp settings cannot be saved yet — no backend endpoint exists.')
    },
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['settings', 'whatsapp'] }),
  })
}

export interface WhatsAppTemplate {
  id: string
  name: string
  category: 'Utility' | 'Marketing' | 'Authentication'
  language: string
  status: 'Approved' | 'Pending' | 'Rejected'
  bodyPreview: string
}

export function useWhatsAppTemplates() {
  return useQuery({
    queryKey: ['whatsapp', 'templates'],
    queryFn: async (): Promise<WhatsAppTemplate[]> => [],
    staleTime: Infinity,
  })
}
