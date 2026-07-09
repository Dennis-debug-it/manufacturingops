import { Eye, RefreshCw, RotateCcw, Save, Send } from 'lucide-react'
import { useEffect, useState } from 'react'
import { InfoAlert } from '../ui/InfoAlert'
import type { EmailTemplatePreviewResponse, EmailTemplateRecord, UpdateEmailTemplateInput } from '../../types/api'

type Props = {
  scopeLabel: string
  templates: EmailTemplateRecord[]
  loading: boolean
  error: string | null
  onReload: () => void | Promise<void>
  onSave: (eventKey: string, input: UpdateEmailTemplateInput) => Promise<EmailTemplateRecord>
  onPreview: (eventKey: string, sampleData?: Record<string, string | null>) => Promise<EmailTemplatePreviewResponse>
  onSendTest: (eventKey: string, testRecipientEmail?: string, sampleData?: Record<string, string | null>) => Promise<{ success: boolean; message?: string | null }>
  onReset: (eventKey: string) => Promise<EmailTemplateRecord>
  onToast: (input: { title: string; description: string; tone: 'success' | 'danger' | 'warning' | 'info' }) => void
}

const defaultSampleDataText = `fullName=Jane Doe
email=jane.doe@example.com
temporaryPassword=Eco!TempPass9
loginUrl=https://app.ecosysdigital.co.ke/login
resetLink=https://app.ecosysdigital.co.ke/reset-password?token=sample
companyName=Acme Facilities Ltd
tenantName=Acme Facilities Ltd
platformName=Ecosys ServiceOps
workOrderNumber=WO-000123
assetName=Generator 250kVA
assignedTo=Alex Kimani
priority=High
dueDate=2026-05-10
supportEmail=support@ecosysdigital.co.ke
senderName=Ecosys ServiceOps
sentAt=2026-05-10 09:00 UTC`

function parseSampleData(value: string) {
  return value
    .split('\n')
    .map((line) => line.trim())
    .filter(Boolean)
    .reduce<Record<string, string>>((accumulator, line) => {
      const separatorIndex = line.indexOf('=')
      if (separatorIndex <= 0) {
        return accumulator
      }

      const key = line.slice(0, separatorIndex).trim()
      const nextValue = line.slice(separatorIndex + 1).trim()
      if (key) {
        accumulator[key] = nextValue
      }
      return accumulator
    }, {})
}

export function EmailTemplateEditor({
  scopeLabel,
  templates,
  loading,
  error,
  onReload,
  onSave,
  onPreview,
  onSendTest,
  onReset,
  onToast,
}: Props) {
  const [selectedKey, setSelectedKey] = useState('')
  const [form, setForm] = useState<UpdateEmailTemplateInput | null>(null)
  const [preview, setPreview] = useState<EmailTemplatePreviewResponse | null>(null)
  const [testRecipientEmail, setTestRecipientEmail] = useState('')
  const [sampleDataText, setSampleDataText] = useState(defaultSampleDataText)
  const [saving, setSaving] = useState(false)
  const [previewing, setPreviewing] = useState(false)
  const [sendingTest, setSendingTest] = useState(false)
  const [resetting, setResetting] = useState(false)

  useEffect(() => {
    if (!templates.length) {
      setSelectedKey('')
      setForm(null)
      setPreview(null)
      return
    }

    const nextSelected = templates.some((item) => item.eventKey === selectedKey) ? selectedKey : templates[0].eventKey
    const template = templates.find((item) => item.eventKey === nextSelected) ?? templates[0]
    setSelectedKey(nextSelected)
    setForm({
      templateName: template.templateName,
      subject: template.subject,
      htmlBody: template.htmlBody,
      textBody: template.textBody,
      enabled: template.enabled,
      senderNameOverride: template.senderNameOverride || '',
      replyToOverride: template.replyToOverride || '',
    })
    setPreview(null)
  }, [templates, selectedKey])

  const selectedTemplate = templates.find((item) => item.eventKey === selectedKey) ?? null

  if (loading) {
    return <div className="surface-card p-6"><p className="text-sm text-muted">Loading email templates...</p></div>
  }

  if (error) {
    return <InfoAlert title={`Unable to load ${scopeLabel.toLowerCase()} templates`} description={error} tone="danger" />
  }

  if (!selectedTemplate || !form) {
    return <InfoAlert title="No email templates available" description="No editable templates were returned by the server." tone="warning" />
  }

  const activeTemplate = selectedTemplate
  const sampleData = parseSampleData(sampleDataText)

  return (
    <div className="grid gap-4 xl:grid-cols-[300px_minmax(0,1fr)]">
      <section className="surface-card space-y-3">
        <div className="flex items-center justify-between gap-3">
          <div>
            <p className="text-lg font-semibold text-app">{scopeLabel}</p>
            <p className="mt-1 text-sm text-muted">Choose an event and edit its governed delivery template.</p>
          </div>
          <button type="button" className="button-secondary" onClick={() => void onReload()}>
            <RefreshCw className="h-4 w-4" />
            Refresh
          </button>
        </div>
        <div className="space-y-2">
          {templates.map((item) => (
            <button
              key={item.eventKey}
              type="button"
              onClick={() => setSelectedKey(item.eventKey)}
              className={`w-full rounded-2xl border px-4 py-3 text-left transition ${
                item.eventKey === selectedKey ? 'border-[#127A78] bg-[#127A78]/8' : 'border-app bg-app/40 hover:bg-app/65'
              }`}
            >
              <div className="flex items-center justify-between gap-3">
                <p className="text-sm font-semibold text-app">{item.templateName}</p>
                <span className={`rounded-full px-2.5 py-1 text-[11px] font-semibold ${item.enabled ? 'bg-emerald-500/15 text-emerald-700' : 'bg-slate-500/15 text-slate-600'}`}>
                  {item.enabled ? 'Enabled' : 'Disabled'}
                </span>
              </div>
              <p className="mt-1 text-xs uppercase tracking-[0.16em] text-muted">{item.eventKey}</p>
              <p className="mt-2 text-xs text-muted">{item.source}{item.isOverride ? ' override' : ' default'}</p>
            </button>
          ))}
        </div>
      </section>

      <section className="surface-card space-y-4">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div>
            <p className="text-lg font-semibold text-app">{selectedTemplate.templateName}</p>
            <p className="mt-1 text-sm text-muted">
              Template key: <span className="font-mono">{selectedTemplate.eventKey}</span>
            </p>
            {selectedTemplate.lastUpdatedAt ? <p className="mt-1 text-xs text-muted">Last updated {new Date(selectedTemplate.lastUpdatedAt).toLocaleString()}</p> : null}
          </div>
          <div className="flex flex-wrap gap-2">
            <button type="button" className="button-secondary" onClick={() => void handlePreview()} disabled={previewing}>
              <Eye className="h-4 w-4" />
              {previewing ? 'Previewing...' : 'Preview'}
            </button>
            <button type="button" className="button-secondary" onClick={() => void handleSendTest()} disabled={sendingTest}>
              <Send className="h-4 w-4" />
              {sendingTest ? 'Queueing...' : 'Send Test Email'}
            </button>
            <button type="button" className="button-secondary" onClick={() => void handleReset()} disabled={resetting}>
              <RotateCcw className="h-4 w-4" />
              {resetting ? 'Resetting...' : 'Reset to Default'}
            </button>
            <button type="button" className="button-primary" onClick={() => void handleSave()} disabled={saving}>
              <Save className="h-4 w-4" />
              {saving ? 'Saving...' : 'Save Template'}
            </button>
          </div>
        </div>

        <InfoAlert
          title="Template fallback order"
          description={`${scopeLabel} templates use tenant override first when available, then platform default, then the built-in Ecosys fallback so delivery does not break when a template is missing.`}
          tone="info"
        />

        <div className="grid gap-3 md:grid-cols-2">
          <label className="block space-y-2">
            <span className="text-sm font-medium text-app">Display Name</span>
            <input value={form.templateName} onChange={(event) => setForm((current) => current ? { ...current, templateName: event.target.value } : current)} className="field-input" />
          </label>
          <label className="panel-subtle flex items-center justify-between rounded-2xl px-4 py-3">
            <span className="text-sm text-app">Enabled</span>
            <input type="checkbox" checked={form.enabled} onChange={(event) => setForm((current) => current ? { ...current, enabled: event.target.checked } : current)} />
          </label>
          <label className="block space-y-2 md:col-span-2">
            <span className="text-sm font-medium text-app">Subject</span>
            <input value={form.subject} onChange={(event) => setForm((current) => current ? { ...current, subject: event.target.value } : current)} className="field-input" />
          </label>
          <label className="block space-y-2">
            <span className="text-sm font-medium text-app">Sender Name Override</span>
            <input value={form.senderNameOverride || ''} onChange={(event) => setForm((current) => current ? { ...current, senderNameOverride: event.target.value } : current)} className="field-input" placeholder="Optional" />
          </label>
          <label className="block space-y-2">
            <span className="text-sm font-medium text-app">Reply-to Override</span>
            <input value={form.replyToOverride || ''} onChange={(event) => setForm((current) => current ? { ...current, replyToOverride: event.target.value } : current)} className="field-input" placeholder="Optional" />
          </label>
          <label className="block space-y-2 md:col-span-2">
            <span className="text-sm font-medium text-app">HTML Body</span>
            <textarea value={form.htmlBody} onChange={(event) => setForm((current) => current ? { ...current, htmlBody: event.target.value } : current)} className="field-input min-h-[260px] font-mono text-xs" />
          </label>
          <label className="block space-y-2 md:col-span-2">
            <span className="text-sm font-medium text-app">Plain Text Body</span>
            <textarea value={form.textBody || ''} onChange={(event) => setForm((current) => current ? { ...current, textBody: event.target.value } : current)} className="field-input min-h-[180px] font-mono text-xs" placeholder="Optional but recommended" />
          </label>
        </div>

        <div className="grid gap-4 xl:grid-cols-[280px_minmax(0,1fr)]">
          <section className="panel-subtle space-y-3 rounded-[28px] p-4">
            <div>
              <p className="text-sm font-semibold text-app">Available Placeholders</p>
              <p className="mt-1 text-xs text-muted">Use these tokens in the subject or body.</p>
            </div>
            <div className="flex flex-wrap gap-2">
              {selectedTemplate.availablePlaceholders.map((item) => (
                <span key={item} className="rounded-full border border-app px-3 py-1 text-xs font-mono text-app">{`{{${item}}}`}</span>
              ))}
            </div>
            <div>
              <p className="text-sm font-semibold text-app">Required Placeholders</p>
              <p className="mt-1 text-xs text-muted">
                {selectedTemplate.requiredPlaceholders.length ? selectedTemplate.requiredPlaceholders.map((item) => `{{${item}}}`).join(', ') : 'No required placeholders for this template.'}
              </p>
            </div>
            <label className="block space-y-2">
              <span className="text-sm font-medium text-app">Test recipient email</span>
              <input value={testRecipientEmail} onChange={(event) => setTestRecipientEmail(event.target.value)} className="field-input" placeholder="name@example.com" />
            </label>
            <label className="block space-y-2">
              <span className="text-sm font-medium text-app">Sample Data</span>
              <textarea value={sampleDataText} onChange={(event) => setSampleDataText(event.target.value)} className="field-input min-h-[220px] font-mono text-xs" />
            </label>
          </section>

          <section className="panel-subtle space-y-3 rounded-[28px] p-4">
            <div>
              <p className="text-sm font-semibold text-app">Preview</p>
              <p className="mt-1 text-xs text-muted">Preview uses the sample data shown on the left.</p>
            </div>
            {preview ? (
              <div className="space-y-3">
                <div className="rounded-2xl border border-app bg-app/50 px-4 py-3">
                  <p className="text-xs uppercase tracking-[0.16em] text-muted">Template Subject</p>
                  <p className="mt-2 text-sm font-semibold text-app">{preview.templateSubject}</p>
                </div>
                <div className="rounded-2xl border border-app bg-app/50 px-4 py-3">
                  <p className="text-xs uppercase tracking-[0.16em] text-muted">Final Subject</p>
                  <p className="mt-2 text-sm font-semibold text-app">{preview.finalSubject}</p>
                </div>
                <div className="rounded-2xl border border-app bg-white px-4 py-4 text-sm text-slate-800">
                  <div dangerouslySetInnerHTML={{ __html: preview.htmlBody }} />
                </div>
                <div className="rounded-2xl border border-app bg-app/50 px-4 py-3">
                  <p className="text-xs uppercase tracking-[0.16em] text-muted">Plain Text</p>
                  <pre className="mt-2 whitespace-pre-wrap font-sans text-sm text-app">{preview.textBody}</pre>
                </div>
              </div>
            ) : (
              <p className="text-sm text-muted">Run preview to render this template with sample values.</p>
            )}
          </section>
        </div>
      </section>
    </div>
  )

  async function handleSave() {
    if (!form) {
      return
    }

    setSaving(true)
    try {
      await onSave(activeTemplate.eventKey, {
        ...form,
        senderNameOverride: form.senderNameOverride?.trim() || null,
        replyToOverride: form.replyToOverride?.trim() || null,
        textBody: form.textBody?.trim() || null,
      })
      onToast({ title: 'Template saved', description: `${activeTemplate.templateName} was updated.`, tone: 'success' })
      await onReload()
    } catch (saveError) {
      onToast({ title: 'Save failed', description: saveError instanceof Error ? saveError.message : 'Unable to save the template.', tone: 'danger' })
    } finally {
      setSaving(false)
    }
  }

  async function handlePreview() {
    setPreviewing(true)
    try {
      setPreview(await onPreview(activeTemplate.eventKey, sampleData))
    } catch (previewError) {
      onToast({ title: 'Preview failed', description: previewError instanceof Error ? previewError.message : 'Unable to preview the template.', tone: 'danger' })
    } finally {
      setPreviewing(false)
    }
  }

  async function handleSendTest() {
    setSendingTest(true)
    try {
      const result = await onSendTest(activeTemplate.eventKey, testRecipientEmail || undefined, sampleData)
      onToast({
        title: result.success ? 'Test email queued' : 'Test email failed',
        description: result.success ? result.message || 'Template test email queued. Check Delivery Logs for status.' : result.message || 'Unable to send the template test email.',
        tone: result.success ? 'success' : 'danger',
      })
    } catch (testError) {
      onToast({ title: 'Test failed', description: testError instanceof Error ? testError.message : 'Unable to send the template test email.', tone: 'danger' })
    } finally {
      setSendingTest(false)
    }
  }

  async function handleReset() {
    setResetting(true)
    try {
      await onReset(activeTemplate.eventKey)
      onToast({ title: 'Template reset', description: `${activeTemplate.templateName} now uses the default template again.`, tone: 'success' })
      await onReload()
    } catch (resetError) {
      onToast({ title: 'Reset failed', description: resetError instanceof Error ? resetError.message : 'Unable to reset the template.', tone: 'danger' })
    } finally {
      setResetting(false)
    }
  }
}
