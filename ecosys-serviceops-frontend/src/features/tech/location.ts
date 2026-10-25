import { useCallback, useEffect, useState } from 'react'

/**
 * Location for arrival/departure. No app can turn location on by itself: the browser asks the
 * technician once and remembers the answer. We explain why first (LocationPrompt) and only then
 * trigger the browser prompt, which must come from a tap.
 */

export type LocationStatus =
  | 'unsupported' // no geolocation in this browser
  | 'prompt' // not asked yet, or the browser can't tell us (no Permissions API)
  | 'denied' // the user blocked location for this site
  | 'granted'

const DISMISSED_KEY = 'ecosys-location-prompt-dismissed'

export async function getLocationStatus(): Promise<LocationStatus> {
  if (!('geolocation' in navigator)) return 'unsupported'
  try {
    const result = await navigator.permissions.query({ name: 'geolocation' })
    return result.state
  } catch {
    // Older Safari has no geolocation permission query; all we can do is ask.
    return 'prompt'
  }
}

/**
 * Best-effort location for arrival/departure; never blocks the action. The browser's own timeout
 * only starts once the permission prompt is answered, so an ignored prompt would hang forever —
 * hence the separate cap.
 */
export function currentPosition(): Promise<{ latitude: number | null; longitude: number | null }> {
  const none = { latitude: null, longitude: null }
  return new Promise((resolve) => {
    if (!('geolocation' in navigator)) return resolve(none)
    const cap = window.setTimeout(() => resolve(none), 6000)
    navigator.geolocation.getCurrentPosition(
      (position) => { window.clearTimeout(cap); resolve({ latitude: position.coords.latitude, longitude: position.coords.longitude }) },
      () => { window.clearTimeout(cap); resolve(none) },
      { timeout: 5000, maximumAge: 60_000 },
    )
  })
}

/** Shows the browser's permission prompt; resolves with the technician's answer. */
function requestLocation(): Promise<LocationStatus> {
  return new Promise((resolve) => {
    if (!('geolocation' in navigator)) return resolve('unsupported')
    navigator.geolocation.getCurrentPosition(
      () => resolve('granted'),
      // A timeout or no fix still means they allowed it; only PERMISSION_DENIED is a "no".
      (error) => resolve(error.code === error.PERMISSION_DENIED ? 'denied' : 'granted'),
      { timeout: 15_000, maximumAge: 60_000 },
    )
  })
}

export function isLocationPromptDismissed() {
  try {
    return localStorage.getItem(DISMISSED_KEY) === '1'
  } catch {
    return false
  }
}

export function dismissLocationPrompt() {
  try {
    localStorage.setItem(DISMISSED_KEY, '1')
  } catch {
    // storage unavailable; the prompt may show again next time
  }
}

export function useLocationStatus() {
  const [status, setStatus] = useState<LocationStatus | null>(null)
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    let cancelled = false
    void getLocationStatus().then((value) => { if (!cancelled) setStatus(value) }, () => { if (!cancelled) setStatus('unsupported') })
    return () => { cancelled = true }
  }, [])

  const enable = useCallback(async () => {
    setBusy(true)
    try {
      const next = await requestLocation()
      setStatus(next)
      return next
    } finally {
      setBusy(false)
    }
  }, [])

  return { status, busy, enable }
}
