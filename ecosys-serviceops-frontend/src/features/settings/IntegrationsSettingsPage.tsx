import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import toast from 'react-hot-toast'
import { Plus, KeyRound, Webhook, Trash2, Copy, Pencil, ScrollText, Send } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { FormDrawer } from '../../shared/components/FormDrawer'
import { Spinner } from '../../shared/components/Spinner'
import { EmptyState } from '../../shared/components/EmptyState'
import { usePermission } from '../../shared/hooks/usePermission'
import { formatDateTime } from '../../shared/utils/dates'
import { api } from '../../shared/api/apiClient'

interface ApiKeyItem {
  id: string
  label: string
  keyPrefix: string
  createdAt: string
  lastUsedAt: string | null
  status: 'Active' | 'Revoked'
}

interface ApiKeyCreated extends ApiKeyItem {
  apiKey: string
}

interface WebhookItem {
  id: string
  url: string
  events: string[]
  status: 'Active' | 'Disabled'
  createdAt: string
}

interface WebhookCreated extends WebhookItem {
  secret: string
}

interface WebhookDelivery {
  id: string
  event: string
  status: string
  responseStatusCode: number | null
  errorMessage: string | null
  durationMs: number | null
  attemptedAt: string
}

const WEBHOOK_EVENTS = ['work_order.created', 'work_order.completed', 'invoice.issued', 'invoice.paid', 'quotation.accepted']

function useApiKeys() {
  return useQuery({ queryKey: ['settings', 'integrations', 'api-keys'], queryFn: () => api.get<ApiKeyItem[]>('/api/settings/api-keys'), staleTime: 30_000 })
}

function useCreateApiKey() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (label: string) => api.post<ApiKeyCreated>('/api/settings/api-keys', { label }),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['settings', 'integrations', 'api-keys'] }),
  })
}

function useRevokeApiKey() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api.delete(`/api/settings/api-keys/${id}`),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['settings', 'integrations', 'api-keys'] }),
  })
}

function useWebhooks() {
  return useQuery({ queryKey: ['settings', 'integrations', 'webhooks'], queryFn: () => api.get<WebhookItem[]>('/api/settings/webhooks'), staleTime: 30_000 })
}

function useCreateWebhook() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: { url: string; events: string[] }) => api.post<WebhookCreated>('/api/settings/webhooks', payload),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['settings', 'integrations', 'webhooks'] }),
  })
}

function useUpdateWebhook() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, ...payload }: { id: string; url: string; events: string[]; isActive: boolean }) =>
      api.put<WebhookItem>(`/api/settings/webhooks/${id}`, payload),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['settings', 'integrations', 'webhooks'] }),
  })
}

function useDeleteWebhook() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api.delete(`/api/settings/webhooks/${id}`),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['settings', 'integrations', 'webhooks'] }),
  })
}

function useTestWebhook() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api.post<WebhookDelivery>(`/api/settings/webhooks/${id}/test`),
    onSuccess: (_data, id) => void queryClient.invalidateQueries({ queryKey: ['settings', 'integrations', 'webhooks', id, 'delivery-log'] }),
  })
}

function useWebhookDeliveryLog(id: string | null) {
  return useQuery({
    queryKey: ['settings', 'integrations', 'webhooks', id, 'delivery-log'],
    queryFn: () => api.get<WebhookDelivery[]>(`/api/settings/webhooks/${id}/delivery-log`),
    enabled: Boolean(id),
  })
}

function copyToClipboard(value: string) {
  void navigator.clipboard.writeText(value)
  toast.success('Copied to clipboard')
}

export function IntegrationsSettingsPage() {
  const canManage = usePermission('settings.manage')
  const { data: apiKeys, isLoading: keysLoading, isError: keysError } = useApiKeys()
  const { data: webhooks, isLoading: webhooksLoading, isError: webhooksError } = useWebhooks()
  const createApiKey = useCreateApiKey()
  const revokeApiKey = useRevokeApiKey()
  const createWebhook = useCreateWebhook()
  const updateWebhook = useUpdateWebhook()
  const deleteWebhook = useDeleteWebhook()
  const testWebhook = useTestWebhook()

  const [showNewKey, setShowNewKey] = useState(false)
  const [keyLabel, setKeyLabel] = useState('')
  const [revealedKey, setRevealedKey] = useState<string | null>(null)

  // webhook drawer doubles as create + edit
  const [webhookDrawerOpen, setWebhookDrawerOpen] = useState(false)
  const [editingWebhookId, setEditingWebhookId] = useState<string | null>(null)
  const [webhookUrl, setWebhookUrl] = useState('')
  const [webhookEvents, setWebhookEvents] = useState<string[]>([])
  const [webhookActive, setWebhookActive] = useState(true)
  const [revealedSecret, setRevealedSecret] = useState<string | null>(null)

  const [logWebhookId, setLogWebhookId] = useState<string | null>(null)
  const { data: deliveryLog, isLoading: logLoading, isError: logError } = useWebhookDeliveryLog(logWebhookId)

  const submitNewKey = async () => {
    const created = await createApiKey.mutateAsync(keyLabel.trim())
    toast.success('API key generated')
    setShowNewKey(false)
    setKeyLabel('')
    setRevealedKey(created.apiKey)
  }

  const revoke = async (id: string) => {
    await revokeApiKey.mutateAsync(id)
    toast.success('API key revoked')
  }

  const toggleEvent = (event: string) => {
    setWebhookEvents((current) => (current.includes(event) ? current.filter((e) => e !== event) : [...current, event]))
  }

  const openCreateWebhook = () => {
    setEditingWebhookId(null)
    setWebhookUrl('')
    setWebhookEvents([])
    setWebhookActive(true)
    setWebhookDrawerOpen(true)
  }

  const openEditWebhook = (webhook: WebhookItem) => {
    setEditingWebhookId(webhook.id)
    setWebhookUrl(webhook.url)
    setWebhookEvents(webhook.events)
    setWebhookActive(webhook.status === 'Active')
    setWebhookDrawerOpen(true)
  }

  const submitWebhook = async () => {
    try {
      if (editingWebhookId) {
        await updateWebhook.mutateAsync({ id: editingWebhookId, url: webhookUrl.trim(), events: webhookEvents, isActive: webhookActive })
        toast.success('Webhook updated')
        setWebhookDrawerOpen(false)
      } else {
        const created = await createWebhook.mutateAsync({ url: webhookUrl.trim(), events: webhookEvents })
        toast.success('Webhook added')
        setWebhookDrawerOpen(false)
        setRevealedSecret(created.secret)
      }
    } catch {
      // interceptor surfaces the error toast
    }
  }

  const removeWebhook = async (id: string) => {
    await deleteWebhook.mutateAsync(id)
    toast.success('Webhook removed')
  }

  const sendTestPing = async (id: string) => {
    try {
      const delivery = await testWebhook.mutateAsync(id)
      if (delivery.status === 'Success') {
        toast.success('Test ping delivered')
      } else {
        toast.error(`Test ping failed: ${delivery.errorMessage ?? 'endpoint did not respond with success'}`)
      }
    } catch {
      toast.error('Could not send test ping — please try again.')
    }
  }

  const keyColumns: DataTableColumn<ApiKeyItem>[] = [
    { key: 'label', header: 'Label', cell: (row) => <span className="font-medium text-slate-900">{row.label}</span> },
    { key: 'keyPrefix', header: 'Key', cell: (row) => <span className="font-mono text-xs">{row.keyPrefix}••••••••</span> },
    { key: 'createdAt', header: 'Created', cell: (row) => formatDateTime(row.createdAt) },
    { key: 'lastUsedAt', header: 'Last Used', cell: (row) => (row.lastUsedAt ? formatDateTime(row.lastUsedAt) : 'Never') },
    { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.status} /> },
    {
      key: 'actions',
      header: '',
      cell: (row) => (canManage && row.status === 'Active' ? (
        <button type="button" disabled={revokeApiKey.isPending} onClick={() => void revoke(row.id)} className="inline-flex items-center gap-1 rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-xs font-medium text-red-600 hover:bg-red-50 disabled:opacity-60">
          <Trash2 className="h-3.5 w-3.5" />Revoke
        </button>
      ) : null),
    },
  ]

  const webhookColumns: DataTableColumn<WebhookItem>[] = [
    { key: 'url', header: 'Endpoint URL', cell: (row) => <span className="font-mono text-xs">{row.url}</span> },
    { key: 'events', header: 'Events', cell: (row) => row.events.join(', ') },
    { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.status} /> },
    { key: 'createdAt', header: 'Created', cell: (row) => formatDateTime(row.createdAt) },
    {
      key: 'actions',
      header: '',
      cell: (row) => (
        <div className="flex items-center justify-end gap-2">
          <button type="button" onClick={() => setLogWebhookId(row.id)} className="inline-flex items-center gap-1 rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-xs font-medium text-slate-700 hover:bg-slate-50">
            <ScrollText className="h-3.5 w-3.5" />Log
          </button>
          {canManage ? (
            <>
              <button type="button" disabled={testWebhook.isPending} onClick={() => void sendTestPing(row.id)} className="inline-flex items-center gap-1 rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-xs font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-60">
                <Send className="h-3.5 w-3.5" />Test
              </button>
              <button type="button" onClick={() => openEditWebhook(row)} className="inline-flex items-center gap-1 rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-xs font-medium text-slate-700 hover:bg-slate-50">
                <Pencil className="h-3.5 w-3.5" />Edit
              </button>
              <button type="button" disabled={deleteWebhook.isPending} onClick={() => void removeWebhook(row.id)} className="inline-flex items-center gap-1 rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-xs font-medium text-red-600 hover:bg-red-50 disabled:opacity-60">
                <Trash2 className="h-3.5 w-3.5" />Delete
              </button>
            </>
          ) : null}
        </div>
      ),
    },
  ]

  const deliveryColumns: DataTableColumn<WebhookDelivery>[] = [
    { key: 'attemptedAt', header: 'When', cell: (row) => formatDateTime(row.attemptedAt) },
    { key: 'event', header: 'Event', cell: (row) => <span className="font-mono text-xs">{row.event}</span> },
    { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.status} /> },
    { key: 'responseStatusCode', header: 'HTTP', cell: (row) => (row.responseStatusCode != null ? String(row.responseStatusCode) : '—') },
    { key: 'durationMs', header: 'Duration', cell: (row) => (row.durationMs != null ? `${row.durationMs} ms` : '—') },
    { key: 'errorMessage', header: 'Error', cell: (row) => row.errorMessage ?? '—' },
  ]

  return (
    <div className="space-y-6">
      <PageHeader title="Integrations" subtitle="API access keys and outbound webhooks for third-party integrations." />

      {revealedKey ? (
        <div className="flex items-center justify-between gap-3 rounded-2xl border border-amber-300 bg-amber-50 px-4 py-3 text-sm text-amber-900">
          <div>
            <p className="font-medium">Copy this key now — it won't be shown again.</p>
            <p className="mt-1 font-mono text-xs">{revealedKey}</p>
          </div>
          <div className="flex shrink-0 items-center gap-2">
            <button type="button" onClick={() => copyToClipboard(revealedKey)} className="inline-flex h-9 items-center gap-1.5 rounded-lg bg-[var(--app-primary)] px-3 text-xs font-medium text-white hover:bg-[var(--app-secondary)]"><Copy className="h-3.5 w-3.5" />Copy</button>
            <button type="button" onClick={() => setRevealedKey(null)} className="text-xs font-medium text-amber-900 hover:underline">Dismiss</button>
          </div>
        </div>
      ) : null}

      {revealedSecret ? (
        <div className="flex items-center justify-between gap-3 rounded-2xl border border-amber-300 bg-amber-50 px-4 py-3 text-sm text-amber-900">
          <div>
            <p className="font-medium">Copy this signing secret now — it won't be shown again.</p>
            <p className="mt-1 font-mono text-xs">{revealedSecret}</p>
          </div>
          <div className="flex shrink-0 items-center gap-2">
            <button type="button" onClick={() => copyToClipboard(revealedSecret)} className="inline-flex h-9 items-center gap-1.5 rounded-lg bg-[var(--app-primary)] px-3 text-xs font-medium text-white hover:bg-[var(--app-secondary)]"><Copy className="h-3.5 w-3.5" />Copy</button>
            <button type="button" onClick={() => setRevealedSecret(null)} className="text-xs font-medium text-amber-900 hover:underline">Dismiss</button>
          </div>
        </div>
      ) : null}

      <div className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-5 shadow-sm">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <KeyRound className="h-4 w-4 text-slate-400" />
            <h2 className="text-sm font-semibold uppercase tracking-[0.18em] text-slate-500">API Keys</h2>
          </div>
          {canManage ? (
            <button type="button" onClick={() => setShowNewKey(true)} className="inline-flex h-10 items-center gap-2 rounded-lg bg-[var(--app-primary)] px-3 text-sm font-medium text-white hover:bg-[var(--app-secondary)]">
              <Plus className="h-4 w-4" />Generate Key
            </button>
          ) : null}
        </div>
        <div className="mt-4">
          <DataTable columns={keyColumns} data={apiKeys ?? []} loading={keysLoading} emptyTitle={keysError ? 'Could not load API keys' : 'No API keys yet'} emptyDescription={keysError ? 'Something went wrong loading keys — try refreshing.' : 'Keys issued for your tenant will appear here.'} />
        </div>
      </div>

      <div className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-5 shadow-sm">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <Webhook className="h-4 w-4 text-slate-400" />
            <h2 className="text-sm font-semibold uppercase tracking-[0.18em] text-slate-500">Outbound Webhooks</h2>
          </div>
          {canManage ? (
            <button type="button" onClick={openCreateWebhook} className="inline-flex h-10 items-center gap-2 rounded-lg bg-[var(--app-primary)] px-3 text-sm font-medium text-white hover:bg-[var(--app-secondary)]">
              <Plus className="h-4 w-4" />Add Webhook
            </button>
          ) : null}
        </div>
        <div className="mt-4">
          <DataTable columns={webhookColumns} data={webhooks ?? []} loading={webhooksLoading} emptyTitle={webhooksError ? 'Could not load webhooks' : 'No webhooks configured'} emptyDescription={webhooksError ? 'Something went wrong loading webhooks — try refreshing.' : 'Outbound event webhooks (work order created, invoice paid, etc.) will appear here.'} />
        </div>
      </div>

      <FormDrawer open={showNewKey} title="Generate API Key" onClose={() => setShowNewKey(false)}>
        <div className="space-y-4">
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Label</label>
            <input value={keyLabel} onChange={(event) => setKeyLabel(event.target.value)} placeholder="e.g. Zapier integration" className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]" />
          </div>
          <button type="button" disabled={!keyLabel.trim() || createApiKey.isPending} onClick={() => void submitNewKey()} className="inline-flex h-11 w-full items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white hover:bg-[var(--app-secondary)] disabled:opacity-60">Generate Key</button>
        </div>
      </FormDrawer>

      <FormDrawer open={webhookDrawerOpen} title={editingWebhookId ? 'Edit Webhook' : 'Add Webhook'} onClose={() => setWebhookDrawerOpen(false)}>
        <div className="space-y-4">
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Endpoint URL</label>
            <input value={webhookUrl} onChange={(event) => setWebhookUrl(event.target.value)} placeholder="https://example.com/webhooks/ecosys" className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]" />
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Events</label>
            <div className="space-y-2">
              {WEBHOOK_EVENTS.map((event) => (
                <label key={event} className="flex items-center gap-2 text-sm text-slate-700">
                  <input type="checkbox" checked={webhookEvents.includes(event)} onChange={() => toggleEvent(event)} className="h-4 w-4 rounded border-slate-300" />
                  {event}
                </label>
              ))}
            </div>
          </div>
          {editingWebhookId ? (
            <label className="flex items-center gap-2 text-sm text-slate-700">
              <input type="checkbox" checked={webhookActive} onChange={(event) => setWebhookActive(event.target.checked)} className="h-4 w-4 rounded border-slate-300" />
              Active
            </label>
          ) : null}
          <button type="button" disabled={!webhookUrl.trim() || webhookEvents.length === 0 || createWebhook.isPending || updateWebhook.isPending} onClick={() => void submitWebhook()} className="inline-flex h-11 w-full items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white hover:bg-[var(--app-secondary)] disabled:opacity-60">
            {editingWebhookId ? 'Save Changes' : 'Add Webhook'}
          </button>
        </div>
      </FormDrawer>

      <FormDrawer open={logWebhookId !== null} title="Webhook Delivery Log" onClose={() => setLogWebhookId(null)}>
        {logLoading ? (
          <Spinner label="Loading delivery log" />
        ) : logError ? (
          <EmptyState title="Could not load delivery log" description="Something went wrong — try again." />
        ) : (deliveryLog ?? []).length === 0 ? (
          <EmptyState icon={ScrollText} title="No deliveries yet" description="Delivery attempts for this webhook will appear here once events fire." />
        ) : (
          <DataTable columns={deliveryColumns} data={deliveryLog ?? []} />
        )}
      </FormDrawer>
    </div>
  )
}
