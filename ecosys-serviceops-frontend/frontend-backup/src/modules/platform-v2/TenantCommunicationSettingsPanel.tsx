import { useEffect, useMemo, useState } from 'react'
import { InfoAlert } from '../../components/ui/InfoAlert'
import { LoadingState } from '../../components/ui/LoadingState'
import { useAsyncData } from '../../hooks/useAsyncData'
import { platformService, toServiceError } from '../../services/platformService'
import type {
  Tenant,
  TenantCommunicationSettings,
  TenantNotificationKey,
  TenantRecipientGroup,
} from '../../types/platform'
import { Field, SectionTitle } from './PlatformCommon'

type Props = {
  tenant: Tenant
  onToast: (input: { title: string; description: string; tone: 'success' | 'danger' | 'warning' | 'info' }) => void
}

type NotificationSection = {
  title: string
  items: Array<{ key: TenantNotificationKey; label: string }>
}

const recipientGroups: Array<{ group: TenantRecipientGroup; label: string }> = [
  { group: 'Admin', label: 'Admin notification emails' },
  { group: 'Operations', label: 'Operations notification emails' },
  { group: 'SLAEscalation', label: 'Priority response emails' },
  { group: 'Dispatch', label: 'Work order dispatch emails' },
  { group: 'Maintenance', label: 'Maintenance notification emails' },
  { group: 'Assets', label: 'Asset notification emails' },
  { group: 'Materials', label: 'Materials notification emails' },
  { group: 'SystemAlerts', label: 'System alert emails' },
]

const notificationSections: NotificationSection[] = [
  {
    title: 'Work Orders',
    items: [
      { key: 'work-order.new-created', label: 'New work order created' },
      { key: 'work-order.assigned', label: 'Work order assigned' },
      { key: 'work-order.updated', label: 'Work order updated' },
      { key: 'work-order.completed', label: 'Work order completed' },
      { key: 'work-order.overdue', label: 'Work order overdue' },
    ],
  },
  {
    title: 'Priority Response',
    items: [
      { key: 'sla.warning', label: 'Priority response warning' },
      { key: 'sla.breach', label: 'Priority response alert' },
    ],
  },
  {
    title: 'Preventive Maintenance',
    items: [
      { key: 'pm.due', label: 'Preventive maintenance due' },
      { key: 'pm.overdue', label: 'Preventive maintenance overdue' },
      { key: 'pm.completed', label: 'Preventive maintenance completed' },
    ],
  },
  {
    title: 'Assets',
    items: [
      { key: 'asset.created', label: 'Asset created' },
      { key: 'asset.updated', label: 'Asset updated' },
      { key: 'asset.deactivated', label: 'Asset deactivated' },
      { key: 'asset.maintenance-due', label: 'Asset maintenance due' },
    ],
  },
  {
    title: 'Materials',
    items: [
      { key: 'materials.request-submitted', label: 'Material request submitted' },
      { key: 'materials.request-approved', label: 'Material request approved' },
      { key: 'materials.request-rejected', label: 'Material request rejected' },
      { key: 'materials.request-issued', label: 'Material request issued' },
      { key: 'materials.low-stock-alert', label: 'Low stock alert' },
    ],
  },
  {
    title: 'Users',
    items: [
      { key: 'users.invited', label: 'Tenant user invited' },
      { key: 'users.activated', label: 'Tenant user activated' },
      { key: 'users.deactivated', label: 'Tenant user deactivated' },
      { key: 'users.role-changed', label: 'Tenant user role changed' },
    ],
  },
  {
    title: 'Tenant Administration',
    items: [
      { key: 'tenant.profile-updated', label: 'Tenant profile updated' },
      { key: 'tenant.branding-updated', label: 'Tenant branding updated' },
      { key: 'tenant.module-access-changed', label: 'Tenant module access changed' },
      { key: 'tenant.subscription-status-changed', label: 'Tenant subscription/licensing status changed' },
    ],
  },
  {
    title: 'System Alerts',
    items: [
      { key: 'system.alerts', label: 'System alerts' },
      { key: 'system.failed-login-attempts', label: 'Failed login attempts' },
      { key: 'system.integration-errors', label: 'Integration errors' },
      { key: 'system.background-job-failures', label: 'Background job failures' },
    ],
  },
]

const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

export function TenantCommunicationSettingsPanel({ tenant, onToast }: Props) {
  const { data, loading, error, reload } = useAsyncData(
    async () => (await platformService.tenantsApi.getCommunicationSettings(tenant.tenantId)).data,
    platformService.tenantsApi.createDefaultCommunicationSettings(tenant.tenantId),
    [tenant.tenantId],
  )

  const [form, setForm] = useState<TenantCommunicationSettings>(data)
  const [recipientText, setRecipientText] = useState<Record<string, string>>({})
  const [testRecipient, setTestRecipient] = useState('')
  const [saving, setSaving] = useState(false)
  const [testing, setTesting] = useState(false)
  const [verifying, setVerifying] = useState(false)
  const [resetting, setResetting] = useState(false)
  const [lastResult, setLastResult] = useState<{ success: boolean; message: string } | null>(null)

  useEffect(() => {
    setForm({
      ...data,
      emailSettings: {
        ...data.emailSettings,
        smtpPasswordSecret: '',
        apiKeySecret: '',
      },
    })

    const textMap: Record<string, string> = {}
    recipientGroups.forEach((item) => {
      const emails = data.recipients
        .filter((row) => row.recipientGroup === item.group && row.isActive)
        .map((row) => row.email)
      textMap[item.group] = emails.join(', ')
    })
    setRecipientText(textMap)
    setLastResult(null)
    setTestRecipient('')
  }, [data])

  const notificationMap = useMemo(() => {
    const map = new Map(form.notificationSettings.map((item) => [item.notificationKey, item]))
    return map
  }, [form.notificationSettings])

  function validate(): string | null {
    if (!form.emailSettings.usePlatformDefaults && !form.emailSettings.overrideSmtpSettings) {
      return 'Enable override delivery settings or switch back to platform defaults.'
    }

    if (form.emailSettings.overrideSmtpSettings && !form.emailSettings.usePlatformDefaults && form.emailSettings.deliveryMode === 'Smtp') {
      if (!form.emailSettings.smtpHost.trim()) {
        return 'SMTP host is required when override SMTP settings is enabled.'
      }

      if (!Number.isFinite(form.emailSettings.smtpPort) || form.emailSettings.smtpPort <= 0) {
        return 'SMTP port must be a positive number.'
      }
    }

    if (!form.emailSettings.usePlatformDefaults && form.emailSettings.enableTenantEmailNotifications && !form.emailSettings.senderEmail.trim()) {
      return 'Sender email is required when tenant email notifications are enabled.'
    }

    if (form.emailSettings.senderEmail && !emailRegex.test(form.emailSettings.senderEmail)) {
      return 'Sender email must be a valid email address.'
    }

    if (form.emailSettings.replyToEmail && !emailRegex.test(form.emailSettings.replyToEmail)) {
      return 'Reply-to email must be a valid email address.'
    }

    for (const group of recipientGroups) {
      const tokens = splitEmails(recipientText[group.group] || '')
      const invalid = tokens.find((email) => !emailRegex.test(email))
      if (invalid) {
        return `${group.label} contains an invalid email: ${invalid}`
      }
    }

    return null
  }

  function buildRecipients() {
    return recipientGroups.flatMap((item) =>
      splitEmails(recipientText[item.group] || '').map((email) => ({
        recipientGroup: item.group,
        email,
        isActive: true,
      })),
    )
  }

  async function save() {
    const validationError = validate()
    if (validationError) {
      onToast({ title: 'Validation failed', description: validationError, tone: 'warning' })
      return
    }

    setSaving(true)
    try {
      const response = await platformService.tenantsApi.saveCommunicationSettings(tenant.tenantId, {
        ...form,
        recipients: buildRecipients(),
      })
      setForm({
        ...response.data,
        emailSettings: {
          ...response.data.emailSettings,
          smtpPasswordSecret: '',
          apiKeySecret: '',
        },
      })
      onToast({ title: 'Tenant email settings saved', description: `${tenant.name} communication settings were updated.`, tone: 'success' })
      setLastResult(null)
      await reload()
    } catch (saveError) {
      onToast({ title: 'Save failed', description: toServiceError(saveError, 'Unable to save tenant communication settings.'), tone: 'danger' })
    } finally {
      setSaving(false)
    }
  }

  async function sendTestEmail() {
    if (testRecipient && !emailRegex.test(testRecipient)) {
      onToast({ title: 'Invalid email', description: 'Test recipient email must be valid.', tone: 'warning' })
      return
    }

    setTesting(true)
    try {
      const response = await platformService.tenantsApi.sendCommunicationTestEmail(tenant.tenantId, testRecipient || undefined)
      const message = response.data.success ? 'Test email sent successfully.' : response.data.lastError || 'Unable to send test email.'
      setLastResult({ success: response.data.success, message })
      onToast({ title: response.data.success ? 'Test email sent' : 'Test email failed', description: message, tone: response.data.success ? 'success' : 'danger' })
      await reload()
    } catch (testError) {
      onToast({ title: 'Test failed', description: toServiceError(testError, 'Unable to send test email.'), tone: 'danger' })
    } finally {
      setTesting(false)
    }
  }

  async function verifySmtp() {
    setVerifying(true)
    try {
      const response = await platformService.tenantsApi.verifyCommunicationSmtp(tenant.tenantId)
      const message = response.data.success ? 'SMTP delivery check completed successfully.' : response.data.lastError || 'Unable to verify delivery connection.'
      setLastResult({ success: response.data.success, message })
      onToast({ title: response.data.success ? 'Connection verified' : 'Connection verification failed', description: message, tone: response.data.success ? 'success' : 'danger' })
      await reload()
    } catch (verifyError) {
      onToast({ title: 'Verification failed', description: toServiceError(verifyError, 'Unable to verify delivery connection.'), tone: 'danger' })
    } finally {
      setVerifying(false)
    }
  }

  async function resetToDefaults() {
    setResetting(true)
    try {
      const response = await platformService.tenantsApi.resetCommunicationToDefaults(tenant.tenantId)
      setForm({
        ...response.data,
        emailSettings: {
          ...response.data.emailSettings,
          smtpPasswordSecret: '',
          apiKeySecret: '',
        },
      })
      onToast({ title: 'Reset complete', description: 'Tenant email settings now inherit platform defaults.', tone: 'success' })
      await reload()
    } catch (resetError) {
      onToast({ title: 'Reset failed', description: toServiceError(resetError, 'Unable to reset to platform defaults.'), tone: 'danger' })
    } finally {
      setResetting(false)
    }
  }

  function toggleNotification(key: string, patch: Partial<{ emailEnabled: boolean; inAppEnabled: boolean; smsEnabled: boolean; isActive: boolean }>) {
    setForm((current) => ({
      ...current,
      notificationSettings: current.notificationSettings.map((item) => (item.notificationKey === key ? { ...item, ...patch } : item)),
    }))
  }

  if (loading) return <LoadingState label="Loading tenant communication settings" />
  if (error) return <InfoAlert title="Unable to load tenant communication settings" description={error} tone="danger" />

  const smtpFieldsDisabled = form.emailSettings.usePlatformDefaults || !form.emailSettings.overrideSmtpSettings || form.emailSettings.deliveryMode !== 'Smtp'
  return (
    <div data-testid="tenant-email-notifications-section" className="space-y-4">
      <SectionTitle title="Email & Notifications" description="Tenant-specific delivery settings, module notification rules, and recipient routing." />

      <section data-testid="tenant-email-delivery-card" className="surface-card space-y-4">
        <InfoAlert title="Outbound delivery only" description="This panel manages tenant SMTP delivery and notification routing. IMAP mailbox intake for work order creation and email thread processing is configured separately under Email Intake." tone="info" />
        <InfoAlert title="SMTP-only delivery" description="Pilot tenants use SMTP for outbound mail. Alternative delivery options are hidden until backend support is completed end-to-end." tone="warning" />
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div>
            <p className="text-lg font-semibold text-app">Email Delivery</p>
            <p className="mt-1 text-sm text-muted">
              {form.emailSettings.usePlatformDefaults ? 'Using platform email defaults' : 'Using tenant-specific email delivery settings'}
            </p>
          </div>
          <button data-testid="tenant-reset-email-defaults-button" type="button" className="button-secondary" onClick={() => void resetToDefaults()} disabled={resetting}>
            {resetting ? 'Resetting...' : 'Reset to platform defaults'}
          </button>
        </div>

        <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
          <label className="panel-subtle flex items-center justify-between rounded-2xl px-4 py-3">
            <span className="text-sm text-app">Use platform default email settings</span>
            <input
              data-testid="tenant-use-platform-defaults-toggle"
              type="checkbox"
              checked={form.emailSettings.usePlatformDefaults}
              onChange={(event) => setForm((current) => ({
                ...current,
                emailSettings: {
                  ...current.emailSettings,
                  usePlatformDefaults: event.target.checked,
                  overrideSmtpSettings: event.target.checked ? false : current.emailSettings.overrideSmtpSettings,
                },
              }))}
            />
          </label>

          <label className="panel-subtle flex items-center justify-between rounded-2xl px-4 py-3">
            <span className="text-sm text-app">Override delivery settings</span>
            <input
              data-testid="tenant-override-smtp-toggle"
              type="checkbox"
              checked={form.emailSettings.overrideSmtpSettings}
              disabled={form.emailSettings.usePlatformDefaults}
              onChange={(event) => setForm((current) => ({
                ...current,
                emailSettings: {
                  ...current.emailSettings,
                  overrideSmtpSettings: event.target.checked,
                },
              }))}
            />
          </label>

          <label className="panel-subtle flex items-center justify-between rounded-2xl px-4 py-3">
            <span className="text-sm text-app">Enable tenant email notifications</span>
            <input
              type="checkbox"
              checked={form.emailSettings.enableTenantEmailNotifications}
              onChange={(event) => setForm((current) => ({
                ...current,
                emailSettings: {
                  ...current.emailSettings,
                  enableTenantEmailNotifications: event.target.checked,
                },
              }))}
            />
          </label>

          <label className="panel-subtle flex items-center justify-between rounded-2xl px-4 py-3">
            <span className="text-sm text-app">Enable SSL/TLS</span>
            <input
              type="checkbox"
              checked={form.emailSettings.enableSslTls}
              onChange={(event) => setForm((current) => ({
                ...current,
                emailSettings: {
                  ...current.emailSettings,
                  enableSslTls: event.target.checked,
                },
              }))}
            />
          </label>
        </div>

        <div className="grid gap-3 md:grid-cols-2">
          <Field label="Delivery method">
            <select
              value={form.emailSettings.deliveryMode}
              disabled={form.emailSettings.usePlatformDefaults}
              onChange={(event) => setForm((current) => ({ ...current, emailSettings: { ...current.emailSettings, deliveryMode: event.target.value as TenantCommunicationSettings['emailSettings']['deliveryMode'] } }))}
              className="field-input"
            >
              <option value="Smtp">SMTP</option>
              <option value="Disabled">Disabled</option>
            </select>
          </Field>
          <Field label="Secure mode">
            <select
              value={form.emailSettings.secureMode}
              disabled={form.emailSettings.usePlatformDefaults}
              onChange={(event) => setForm((current) => ({ ...current, emailSettings: { ...current.emailSettings, secureMode: event.target.value as TenantCommunicationSettings['emailSettings']['secureMode'] } }))}
              className="field-input"
            >
              <option value="Auto">Auto</option>
              <option value="None">None</option>
              <option value="StartTls">STARTTLS</option>
              <option value="SslOnConnect">SSL on Connect</option>
            </select>
            <p className="text-xs text-muted">Port 465 uses SSL/TLS. Port 587 uses STARTTLS.</p>
          </Field>
          <Field label="SMTP host"><input data-testid="tenant-smtp-host-input" value={form.emailSettings.smtpHost} disabled={smtpFieldsDisabled} onChange={(event) => setForm((current) => ({ ...current, emailSettings: { ...current.emailSettings, smtpHost: event.target.value } }))} className="field-input" /></Field>
          <Field label="SMTP port"><input data-testid="tenant-smtp-port-input" type="number" min={1} value={form.emailSettings.smtpPort} disabled={smtpFieldsDisabled} onChange={(event) => setForm((current) => applyRecommendedTenantPort(current, Number(event.target.value) || 0))} className="field-input" /></Field>
          <Field label="SMTP username"><input value={form.emailSettings.smtpUsername} disabled={smtpFieldsDisabled} onChange={(event) => setForm((current) => ({ ...current, emailSettings: { ...current.emailSettings, smtpUsername: event.target.value } }))} className="field-input" /></Field>
          <Field label="SMTP password / masked secret"><input type="password" placeholder={form.emailSettings.smtpPasswordMasked || '********'} value={form.emailSettings.smtpPasswordSecret || ''} disabled={smtpFieldsDisabled} onChange={(event) => setForm((current) => ({ ...current, emailSettings: { ...current.emailSettings, smtpPasswordSecret: event.target.value } }))} className="field-input" /></Field>
          <Field label="Timeout seconds"><input type="number" min={5} value={form.emailSettings.timeoutSeconds || 30} disabled={form.emailSettings.usePlatformDefaults} onChange={(event) => setForm((current) => ({ ...current, emailSettings: { ...current.emailSettings, timeoutSeconds: Number(event.target.value) || 30 } }))} className="field-input" /></Field>
          <Field label="Max retries"><input type="number" min={0} value={form.emailSettings.maxRetries || 0} disabled={form.emailSettings.usePlatformDefaults} onChange={(event) => setForm((current) => ({ ...current, emailSettings: { ...current.emailSettings, maxRetries: Number(event.target.value) || 0 } }))} className="field-input" /></Field>
          <Field label="Sender name"><input value={form.emailSettings.senderName} disabled={smtpFieldsDisabled} onChange={(event) => setForm((current) => ({ ...current, emailSettings: { ...current.emailSettings, senderName: event.target.value } }))} className="field-input" /></Field>
          <Field label="Sender email"><input data-testid="tenant-sender-email-input" type="email" value={form.emailSettings.senderEmail} disabled={form.emailSettings.usePlatformDefaults} onChange={(event) => setForm((current) => ({ ...current, emailSettings: { ...current.emailSettings, senderEmail: event.target.value } }))} className="field-input" /></Field>
          <Field label="Reply-to email"><input type="email" value={form.emailSettings.replyToEmail} disabled={form.emailSettings.usePlatformDefaults} onChange={(event) => setForm((current) => ({ ...current, emailSettings: { ...current.emailSettings, replyToEmail: event.target.value } }))} className="field-input" /></Field>
        </div>

        <div className="flex justify-end">
          <button data-testid="tenant-save-email-settings-button" type="button" className="button-primary" onClick={() => void save()} disabled={saving}>
            {saving ? 'Saving...' : 'Save tenant email settings'}
          </button>
        </div>
      </section>

      <section data-testid="tenant-notification-rules-card" className="surface-card space-y-4">
        <div>
          <p className="text-lg font-semibold text-app">Notification Rules</p>
          <p className="mt-1 text-sm text-muted">ServiceOps module notification triggers for this tenant.</p>
        </div>
        <div className="space-y-3">
          {notificationSections.map((section) => (
            <article key={section.title} className="panel-subtle rounded-2xl p-4">
              <p className="text-sm font-semibold text-app">{section.title}</p>
              <div className="mt-3 grid gap-2 md:grid-cols-2">
                {section.items.map((item) => {
                  const value = notificationMap.get(item.key)
                  if (!value) return null
                  return (
                    <label key={item.key} className="flex items-center justify-between rounded-xl border border-border/60 bg-app/40 px-3 py-2">
                      <span className="text-sm text-app">{item.label}</span>
                      <input type="checkbox" checked={value.emailEnabled && value.isActive} onChange={(event) => toggleNotification(item.key, { emailEnabled: event.target.checked, isActive: event.target.checked })} />
                    </label>
                  )
                })}
              </div>
            </article>
          ))}
        </div>
      </section>

      <section data-testid="tenant-recipients-card" className="surface-card space-y-4">
        <div>
          <p className="text-lg font-semibold text-app">Recipients</p>
          <p className="mt-1 text-sm text-muted">Comma-separated emails for each alert group.</p>
        </div>
        <div className="grid gap-3 md:grid-cols-2">
          {recipientGroups.map((item) => (
            <Field key={item.group} label={item.label}>
              <textarea
                value={recipientText[item.group] || ''}
                onChange={(event) => setRecipientText((current) => ({ ...current, [item.group]: event.target.value }))}
                className="field-input min-h-[90px]"
                placeholder="email1@example.com, email2@example.com"
              />
            </Field>
          ))}
        </div>
      </section>

      <section data-testid="tenant-test-verification-card" className="surface-card space-y-4">
        <div>
          <p className="text-lg font-semibold text-app">Test & Verification</p>
          <p className="mt-1 text-sm text-muted">Validate connectivity and test delivery for this tenant configuration.</p>
        </div>
        <div className="grid gap-3 md:grid-cols-[1fr_auto_auto]">
          <Field label="Test recipient email"><input type="email" value={testRecipient} onChange={(event) => setTestRecipient(event.target.value)} className="field-input" placeholder="ops@example.com (optional)" /></Field>
          <button data-testid="tenant-verify-smtp-button" type="button" className="button-secondary self-end" onClick={() => void verifySmtp()} disabled={verifying}>{verifying ? 'Verifying...' : 'Verify delivery connection'}</button>
          <button data-testid="tenant-send-test-email-button" type="button" className="button-secondary self-end" onClick={() => void sendTestEmail()} disabled={testing}>{testing ? 'Sending...' : 'Send test email'}</button>
        </div>
        {lastResult ? (
          <InfoAlert
            title={lastResult.success ? 'Last action succeeded' : 'Last action failed'}
            description={lastResult.message}
            tone={lastResult.success ? 'success' : 'danger'}
          />
        ) : null}
      </section>
    </div>
  )
}

function splitEmails(value: string) {
  return value
    .split(',')
    .map((item) => item.trim().toLowerCase())
    .filter(Boolean)
}

function applyRecommendedTenantPort(current: TenantCommunicationSettings, port: number): TenantCommunicationSettings {
  if (port === 465) {
    return { ...current, emailSettings: { ...current.emailSettings, smtpPort: port, enableSslTls: true, secureMode: 'SslOnConnect' } }
  }

  if (port === 587) {
    return { ...current, emailSettings: { ...current.emailSettings, smtpPort: port, enableSslTls: true, secureMode: 'StartTls' } }
  }

  return { ...current, emailSettings: { ...current.emailSettings, smtpPort: port } }
}
