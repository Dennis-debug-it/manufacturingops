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
        { key: 'autoDeductOnWorkOrderComplete', label: 'Close Old Restock Requests on Work Order Completion', type: 'checkbox', helperText: 'Only affects restock requests raised before they were retired: completing the job books their issued parts as used. Parts requests and issue slips already book parts on the job when they are dispatched or issued.' },
        { key: 'requireApprovalAboveValue', label: 'Require Approval Above Value', type: 'number', helperText: 'Issue slips worth more than this (in your workspace currency) wait for approval, and a stock count whose differences are worth more than this can only be posted by someone who approves materials. 0 turns this off.' },
        { key: 'allowNegativeStock', label: 'Allow Negative Stock Balances', type: 'checkbox', helperText: 'Issues, job usage and adjustments may take a balance below zero. Transfers and van loads never can.' },
        { key: 'defaultReorderLeadDays', label: 'Default Reorder Lead Time (days)', type: 'number', helperText: 'Expected delivery for new purchase orders and reorder suggestions when the supplier has no lead time set.' },
      ]}
    />
  )
}
