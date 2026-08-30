import { useEffect, useState } from 'react'
import toast from 'react-hot-toast'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Info, Send } from 'lucide-react'
import { api } from '../../shared/api/apiClient'
import { PageHeader } from '../../shared/components/PageHeader'
import { Spinner } from '../../shared/components/Spinner'
import { ErrorState } from '../../shared/components/ErrorState'
import { usePermission } from '../../shared/hooks/usePermission'
import { formatDateTime } from '../../shared/utils/dates'

type DeliveryMode = 'Disabled' | 'Smtp' | 'Api'
type SecureMode = 'Auto' | 'None' | 'StartTls' | 'SslOnConnect'

interface EmailDeliverySettings {
  isEnabled: boolean
  deliveryMode: DeliveryMode
  fromName: string
  fromEmail: string
  replyToEmail: string | null
  smtpHost: string
  smtpPort: number
  enableSslTls: boolean
  secureMode: SecureMode
  smtpUsername: string | null
  timeoutSeconds: number
  maxRetries: number
  hasSecret: boolean
  isConfigured: boolean
  lastTestedAt: string | null
  lastError: string | null
  usePlatformDefaults: boolean
  /** What is really in force — the fields above are inert while this is true. */
  usesPlatformMail: boolean
  effectiveFromEmail: string | null
}

interface EmailDeliveryTestResult {
  success: boolean
  lastTestedAt: string | null
  lastError: string | null
  usedPlatformMail: boolean
  message: string
}

const settingsUrl = '/api/settings/email-notifications'

const inputClass =
  'h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)] disabled:bg-slate-50 disabled:text-slate-500'

export function EmailDeliverySettingsPage() {
  const canManage = usePermission('settings.manage')
  const queryClient = useQueryClient()
  const { data, isLoading, isError, refetch } = useQuery({
    queryKey: ['settings', 'email-delivery'],
    queryFn: () => api.get<EmailDeliverySettings>(settingsUrl),
  })

  const [values, setValues] = useState<EmailDeliverySettings | null>(null)
  const [smtpPassword, setSmtpPassword] = useState('')

  useEffect(() => {
    if (data) {
      setValues(data)
      setSmtpPassword('')
    }
  }, [data])

  const save = useMutation({
    mutationFn: (payload: EmailDeliverySettings & { smtpPasswordSecret: string | null }) =>
      api.put<EmailDeliverySettings>(settingsUrl, payload),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['settings', 'email-delivery'] }),
  })

  const sendTest = useMutation({
    mutationFn: (recipient: string | null) =>
      api.post<EmailDeliveryTestResult>(`${settingsUrl}/test`, { testRecipientEmail: recipient }),
    onSuccess: (result) => {
      if (result.success) {
        toast.success(result.message)
      } else {
        toast.error(`${result.message}${result.lastError ? ` (${result.lastError})` : ''}`)
      }
      void queryClient.invalidateQueries({ queryKey: ['settings', 'email-delivery'] })
    },
  })

  if (isLoading) {
    return <Spinner label="Loading email delivery settings" />
  }

  // Without this branch an exhausted retry leaves `values` null forever and the spinner never releases.
  if (isError || !values) {
    return (
      <ErrorState
        title="Email delivery settings unavailable"
        description="We couldn’t load this workspace’s mail configuration. Nothing has been changed — try again."
        onRetry={() => void refetch()}
      />
    )
  }

  const usingOwnSmtp = !values.usePlatformDefaults
  const fieldsDisabled = !canManage || !usingOwnSmtp

  const setField = <K extends keyof EmailDeliverySettings>(key: K, value: EmailDeliverySettings[K]) =>
    setValues((current) => (current ? { ...current, [key]: value } : current))

  const onSave = async () => {
    if (!values) return
    try {
      // A blank password leaves the stored secret untouched — the API never sends it back to us.
      await save.mutateAsync({ ...values, smtpPasswordSecret: smtpPassword.trim() === '' ? null : smtpPassword })
      setSmtpPassword('')
      toast.success('Email delivery settings saved')
    } catch {
      // interceptor surfaces the error
    }
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title="Email Delivery"
        subtitle="Choose whether this workspace sends email through Ecosys platform mail or your own SMTP server."
        primaryAction={
          canManage ? (
            <button
              type="button"
              onClick={() => void onSave()}
              disabled={save.isPending}
              className="inline-flex h-11 items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:opacity-60"
            >
              {save.isPending ? 'Saving…' : 'Save Changes'}
            </button>
          ) : undefined
        }
      />

      <section className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-5 shadow-sm">
        <h2 className="text-sm font-semibold text-slate-900">Delivery mode</h2>
        <div className="mt-3 space-y-3">
          <label className="flex items-start gap-3 text-sm text-slate-700">
            <input
              type="radio"
              name="deliveryMode"
              checked={values.usePlatformDefaults}
              disabled={!canManage}
              onChange={() => setField('usePlatformDefaults', true)}
              className="mt-1 h-4 w-4 border-slate-300"
            />
            <span>
              <span className="font-medium text-slate-900">Use Ecosys platform mail</span>
              <span className="block text-xs text-slate-500">Nothing to configure. Email is sent by the platform on your behalf.</span>
            </span>
          </label>
          <label className="flex items-start gap-3 text-sm text-slate-700">
            <input
              type="radio"
              name="deliveryMode"
              checked={usingOwnSmtp}
              disabled={!canManage}
              onChange={() => setField('usePlatformDefaults', false)}
              className="mt-1 h-4 w-4 border-slate-300"
            />
            <span>
              <span className="font-medium text-slate-900">Use our own SMTP server</span>
              <span className="block text-xs text-slate-500">Email is sent from your mail server, using the details below.</span>
            </span>
          </label>
        </div>

        <div className="mt-4 flex items-start gap-2 rounded-lg bg-slate-50 p-3 text-xs text-slate-600">
          <Info className="mt-0.5 h-4 w-4 shrink-0 text-slate-400" />
          <span>
            {values.usesPlatformMail ? (
              <>
                Currently sending through <strong>platform mail</strong>
                {values.effectiveFromEmail ? <> as <strong>{values.effectiveFromEmail}</strong></> : null}. The SMTP details below are not in use.
              </>
            ) : (
              <>
                Currently sending through <strong>your own SMTP server</strong>
                {values.effectiveFromEmail ? <> as <strong>{values.effectiveFromEmail}</strong></> : null}.
              </>
            )}
            {usingOwnSmtp !== !values.usesPlatformMail ? ' Save your change to apply the new mode.' : ''}
          </span>
        </div>
      </section>

      <section className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-5 shadow-sm">
        <h2 className="text-sm font-semibold text-slate-900">Sender</h2>
        <div className="mt-4 grid grid-cols-1 gap-4 sm:grid-cols-2">
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="fromName">From name</label>
            <input id="fromName" value={values.fromName} disabled={!canManage} onChange={(event) => setField('fromName', event.target.value)} className={inputClass} />
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="fromEmail">From email</label>
            <input id="fromEmail" type="email" value={values.fromEmail} disabled={!canManage} onChange={(event) => setField('fromEmail', event.target.value)} className={inputClass} />
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="replyToEmail">Reply-to email</label>
            <input id="replyToEmail" type="email" value={values.replyToEmail ?? ''} disabled={!canManage} onChange={(event) => setField('replyToEmail', event.target.value)} className={inputClass} />
          </div>
        </div>
      </section>

      <section className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-5 shadow-sm">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <h2 className="text-sm font-semibold text-slate-900">SMTP server</h2>
          {!usingOwnSmtp ? <span className="text-xs text-slate-500">Switch to “our own SMTP server” above to edit these.</span> : null}
        </div>
        <div className="mt-4 grid grid-cols-1 gap-4 sm:grid-cols-2">
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="smtpHost">Host</label>
            <input id="smtpHost" value={values.smtpHost} disabled={fieldsDisabled} placeholder="smtp.example.com" onChange={(event) => setField('smtpHost', event.target.value)} className={inputClass} />
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="smtpPort">Port</label>
            <input id="smtpPort" type="number" value={values.smtpPort} disabled={fieldsDisabled} onChange={(event) => setField('smtpPort', Number(event.target.value))} className={inputClass} />
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="secureMode">Security</label>
            <select id="secureMode" value={values.secureMode} disabled={fieldsDisabled} onChange={(event) => setField('secureMode', event.target.value as SecureMode)} className={`${inputClass} bg-white`}>
              <option value="Auto">Auto (from port)</option>
              <option value="StartTls">STARTTLS</option>
              <option value="SslOnConnect">SSL on connect</option>
              <option value="None">None</option>
            </select>
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="smtpUsername">Username</label>
            <input id="smtpUsername" value={values.smtpUsername ?? ''} disabled={fieldsDisabled} onChange={(event) => setField('smtpUsername', event.target.value)} className={inputClass} />
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="smtpPassword">Password</label>
            <input
              id="smtpPassword"
              type="password"
              value={smtpPassword}
              disabled={fieldsDisabled}
              placeholder={values.hasSecret ? 'Saved — leave blank to keep' : ''}
              onChange={(event) => setSmtpPassword(event.target.value)}
              className={inputClass}
            />
          </div>
        </div>
      </section>

      <section className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-5 shadow-sm">
        <h2 className="text-sm font-semibold text-slate-900">Test</h2>
        <p className="mt-1 text-xs text-slate-500">
          Sends a test message using whichever mailbox is currently in force, so the result reflects what your workspace really does.
        </p>
        <div className="mt-3 flex flex-wrap items-center gap-3">
          <button
            type="button"
            onClick={() => sendTest.mutate(null)}
            disabled={!canManage || sendTest.isPending}
            className="inline-flex h-11 items-center gap-2 rounded-lg border border-slate-300 px-4 text-sm font-medium text-slate-700 transition hover:border-[var(--app-primary)] hover:text-[var(--app-primary)] disabled:opacity-60"
          >
            <Send className="h-4 w-4" />
            {sendTest.isPending ? 'Sending…' : 'Send test email'}
          </button>
          {values.lastTestedAt ? (
            <span className="text-xs text-slate-500">
              Last tested {formatDateTime(values.lastTestedAt)}
              {values.lastError ? <span className="text-rose-600"> — {values.lastError}</span> : <span className="text-emerald-700"> — no errors</span>}
            </span>
          ) : (
            <span className="text-xs text-slate-500">Never tested.</span>
          )}
        </div>
      </section>
    </div>
  )
}
