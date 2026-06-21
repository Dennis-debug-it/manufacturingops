import { EmailTemplateEditor } from '../../../../components/email/EmailTemplateEditor'
import { useToast } from '../../../../components/ui/ToastProvider'
import { useAsyncData } from '../../../../hooks/useAsyncData'
import { platformSettingsService } from '../../../../services/platformSettingsService'

export function EmailTemplatesSettingsPanel() {
  const { pushToast } = useToast()
  const { data, loading, error, reload } = useAsyncData(() => platformSettingsService.listEmailTemplates(), [], [])
  const templates = data.filter((template) => !template.supportsTenantOverride)

  return (
    <EmailTemplateEditor
      scopeLabel="Platform Email Templates"
      templates={templates}
      loading={loading}
      error={error}
      onReload={reload}
      onSave={(eventKey, input) => platformSettingsService.updateEmailTemplate(eventKey, input)}
      onPreview={(eventKey, sampleData) => platformSettingsService.previewEmailTemplate(eventKey, sampleData)}
      onSendTest={(eventKey, testRecipientEmail, sampleData) => platformSettingsService.testEmailTemplate(eventKey, testRecipientEmail, sampleData)}
      onReset={(eventKey) => platformSettingsService.resetEmailTemplate(eventKey)}
      onToast={pushToast}
    />
  )
}
