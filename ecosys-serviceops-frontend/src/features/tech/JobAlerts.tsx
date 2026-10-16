import { useState } from 'react'
import toast from 'react-hot-toast'
import { BellRing } from 'lucide-react'
import { cn } from '../../shared/utils/cn'
import { dismissJobAlertsPrompt, isJobAlertsPromptDismissed, usePushStatus, type PushStatus } from './push'

const turnOnFailed = "Couldn't turn on job alerts. Check your connection and try again."

function resultToast(status: PushStatus | null) {
  if (status === 'on') toast.success("Job alerts are on. You'll be notified when you're assigned a job.")
  else if (status === 'denied') toast.error('Notifications are blocked for this site. Allow them in your browser settings to get job alerts.')
}

/** One-time card at the top of the technician app offering alerts for new jobs. */
export function JobAlertsPrompt() {
  const { status, busy, enable } = usePushStatus()
  const [dismissed, setDismissed] = useState(isJobAlertsPromptDismissed)

  if (status !== 'off' || dismissed) return null

  const turnOn = async () => {
    try {
      resultToast(await enable())
    } catch {
      toast.error(turnOnFailed)
    }
  }

  return (
    <section className="mb-3 flex items-start gap-3 rounded-xl border border-teal-200 bg-teal-50 p-4">
      <BellRing className="mt-0.5 h-5 w-5 shrink-0 text-teal-700" />
      <div className="min-w-0 flex-1">
        <p className="text-sm font-semibold text-slate-900">Get notified about new jobs</p>
        <p className="mt-0.5 text-sm text-slate-600">We'll send a notification to this phone when a job is assigned to you, even when the app is closed.</p>
        <div className="mt-3 flex gap-2">
          <button
            type="button"
            disabled={busy}
            onClick={() => void turnOn()}
            className="inline-flex h-9 items-center rounded-lg bg-[var(--app-primary)] px-3 text-sm font-semibold text-white disabled:opacity-60"
          >
            {busy ? 'Turning on…' : 'Turn on'}
          </button>
          <button
            type="button"
            onClick={() => { dismissJobAlertsPrompt(); setDismissed(true) }}
            className="inline-flex h-9 items-center rounded-lg border border-slate-300 bg-white px-3 text-sm font-medium text-slate-700"
          >
            Not now
          </button>
        </div>
      </div>
    </section>
  )
}

/** On/off switch for new-job alerts on this device, in the technician's account page. */
export function JobAlertsSetting() {
  const { status, busy, enable, disable } = usePushStatus()

  const toggle = async () => {
    try {
      if (status === 'on') {
        await disable()
      } else {
        resultToast(await enable())
      }
    } catch {
      toast.error(turnOnFailed)
    }
  }

  const hint =
    status === 'denied' ? 'Notifications are blocked for this site. Allow them in your browser or phone settings, then come back here.'
    : status === 'ios-install' ? 'On iPhone, add Ecosys to your Home Screen (Share → Add to Home Screen) and open it from there to get job alerts.'
    : status === 'unsupported' ? "This browser can't show job alerts."
    : 'A notification on this phone when a job is assigned to you.'

  const canToggle = status === 'on' || status === 'off'

  return (
    <section className="space-y-2 rounded-xl border border-slate-200 bg-white p-4">
      <div className="flex items-center justify-between gap-3">
        <h2 className="text-sm font-semibold text-slate-900">New job alerts</h2>
        {canToggle ? (
          <button
            type="button"
            role="switch"
            aria-checked={status === 'on'}
            aria-label="New job alerts on this phone"
            disabled={busy}
            onClick={() => void toggle()}
            className={cn('h-8 rounded-md px-3 text-xs font-medium ring-1 ring-inset disabled:opacity-60', status === 'on' ? 'bg-[var(--app-primary)] text-white ring-transparent' : 'bg-white text-slate-600 ring-slate-300')}
          >
            {status === 'on' ? 'On' : 'Off'}
          </button>
        ) : null}
      </div>
      <p className="text-sm text-slate-500">{hint}</p>
    </section>
  )
}
