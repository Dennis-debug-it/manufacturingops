import { useEffect, useState } from 'react'
import toast from 'react-hot-toast'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { PageHeader } from '../../shared/components/PageHeader'
import { Spinner } from '../../shared/components/Spinner'
import { Collapsible } from '../../shared/components/Collapsible'
import { usePermission } from '../../shared/hooks/usePermission'
import { api } from '../../shared/api/apiClient'

type EmailDeliveryMode = 'Disabled' | 'Smtp' | 'Api'
type EmailSecureMode = 'None' | 'StartTls' | 'SslOnConnect'

interface NotificationPreference {
  notificationKey: string
  emailEnabled: boolean
  inAppEnabled: boolean
  smsEnabled: boolean
  isActive: boolean
}

interface PlatformEmailSettings {
  deliveryMode: EmailDeliveryMode
  smtpHost: string
  smtpPort: number
  smtpUsername: string | null
  smtpPasswordMasked: string | null
  senderName: string
  senderEmail: string
  replyToEmail: string | null
  enableSslTls: boolean
  secureMode: EmailSecureMode
  enableEmailNotifications: boolean
  enableSystemAlerts: boolean
  enableInvoiceEmails: boolean
  enableQuotationEmails: boolean
  enablePaymentReceiptEmails: boolean
  enableWorkOrderNotificationEmails: boolean
  enableSlaEscalationEmails: boolean
  enableTenantOnboardingEmails: boolean
  subjectPrefix: string
  subjectSuffix: string | null
  includeEnvironmentInSubject: boolean
  environmentLabel: string
  includeTenantNameInSubject: boolean
  enableEventSubjectTags: boolean
  notificationPreferences: NotificationPreference[]
  lastTestedAt: string | null
  lastError: string | null
}

const emailFlagFields: Array<{ key: keyof PlatformEmailSettings; label: string }> = [
  { key: 'enableSystemAlerts', label: 'System Alerts' },
  { key: 'enableInvoiceEmails', label: 'Invoice Emails' },
  { key: 'enableQuotationEmails', label: 'Quotation Emails' },
  { key: 'enablePaymentReceiptEmails', label: 'Payment Receipt Emails' },
  { key: 'enableWorkOrderNotificationEmails', label: 'Work Order Notification Emails' },
  { key: 'enableSlaEscalationEmails', label: 'SLA Escalation Emails' },
  { key: 'enableTenantOnboardingEmails', label: 'Tenant Onboarding Emails' },
]

export function EmailDeliverySettingsPage() {
  const canManage = usePermission('platform.settings.manage')
  const queryClient = useQueryClient()
  const { data, isLoading, isError } = useQuery({
    queryKey: ['platform', 'email-delivery'],
    queryFn: () => api.get<PlatformEmailSettings>('/api/platform/settings/email'),
    staleTime: 30_000,
  })

  const [form, setForm] = useState<PlatformEmailSettings | null>(null)
  const [smtpPasswordSecret, setSmtpPasswordSecret] = useState('')
  const [testRecipient, setTestRecipient] = useState('')

  useEffect(() => {
    if (data) setForm(data)
  }, [data])

  const save = useMutation({
    mutationFn: () => api.put<PlatformEmailSettings>('/api/platform/settings/email', {
      ...form,
      smtpPasswordSecret: smtpPasswordSecret || null,
      apiKeySecret: null,
      apiEndpoint: null,
      apiProviderName: null,
      timeoutSeconds: 30,
      maxRetries: 0,
    }),
    onSuccess: (result) => {
      toast.success('Platform email delivery settings saved')
      setForm(result)
      setSmtpPasswordSecret('')
      void queryClient.invalidateQueries({ queryKey: ['platform', 'email-delivery'] })
    },
  })

  const test = useMutation({
    mutationFn: () => api.post<{ success: boolean; message: string | null }>('/api/platform/settings/email/test', { testRecipientEmail: testRecipient || null }),
    onSuccess: (result) => toast[result.success ? 'success' : 'error'](result.message ?? (result.success ? 'Test email queued' : 'Test failed')),
  })

  const verify = useMutation({
    mutationFn: () => api.post<{ success: boolean; lastError: string | null }>('/api/platform/settings/email/verify'),
    onSuccess: (result) => toast[result.success ? 'success' : 'error'](result.success ? 'Connection verified' : (result.lastError ?? 'Verification failed')),
  })

  if (isLoading || !form) {
    return isLoading ? <Spinner label="Loading email delivery settings" /> : (
      <div className="rounded-2xl border border-dashed border-slate-300 bg-white p-8 text-center text-sm text-slate-600">Platform email delivery settings could not be loaded.</div>
    )
  }

  if (isError) {
    return <div className="rounded-2xl border border-dashed border-slate-300 bg-white p-8 text-center text-sm text-slate-600">Platform email delivery settings could not be loaded.</div>
  }

  const set = <K extends keyof PlatformEmailSettings>(key: K, value: PlatformEmailSettings[K]) =>
    setForm((current) => (current ? { ...current, [key]: value } : current))

  const setPreference = (notificationKey: string, field: 'emailEnabled' | 'inAppEnabled' | 'smsEnabled' | 'isActive', value: boolean) =>
    setForm((current) =>
      current
        ? { ...current, notificationPreferences: current.notificationPreferences.map((pref) => (pref.notificationKey === notificationKey ? { ...pref, [field]: value } : pref)) }
        : current,
    )

  return (
    <div className="space-y-6">
      <PageHeader
        title="Email Delivery"
        subtitle="Platform SMTP configuration, notification email flags, and subject line rules."
        primaryAction={canManage ? <button type="button" onClick={() => save.mutate()} disabled={save.isPending} className="inline-flex h-11 items-center justify-center rounded-lg bg-slate-900 px-4 text-sm font-medium text-white hover:bg-slate-800 disabled:opacity-60">{save.isPending ? 'Saving…' : 'Save Changes'}</button> : undefined}
      />

      {form.lastError ? <div className="rounded-2xl border border-rose-200 bg-rose-50 p-4 text-sm text-rose-700">Last error: {form.lastError}</div> : null}

      <Collapsible title="SMTP Delivery" defaultOpen>
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Delivery Mode</label>
            <select value={form.deliveryMode} disabled={!canManage} onChange={(event) => set('deliveryMode', event.target.value as EmailDeliveryMode)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900 disabled:bg-slate-50">
              {['Disabled', 'Smtp', 'Api'].map((mode) => <option key={mode} value={mode}>{mode}</option>)}
            </select>
          </div>
          <label className="flex items-center gap-2 self-end pb-2 text-sm text-slate-700">
            <input type="checkbox" checked={form.enableEmailNotifications} disabled={!canManage} onChange={(event) => set('enableEmailNotifications', event.target.checked)} className="h-4 w-4 rounded border-slate-300" />
            Enable Email Notifications
          </label>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">SMTP Host</label>
            <input value={form.smtpHost} disabled={!canManage} onChange={(event) => set('smtpHost', event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900 disabled:bg-slate-50" />
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">SMTP Port</label>
            <input type="number" value={form.smtpPort} disabled={!canManage} onChange={(event) => set('smtpPort', Number(event.target.value))} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900 disabled:bg-slate-50" />
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">SMTP Username</label>
            <input value={form.smtpUsername ?? ''} disabled={!canManage} onChange={(event) => set('smtpUsername', event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900 disabled:bg-slate-50" />
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">SMTP Password</label>
            <input type="password" value={smtpPasswordSecret} disabled={!canManage} onChange={(event) => setSmtpPasswordSecret(event.target.value)} placeholder={form.smtpPasswordMasked ?? 'Enter to set/replace'} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900 disabled:bg-slate-50" />
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Sender Name</label>
            <input value={form.senderName} disabled={!canManage} onChange={(event) => set('senderName', event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900 disabled:bg-slate-50" />
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Sender Email</label>
            <input value={form.senderEmail} disabled={!canManage} onChange={(event) => set('senderEmail', event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900 disabled:bg-slate-50" />
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Reply-To Email</label>
            <input value={form.replyToEmail ?? ''} disabled={!canManage} onChange={(event) => set('replyToEmail', event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900 disabled:bg-slate-50" />
          </div>
          <label className="flex items-center gap-2 self-end pb-2 text-sm text-slate-700">
            <input type="checkbox" checked={form.enableSslTls} disabled={!canManage} onChange={(event) => set('enableSslTls', event.target.checked)} className="h-4 w-4 rounded border-slate-300" />
            Use SSL/TLS
          </label>
        </div>
        {canManage ? (
          <div className="mt-4 flex flex-wrap items-center gap-2">
            <input value={testRecipient} onChange={(event) => setTestRecipient(event.target.value)} placeholder="Test recipient email (optional)" className="h-10 w-64 rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900" />
            <button type="button" onClick={() => test.mutate()} disabled={test.isPending} className="rounded-lg border border-slate-200 bg-white px-3 py-2 text-xs font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-60">Send Test Email</button>
            <button type="button" onClick={() => verify.mutate()} disabled={verify.isPending} className="rounded-lg border border-slate-200 bg-white px-3 py-2 text-xs font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-60">Verify Connection</button>
          </div>
        ) : null}
      </Collapsible>

      <Collapsible title="Notification Email Flags">
        <div className="grid grid-cols-1 gap-2 sm:grid-cols-2">
          {emailFlagFields.map((field) => (
            <label key={field.key} className="flex items-center gap-2 text-sm text-slate-700">
              <input type="checkbox" checked={Boolean(form[field.key])} disabled={!canManage} onChange={(event) => set(field.key, event.target.checked as never)} className="h-4 w-4 rounded border-slate-300" />
              {field.label}
            </label>
          ))}
        </div>
      </Collapsible>

      <Collapsible title="Subject Line Rules">
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Subject Prefix</label>
            <input value={form.subjectPrefix} disabled={!canManage} onChange={(event) => set('subjectPrefix', event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900 disabled:bg-slate-50" />
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Subject Suffix</label>
            <input value={form.subjectSuffix ?? ''} disabled={!canManage} onChange={(event) => set('subjectSuffix', event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900 disabled:bg-slate-50" />
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Environment Label</label>
            <input value={form.environmentLabel} disabled={!canManage} onChange={(event) => set('environmentLabel', event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900 disabled:bg-slate-50" />
          </div>
          <div className="flex flex-col justify-end gap-2 pb-1">
            <label className="flex items-center gap-2 text-sm text-slate-700">
              <input type="checkbox" checked={form.includeEnvironmentInSubject} disabled={!canManage} onChange={(event) => set('includeEnvironmentInSubject', event.target.checked)} className="h-4 w-4 rounded border-slate-300" />
              Include Environment in Subject
            </label>
            <label className="flex items-center gap-2 text-sm text-slate-700">
              <input type="checkbox" checked={form.includeTenantNameInSubject} disabled={!canManage} onChange={(event) => set('includeTenantNameInSubject', event.target.checked)} className="h-4 w-4 rounded border-slate-300" />
              Include Tenant Name in Subject
            </label>
            <label className="flex items-center gap-2 text-sm text-slate-700">
              <input type="checkbox" checked={form.enableEventSubjectTags} disabled={!canManage} onChange={(event) => set('enableEventSubjectTags', event.target.checked)} className="h-4 w-4 rounded border-slate-300" />
              Enable Event Subject Tags
            </label>
          </div>
        </div>
      </Collapsible>

      <Collapsible title="Notification Preferences">
        <div className="overflow-x-auto rounded-xl border border-slate-200">
          <table className="min-w-full divide-y divide-slate-200 text-left text-sm">
            <thead className="bg-slate-50">
              <tr>
                <th className="px-4 py-2 font-medium text-slate-600">Notification</th>
                <th className="px-4 py-2 text-center font-medium text-slate-600">Email</th>
                <th className="px-4 py-2 text-center font-medium text-slate-600">In-App</th>
                <th className="px-4 py-2 text-center font-medium text-slate-600">SMS</th>
                <th className="px-4 py-2 text-center font-medium text-slate-600">Active</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {form.notificationPreferences.map((pref) => (
                <tr key={pref.notificationKey}>
                  <td className="px-4 py-2 text-slate-700">{pref.notificationKey}</td>
                  <td className="px-4 py-2 text-center"><input type="checkbox" checked={pref.emailEnabled} disabled={!canManage} onChange={(event) => setPreference(pref.notificationKey, 'emailEnabled', event.target.checked)} className="h-4 w-4 rounded border-slate-300" /></td>
                  <td className="px-4 py-2 text-center"><input type="checkbox" checked={pref.inAppEnabled} disabled={!canManage} onChange={(event) => setPreference(pref.notificationKey, 'inAppEnabled', event.target.checked)} className="h-4 w-4 rounded border-slate-300" /></td>
                  <td className="px-4 py-2 text-center"><input type="checkbox" checked={pref.smsEnabled} disabled={!canManage} onChange={(event) => setPreference(pref.notificationKey, 'smsEnabled', event.target.checked)} className="h-4 w-4 rounded border-slate-300" /></td>
                  <td className="px-4 py-2 text-center"><input type="checkbox" checked={pref.isActive} disabled={!canManage} onChange={(event) => setPreference(pref.notificationKey, 'isActive', event.target.checked)} className="h-4 w-4 rounded border-slate-300" /></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </Collapsible>
    </div>
  )
}
