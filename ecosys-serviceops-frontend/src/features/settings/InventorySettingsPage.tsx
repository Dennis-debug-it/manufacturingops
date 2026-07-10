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
        { key: 'lowStockThresholdPercent', label: 'Low Stock Threshold (%)', type: 'number', helperText: 'Alert when stock falls below this percentage of reorder level.' },
        { key: 'autoDeductOnWorkOrderComplete', label: 'Auto-Deduct Parts on Work Order Completion', type: 'checkbox' },
        { key: 'requireApprovalAboveValue', label: 'Require Approval Above Value (KES)', type: 'number' },
        { key: 'allowNegativeStock', label: 'Allow Negative Stock Balances', type: 'checkbox' },
        { key: 'defaultReorderLeadDays', label: 'Default Reorder Lead Time (days)', type: 'number' },
      ]}
    />
  )
}
