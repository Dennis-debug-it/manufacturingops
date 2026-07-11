import { SettingsForm } from './components/SettingsForm'
import { IntakeRulesSection } from './components/IntakeRulesSection'

export function EmailIntakeSettingsPage() {
  return (
    <div className="space-y-6">
    <SettingsForm
      title="Email Intake"
      subtitle="Mailbox connection settings. Matching rules are configured below — you can have as many as you need."
      queryKey="email-intake"
      fetchUrl="/api/settings/email-intake"
      saveUrl="/api/settings/email-intake"
      fields={[
        { key: 'isEnabled', label: 'Enable Email Intake', type: 'checkbox' },
        { key: 'intakeEmailAddress', label: 'Intake Email Address', type: 'email' },
        { key: 'mailboxProvider', label: 'Mailbox Provider (e.g. IMAP)', type: 'text' },
        { key: 'host', label: 'Host', type: 'text' },
        { key: 'port', label: 'Port', type: 'number' },
        { key: 'useSsl', label: 'Use SSL', type: 'checkbox' },
        { key: 'username', label: 'Username', type: 'text' },
        { key: 'defaultPriority', label: 'Default Work Order Priority', type: 'text' },
        { key: 'createWorkOrderFromUnknownSender', label: 'Create Work Order From Unknown Senders', type: 'checkbox' },
        { key: 'subjectParsingRules', label: 'Subject Parsing Rules', type: 'textarea', helperText: 'Free-form notes on how subjects map to work order fields.' },
        { key: 'allowedSenderDomains', label: 'Allowed Sender Domains (comma-separated)', type: 'text' },
      ]}
    />
    <IntakeRulesSection />
    </div>
  )
}
