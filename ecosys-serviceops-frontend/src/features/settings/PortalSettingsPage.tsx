import { StubSettingsForm } from './components/SettingsForm'

export function PortalSettingsPage() {
  return (
    <StubSettingsForm
      title="Client Portal"
      subtitle="Control what client portal contacts can see and do."
      gapNote="No dedicated tenant-level Portal Settings endpoint exists yet — the portal auth and data API are real, but visibility toggles here are not yet persisted."
      defaultValues={{
        allowRequestSubmission: true,
        allowQuoteResponses: true,
        showInvoices: true,
        showContracts: true,
        showAssetRegister: true,
        requireApprovalForNewContacts: false,
        welcomeMessage: 'Welcome to your ECOSYS client portal — track requests, invoices, and quotes in one place.',
      }}
      fields={[
        { key: 'allowRequestSubmission', label: 'Allow Clients to Submit Service Requests', type: 'checkbox' },
        { key: 'allowQuoteResponses', label: 'Allow Clients to Accept/Decline Quotes', type: 'checkbox' },
        { key: 'showInvoices', label: 'Show Invoices Tab', type: 'checkbox' },
        { key: 'showContracts', label: 'Show Contracts Tab', type: 'checkbox' },
        { key: 'showAssetRegister', label: 'Show Asset Register', type: 'checkbox' },
        { key: 'requireApprovalForNewContacts', label: 'Require Admin Approval for New Portal Contacts', type: 'checkbox' },
        { key: 'welcomeMessage', label: 'Welcome Message', type: 'textarea' },
      ]}
    />
  )
}
