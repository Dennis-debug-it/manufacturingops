import { StubSettingsForm } from './components/SettingsForm'

export function InventorySettingsPage() {
  return (
    <StubSettingsForm
      title="Inventory Settings"
      subtitle="Stock alert thresholds and automatic deduction rules."
      gapNote="No dedicated tenant-level Inventory Settings endpoint exists yet — this form is fully built out but changes are not persisted."
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
