import { useQuery } from '@tanstack/react-query'
import toast from 'react-hot-toast'
import { Plus, KeyRound, Webhook } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { EmptyState } from '../../shared/components/EmptyState'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { usePermission } from '../../shared/hooks/usePermission'
import { formatDateTime } from '../../shared/utils/dates'

// TODO(backend): No tenant-level Integrations endpoint (API keys, outbound webhooks) exists yet.
// These hooks return typed empty data until a real endpoint is available.
interface ApiKeyItem {
  id: string
  label: string
  keyPrefix: string
  createdAt: string
  lastUsedAt: string | null
  status: 'Active' | 'Revoked'
}

interface WebhookItem {
  id: string
  url: string
  events: string[]
  status: 'Active' | 'Disabled'
  createdAt: string
}

function useApiKeys() {
  return useQuery({ queryKey: ['settings', 'integrations', 'api-keys'], queryFn: async (): Promise<ApiKeyItem[]> => [], staleTime: Infinity })
}

function useWebhooks() {
  return useQuery({ queryKey: ['settings', 'integrations', 'webhooks'], queryFn: async (): Promise<WebhookItem[]> => [], staleTime: Infinity })
}

export function IntegrationsSettingsPage() {
  const canManage = usePermission('settings.manage')
  const { data: apiKeys, isLoading: keysLoading } = useApiKeys()
  const { data: webhooks, isLoading: webhooksLoading } = useWebhooks()

  const keyColumns: DataTableColumn<ApiKeyItem>[] = [
    { key: 'label', header: 'Label', cell: (row) => <span className="font-medium text-slate-900">{row.label}</span> },
    { key: 'keyPrefix', header: 'Key', cell: (row) => <span className="font-mono text-xs">{row.keyPrefix}••••••••</span> },
    { key: 'createdAt', header: 'Created', cell: (row) => formatDateTime(row.createdAt) },
    { key: 'lastUsedAt', header: 'Last Used', cell: (row) => (row.lastUsedAt ? formatDateTime(row.lastUsedAt) : 'Never') },
    { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.status} /> },
  ]

  const webhookColumns: DataTableColumn<WebhookItem>[] = [
    { key: 'url', header: 'Endpoint URL', cell: (row) => <span className="font-mono text-xs">{row.url}</span> },
    { key: 'events', header: 'Events', cell: (row) => row.events.join(', ') },
    { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.status} /> },
    { key: 'createdAt', header: 'Created', cell: (row) => formatDateTime(row.createdAt) },
  ]

  return (
    <div className="space-y-6">
      <PageHeader title="Integrations" subtitle="API access keys and outbound webhooks for third-party integrations." />
      <div className="rounded-2xl border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-900">
        No tenant-level Integrations endpoint exists yet — key/webhook management here is a UI stub until a backend endpoint is available.
      </div>

      <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <KeyRound className="h-4 w-4 text-slate-400" />
            <h2 className="text-sm font-semibold uppercase tracking-[0.18em] text-slate-500">API Keys</h2>
          </div>
          {canManage ? (
            <button
              type="button"
              onClick={() => toast.error('API key generation is not yet available — no backend Integrations endpoint exists.')}
              className="inline-flex h-10 items-center gap-2 rounded-lg bg-slate-900 px-3 text-sm font-medium text-white hover:bg-slate-800"
            >
              <Plus className="h-4 w-4" />Generate Key
            </button>
          ) : null}
        </div>
        <div className="mt-4">
          {keysLoading ? null : (apiKeys ?? []).length === 0 ? (
            <EmptyState title="No API keys yet" description="Generated keys for third-party integrations will appear here once the backend endpoint is connected." />
          ) : (
            <DataTable columns={keyColumns} data={apiKeys ?? []} />
          )}
        </div>
      </div>

      <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <Webhook className="h-4 w-4 text-slate-400" />
            <h2 className="text-sm font-semibold uppercase tracking-[0.18em] text-slate-500">Outbound Webhooks</h2>
          </div>
          {canManage ? (
            <button
              type="button"
              onClick={() => toast.error('Webhook registration is not yet available — no backend Integrations endpoint exists.')}
              className="inline-flex h-10 items-center gap-2 rounded-lg bg-slate-900 px-3 text-sm font-medium text-white hover:bg-slate-800"
            >
              <Plus className="h-4 w-4" />Add Webhook
            </button>
          ) : null}
        </div>
        <div className="mt-4">
          {webhooksLoading ? null : (webhooks ?? []).length === 0 ? (
            <EmptyState title="No webhooks configured" description="Outbound event webhooks (work order created, invoice paid, etc.) will appear here once the backend endpoint is connected." />
          ) : (
            <DataTable columns={webhookColumns} data={webhooks ?? []} />
          )}
        </div>
      </div>
    </div>
  )
}
