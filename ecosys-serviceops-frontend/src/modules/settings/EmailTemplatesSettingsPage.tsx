import { EmailTemplateEditor } from '../../components/email/EmailTemplateEditor'
import { PageHeader } from '../../components/ui/PageHeader'
import { useToast } from '../../components/ui/ToastProvider'
import { useAsyncData } from '../../hooks/useAsyncData'
import { settingsService } from '../../services/settingsService'

export function EmailTemplatesSettingsPage() {
  const { pushToast } = useToast()
  const { data, loading, error, reload } = useAsyncData((signal) => settingsService.listEmailTemplates(signal), [], [])
  const templates = data.filter((template) => template.supportsTenantOverride)

  return (
    <div className="space-y-4">
      <PageHeader
        eyebrow="Settings"
        title="Email Templates"
        description="Override workspace email templates for invites, onboarding, and operational notifications where customization is allowed."
      />
      <EmailTemplateEditor
        scopeLabel="Workspace Email Templates"
        templates={templates}
        loading={loading}
        error={error}
        onReload={reload}
        onSave={(eventKey, input) => settingsService.updateEmailTemplate(eventKey, input)}
        onPreview={(eventKey, sampleData) => settingsService.previewEmailTemplate(eventKey, sampleData)}
        onSendTest={(eventKey, testRecipientEmail, sampleData) => settingsService.testEmailTemplate(eventKey, testRecipientEmail, sampleData)}
        onReset={(eventKey) => settingsService.resetEmailTemplate(eventKey)}
        onToast={pushToast}
      />
    </div>
  )
}
