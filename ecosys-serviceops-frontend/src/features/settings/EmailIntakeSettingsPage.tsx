import { Link } from 'react-router-dom'
import { SettingsForm } from './components/SettingsForm'
import { IntakeRulesSection } from './components/IntakeRulesSection'

export function EmailIntakeSettingsPage() {
  return (
    <div className="space-y-4">
    <p className="rounded-xl border border-slate-200 bg-white p-4 text-sm text-slate-600">Email is routed through your ServiceOps-managed address. <Link to="/settings/email" className="font-medium text-slate-900 underline">View workspace email</Link></p>
    <SettingsForm
      title="Email Intake"
      subtitle="Mailbox connection settings. Matching rules are configured below — you can have as many as you need."
      queryKey="email-intake"
      fetchUrl="/api/settings/email-intake"
      saveUrl="/api/settings/email-intake"
      fields={[
        { key: 'isEnabled', label: 'Enable Email Intake', type: 'checkbox' },
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
