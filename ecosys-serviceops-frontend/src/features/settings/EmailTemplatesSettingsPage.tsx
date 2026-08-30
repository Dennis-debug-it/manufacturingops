import { useEffect, useState } from 'react'
import DOMPurify from 'dompurify'
import toast from 'react-hot-toast'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Mail } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { Spinner } from '../../shared/components/Spinner'
import { EmptyState } from '../../shared/components/EmptyState'
import { ErrorState } from '../../shared/components/ErrorState'
import { usePermission } from '../../shared/hooks/usePermission'
import { api } from '../../shared/api/apiClient'

interface EmailTemplateSummary {
  eventKey: string
  templateName: string
  subject: string
  htmlBody: string
  textBody: string | null
  enabled: boolean
  senderNameOverride: string | null
  replyToOverride: string | null
  availablePlaceholders: string[]
  requiredPlaceholders: string[]
  supportsTenantOverride: boolean
  isOverride: boolean
  source: string
  lastUpdatedBy: string | null
  lastUpdatedAt: string | null
}

function TemplateEditor({ eventKey, onClose }: { eventKey: string; onClose: () => void }) {
  const canManage = usePermission('settings.manage')
  const queryClient = useQueryClient()
  const { data, isLoading, isError, refetch } = useQuery({
    queryKey: ['settings', 'email-templates', eventKey],
    queryFn: () => api.get<EmailTemplateSummary>(`/api/settings/email-templates/${eventKey}`),
  })

  const [subject, setSubject] = useState('')
  const [htmlBody, setHtmlBody] = useState('')
  const [enabled, setEnabled] = useState(true)
  const [preview, setPreview] = useState<{ subject: string; htmlBody: string } | null>(null)

  useEffect(() => {
    if (data) {
      setSubject(data.subject)
      setHtmlBody(data.htmlBody)
      setEnabled(data.enabled)
    }
  }, [data])

  const save = useMutation({
    mutationFn: () => api.put<EmailTemplateSummary>(`/api/settings/email-templates/${eventKey}`, {
      templateName: data?.templateName,
      subject,
      htmlBody,
      textBody: data?.textBody ?? null,
      enabled,
      senderNameOverride: data?.senderNameOverride ?? null,
      replyToOverride: data?.replyToOverride ?? null,
    }),
    onSuccess: () => {
      toast.success('Email template saved')
      void queryClient.invalidateQueries({ queryKey: ['settings', 'email-templates'] })
    },
  })

  const reset = useMutation({
    mutationFn: () => api.post<EmailTemplateSummary>(`/api/settings/email-templates/${eventKey}/reset`),
    onSuccess: (result) => {
      toast.success('Reset to default')
      setSubject(result.subject)
      setHtmlBody(result.htmlBody)
      setEnabled(result.enabled)
    },
  })

  const loadPreview = async () => {
    try {
      const result = await api.post<{ subject: string; htmlBody: string }>(`/api/settings/email-templates/${eventKey}/preview`, {})
      setPreview(result)
    } catch {
      // interceptor surfaces error toast
    }
  }

  if (isLoading) {
    return <Spinner label="Loading template" />
  }

  // Without this branch an exhausted retry leaves the drawer spinning over an unusable editor.
  if (isError || !data) {
    return (
      <div className="fixed inset-0 z-50 flex justify-end bg-[var(--app-primary)]/40">
        <div className="h-full w-full max-w-2xl overflow-y-auto bg-white p-6 shadow-xl">
          <div className="flex justify-end">
            <button type="button" onClick={onClose} className="rounded-lg border border-slate-200 px-3 py-1.5 text-xs font-medium text-slate-600 hover:bg-slate-50">Close</button>
          </div>
          <ErrorState
            className="mt-6"
            title="Template unavailable"
            description="We couldn’t load this email template. Editing is disabled until it loads — try again."
            onRetry={() => void refetch()}
          />
        </div>
      </div>
    )
  }

  return (
    <div className="fixed inset-0 z-50 flex justify-end bg-[var(--app-primary)]/40">
      <div className="h-full w-full max-w-2xl overflow-y-auto bg-white p-6 shadow-xl">
        <div className="flex items-start justify-between gap-4">
          <div>
            <h2 className="text-lg font-semibold text-slate-900">{data.templateName}</h2>
            <p className="mt-1 text-xs text-slate-500">{data.eventKey} · Source: {data.source}{data.isOverride ? ' (tenant override)' : ' (platform default)'}</p>
          </div>
          <button type="button" onClick={onClose} className="rounded-lg border border-slate-200 px-3 py-1.5 text-xs font-medium text-slate-600 hover:bg-slate-50">Close</button>
        </div>

        <div className="mt-6 space-y-4">
          <label className="flex items-center gap-2 text-sm text-slate-700">
            <input type="checkbox" checked={enabled} disabled={!canManage} onChange={(event) => setEnabled(event.target.checked)} className="h-4 w-4 rounded border-slate-300" />
            Enabled
          </label>

          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Subject</label>
            <input value={subject} disabled={!canManage} onChange={(event) => setSubject(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)] disabled:bg-slate-50" />
          </div>

          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">HTML Body</label>
            <textarea value={htmlBody} disabled={!canManage} onChange={(event) => setHtmlBody(event.target.value)} rows={12} className="w-full rounded-lg border border-slate-300 px-3 py-2 font-mono text-xs outline-none focus:border-[var(--app-primary)] disabled:bg-slate-50" />
          </div>

          {data.availablePlaceholders.length > 0 ? (
            <div className="rounded-lg bg-slate-50 p-3 text-xs text-slate-600">
              <p className="font-medium text-slate-700">Available placeholders</p>
              <p className="mt-1 font-mono">{data.availablePlaceholders.map((p) => `{{${p}}}`).join(', ')}</p>
            </div>
          ) : null}

          <div className="flex flex-wrap gap-2">
            <button type="button" onClick={loadPreview} className="rounded-lg border border-slate-200 bg-white px-3 py-2 text-xs font-medium text-slate-700 hover:bg-slate-50">Preview with sample data</button>
            {canManage ? (
              <>
                <button type="button" onClick={() => save.mutate()} disabled={save.isPending} className="rounded-lg bg-[var(--app-primary)] px-3 py-2 text-xs font-medium text-white hover:bg-[var(--app-secondary)] disabled:opacity-60">Save</button>
                {data.isOverride ? (
                  <button type="button" onClick={() => reset.mutate()} disabled={reset.isPending} className="rounded-lg border border-slate-200 bg-white px-3 py-2 text-xs font-medium text-slate-700 hover:bg-slate-50">Reset to Default</button>
                ) : null}
              </>
            ) : null}
          </div>

          {preview ? (
            <div className="rounded-lg border border-slate-200 p-4">
              <p className="text-xs font-medium text-slate-500">Rendered Subject</p>
              <p className="text-sm text-slate-900">{preview.subject}</p>
              <p className="mt-3 text-xs font-medium text-slate-500">Rendered Body</p>
              <div className="mt-1 max-h-64 overflow-auto rounded border border-slate-100 p-3 text-xs" dangerouslySetInnerHTML={{ __html: DOMPurify.sanitize(preview.htmlBody) }} />
            </div>
          ) : null}
        </div>
      </div>
    </div>
  )
}

export function EmailTemplatesSettingsPage() {
  const { data, isLoading, isError, refetch } = useQuery({
    queryKey: ['settings', 'email-templates'],
    queryFn: () => api.get<EmailTemplateSummary[]>('/api/settings/email-templates'),
    staleTime: 30_000,
  })
  const [selected, setSelected] = useState<string | null>(null)

  if (isLoading) {
    return <Spinner label="Loading email templates" />
  }

  if (isError) {
    return (
      <ErrorState
        title="Email templates unavailable"
        description="We couldn’t load the configured template list, so this is not a confirmation that there are none."
        onRetry={() => void refetch()}
      />
    )
  }

  return (
    <div className="space-y-6">
      <PageHeader title="Email Templates" subtitle="Tenant-editable communication templates. Falls back to the platform default until overridden." />
      {(data ?? []).length === 0 ? (
        <EmptyState icon={Mail} title="No email templates found" description="No configured email template events were returned by the backend." />
      ) : (
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          {(data ?? []).map((template) => (
            <button
              key={template.eventKey}
              type="button"
              onClick={() => setSelected(template.eventKey)}
              className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 text-left shadow-sm transition hover:border-slate-300"
            >
              <div className="flex items-center justify-between gap-2">
                <span className="font-medium text-slate-900">{template.templateName}</span>
                <span className={`rounded-full px-2 py-0.5 text-[10px] font-medium ${template.enabled ? 'bg-emerald-50 text-emerald-700' : 'bg-slate-100 text-slate-500'}`}>{template.enabled ? 'Enabled' : 'Disabled'}</span>
              </div>
              <p className="mt-1 text-xs text-slate-500">{template.eventKey}</p>
              <p className="mt-2 text-sm text-slate-600">{template.subject}</p>
              <p className="mt-2 text-xs text-slate-400">{template.isOverride ? 'Tenant override' : 'Platform default'}</p>
            </button>
          ))}
        </div>
      )}

      {selected ? <TemplateEditor eventKey={selected} onClose={() => setSelected(null)} /> : null}
    </div>
  )
}
