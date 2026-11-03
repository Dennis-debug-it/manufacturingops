import { SettingsForm } from './components/SettingsForm'

export function NotificationsSettingsPage() {
  return (
    <SettingsForm
      title="Workspace Notifications"
      subtitle="Tenant-wide event notification matrix. Each person chooses their own channels under My Notifications."
      queryKey="notifications"
      fetchUrl="/api/settings/notifications"
      saveUrl="/api/settings/notifications"
      fields={[
        { key: 'emailAlertsEnabled', label: 'Enable Email Alerts', type: 'checkbox' },
        { key: 'smsAlertsEnabled', label: 'Enable SMS Alerts', type: 'checkbox' },
        { key: 'workOrderAssignmentAlerts', label: 'Work Order Assignment Alerts', type: 'checkbox' },
        { key: 'licenseExpiryAlerts', label: 'License Expiry Alerts', type: 'checkbox' },
        { key: 'dailyDigestEnabled', label: 'Daily Digest Email', type: 'checkbox' },
      ]}
    />
  )
}
