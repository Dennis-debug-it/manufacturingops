import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import toast from 'react-hot-toast'
import { api } from '../../shared/api/apiClient'
import { endpoints } from '../../shared/api/endpoints'
import { useAuth } from '../../shared/hooks/useAuth'
import { PageHeader } from '../../shared/components/PageHeader'
import { Spinner } from '../../shared/components/Spinner'

interface EmailIdentity {
  emailAddress: string | null
  displayName: string
  replyToAddress: string | null
  financeReplyToAddress: string | null
  operationsReplyToAddress: string | null
  inboundEnabled: boolean
  outboundEnabled: boolean
  emailSignature: string | null
}

interface EmailDelivery {
  useCustomSmtp: boolean
  /** What sends right now: "Custom" only once host and From address are saved. */
  activeMode: 'Platform' | 'Custom'
  host: string
  port: number
  useSsl: boolean
  username: string | null
  hasPassword: boolean
  fromAddress: string | null
  managedEmailAddress: string | null
  lastTestedAt: string | null
  lastError: string | null
}

interface ServerDraft {
  useCustomSmtp: boolean
  host: string
  port: string
  useSsl: boolean
  username: string
  password: string
  fromAddress: string
}

interface TestResult {
  success: boolean
  message: string
  technicalDetail: string | null
}

const toServerDraft = (value: EmailDelivery): ServerDraft => ({
  useCustomSmtp: value.useCustomSmtp,
  host: value.host ?? '',
  port: String(value.port || 587),
  useSsl: value.useSsl,
  username: value.username ?? '',
  password: '',
  fromAddress: value.fromAddress ?? '',
})

/**
 * The workspace's email, on one page: who mail comes from and where replies go, and — only for
 * workspaces that want it — their own mail server. (Sending through the workspace's own server used
 * to be a separate, technical "Email Delivery" page.)
 */
export function EmailIdentitySettingsPage() {
  const { user, tenant } = useAuth()
  const canManage = user?.role === 'admin' || user?.role === 'super_admin'
  const queryClient = useQueryClient()
  const identityKey = ['tenant-email-identity', tenant?.tenantId]
  const serverKey = ['tenant-email-delivery', tenant?.tenantId]
  const identity = useQuery({ queryKey: identityKey, queryFn: () => api.get<EmailIdentity>('/api/tenant/email-identity'), enabled: canManage })
  const server = useQuery({ queryKey: serverKey, queryFn: () => api.get<EmailDelivery>(endpoints.tenant.emailDelivery), enabled: canManage })
  const [draft, setDraft] = useState<EmailIdentity | null>(null)
  const [serverDraft, setServerDraft] = useState<ServerDraft | null>(null)
  const [testTo, setTestTo] = useState('')
  const [testResult, setTestResult] = useState<TestResult | null>(null)
  const values = draft ?? identity.data
  const serverValues = serverDraft ?? (server.data ? toServerDraft(server.data) : null)
  const saved = server.data

  const save = useMutation({
    mutationFn: async ({ value, serverValue }: { value: EmailIdentity; serverValue: ServerDraft | null }) => {
      const savedIdentity = await api.put<EmailIdentity>('/api/tenant/email-identity', {
        displayName: value.displayName.trim(), replyToAddress: value.replyToAddress?.trim() || null,
        financeReplyToAddress: value.financeReplyToAddress?.trim() || null,
        operationsReplyToAddress: value.operationsReplyToAddress?.trim() || null,
        // Not shown (they have no effect yet); sent back unchanged.
        inboundEnabled: value.inboundEnabled, emailSignature: value.emailSignature?.trim() || null,
        outboundEnabled: value.outboundEnabled,
      })
      const savedServer = serverValue
        ? await api.put<EmailDelivery>(endpoints.tenant.emailDelivery, {
          useCustomSmtp: serverValue.useCustomSmtp,
          host: serverValue.host.trim(),
          port: Number(serverValue.port) || 587,
          useSsl: serverValue.useSsl,
          username: serverValue.username.trim() || null,
          password: serverValue.password || null,
          fromAddress: serverValue.fromAddress.trim() || null,
        })
        : null
      return { savedIdentity, savedServer }
    },
    onSuccess: ({ savedIdentity, savedServer }) => {
      queryClient.setQueryData(identityKey, savedIdentity)
      if (savedServer) queryClient.setQueryData(serverKey, savedServer)
      setDraft(null)
      setServerDraft(null)
      setTestResult(null)
      toast.success('Email settings saved')
    },
  })

  const test = useMutation({
    mutationFn: () => api.post<TestResult>(endpoints.tenant.emailDeliveryTest, { toAddress: testTo.trim() || null }),
    onSuccess: (result) => {
      setTestResult(result)
      void queryClient.invalidateQueries({ queryKey: serverKey })
    },
  })

  const set = <K extends keyof EmailIdentity>(key: K, value: EmailIdentity[K]) => { if (values) setDraft({ ...values, [key]: value }) }
  const setServer = <K extends keyof ServerDraft>(key: K, value: ServerDraft[K]) => { if (serverValues) setServerDraft({ ...serverValues, [key]: value }) }
  const inputClass = 'h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900'
  const unsaved = draft !== null || serverDraft !== null
  const sendingFrom = saved?.activeMode === 'Custom' ? saved.fromAddress : values?.emailAddress

  return <div className="space-y-4">
    <PageHeader title="Email" subtitle="Who your emails come from and where replies go." />
    {!canManage ? <p className="text-sm text-slate-600">Your workspace administrator manages these email settings.</p>
      : identity.isLoading || server.isLoading ? <Spinner label="Loading email settings" />
      : !values ? <p role="alert" className="text-sm text-slate-600">Email settings could not be loaded.</p>
      : <form onSubmit={(event) => { event.preventDefault(); save.mutate({ value: values, serverValue: serverDraft }) }} className="space-y-5 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
        <div className="rounded-xl border border-emerald-200 bg-emerald-50 p-4">
          <p className="text-xs font-semibold uppercase tracking-wider text-emerald-800">Emails are sent from</p>
          <p className="mt-1 break-all text-sm font-medium text-emerald-950" data-testid="sending-from">{sendingFrom ?? 'Contact your platform administrator to provision your address.'}</p>
          <p className="mt-1 text-xs text-emerald-800">
            {saved?.activeMode === 'Custom' ? `Through your own mail server (${saved.host}).` : 'Set up for you by Ecosys — nothing to configure.'}
          </p>
          {saved?.lastError ? <p className="mt-2 text-xs font-medium text-rose-700">Last test failed: {saved.lastError}</p> : null}
        </div>

        <div className="grid gap-4 sm:grid-cols-2">
          <label className="space-y-2 text-sm text-slate-700"><span>Sender name</span><input required maxLength={150} value={values.displayName} onChange={(event) => set('displayName', event.target.value)} className={inputClass} /><span className="block text-xs text-slate-500">What clients see as the sender.</span></label>
          <label className="space-y-2 text-sm text-slate-700"><span>Replies go to</span><input type="email" placeholder="e.g. office@yourcompany.com" value={values.replyToAddress ?? ''} onChange={(event) => set('replyToAddress', event.target.value)} className={inputClass} /></label>
          <label className="space-y-2 text-sm text-slate-700"><span>Finance replies go to</span><input type="email" placeholder="Same as above when blank" value={values.financeReplyToAddress ?? ''} onChange={(event) => set('financeReplyToAddress', event.target.value)} className={inputClass} /><span className="block text-xs text-slate-500">Invoices, receipts, statements and quotations.</span></label>
          <label className="space-y-2 text-sm text-slate-700"><span>Operations replies go to</span><input type="email" placeholder="Same as above when blank" value={values.operationsReplyToAddress ?? ''} onChange={(event) => set('operationsReplyToAddress', event.target.value)} className={inputClass} /><span className="block text-xs text-slate-500">Work orders, maintenance and technician updates.</span></label>
        </div>

        <label className="flex items-center gap-2 text-sm text-slate-700"><input type="checkbox" checked={values.outboundEnabled} onChange={(event) => set('outboundEnabled', event.target.checked)} />Send emails from this workspace</label>

        {serverValues && saved ? (
          <div className="space-y-4 border-t border-slate-100 pt-4">
            <label className="flex items-center gap-2 text-sm font-medium text-slate-800">
              <input type="checkbox" checked={serverValues.useCustomSmtp} onChange={(event) => setServer('useCustomSmtp', event.target.checked)} />
              Send from our own mail server (advanced)
            </label>
            {serverValues.useCustomSmtp ? (
              <div className="grid gap-4 sm:grid-cols-2" data-testid="own-smtp-fields">
                <p className="text-xs text-slate-500 sm:col-span-2">Your IT team or email provider can give you these details.</p>
                <label className="space-y-2 text-sm text-slate-700"><span>SMTP Host</span><input required placeholder="smtp.yourcompany.com" value={serverValues.host} onChange={(event) => setServer('host', event.target.value)} className={inputClass} /></label>
                <div className="grid grid-cols-2 gap-3">
                  <label className="space-y-2 text-sm text-slate-700"><span>Port</span><input type="number" min={1} max={65535} value={serverValues.port} onChange={(event) => setServer('port', event.target.value)} className={inputClass} /></label>
                  <label className="flex items-end gap-2 pb-3 text-sm text-slate-700"><input type="checkbox" checked={serverValues.useSsl} onChange={(event) => setServer('useSsl', event.target.checked)} />Use TLS/SSL</label>
                </div>
                <label className="space-y-2 text-sm text-slate-700"><span>Username</span><input autoComplete="off" value={serverValues.username} onChange={(event) => setServer('username', event.target.value)} className={inputClass} /></label>
                <label className="space-y-2 text-sm text-slate-700"><span>Password</span><input type="password" autoComplete="new-password" placeholder={saved.hasPassword ? 'Saved — leave blank to keep' : ''} value={serverValues.password} onChange={(event) => setServer('password', event.target.value)} className={inputClass} /></label>
                <label className="space-y-2 text-sm text-slate-700 sm:col-span-2"><span>From Address</span><input type="email" required placeholder="service@yourcompany.com" value={serverValues.fromAddress} onChange={(event) => setServer('fromAddress', event.target.value)} className={inputClass} /><span className="block text-xs text-slate-500">Must be an address your mail server lets this account send as.</span></label>
              </div>
            ) : null}
          </div>
        ) : null}

        <div className="flex flex-wrap items-center gap-3">
          <button disabled={save.isPending} className="inline-flex h-11 items-center rounded-lg bg-slate-900 px-4 text-sm font-medium text-white hover:bg-slate-800 disabled:opacity-60">{save.isPending ? 'Saving…' : 'Save Changes'}</button>
          <Link to="/settings/email/delivery-logs" className="text-sm font-medium text-slate-600 underline underline-offset-4">See sent and failed emails</Link>
        </div>

        {saved?.activeMode === 'Custom' && serverValues?.useCustomSmtp ? (
          <section className="space-y-3 border-t border-slate-100 pt-4">
            <h2 className="text-sm font-semibold text-slate-900">Send a test email</h2>
            <p className="text-sm text-slate-500">Sends one real message through your mail server{saved.lastTestedAt ? ` · last tested ${new Date(saved.lastTestedAt).toLocaleString()}` : ''}.</p>
            <div className="flex flex-wrap gap-2">
              <input type="email" aria-label="Test recipient" placeholder={user?.hasEmail !== false && user?.email ? user.email : 'you@yourcompany.com'} value={testTo} onChange={(event) => setTestTo(event.target.value)} className={`${inputClass} max-w-sm`} />
              <button type="button" onClick={() => test.mutate()} disabled={test.isPending || unsaved} className="inline-flex h-11 items-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-60">{test.isPending ? 'Sending…' : 'Send Test'}</button>
            </div>
            {unsaved ? <p className="text-xs text-amber-700">Save your changes before testing.</p> : null}
            {testResult ? (
              <p role="status" className={`text-sm ${testResult.success ? 'text-emerald-700' : 'text-rose-700'}`}>
                {testResult.message}{testResult.technicalDetail ? <span className="block text-xs text-slate-500">{testResult.technicalDetail}</span> : null}
              </p>
            ) : null}
          </section>
        ) : null}
      </form>}
  </div>
}
