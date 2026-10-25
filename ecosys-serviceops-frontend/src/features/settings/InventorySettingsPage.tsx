import { SettingsForm } from './components/SettingsForm'

export function InventorySettingsPage() {
  return (
    <SettingsForm
      title="Inventory Settings"
      subtitle="Stock alert thresholds and automatic deduction rules."
      queryKey="module-inventory"
      fetchUrl="/api/settings/module/inventory"
      saveUrl="/api/settings/module/inventory"
      defaultValues={{
        lowStockThresholdPercent: 20,
        autoDeductOnWorkOrderComplete: true,
        requireApprovalAboveValue: 50000,
        allowNegativeStock: false,
        defaultReorderLeadDays: 7,
      }}
      fields={[
        { key: 'lowStockThresholdPercent', label: 'Critical Stock Threshold (%)', type: 'number', helperText: 'Parts at or below their reorder level are Low; at or below this percentage of it they are Critical.' },
        { key: 'autoDeductOnWorkOrderComplete', label: 'Auto-Deduct Parts on Work Order Completion', type: 'checkbox', helperText: 'Completing a job books its issued, still-open material requests as fully used.' },
        { key: 'requireApprovalAboveValue', label: 'Require Approval Above Value', type: 'number', helperText: 'Material issues worth more than this (in your workspace currency) wait for approval. 0 turns this off.' },
        { key: 'allowNegativeStock', label: 'Allow Negative Stock Balances', type: 'checkbox', helperText: 'Issues, job usage and adjustments may take a balance below zero. Transfers and van loads never can.' },
        { key: 'defaultReorderLeadDays', label: 'Default Reorder Lead Time (days)', type: 'number', helperText: 'Expected delivery for new purchase orders and reorder suggestions when the supplier has no lead time set.' },
      ]}
    />
  )
}
