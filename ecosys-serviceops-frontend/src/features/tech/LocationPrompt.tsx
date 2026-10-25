import { useState } from 'react'
import toast from 'react-hot-toast'
import { MapPin } from 'lucide-react'
import { dismissLocationPrompt, isLocationPromptDismissed, useLocationStatus, type LocationStatus } from './location'

const blockedHint = 'Location is blocked for this site. To turn it on, tap the padlock next to the web address → Permissions → Location, or allow it for your browser in your phone settings.'

function resultToast(status: LocationStatus) {
  if (status === 'granted') toast.success("Location is on. Your arrivals and departures will be recorded automatically.")
  else if (status === 'denied') toast.error(blockedHint)
}

/** One-time card at the top of the technician app explaining why we ask for location, before the browser does. */
export function LocationPrompt() {
  const { status, busy, enable } = useLocationStatus()
  const [dismissed, setDismissed] = useState(isLocationPromptDismissed)

  if (status !== 'prompt' || dismissed) return null

  const turnOn = async () => {
    const next = await enable()
    resultToast(next)
    // Without the Permissions API the status can't tell us they've answered; don't ask twice.
    dismissLocationPrompt()
    setDismissed(true)
  }

  return (
    <section className="mb-3 flex items-start gap-3 rounded-xl border border-teal-200 bg-teal-50 p-4">
      <MapPin className="mt-0.5 h-5 w-5 shrink-0 text-teal-700" />
      <div className="min-w-0 flex-1">
        <p className="text-sm font-semibold text-slate-900">Confirm site arrivals with your location</p>
        <p className="mt-0.5 text-sm text-slate-600">
          We use your location only when you tap On My Way, I've Arrived or Record Departure, to confirm you reached the site. We don't track you in the background. Your phone will ask once; tap Allow.
        </p>
        <div className="mt-3 flex gap-2">
          <button
            type="button"
            disabled={busy}
            onClick={() => void turnOn()}
            className="inline-flex h-9 items-center rounded-lg bg-[var(--app-primary)] px-3 text-sm font-semibold text-white disabled:opacity-60"
          >
            {busy ? 'Waiting for your phone…' : 'Allow location'}
          </button>
          <button
            type="button"
            onClick={() => { dismissLocationPrompt(); setDismissed(true) }}
            className="inline-flex h-9 items-center rounded-lg border border-slate-300 bg-white px-3 text-sm font-medium text-slate-700"
          >
            Not now
          </button>
        </div>
      </div>
    </section>
  )
}

/** Location status for this device, in the technician's account page. */
export function LocationSetting() {
  const { status, busy, enable } = useLocationStatus()

  const hint =
    status === 'granted' ? 'On. Arrivals and departures record where you are.'
    : status === 'denied' ? blockedHint
    : status === 'unsupported' ? "This browser can't share location."
    : 'Used only when you record a trip, arrival or departure, to confirm you reached the site.'

  return (
    <section className="space-y-2 rounded-xl border border-slate-200 bg-white p-4">
      <div className="flex items-center justify-between gap-3">
        <h2 className="text-sm font-semibold text-slate-900">Location</h2>
        {status === 'prompt' ? (
          <button
            type="button"
            disabled={busy}
            onClick={() => void enable().then(resultToast)}
            className="h-8 rounded-md bg-white px-3 text-xs font-medium text-slate-600 ring-1 ring-inset ring-slate-300 disabled:opacity-60"
          >
            {busy ? 'Waiting…' : 'Allow'}
          </button>
        ) : null}
      </div>
      <p className="text-sm text-slate-500">{hint}</p>
    </section>
  )
}
