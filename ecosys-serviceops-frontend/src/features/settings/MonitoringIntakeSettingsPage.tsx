import { useState } from 'react'
import toast from 'react-hot-toast'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Plus } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { Spinner } from '../../shared/components/Spinner'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { EmptyState } from '../../shared/components/EmptyState'
import { FormDrawer } from '../../shared/components/FormDrawer'
import { usePermission } from '../../shared/hooks/usePermission'
import { formatDateTime } from '../../shared/utils/dates'
import { api } from '../../shared/api/apiClient'
import { SettingsForm } from './components/SettingsForm'

interface MonitoringWebhookIntegration {
  id: string
  name: string
  toolType: string
  endpointSlug: string
  isActive: boolean
  defaultPriority: string
  createWorkOrderOnAlert: boolean
  lastReceivedAt: string | null
  lastStatus: string | null
  lastError: string | null
  hasSecret: boolean
  generatedSecret: string | null
  endpointUrl: string | null
}

function NewWebhookDrawer({ open, onClose }: { open: boolean; onClose: () => void }) {
  const queryClient = useQueryClient()
  const [name, setName] = useState('')
  const [toolType, setToolType] = useState('')
  const [defaultPriority, setDefaultPriority] = useState('Medium')
  const [createWorkOrderOnAlert, setCreateWorkOrderOnAlert] = useState(true)
  const [createdSecret, setCreatedSecret] = useState<string | null>(null)

  const create = useMutation({
    mutationFn: () => api.post<MonitoringWebhookIntegration>('/api/settings/monitoring-webhooks', {
      name,
      toolType,
      isActive: true,
      defaultClientId: null,
      defaultAssetId: null,
      defaultBranchId: null,
      defaultAssignmentGroupId: null,
      defaultPriority,
      createWorkOrderOnAlert,
      payloadMappingJson: null,
    }),
    onSuccess: (result) => {
      toast.success('Webhook integration created')
      setCreatedSecret(result.generatedSecret)
      void queryClient.invalidateQueries({ queryKey: ['settings', 'monitoring-webhooks'] })
    },
  })

  return (
    <FormDrawer open={open} title="New Monitoring Webhook" onClose={() => { setCreatedSecret(null); onClose() }}>
      {createdSecret ? (
        <div className="space-y-4">
          <div className="rounded-lg border border-amber-200 bg-amber-50 p-4 text-sm text-amber-800">
            Save this secret now — it will not be shown again.
            <p className="mt-2 break-all rounded bg-white px-3 py-2 font-mono text-xs">{createdSecret}</p>
          </div>
          <button type="button" onClick={() => { setCreatedSecret(null); onClose() }} className="inline-flex h-11 w-full items-center justify-center rounded-lg bg-slate-900 px-4 text-sm font-medium text-white hover:bg-slate-800">Done</button>
        </div>
      ) : (
        <div className="space-y-4">
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Name</label>
            <input value={name} onChange={(event) => setName(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900" />
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Tool Type</label>
            <input value={toolType} onChange={(event) => setToolType(event.target.value)} placeholder="e.g. Zabbix, PRTG, Datadog" className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900" />
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Default Work Order Priority</label>
            <input value={defaultPriority} onChange={(event) => setDefaultPriority(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900" />
          </div>
          <label className="flex items-center gap-2 text-sm text-slate-700">
            <input type="checkbox" checked={createWorkOrderOnAlert} onChange={(event) => setCreateWorkOrderOnAlert(event.target.checked)} className="h-4 w-4 rounded border-slate-300" />
            Create work order automatically on alert
          </label>
          <button
            type="button"
            disabled={!name.trim() || !toolType.trim() || create.isPending}
            onClick={() => create.mutate()}
            className="inline-flex h-11 w-full items-center justify-center rounded-lg bg-slate-900 px-4 text-sm font-medium text-white hover:bg-slate-800 disabled:opacity-60"
          >
            {create.isPending ? 'Creating…' : 'Create Webhook'}
          </button>
        </div>
      )}
    </FormDrawer>
  )
}

function WebhookRow({ webhook }: { webhook: MonitoringWebhookIntegration }) {
  const canManage = usePermission('settings.manage')
  const queryClient = useQueryClient()
  const test = useMutation({
    mutationFn: () => api.post(`/api/settings/monitoring-webhooks/${webhook.id}/test`),
    onSuccess: () => {
      toast.success('Test event recorded')
      void queryClient.invalidateQueries({ queryKey: ['settings', 'monitoring-webhooks'] })
    },
  })

  return (
    <tr>
      <td className="px-4 py-3 text-sm font-medium text-slate-900">{webhook.name}</td>
      <td className="px-4 py-3 text-sm text-slate-600">{webhook.toolType}</td>
      <td className="px-4 py-3 text-xs text-slate-500 font-mono break-all">{webhook.endpointUrl ?? webhook.endpointSlug}</td>
      <td className="px-4 py-3"><StatusBadge status={webhook.isActive ? 'Active' : 'Inactive'} /></td>
      <td className="px-4 py-3 text-xs text-slate-500">{webhook.lastReceivedAt ? formatDateTime(webhook.lastReceivedAt) : 'Never'}</td>
      <td className="px-4 py-3">
        {canManage ? (
          <button type="button" onClick={() => test.mutate()} disabled={test.isPending} className="rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-xs font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-60">Test</button>
        ) : null}
      </td>
    </tr>
  )
}

export function MonitoringIntakeSettingsPage() {
  const canManage = usePermission('settings.manage')
  const [showNew, setShowNew] = useState(false)
  const { data, isLoading, isError } = useQuery({
    queryKey: ['settings', 'monitoring-webhooks'],
    queryFn: () => api.get<MonitoringWebhookIntegration[]>('/api/settings/monitoring-webhooks'),
    staleTime: 30_000,
  })

  return (
    <div className="space-y-8">
      <SettingsForm
        title="Monitoring Intake"
        subtitle="Default routing for monitoring alert webhooks and auto-created work orders."
        queryKey="monitoring"
        fetchUrl="/api/settings/monitoring"
        saveUrl="/api/settings/monitoring"
        fields={[
          { key: 'isEnabled', label: 'Enable Monitoring Intake', type: 'checkbox' },
          { key: 'providerName', label: 'Provider Name', type: 'text' },
          { key: 'endpointLabel', label: 'Endpoint Label', type: 'text' },
          { key: 'defaultPriority', label: 'Default Work Order Priority', type: 'text' },
          { key: 'autoCreateWorkOrders', label: 'Auto-Create Work Orders', type: 'checkbox' },
        ]}
      />

      <div className="space-y-4">
        <PageHeader
          title="Webhook Integrations"
          subtitle="Per-tool webhook endpoints for monitoring alerts."
          primaryAction={canManage ? <button type="button" onClick={() => setShowNew(true)} className="inline-flex h-11 items-center gap-2 rounded-lg bg-slate-900 px-4 text-sm font-medium text-white hover:bg-slate-800"><Plus className="h-4 w-4" />New Webhook</button> : undefined}
        />
        {isLoading ? (
          <Spinner label="Loading webhook integrations" />
        ) : isError ? (
          <div className="rounded-2xl border border-dashed border-slate-300 bg-white p-8 text-center text-sm text-slate-600">Webhook integrations could not be loaded.</div>
        ) : (data ?? []).length === 0 ? (
          <EmptyState title="No webhook integrations" description="Create one to receive monitoring alerts as work orders." />
        ) : (
          <div className="overflow-x-auto rounded-2xl border border-slate-200 bg-white shadow-sm">
            <table className="min-w-full divide-y divide-slate-200 text-left text-sm">
              <thead className="bg-slate-50">
                <tr>
                  <th className="px-4 py-3 font-medium text-slate-600">Name</th>
                  <th className="px-4 py-3 font-medium text-slate-600">Tool</th>
                  <th className="px-4 py-3 font-medium text-slate-600">Endpoint</th>
                  <th className="px-4 py-3 font-medium text-slate-600">Status</th>
                  <th className="px-4 py-3 font-medium text-slate-600">Last Received</th>
                  <th className="px-4 py-3" />
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {(data ?? []).map((webhook) => <WebhookRow key={webhook.id} webhook={webhook} />)}
              </tbody>
            </table>
          </div>
        )}
      </div>

      <NewWebhookDrawer open={showNew} onClose={() => setShowNew(false)} />
    </div>
  )
}
