import { SettingsForm } from './components/SettingsForm'

export function SecuritySettingsPage() {
  return (
    <SettingsForm
      title="Security"
      subtitle="Session and authentication policies."
      queryKey="security"
      fetchUrl="/api/settings/security"
      saveUrl="/api/settings/security"
      fields={[
        { key: 'minPasswordLength', label: 'Minimum Password Length', type: 'number' },
        { key: 'requireUppercase', label: 'Require Uppercase Letter', type: 'checkbox' },
        { key: 'requireLowercase', label: 'Require Lowercase Letter', type: 'checkbox' },
        { key: 'requireDigit', label: 'Require Digit', type: 'checkbox' },
        { key: 'requireSpecialCharacter', label: 'Require Special Character', type: 'checkbox' },
        { key: 'passwordRotationDays', label: 'Password Rotation (days)', type: 'number' },
        { key: 'sessionTimeoutMinutes', label: 'Session Timeout (minutes)', type: 'number' },
        { key: 'requireMfa', label: 'Require Multi-Factor Authentication', type: 'checkbox' },
      ]}
    />
  )
}
