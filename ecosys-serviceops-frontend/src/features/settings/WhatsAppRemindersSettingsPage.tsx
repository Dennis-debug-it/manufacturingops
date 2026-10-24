import { useState } from 'react'
import toast from 'react-hot-toast'
import { MessageCircle } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { Spinner } from '../../shared/components/Spinner'
import { ErrorState } from '../../shared/components/ErrorState'
import { formatDateTime } from '../../shared/utils/dates'
import { cn } from '../../shared/utils/cn'
import { outboxStatusLabel, toggleAllowedKey, usagePercent } from '../whatsapp/reminderLogic'
import { useSaveWhatsAppTenantSettings, useWhatsAppOutbox, useWhatsAppTenantSettings, type WhatsAppKeySetting, type WhatsAppOutboxItem } from '../whatsapp/remindersApi'

const cardClass = 'rounded-xl border border-slate-200 bg-white p-4'

const statusTone: Record<string, string> = {
  Read: 'bg-emerald-50 text-emerald-700 ring-emerald-200',
  Delivered: 'bg-emerald-50 text-emerald-700 ring-emerald-200',
  Sent: 'bg-sky-50 text-sky-700 ring-sky-200',
  Pending: 'bg-slate-50 text-slate-600 ring-slate-200',
  Sending: 'bg-slate-50 text-slate-600 ring-slate-200',
  Failed: 'bg-rose-50 text-rose-700 ring-rose-200',
  Skipped: 'bg-amber-50 text-amber-800 ring-amber-200',
}

function KeyList({ title, description, items, allowed, disabled, onToggle }: {
  title: string
  description: string
  items: WhatsAppKeySetting[]
  allowed: string[]
  disabled: boolean
  onToggle: (key: string, on: boolean) => void
}) {
  return (
    <section className={cardClass}>
      <h2 className="text-sm font-semibold text-slate-900">{title}</h2>
      <p className="mt-0.5 text-sm text-slate-500">{description}</p>
      <ul className="mt-2 divide-y divide-slate-100">
        {items.map((item) => {
          const on = allowed.includes(item.key)
          return (
            <li key={item.key} className="flex flex-wrap items-center justify-between gap-2 py-2">
              <span className="min-w-0 text-sm text-slate-800">
                {item.label}
                {item.isDefault ? <span className="ml-2 text-xs text-slate-400">on by default</span> : null}
              </span>
              <button
                type="button"
                role="switch"
                aria-checked={on}
                aria-label={`${item.label} by WhatsApp`}
                disabled={disabled}
                onClick={() => onToggle(item.key, !on)}
                className={cn('h-8 rounded-md px-3 text-xs font-medium ring-1 ring-inset disabled:opacity-60', on ? 'bg-[var(--app-primary)] text-white ring-transparent' : 'bg-white text-slate-500 ring-slate-300')}
              >
                {on ? 'Allowed' : 'Off'}
              </button>
            </li>
          )
        })}
      </ul>
    </section>
  )
}

function OutboxLog() {
  const [status, setStatus] = useState('')
  const { data, isLoading, isError, fetchNextPage, hasNextPage, isFetchingNextPage } = useWhatsAppOutbox(status)
  const items: WhatsAppOutboxItem[] = data?.pages.flatMap((page) => page.items) ?? []

  return (
    <section className={cardClass}>
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h2 className="text-sm font-semibold text-slate-900">Message log</h2>
        <select value={status} onChange={(event) => setStatus(event.target.value)} aria-label="Filter by status" className="h-9 rounded-lg border border-slate-300 px-2 text-sm">
          <option value="">All messages</option>
          {['Pending', 'Sent', 'Delivered', 'Read', 'Failed', 'Skipped'].map((value) => <option key={value} value={value}>{outboxStatusLabel(value)}</option>)}
        </select>
      </div>
      {isLoading ? <Spinner label="Loading messages" /> : isError ? (
        <p className="mt-2 text-sm text-rose-700">The message log could not be loaded.</p>
      ) : items.length === 0 ? (
        <p className="mt-2 text-sm text-slate-500">No WhatsApp messages yet.</p>
      ) : (
        <ul className="mt-2 divide-y divide-slate-100">
          {items.map((item) => (
            <li key={item.id} className="flex flex-wrap items-start justify-between gap-2 py-2 text-sm">
              <div className="min-w-0">
                <p className="text-slate-900">{item.label} <span className="text-slate-500">to {item.recipientName ? `${item.recipientName} · ` : ''}{item.to}</span></p>
                <p className="text-xs text-slate-500">
                  {formatDateTime(item.createdAt)}
                  {item.readAt ? ` · read ${formatDateTime(item.readAt)}` : item.deliveredAt ? ` · delivered ${formatDateTime(item.deliveredAt)}` : ''}
                  {item.attemptCount > 1 ? ` · ${item.attemptCount} attempts` : ''}
                </p>
                {item.error && (item.status === 'Failed' || item.status === 'Skipped' || item.status === 'Pending') ? <p className="text-xs text-rose-700">{item.error}</p> : null}
              </div>
              <span className={cn('shrink-0 rounded-full px-2 py-0.5 text-xs font-medium ring-1 ring-inset', statusTone[item.status] ?? statusTone.Pending)}>{outboxStatusLabel(item.status)}</span>
            </li>
          ))}
        </ul>
      )}
      {hasNextPage ? (
        <button type="button" disabled={isFetchingNextPage} onClick={() => void fetchNextPage()} className="mt-2 inline-flex h-9 items-center rounded-lg border border-slate-300 bg-white px-3 text-sm font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-60">
          {isFetchingNextPage ? 'Loading…' : 'Show older'}
        </button>
      ) : null}
    </section>
  )
}

/** Settings → WhatsApp Reminders (Release 3 design 4.2). */
export function WhatsAppRemindersSettingsPage() {
  const { data, isLoading, isError, refetch } = useWhatsAppTenantSettings()
  const save = useSaveWhatsAppTenantSettings()

  if (isLoading) return <Spinner label="Loading WhatsApp settings" />
  if (isError || !data) {
    return <ErrorState title="WhatsApp settings unavailable" description="We couldn’t load the WhatsApp settings. Nothing has been changed — try again." onRetry={() => void refetch()} />
  }

  const allowed = data.keys.filter((k) => k.allowed).map((k) => k.key)
  const toggle = async (key: string, on: boolean) => {
    try {
      await save.mutateAsync(toggleAllowedKey(allowed, key, on))
      toast.success('Saved')
    } catch {
      // interceptor surfaces the error toast
    }
  }
  const percent = usagePercent(data.usedThisMonth, data.monthlyCap)

  return (
    <div className="space-y-4">
      <PageHeader title="WhatsApp reminders" subtitle="Choose which notifications may also go out on WhatsApp." />

      <section className={cn(cardClass, 'flex items-start gap-3')}>
        <MessageCircle className="mt-0.5 h-5 w-5 shrink-0 text-emerald-600" />
        <div className="min-w-0 text-sm text-slate-600">
          <p>
            Messages are sent from the shared Ecosys WhatsApp number{data.sharedNumber ? ` (${data.sharedNumber})` : ''}, not your own.
            Every message starts with your company name so people know who it is from. Ecosys looks after the WhatsApp account and its message templates.
          </p>
          <p className="mt-1">
            People only get WhatsApp messages after they opt in: staff confirm their own number under My Notifications or in the technician app,
            and client contacts confirm theirs in the portal or you record their agreement on the client’s Client Portal tab. Anyone can reply STOP to stop all messages.
            Email still goes out as usual.
          </p>
        </div>
      </section>

      {!data.featureEnabled ? (
        <p className="rounded-xl border border-amber-200 bg-amber-50 p-4 text-sm text-amber-800">WhatsApp reminders aren’t switched on for this workspace. Contact Ecosys to turn them on.</p>
      ) : !data.configured ? (
        <p className="rounded-xl border border-amber-200 bg-amber-50 p-4 text-sm text-amber-800">The Ecosys WhatsApp number isn’t live yet. Your choices are saved and take effect once it is.</p>
      ) : null}

      <section className={cardClass}>
        <div className="flex items-center justify-between text-sm">
          <span className="font-semibold text-slate-900">This month</span>
          <span className="text-slate-600">{data.usedThisMonth.toLocaleString()} of {data.monthlyCap.toLocaleString()} messages</span>
        </div>
        <div className="mt-2 h-2 overflow-hidden rounded-full bg-slate-100" role="progressbar" aria-valuenow={percent} aria-valuemin={0} aria-valuemax={100} aria-label="WhatsApp messages used this month">
          <div className={cn('h-full rounded-full', percent >= 90 ? 'bg-amber-500' : 'bg-emerald-500')} style={{ width: `${percent}%` }} />
        </div>
        <p className="mt-1 text-xs text-slate-500">Past the monthly limit, WhatsApp messages are skipped until next month. Email is not affected.</p>
      </section>

      <KeyList
        title="Staff reminders"
        description="Each person also chooses which of these they want, on their own notifications page."
        items={data.keys.filter((k) => k.audience === 'Staff')}
        allowed={allowed}
        disabled={save.isPending || !data.featureEnabled}
        onToggle={(key, on) => void toggle(key, on)}
      />
      <KeyList
        title="Client updates"
        description="Sent to client contacts who agreed to WhatsApp messages."
        items={data.keys.filter((k) => k.audience === 'Client')}
        allowed={allowed}
        disabled={save.isPending || !data.featureEnabled}
        onToggle={(key, on) => void toggle(key, on)}
      />

      <OutboxLog />
    </div>
  )
}
