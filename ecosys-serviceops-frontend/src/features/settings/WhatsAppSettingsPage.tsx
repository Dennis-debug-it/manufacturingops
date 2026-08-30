import { useEffect, useState } from 'react'
import toast from 'react-hot-toast'
import { PageHeader } from '../../shared/components/PageHeader'
import { Spinner } from '../../shared/components/Spinner'
import { EmptyState } from '../../shared/components/EmptyState'
import { ErrorState } from '../../shared/components/ErrorState'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { usePermission } from '../../shared/hooks/usePermission'
import { useSaveWhatsAppSettings, useWhatsAppSettings, useWhatsAppTemplates, type WhatsAppSettings } from '../whatsapp/api'

export function WhatsAppSettingsPage() {
  const canManage = usePermission('settings.manage')
  const { data, isLoading, isError, refetch } = useWhatsAppSettings()
  const { data: templates, isError: templatesError, refetch: refetchTemplates } = useWhatsAppTemplates()
  const save = useSaveWhatsAppSettings()
  const [values, setValues] = useState<WhatsAppSettings | null>(null)

  useEffect(() => {
    if (data) setValues(data)
  }, [data])

  if (isLoading) {
    return <Spinner label="Loading settings" />
  }

  // Without this branch an exhausted retry leaves `values` null forever and the spinner never releases.
  if (isError || !values) {
    return (
      <ErrorState
        title="WhatsApp settings unavailable"
        description="We couldn’t load the WhatsApp integration settings. Nothing has been changed — try again."
        onRetry={() => void refetch()}
      />
    )
  }

  const setField = <K extends keyof WhatsAppSettings>(key: K, value: WhatsAppSettings[K]) =>
    setValues((current) => (current ? { ...current, [key]: value } : current))

  const onSave = async () => {
    if (!values) return
    await save.mutateAsync(values)
    toast.success('Settings saved')
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title="WhatsApp"
        subtitle="WhatsApp Business API integration for client and technician messaging."
        primaryAction={
          canManage ? (
            <button type="button" onClick={() => void onSave()} disabled={save.isPending} className="inline-flex h-11 items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:opacity-60">
              {save.isPending ? 'Saving…' : 'Save Changes'}
            </button>
          ) : undefined
        }
      />

      <div className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-5 shadow-sm">
        <label className="flex items-center gap-2 text-sm font-medium text-slate-700">
          <input type="checkbox" checked={values.isEnabled} disabled={!canManage} onChange={(event) => setField('isEnabled', event.target.checked)} className="h-4 w-4 rounded border-slate-300" />
          Enable WhatsApp Integration
        </label>
      </div>

      <div className="grid grid-cols-1 gap-4 rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-5 shadow-sm sm:grid-cols-2">
        <div>
          <label className="mb-1.5 block text-sm font-medium text-slate-700">Business Account ID</label>
          <input value={values.businessAccountId} disabled={!canManage} onChange={(event) => setField('businessAccountId', event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)] disabled:bg-slate-50" />
        </div>
        <div>
          <label className="mb-1.5 block text-sm font-medium text-slate-700">Phone Number ID</label>
          <input value={values.phoneNumberId} disabled={!canManage} onChange={(event) => setField('phoneNumberId', event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)] disabled:bg-slate-50" />
        </div>
        <div className="sm:col-span-2">
          <label className="mb-1.5 block text-sm font-medium text-slate-700">Access Token</label>
          <input type="password" value={values.accessToken} disabled={!canManage} onChange={(event) => setField('accessToken', event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)] disabled:bg-slate-50" />
        </div>
        <div>
          <label className="mb-1.5 block text-sm font-medium text-slate-700">Webhook Verify Token</label>
          <input value={values.webhookVerifyToken} disabled={!canManage} onChange={(event) => setField('webhookVerifyToken', event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)] disabled:bg-slate-50" />
        </div>
        <div>
          <label className="mb-1.5 block text-sm font-medium text-slate-700">Webhook URL</label>
          <input value={values.webhookUrl} disabled={!canManage} onChange={(event) => setField('webhookUrl', event.target.value)} placeholder="https://your-tenant.ecosys.app/api/whatsapp/webhook" className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)] disabled:bg-slate-50" />
        </div>
      </div>

      <div className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-5 shadow-sm">
        <label className="flex items-center gap-2 text-sm font-medium text-slate-700">
          <input type="checkbox" checked={values.autoReplyEnabled} disabled={!canManage} onChange={(event) => setField('autoReplyEnabled', event.target.checked)} className="h-4 w-4 rounded border-slate-300" />
          Enable Auto-Reply
        </label>
        <textarea
          value={values.autoReplyMessage}
          disabled={!canManage || !values.autoReplyEnabled}
          onChange={(event) => setField('autoReplyMessage', event.target.value)}
          rows={3}
          className="mt-3 w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[var(--app-primary)] disabled:bg-slate-50"
        />
      </div>

      <div className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-5 shadow-sm">
        <h2 className="text-sm font-semibold uppercase tracking-[0.18em] text-slate-500">Message Templates</h2>
        {templatesError ? (
          <div className="mt-4">
            <ErrorState
              title="Templates unavailable"
              description="The approved template list couldn’t be loaded, so this is not a confirmation that there are none."
              onRetry={() => void refetchTemplates()}
            />
          </div>
        ) : (templates ?? []).length === 0 ? (
          <div className="mt-4">
            <EmptyState title="No templates yet" description="Approved WhatsApp Business message templates will appear here once the integration is connected." />
          </div>
        ) : (
          <div className="mt-4 divide-y divide-slate-100">
            {(templates ?? []).map((template) => (
              <div key={template.id} className="flex items-center justify-between py-3">
                <div>
                  <p className="text-sm font-medium text-slate-900">{template.name}</p>
                  <p className="text-xs text-slate-500">{template.category} · {template.language}</p>
                </div>
                <StatusBadge status={template.status} />
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  )
}
