import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import toast from 'react-hot-toast'
import { api } from '../../shared/api/apiClient'
import { endpoints } from '../../shared/api/endpoints'
import { useAuth } from '../../shared/hooks/useAuth'
import { PageHeader } from '../../shared/components/PageHeader'
import { Spinner } from '../../shared/components/Spinner'

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

interface DeliveryDraft {
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

const toDraft = (value: EmailDelivery): DeliveryDraft => ({
  useCustomSmtp: value.useCustomSmtp,
  host: value.host ?? '',
  port: String(value.port || 587),
  useSsl: value.useSsl,
  username: value.username ?? '',
  password: '',
  fromAddress: value.fromAddress ?? '',
})

export function EmailDeliverySettingsPage() {
  const { user, tenant } = useAuth()
  const canManage = user?.role === 'admin' || user?.role === 'super_admin'
  const queryClient = useQueryClient()
  const queryKey = ['tenant-email-delivery', tenant?.tenantId]
  const delivery = useQuery({ queryKey, queryFn: () => api.get<EmailDelivery>(endpoints.tenant.emailDelivery), enabled: canManage })
  const [draft, setDraft] = useState<DeliveryDraft | null>(null)
  const [testTo, setTestTo] = useState('')
  const [testResult, setTestResult] = useState<TestResult | null>(null)
  const values = draft ?? (delivery.data ? toDraft(delivery.data) : null)

  const save = useMutation({
    mutationFn: (value: DeliveryDraft) => api.put<EmailDelivery>(endpoints.tenant.emailDelivery, {
      useCustomSmtp: value.useCustomSmtp,
      host: value.host.trim(),
      port: Number(value.port) || 587,
      useSsl: value.useSsl,
      username: value.username.trim() || null,
      password: value.password || null,
      fromAddress: value.fromAddress.trim() || null,
    }),
    onSuccess: (value) => {
      queryClient.setQueryData(queryKey, value)
      setDraft(null)
      setTestResult(null)
      toast.success(value.activeMode === 'Custom' ? 'Saved — email now sends through your mail server' : 'Saved — email sends through platform mail')
    },
  })

  const test = useMutation({
    mutationFn: () => api.post<TestResult>(endpoints.tenant.emailDeliveryTest, { toAddress: testTo.trim() || null }),
    onSuccess: (result) => {
      setTestResult(result)
      void queryClient.invalidateQueries({ queryKey })
    },
  })

  const set = <K extends keyof DeliveryDraft>(key: K, value: DeliveryDraft[K]) => { if (values) setDraft({ ...values, [key]: value }) }
  const inputClass = 'h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900 disabled:bg-slate-50 disabled:text-slate-400'
  const saved = delivery.data

  return <div className="space-y-4">
    <PageHeader title="Email Delivery" subtitle="Send through platform mail, or connect your own SMTP server so mail comes from your own domain." />
    {!canManage ? <p className="text-sm text-slate-600">Your workspace administrator manages email delivery.</p>
      : delivery.isLoading ? <Spinner label="Loading email delivery" />
      : !values || !saved ? <p role="alert" className="text-sm text-slate-600">Email delivery settings could not be loaded.</p>
      : <>
        <div className={`rounded-xl border p-4 text-sm ${saved.activeMode === 'Custom' ? 'border-emerald-200 bg-emerald-50 text-emerald-900' : 'border-slate-200 bg-white text-slate-700'}`}>
          <p className="font-semibold">{saved.activeMode === 'Custom' ? `Sending through ${saved.host} as ${saved.fromAddress}` : 'Sending through platform mail'}</p>
          <p className="mt-1 text-xs">
            {saved.activeMode === 'Custom'
              ? 'Invitations, password resets, invoices, quotes, reports and notifications all go through your server.'
              : `Mail is sent as ${saved.managedEmailAddress ?? 'your workspace address'} through the platform's mail server.`}
          </p>
          {saved.lastError ? <p className="mt-2 text-xs font-medium text-rose-700">Last test failed: {saved.lastError}</p> : null}
        </div>

        <form onSubmit={(event) => { event.preventDefault(); save.mutate(values) }} className="space-y-5 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
          <label className="flex items-center gap-2 text-sm font-medium text-slate-800">
            <input type="checkbox" checked={values.useCustomSmtp} onChange={(event) => set('useCustomSmtp', event.target.checked)} />
            Use my own SMTP server
          </label>
          <fieldset disabled={!values.useCustomSmtp} className="grid gap-4 sm:grid-cols-2">
            <label className="space-y-2 text-sm text-slate-700"><span>SMTP Host</span><input required={values.useCustomSmtp} placeholder="smtp.yourcompany.com" value={values.host} onChange={(event) => set('host', event.target.value)} className={inputClass} /></label>
            <div className="grid grid-cols-2 gap-3">
              <label className="space-y-2 text-sm text-slate-700"><span>Port</span><input type="number" min={1} max={65535} value={values.port} onChange={(event) => set('port', event.target.value)} className={inputClass} /></label>
              <label className="flex items-end gap-2 pb-3 text-sm text-slate-700"><input type="checkbox" checked={values.useSsl} onChange={(event) => set('useSsl', event.target.checked)} />Use TLS/SSL</label>
            </div>
            <label className="space-y-2 text-sm text-slate-700"><span>Username</span><input autoComplete="off" value={values.username} onChange={(event) => set('username', event.target.value)} className={inputClass} /></label>
            <label className="space-y-2 text-sm text-slate-700"><span>Password</span><input type="password" autoComplete="new-password" placeholder={saved.hasPassword ? 'Saved — leave blank to keep' : ''} value={values.password} onChange={(event) => set('password', event.target.value)} className={inputClass} /></label>
            <label className="space-y-2 text-sm text-slate-700 sm:col-span-2"><span>From Address</span><input type="email" required={values.useCustomSmtp} placeholder="service@yourcompany.com" value={values.fromAddress} onChange={(event) => set('fromAddress', event.target.value)} className={inputClass} /><span className="block text-xs text-slate-500">Must be an address your mail server allows this account to send as. Reply-to addresses from Workspace Email still apply.</span></label>
          </fieldset>
          <button disabled={save.isPending} className="inline-flex h-11 items-center rounded-lg bg-slate-900 px-4 text-sm font-medium text-white hover:bg-slate-800 disabled:opacity-60">{save.isPending ? 'Saving…' : 'Save Changes'}</button>
        </form>

        {saved.activeMode === 'Custom' ? (
          <section className="space-y-3 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
            <h2 className="text-sm font-semibold text-slate-900">Send a test email</h2>
            <p className="text-sm text-slate-500">Sends one real message through your saved server{saved.lastTestedAt ? ` · last tested ${new Date(saved.lastTestedAt).toLocaleString()}` : ''}.</p>
            <div className="flex flex-wrap gap-2">
              <input type="email" placeholder={user?.hasEmail !== false && user?.email ? user.email : 'you@yourcompany.com'} value={testTo} onChange={(event) => setTestTo(event.target.value)} className={`${inputClass} max-w-sm`} />
              <button type="button" onClick={() => test.mutate()} disabled={test.isPending || draft !== null} className="inline-flex h-11 items-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-60">{test.isPending ? 'Sending…' : 'Send Test'}</button>
            </div>
            {draft !== null ? <p className="text-xs text-amber-700">Save your changes before testing.</p> : null}
            {testResult ? (
              <p role="status" className={`text-sm ${testResult.success ? 'text-emerald-700' : 'text-rose-700'}`}>
                {testResult.message}{testResult.technicalDetail ? <span className="block text-xs text-slate-500">{testResult.technicalDetail}</span> : null}
              </p>
            ) : null}
          </section>
        ) : null}
      </>}
  </div>
}
