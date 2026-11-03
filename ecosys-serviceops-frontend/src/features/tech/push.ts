import { useCallback, useEffect, useState } from 'react'
import { api } from '../../shared/api/apiClient'
import { endpoints } from '../../shared/api/endpoints'

/**
 * Push notifications on this device: new job assignments, and anything else the user has set to
 * Push. The browser asks for permission only when the user taps "Turn on" (browsers block prompts
 * that aren't started by a tap). The subscription is sent to the API, whose push worker sends to it
 * (WebPush.cs, NotificationInboxWorkers.cs).
 *
 * iOS only supports push for the app added to the home screen, not in Safari itself.
 */

export type PushStatus =
  | 'unsupported' // browser can't do push, or the app isn't installed on iOS
  | 'ios-install' // iOS Safari: works once added to the home screen
  | 'denied' // the user blocked notifications for this site
  | 'off'
  | 'on'

const DISMISSED_KEY = 'ecosys-job-alerts-dismissed'

function isIos() {
  return /iPad|iPhone|iPod/.test(navigator.userAgent) || (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1)
}

async function getRegistration() {
  if (!('serviceWorker' in navigator) || !('PushManager' in window) || !('Notification' in window)) return null
  // No worker is registered in `vite dev`; don't wait forever on navigator.serviceWorker.ready.
  return (await navigator.serviceWorker.getRegistration()) ?? null
}

export async function getPushStatus(): Promise<PushStatus> {
  const registration = await getRegistration()
  if (!registration) return isIos() && !('PushManager' in window) ? 'ios-install' : 'unsupported'
  if (Notification.permission === 'denied') return 'denied'
  const subscription = await registration.pushManager.getSubscription()
  return subscription && Notification.permission === 'granted' ? 'on' : 'off'
}

function urlBase64ToUint8Array(value: string) {
  const padded = (value + '='.repeat((4 - (value.length % 4)) % 4)).replace(/-/g, '+').replace(/_/g, '/')
  const raw = atob(padded)
  return Uint8Array.from(raw, (char) => char.charCodeAt(0))
}

async function saveSubscription(subscription: PushSubscription) {
  const json = subscription.toJSON()
  await api.post(endpoints.me.pushSubscriptions, { endpoint: json.endpoint, keys: json.keys }, { suppressGlobalErrorToast: true })
}

/** Asks for permission (must run from a tap) and registers this device. */
export async function enablePush(): Promise<PushStatus> {
  const registration = await getRegistration()
  if (!registration) return 'unsupported'
  const permission = await Notification.requestPermission()
  if (permission !== 'granted') return permission === 'denied' ? 'denied' : 'off'

  const { publicKey } = await api.get<{ publicKey: string }>(endpoints.me.pushPublicKey, undefined, { suppressGlobalErrorToast: true })
  const applicationServerKey = urlBase64ToUint8Array(publicKey)
  let subscription = await registration.pushManager.getSubscription()
  if (subscription && !sameKey(subscription, applicationServerKey)) {
    await subscription.unsubscribe()
    subscription = null
  }
  subscription ??= await registration.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey })
  await saveSubscription(subscription)
  return 'on'
}

/** Stops alerts on this device (also used on sign-out, so the next person on this phone doesn't get them). */
export async function disablePush() {
  const registration = await getRegistration()
  const subscription = await registration?.pushManager.getSubscription()
  if (!subscription) return
  try {
    await api.delete(endpoints.me.pushSubscriptions, { data: { endpoint: subscription.endpoint }, suppressGlobalErrorToast: true })
  } catch {
    // offline or signed out already; unsubscribing below still stops delivery (the API drops dead endpoints)
  }
  await subscription.unsubscribe()
}

/** Re-sends an existing subscription so the server's copy stays tied to whoever is signed in now. */
export async function resyncPush() {
  if ((await getPushStatus()) !== 'on') return
  const registration = await getRegistration()
  const subscription = await registration?.pushManager.getSubscription()
  if (subscription) await saveSubscription(subscription)
}

function sameKey(subscription: PushSubscription, key: Uint8Array) {
  const current = subscription.options.applicationServerKey
  if (!current) return false
  const bytes = new Uint8Array(current)
  return bytes.length === key.length && bytes.every((byte, index) => byte === key[index])
}

export function isJobAlertsPromptDismissed() {
  try {
    return localStorage.getItem(DISMISSED_KEY) === '1'
  } catch {
    return false
  }
}

export function dismissJobAlertsPrompt() {
  try {
    localStorage.setItem(DISMISSED_KEY, '1')
  } catch {
    // storage unavailable; the prompt may show again next time
  }
}

export function usePushStatus() {
  const [status, setStatus] = useState<PushStatus | null>(null)
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    let cancelled = false
    void getPushStatus().then((value) => { if (!cancelled) setStatus(value) }, () => { if (!cancelled) setStatus('unsupported') })
    return () => { cancelled = true }
  }, [])

  const run = useCallback(async (action: () => Promise<PushStatus | void>) => {
    setBusy(true)
    try {
      const next = (await action()) ?? (await getPushStatus())
      setStatus(next)
      return next
    } finally {
      setBusy(false)
    }
  }, [])

  return {
    status,
    busy,
    enable: () => run(enablePush),
    disable: () => run(disablePush),
  }
}
