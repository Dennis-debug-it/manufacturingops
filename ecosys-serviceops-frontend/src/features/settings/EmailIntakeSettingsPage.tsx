import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import toast from 'react-hot-toast'
import { api } from '../../shared/api/apiClient'
import { resolveErrorMessage } from '../../shared/api/apiError'
import { useAuth } from '../../shared/hooks/useAuth'
import { PageHeader } from '../../shared/components/PageHeader'
import { Spinner } from '../../shared/components/Spinner'
import { formatDateTime } from '../../shared/utils/dates'
import { useClients } from '../clients/api'
import { useAssignmentGroups } from '../team/api'

interface EmailIntakeSettings {
  isEnabled: boolean
  intakeEmailAddress: string | null
  host: string
  port: number
  useSsl: boolean
  username: string | null
  hasPassword: boolean
  defaultClientId: string | null
  defaultBranchId: string | null
  defaultAssignmentGroupId: string | null
  defaultPriority: string
  createWorkOrderFromUnknownSender: boolean
  subjectParsingRules: string | null
  allowedSenderDomains: string | null
  lastCheckedAt: string | null
  isConnectionHealthy: boolean
  lastError: string | null
  autoReplyEnabled: boolean
  isReading: boolean
}

interface Draft {
  isEnabled: boolean
  intakeEmailAddress: string
  host: string
  port: string
  useSsl: boolean
  username: string
  password: string
  defaultClientId: string
  defaultAssignmentGroupId: string
  defaultPriority: string
  createWorkOrderFromUnknownSender: boolean
  subjectParsingRules: string
  allowedSenderDomains: string
  autoReplyEnabled: boolean
}

interface IntakeMessage {
  id: string
  receivedAt: string
  fromAddress: string
  fromName: string | null
  subject: string
  outcome: 'WorkOrderCreated' | 'AddedToWorkOrder' | 'Rejected' | 'Ignored' | 'Failed'
  detail: string | null
  workOrderId: string | null
  workOrderNumber: string | null
  autoReplySent: boolean
}

const URL = '/api/settings/email-intake'
const PRIORITIES = ['Low', 'Medium', 'High', 'Critical']
const OUTCOME_LABELS: Record<IntakeMessage['outcome'], { label: string; className: string }> = {
  WorkOrderCreated: { label: 'Work order created', className: 'bg-emerald-50 text-emerald-700' },
  AddedToWorkOrder: { label: 'Added to job', className: 'bg-sky-50 text-sky-700' },
  Rejected: { label: 'Not recognised', className: 'bg-amber-50 text-amber-800' },
  Ignored: { label: 'Ignored', className: 'bg-slate-100 text-slate-600' },
  Failed: { label: 'Failed', className: 'bg-rose-50 text-rose-700' },
}

const toDraft = (value: EmailIntakeSettings): Draft => ({
  isEnabled: value.isEnabled,
  intakeEmailAddress: value.intakeEmailAddress ?? '',
  host: value.host ?? '',
  port: String(value.port || 993),
  useSsl: value.useSsl,
  username: value.username ?? '',
  password: '',
  defaultClientId: value.defaultClientId ?? '',
  defaultAssignmentGroupId: value.defaultAssignmentGroupId ?? '',
  defaultPriority: PRIORITIES.includes(value.defaultPriority) ? value.defaultPriority : 'Medium',
  createWorkOrderFromUnknownSender: value.createWorkOrderFromUnknownSender,
  subjectParsingRules: value.subjectParsingRules ?? '',
  allowedSenderDomains: value.allowedSenderDomains ?? '',
  autoReplyEnabled: value.autoReplyEnabled,
})

/** Email intake: a mailbox the workspace already has; new emails become work orders, with automatic replies. */
export function EmailIntakeSettingsPage() {
  const { user, tenant } = useAuth()
  const canManage = user?.role === 'admin' || user?.role === 'super_admin'
  const queryClient = useQueryClient()
  const settingsKey = ['email-intake', tenant?.tenantId]
  const messagesKey = ['email-intake-messages', tenant?.tenantId]
  const settings = useQuery({ queryKey: settingsKey, queryFn: () => api.get<EmailIntakeSettings>(URL), enabled: canManage })
  const messages = useQuery({ queryKey: messagesKey, queryFn: () => api.get<IntakeMessage[]>(`${URL}/messages`), enabled: canManage, refetchInterval: 30_000 })
  const clients = useClients()
  const groups = useAssignmentGroups()
  const [draft, setDraft] = useState<Draft | null>(null)
  const [testResult, setTestResult] = useState<{ success: boolean; message: string } | null>(null)
  const values = draft ?? (settings.data ? toDraft(settings.data) : null)
  const saved = settings.data

  const save = useMutation({
    mutationFn: (value: Draft) => api.put<EmailIntakeSettings>(URL, {
      isEnabled: value.isEnabled,
      intakeEmailAddress: value.intakeEmailAddress.trim() || null,
      mailboxProvider: 'IMAP',
      host: value.host.trim(),
      port: Number(value.port) || 993,
      useSsl: value.useSsl,
      username: value.username.trim() || null,
      password: value.password || null,
      defaultClientId: value.defaultClientId || null,
      defaultBranchId: saved?.defaultBranchId ?? null,
      defaultAssignmentGroupId: value.defaultAssignmentGroupId || null,
      defaultPriority: value.defaultPriority,
      createWorkOrderFromUnknownSender: value.createWorkOrderFromUnknownSender,
      subjectParsingRules: value.subjectParsingRules.trim() || null,
      allowedSenderDomains: value.allowedSenderDomains.trim() || null,
      autoReplyEnabled: value.autoReplyEnabled,
    }, { suppressGlobalErrorToast: true }),
    onSuccess: (value) => {
      queryClient.setQueryData(settingsKey, value)
      setDraft(null)
      toast.success(value.isEnabled ? 'Saved — new emails will become work orders' : 'Saved')
    },
    onError: (error) => toast.error(resolveErrorMessage(error)),
  })

  const test = useMutation({
    mutationFn: (value: Draft) => api.post<{ success: boolean; lastError: string | null }>(`${URL}/test-connection`, {
      host: value.host.trim() || null,
      port: Number(value.port) || 993,
      useSsl: value.useSsl,
      username: value.username.trim() || null,
      password: value.password || null,
    }, { suppressGlobalErrorToast: true }),
    onSuccess: (result) => {
      setTestResult(result.success
        ? { success: true, message: 'Connected — the mailbox can be read.' }
        : { success: false, message: `Could not connect: ${result.lastError ?? 'unknown error'}` })
      void queryClient.invalidateQueries({ queryKey: settingsKey })
    },
    onError: (error) => setTestResult({ success: false, message: resolveErrorMessage(error) }),
  })

  const checkNow = useMutation({
    mutationFn: () => api.post<{ handled: number; isConnectionHealthy: boolean; lastError: string | null }>(`${URL}/check-now`, {}),
    onSuccess: (result) => {
      if (!result.isConnectionHealthy) toast.error(result.lastError ?? 'The mailbox could not be read.')
      else toast.success(result.handled === 0 ? 'No new emails' : `${result.handled} new email${result.handled === 1 ? '' : 's'} handled`)
      void queryClient.invalidateQueries({ queryKey: settingsKey })
      void queryClient.invalidateQueries({ queryKey: messagesKey })
    },
  })

  const set = <K extends keyof Draft>(key: K, value: Draft[K]) => { if (values) setDraft({ ...values, [key]: value }) }
  const inputClass = 'h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900'
  const sectionClass = 'space-y-4 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm'

  if (!canManage) {
    return <div className="space-y-4"><PageHeader title="Email Intake" /><p className="text-sm text-slate-600">Your workspace administrator manages email intake.</p></div>
  }

  return <div className="space-y-4">
    <PageHeader title="Email Intake" subtitle="Clients email your service mailbox; each new email becomes a work order, and they get an automatic reply." />
    {settings.isLoading ? <Spinner label="Loading email intake" />
      : !values || !saved ? <p role="alert" className="text-sm text-slate-600">Email intake settings could not be loaded.</p>
      : <>
        <div data-testid="intake-status" className={`flex flex-wrap items-center justify-between gap-3 rounded-xl border p-4 text-sm ${!saved.isEnabled ? 'border-slate-200 bg-white text-slate-700' : saved.lastError ? 'border-rose-200 bg-rose-50 text-rose-800' : 'border-emerald-200 bg-emerald-50 text-emerald-900'}`}>
          <div>
            <p className="font-semibold">
              {!saved.isEnabled ? 'Email intake is off' : saved.lastError ? 'The mailbox could not be read' : saved.isReading ? `Reading ${saved.username ?? 'the mailbox'} every minute` : 'Connecting to the mailbox…'}
            </p>
            <p className="mt-1 text-xs">
              {saved.lastError ? saved.lastError : saved.lastCheckedAt ? `Last checked ${formatDateTime(saved.lastCheckedAt)}.` : 'Emails that arrive after intake is switched on are picked up.'}
            </p>
          </div>
          {saved.isEnabled ? (
            <button type="button" onClick={() => checkNow.mutate()} disabled={checkNow.isPending || draft !== null} className="inline-flex h-10 items-center rounded-lg border border-slate-300 bg-white px-3 text-sm font-medium text-slate-700 disabled:opacity-60">
              {checkNow.isPending ? 'Checking…' : 'Check now'}
            </button>
          ) : null}
        </div>

        <form onSubmit={(event) => { event.preventDefault(); save.mutate(values) }} className="space-y-4">
          <section className={sectionClass}>
            <label className="flex items-center gap-2 text-sm font-medium text-slate-800">
              <input type="checkbox" checked={values.isEnabled} onChange={(event) => set('isEnabled', event.target.checked)} />
              Turn emails into work orders
            </label>
            <h2 className="text-sm font-semibold text-slate-900">Service mailbox</h2>
            <p className="text-xs text-slate-500">
              The mailbox clients send requests to, e.g. service@yourcompany.com. Your email provider can give you its incoming (IMAP) server.
              Gmail: imap.gmail.com, port 993, with an app password. Most hosting and cPanel mailboxes work with their normal password.
              Microsoft 365 mailboxes don't allow password sign-in for this, so use another mailbox (or forward to one).
            </p>
            <div className="grid gap-4 sm:grid-cols-2">
              <label className="space-y-2 text-sm text-slate-700 sm:col-span-2"><span>Service email address</span><input type="email" placeholder="service@yourcompany.com" value={values.intakeEmailAddress} onChange={(event) => set('intakeEmailAddress', event.target.value)} className={inputClass} /></label>
              <label className="space-y-2 text-sm text-slate-700"><span>Incoming mail server (IMAP)</span><input required={values.isEnabled} placeholder="imap.yourcompany.com" value={values.host} onChange={(event) => set('host', event.target.value)} className={inputClass} /></label>
              <div className="grid grid-cols-2 gap-3">
                <label className="space-y-2 text-sm text-slate-700"><span>Port</span><input type="number" min={1} max={65535} value={values.port} onChange={(event) => set('port', event.target.value)} className={inputClass} /></label>
                <label className="flex items-end gap-2 pb-3 text-sm text-slate-700"><input type="checkbox" checked={values.useSsl} onChange={(event) => set('useSsl', event.target.checked)} />Use SSL</label>
              </div>
              <label className="space-y-2 text-sm text-slate-700"><span>Mailbox username</span><input autoComplete="off" required={values.isEnabled} value={values.username} onChange={(event) => set('username', event.target.value)} className={inputClass} /></label>
              <label className="space-y-2 text-sm text-slate-700"><span>Mailbox password</span><input type="password" autoComplete="new-password" required={values.isEnabled && !saved.hasPassword} placeholder={saved.hasPassword ? 'Saved — leave blank to keep' : ''} value={values.password} onChange={(event) => set('password', event.target.value)} className={inputClass} /></label>
            </div>
            <div className="flex flex-wrap items-center gap-3">
              <button type="button" onClick={() => test.mutate(values)} disabled={test.isPending} className="inline-flex h-10 items-center rounded-lg border border-slate-300 bg-white px-3 text-sm font-medium text-slate-700 disabled:opacity-60">{test.isPending ? 'Testing…' : 'Test connection'}</button>
              {testResult ? <p role="status" className={`text-sm ${testResult.success ? 'text-emerald-700' : 'text-rose-700'}`}>{testResult.message}</p> : null}
            </div>
          </section>

          <section className={sectionClass}>
            <h2 className="text-sm font-semibold text-slate-900">Creating work orders</h2>
            <p className="text-xs text-slate-500">
              The sender is matched to a client by their email address (client email, site contact or portal contact) or their company's email domain.
              A reply that keeps the work order number in the subject is added to that job instead of creating a new one.
            </p>
            <div className="grid gap-4 sm:grid-cols-2">
              <label className="space-y-2 text-sm text-slate-700"><span>Default priority</span>
                <select value={values.defaultPriority} onChange={(event) => set('defaultPriority', event.target.value)} className={inputClass}>
                  {PRIORITIES.map((priority) => <option key={priority} value={priority}>{priority}</option>)}
                </select>
              </label>
              <label className="space-y-2 text-sm text-slate-700"><span>Assign to group (optional)</span>
                <select value={values.defaultAssignmentGroupId} onChange={(event) => set('defaultAssignmentGroupId', event.target.value)} className={inputClass}>
                  <option value="">No group</option>
                  {(groups.data ?? []).map((group) => <option key={group.id} value={group.id}>{group.name}</option>)}
                </select>
              </label>
              <label className="space-y-2 text-sm text-slate-700 sm:col-span-2"><span>Priority keywords in the subject</span>
                <textarea rows={3} placeholder={'emergency = Critical\nurgent = High'} value={values.subjectParsingRules} onChange={(event) => set('subjectParsingRules', event.target.value)} className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-slate-900" />
                <span className="block text-xs text-slate-500">One per line: keyword = Low, Medium, High or Critical. Left blank, "emergency" means Critical and "urgent" means High.</span>
              </label>
            </div>
            <label className="flex items-center gap-2 text-sm text-slate-700">
              <input type="checkbox" checked={values.createWorkOrderFromUnknownSender} onChange={(event) => set('createWorkOrderFromUnknownSender', event.target.checked)} />
              Also accept emails from senders who aren't a known client
            </label>
            {values.createWorkOrderFromUnknownSender ? (
              <div className="grid gap-4 sm:grid-cols-2">
                <label className="space-y-2 text-sm text-slate-700"><span>Log them under this client</span>
                  <select required value={values.defaultClientId} onChange={(event) => set('defaultClientId', event.target.value)} className={inputClass}>
                    <option value="">Choose a client…</option>
                    {(clients.data ?? []).map((client) => <option key={client.id} value={client.id}>{client.clientName}</option>)}
                  </select>
                </label>
                <label className="space-y-2 text-sm text-slate-700"><span>Only from these domains (optional)</span><input placeholder="acme.com, example.co.ke" value={values.allowedSenderDomains} onChange={(event) => set('allowedSenderDomains', event.target.value)} className={inputClass} /></label>
              </div>
            ) : null}
          </section>

          <section className={sectionClass}>
            <label className="flex items-center gap-2 text-sm font-medium text-slate-800">
              <input type="checkbox" checked={values.autoReplyEnabled} onChange={(event) => set('autoReplyEnabled', event.target.checked)} />
              Send automatic replies
            </label>
            <ul className="list-disc space-y-1 pl-5 text-sm text-slate-600">
              <li><strong>Request received</strong> — thanks the sender and gives the work order number.</li>
              <li><strong>Added to job</strong> — confirms a reply was added to their work order.</li>
              <li><strong>Not recognised</strong> — tells an unknown sender their email wasn't logged.</li>
            </ul>
            <p className="text-xs text-slate-500">
              Change the wording under <Link to="/settings/email-templates" className="font-medium text-slate-700 underline">Email Templates</Link>.
              Auto-responders, mailing lists and no-reply senders never get a reply, and each address gets at most three an hour.
            </p>
          </section>

          <button disabled={save.isPending} className="inline-flex h-11 items-center rounded-lg bg-slate-900 px-4 text-sm font-medium text-white hover:bg-slate-800 disabled:opacity-60">{save.isPending ? 'Saving…' : 'Save Changes'}</button>
        </form>

        <section className={sectionClass}>
          <h2 className="text-sm font-semibold text-slate-900">Recent emails</h2>
          {messages.isLoading ? <Spinner label="Loading recent emails" />
            : (messages.data ?? []).length === 0 ? <p className="text-sm text-slate-500">No emails yet. New emails appear here within a minute of arriving.</p>
            : (
              <ul className="divide-y divide-slate-100" data-testid="intake-messages">
                {(messages.data ?? []).map((message) => {
                  const outcome = OUTCOME_LABELS[message.outcome] ?? OUTCOME_LABELS.Ignored
                  return (
                    <li key={message.id} className="flex flex-wrap items-start justify-between gap-3 py-3 text-sm">
                      <div className="min-w-0">
                        <p className="truncate font-medium text-slate-900">{message.subject}</p>
                        <p className="text-xs text-slate-500">{message.fromName ? `${message.fromName} · ` : ''}{message.fromAddress} · {formatDateTime(message.receivedAt)}</p>
                        {message.detail ? <p className="text-xs text-slate-500">{message.detail}{message.autoReplySent ? ' Auto-reply sent.' : ''}</p> : null}
                      </div>
                      <div className="flex items-center gap-2">
                        {message.workOrderId && message.workOrderNumber ? <Link to={`/work-orders/${message.workOrderId}`} className="text-xs font-medium text-slate-700 underline">{message.workOrderNumber}</Link> : null}
                        <span className={`rounded-full px-2 py-0.5 text-[11px] font-medium ${outcome.className}`}>{outcome.label}</span>
                      </div>
                    </li>
                  )
                })}
              </ul>
            )}
        </section>
      </>}
  </div>
}
