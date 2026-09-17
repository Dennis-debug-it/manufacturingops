import { SettingsForm } from './components/SettingsForm'

export function FinanceDefaultsSettingsPage() {
  return (
    <SettingsForm
      title="Finance Defaults"
      subtitle="Default tax rate, payment terms, and approval rules new quotations and invoices are created with."
      queryKey="finance-defaults"
      fetchUrl="/api/settings/finance-defaults"
      saveUrl="/api/settings/finance-defaults"
      defaultValues={{
        enableVat: true,
        taxName: 'VAT',
        defaultVatRate: 16,
        defaultPaymentTerms: '30 days',
        defaultInvoiceDueDays: 30,
        defaultQuotationValidityDays: 14,
        defaultExpenseApprovalRequired: true,
      }}
      fields={[
        { key: 'enableVat', label: 'Apply tax by default on new documents', type: 'checkbox' },
        { key: 'taxName', label: 'Tax name', type: 'text', helperText: 'e.g. VAT, GST, Sales Tax' },
        { key: 'defaultVatRate', label: 'Default tax rate (%)', type: 'number' },
        { key: 'defaultPaymentTerms', label: 'Default payment terms', type: 'text', helperText: 'e.g. "30 days"' },
        { key: 'defaultInvoiceDueDays', label: 'Default invoice due days', type: 'number' },
        { key: 'defaultQuotationValidityDays', label: 'Default quotation validity (days)', type: 'number' },
        { key: 'defaultExpenseApprovalRequired', label: 'Require approval for new expenses by default', type: 'checkbox' },
      ]}
    />
  )
}
