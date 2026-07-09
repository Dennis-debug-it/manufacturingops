import { SettingsForm } from '../../features/settings/components/SettingsForm'

export function SecuritySettingsPage() {
  return (
    <SettingsForm
      title="Platform Security"
      subtitle="Password policy, session timeout, and lockout thresholds enforced platform-wide."
      queryKey="platform-security"
      fetchUrl="/api/platform/settings/security"
      saveUrl="/api/platform/settings/security"
      permissionAction="platform.settings.manage"
      fields={[
        { key: 'requireStrongPasswords', label: 'Require Strong Passwords', type: 'checkbox' },
        { key: 'minimumPasswordLength', label: 'Minimum Password Length', type: 'number' },
        { key: 'requireEmailVerification', label: 'Require Email Verification', type: 'checkbox' },
        { key: 'sessionTimeoutMinutes', label: 'Session Timeout (minutes)', type: 'number' },
        { key: 'failedLoginLockoutThreshold', label: 'Failed Login Lockout Threshold', type: 'number' },
        { key: 'passwordResetExpiryMinutes', label: 'Password Reset Link Expiry (minutes)', type: 'number' },
      ]}
    />
  )
}
