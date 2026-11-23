import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../../shared/api/apiClient'
import { endpoints } from '../../shared/api/endpoints'
import { notificationKeys } from '../notifications/api'
import type { WhatsAppOutboxStatus } from './reminderLogic'

/** The signed-in person's own WhatsApp opt-in . */
export interface WhatsAppMyStatus {
  /** The workspace has WhatsApp and the shared Ecosys number is set up. */
  available: boolean
  featureEnabled: boolean
  configured: boolean
  /** The shared Ecosys number messages come from. */
  sharedNumber: string | null
  number: string | null
  verifiedAt: string | null
  optedIn: boolean
  /** The number replied STOP; confirming it again lifts that. */
  blockedByStop: boolean
  pendingNumber: string | null
  pendingExpiresAt: string | null
  defaultCallingCode: string
}

export interface WhatsAppStartResult {
  phoneNumber: string
  expiresAt: string
}

/** What the opt-in card needs; the staff and portal versions differ only in their endpoints. */
export interface WhatsAppOptInApi {
  queryKey: readonly unknown[]
  get: () => Promise<WhatsAppMyStatus>
  start: (phoneNumber: string) => Promise<WhatsAppStartResult>
  verify: (code: string) => Promise<WhatsAppMyStatus>
  optIn?: () => Promise<WhatsAppMyStatus>
  optOut: () => Promise<WhatsAppMyStatus>
  /** The portal client has no global error toast, so the card shows errors itself. */
  showErrors: boolean
}

export const staffWhatsAppApi: WhatsAppOptInApi = {
  queryKey: ['me', 'whatsapp'],
  get: () => api.get<WhatsAppMyStatus>(endpoints.me.whatsApp, undefined, { suppressGlobalErrorToast: true }),
  start: (phoneNumber) => api.post<WhatsAppStartResult>(endpoints.me.whatsAppStart, { phoneNumber }),
  verify: (code) => api.post<WhatsAppMyStatus>(endpoints.me.whatsAppVerify, { code }),
  optIn: () => api.post<WhatsAppMyStatus>(endpoints.me.whatsAppOptIn),
  optOut: () => api.post<WhatsAppMyStatus>(endpoints.me.whatsAppOptOut),
  showErrors: false,
}

export function useWhatsAppMyStatus(source: WhatsAppOptInApi) {
  return useQuery({ queryKey: source.queryKey, queryFn: source.get, retry: false })
}

/** Every change returns the new status; opting in also turns WhatsApp on for keys, so preferences reload. */
export function useWhatsAppOptInMutations(source: WhatsAppOptInApi) {
  const queryClient = useQueryClient()
  const refresh = (status?: WhatsAppMyStatus) => {
    if (status) queryClient.setQueryData(source.queryKey, status)
    else void queryClient.invalidateQueries({ queryKey: source.queryKey })
    void queryClient.invalidateQueries({ queryKey: notificationKeys.preferences })
  }
  return {
    start: useMutation({ mutationFn: source.start, onSuccess: () => refresh() }),
    verify: useMutation({ mutationFn: source.verify, onSuccess: refresh }),
    optIn: useMutation({ mutationFn: () => (source.optIn ? source.optIn() : source.get()), onSuccess: refresh }),
    optOut: useMutation({ mutationFn: source.optOut, onSuccess: refresh }),
  }
}

// ── Workspace settings (admin) ───────────────────────────────────────────────

export interface WhatsAppKeySetting {
  key: string
  label: string
  audience: 'Staff'
  templateName: string
  allowed: boolean
  isDefault: boolean
}

export interface WhatsAppTenantSettings {
  featureEnabled: boolean
  configured: boolean
  sharedNumber: string | null
  monthlyCap: number
  usedThisMonth: number
  keys: WhatsAppKeySetting[]
}

export interface WhatsAppOutboxItem {
  id: string
  key: string
  label: string
  templateName: string
  /** Masked, e.g. +2547••••5678. */
  to: string
  recipientName: string | null
  status: WhatsAppOutboxStatus
  attemptCount: number
  createdAt: string
  sentAt: string | null
  deliveredAt: string | null
  readAt: string | null
  failedAt: string | null
  error: string | null
}

export interface WhatsAppOutboxPage {
  items: WhatsAppOutboxItem[]
  hasMore: boolean
}

const settingsKey = ['whatsapp-notifications', 'settings'] as const

export function useWhatsAppTenantSettings() {
  return useQuery({ queryKey: settingsKey, queryFn: () => api.get<WhatsAppTenantSettings>(endpoints.whatsAppNotifications.settings) })
}

export function useSaveWhatsAppTenantSettings() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (allowedKeys: string[]) => api.put<WhatsAppTenantSettings>(endpoints.whatsAppNotifications.settings, { allowedKeys }),
    onSuccess: (data) => {
      queryClient.setQueryData(settingsKey, data)
      void queryClient.invalidateQueries({ queryKey: notificationKeys.preferences })
    },
  })
}

export function useWhatsAppOutbox(status: string) {
  return useInfiniteQuery({
    queryKey: ['whatsapp-notifications', 'outbox', status],
    queryFn: ({ pageParam }) =>
      api.get<WhatsAppOutboxPage>(endpoints.whatsAppNotifications.outbox, { status: status || undefined, before: pageParam ?? undefined, limit: 50 }),
    initialPageParam: null as string | null,
    getNextPageParam: (last) => (last.hasMore ? last.items[last.items.length - 1]?.createdAt ?? null : null),
  })
}

