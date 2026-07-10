import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../../shared/api/apiClient'

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
    queryFn: () => api.get<WhatsAppConversationListItem[]>('/api/whatsapp/conversations'),
    staleTime: 15_000,
  })
}

export interface CreateWhatsAppConversationPayload {
  contactName: string
  contactPhone: string
  clientId?: string | undefined
}

export function useCreateWhatsAppConversation() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: CreateWhatsAppConversationPayload) =>
      api.post<WhatsAppConversationListItem>('/api/whatsapp/conversations', payload),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['whatsapp', 'conversations'] }),
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
    queryFn: () => api.get<WhatsAppConversationDetail>(`/api/whatsapp/conversations/${id}`),
    enabled: Boolean(id),
    staleTime: 10_000,
  })
}

export function useSendWhatsAppMessage(conversationId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (body: string) =>
      api.post<WhatsAppTimelineMessage>(`/api/whatsapp/conversations/${conversationId}/messages`, { body }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['whatsapp', 'conversations', conversationId] })
      void queryClient.invalidateQueries({ queryKey: ['whatsapp', 'conversations'] })
    },
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

export function useWhatsAppSettings() {
  return useQuery({
    queryKey: ['settings', 'whatsapp'],
    queryFn: () => api.get<WhatsAppSettings>('/api/whatsapp/settings'),
    staleTime: 30_000,
  })
}

export function useSaveWhatsAppSettings() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (settings: WhatsAppSettings) => api.put<WhatsAppSettings>('/api/whatsapp/settings', settings),
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
    queryFn: () => api.get<WhatsAppTemplate[]>('/api/whatsapp/templates'),
    staleTime: 30_000,
  })
}
