import toast from 'react-hot-toast'
import { BellRing } from 'lucide-react'
import { Spinner } from '../../shared/components/Spinner'
import { PageHeader } from '../../shared/components/PageHeader'
import { cn } from '../../shared/utils/cn'
import { usePushStatus } from '../tech/push'
import { sendTestPush, useNotificationPreferences, useSaveNotificationPreferences } from './api'
import { CHANNEL_FIELDS, groupPreferences } from './logic'
import type { NotificationPreference } from './types'
import { WhatsAppOptInCard } from '../whatsapp/WhatsAppOptInCard'
import { staffWhatsAppApi } from '../whatsapp/remindersApi'
import { WHATSAPP_ENABLED } from '../../shared/config/features'

type Field = (typeof CHANNEL_FIELDS)[number]['field']

/**
 * The signed-in user's choices per notification and channel, grouped by category. Each toggle
 * saves at once. Only the channels a notification can use get a toggle; WhatsApp shows only for the
 * notifications the workspace allows on WhatsApp, and sends only once the person has opted in.
 */
export function NotificationPreferencesList() {
  const { data, isLoading, isError } = useNotificationPreferences()
  const save = useSaveNotificationPreferences()

  const toggle = async (pref: NotificationPreference, field: Field) => {
    try {
      await save.mutateAsync([{
        notificationKey: pref.notificationKey,
        emailEnabled: pref.emailEnabled,
        inAppEnabled: pref.inAppEnabled,
        pushEnabled: pref.pushEnabled,
        whatsAppEnabled: pref.whatsAppEnabled,
        [field]: !pref[field],
      }])
    } catch {
      // interceptor surfaces the error toast
    }
  }

  if (isLoading) return <Spinner label="Loading notification settings" />
  if (isError) return <p className="text-sm text-rose-700">Notification settings could not be loaded.</p>

  return (
    <div className="space-y-3">
      {groupPreferences(data ?? []).map((group) => (
        <section key={group.category} className="rounded-xl border border-slate-200 bg-white p-4">
          <h2 className="text-sm font-semibold text-slate-900">{group.category}</h2>
          <ul className="mt-1 divide-y divide-slate-100">
            {group.items.map((pref) => (
              <li key={pref.notificationKey} className="flex flex-wrap items-center justify-between gap-2 py-2">
                <span className="min-w-0 text-sm text-slate-800">{pref.label}</span>
                <span className="flex shrink-0 gap-1">
                  {CHANNEL_FIELDS.filter(({ channel }) => pref.channels.includes(channel)).map(({ field, label }) => (
                    <button
                      key={field}
                      type="button"
                      role="switch"
                      aria-checked={pref[field]}
                      aria-label={`${pref.label}: ${label}`}
                      disabled={save.isPending}
                      onClick={() => void toggle(pref, field)}
                      className={cn('h-8 rounded-md px-2.5 text-xs font-medium ring-1 ring-inset disabled:opacity-60', pref[field] ? 'bg-[var(--app-primary)] text-white ring-transparent' : 'bg-white text-slate-500 ring-slate-300')}
                    >
                      {label}
                    </button>
                  ))}
                </span>
              </li>
            ))}
          </ul>
        </section>
      ))}
    </div>
  )
}

/** Push needs this browser's permission as well as the "Push" toggles. */
function DevicePush() {
  const { status, busy, enable, disable } = usePushStatus()
  const canToggle = status === 'on' || status === 'off'

  const toggle = async () => {
    try {
      const next = status === 'on' ? await disable() : await enable()
      if (next === 'denied') toast.error('Notifications are blocked for this site. Allow them in your browser settings.')
    } catch {
      toast.error("Couldn't change push notifications. Check your connection and try again.")
    }
  }

  const test = async () => {
    try {
      await sendTestPush()
      toast.success('Test sent. It should appear in a few seconds.')
    } catch {
      toast.error("Couldn't send a test notification.")
    }
  }

  return (
    <section className="flex flex-wrap items-start gap-3 rounded-xl border border-slate-200 bg-white p-4">
      <BellRing className="mt-0.5 h-5 w-5 shrink-0 text-slate-500" />
      <div className="min-w-0 flex-1">
        <p className="text-sm font-semibold text-slate-900">Push notifications on this device</p>
        <p className="mt-0.5 text-sm text-slate-500">
          {status === 'denied' ? 'Notifications are blocked for this site. Allow them in your browser settings, then come back here.'
            : status === 'unsupported' || status === 'ios-install' ? "This browser can't show push notifications."
            : 'Notifications you have set to Push appear on this device, even when Ecosys is closed.'}
        </p>
      </div>
      {canToggle ? (
        <span className="flex gap-2">
          {status === 'on' ? (
            <button type="button" onClick={() => void test()} className="inline-flex h-9 items-center rounded-lg border border-slate-300 bg-white px-3 text-sm font-medium text-slate-700 hover:bg-slate-50">
              Send a test
            </button>
          ) : null}
          <button
            type="button"
            role="switch"
            aria-checked={status === 'on'}
            aria-label="Push notifications on this device"
            disabled={busy}
            onClick={() => void toggle()}
            className={cn('h-9 rounded-lg px-3 text-sm font-medium ring-1 ring-inset disabled:opacity-60', status === 'on' ? 'bg-[var(--app-primary)] text-white ring-transparent' : 'bg-white text-slate-600 ring-slate-300')}
          >
            {status === 'on' ? 'On' : 'Off'}
          </button>
        </span>
      ) : null}
    </section>
  )
}

/** Office: /settings/notifications. */
export function MyNotificationSettingsPage() {
  return (
    <div className="space-y-4">
      <PageHeader title="My notifications" subtitle={WHATSAPP_ENABLED ? 'Choose how you hear about each event: in the bell, as a push notification, by email or on WhatsApp.' : 'Choose how you hear about each event: in the bell, as a push notification or by email.'} />
      <DevicePush />
      <WhatsAppOptInCard source={staffWhatsAppApi} audience="staff" />
      <NotificationPreferencesList />
    </div>
  )
}
