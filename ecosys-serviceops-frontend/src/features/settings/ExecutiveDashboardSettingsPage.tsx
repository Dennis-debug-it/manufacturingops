import { SettingsForm } from './components/SettingsForm'

export function ExecutiveDashboardSettingsPage() {
  return (
    <SettingsForm
      title="Executive Dashboard Settings"
      subtitle="Metric targets and thresholds shown on the Executive Dashboard."
      queryKey="module-executive-dashboard"
      fetchUrl="/api/settings/module/executive-dashboard"
      saveUrl="/api/settings/module/executive-dashboard"
      defaultValues={{
        slaComplianceTargetPercent: 95,
        firstTimeFixTargetPercent: 85,
        overdueWorkOrderWarningThreshold: 10,
        revenueTargetMonthlyKes: 5000000,
        customerSatisfactionTarget: 4.5,
      }}
      fields={[
        { key: 'slaComplianceTargetPercent', label: 'SLA Compliance Target (%)', type: 'number' },
        { key: 'firstTimeFixTargetPercent', label: 'First-Time Fix Target (%)', type: 'number' },
        { key: 'overdueWorkOrderWarningThreshold', label: 'Overdue Work Order Warning Threshold', type: 'number', helperText: 'Show a dashboard warning banner above this count.' },
        { key: 'revenueTargetMonthlyKes', label: 'Monthly Revenue Target (KES)', type: 'number' },
        { key: 'customerSatisfactionTarget', label: 'Customer Satisfaction Target (out of 5)', type: 'number' },
      ]}
    />
  )
}
