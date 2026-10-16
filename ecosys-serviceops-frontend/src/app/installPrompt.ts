/**
 * Offers "install the app" to users instead of relying on the browser to do it unasked.
 *
 * Chrome/Edge/Samsung Internet never open an install dialog on their own: they fire
 * `beforeinstallprompt` and, at most, show a small infobar that is easy to miss (and never on
 * desktop). We keep that event and show our own banner, whose button calls `prompt()`.
 * iOS Safari has no install API at all, so there the banner explains Share → Add to Home Screen.
 *
 * Installed with `installPromptListener()` at start-up (before React renders) because the event
 * can fire as soon as the page loads.
 */

interface BeforeInstallPromptEvent extends Event {
  prompt(): Promise<void>
  userChoice: Promise<{ outcome: 'accepted' | 'dismissed' }>
}

export type InstallPromptState =
  | { kind: 'hidden' }
  | { kind: 'native' }
  | { kind: 'ios' }

const DISMISSED_KEY = 'ecosys-install-dismissed-at'
/** After "Not now", don't ask again on this device for a week. */
const DISMISS_FOR_MS = 7 * 24 * 60 * 60 * 1000

let deferred: BeforeInstallPromptEvent | null = null
let state: InstallPromptState = { kind: 'hidden' }
const listeners = new Set<() => void>()

function setState(next: InstallPromptState) {
  state = next
  listeners.forEach((listener) => listener())
}

function isStandalone() {
  return (
    window.matchMedia?.('(display-mode: standalone)').matches === true ||
    (navigator as Navigator & { standalone?: boolean }).standalone === true
  )
}

function isIosSafari() {
  const ua = navigator.userAgent
  const ios = /iPad|iPhone|iPod/.test(ua) || (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1)
  // Other iOS browsers (CriOS, FxiOS, EdgiOS) can't add to the home screen in older iOS versions.
  return ios && /Safari/.test(ua) && !/CriOS|FxiOS|EdgiOS|OPiOS/.test(ua)
}

function recentlyDismissed() {
  try {
    const at = Number(localStorage.getItem(DISMISSED_KEY))
    return Number.isFinite(at) && at > 0 && Date.now() - at < DISMISS_FOR_MS
  } catch {
    return false
  }
}

export function installPromptListener() {
  if (isStandalone()) return

  window.addEventListener('beforeinstallprompt', (event) => {
    // Stop the browser's own mini-infobar; our banner offers the same thing more visibly.
    event.preventDefault()
    deferred = event as BeforeInstallPromptEvent
    if (!recentlyDismissed()) setState({ kind: 'native' })
  })

  window.addEventListener('appinstalled', () => {
    deferred = null
    setState({ kind: 'hidden' })
  })

  if (isIosSafari() && !recentlyDismissed()) setState({ kind: 'ios' })
}

export function subscribeInstallPrompt(listener: () => void) {
  listeners.add(listener)
  return () => {
    listeners.delete(listener)
  }
}

export function getInstallPromptState() {
  return state
}

/** Opens the browser's install dialog. */
export async function promptInstall() {
  const event = deferred
  if (!event) return
  deferred = null
  setState({ kind: 'hidden' })
  await event.prompt()
  const { outcome } = await event.userChoice
  if (outcome === 'dismissed') rememberDismissal()
}

export function dismissInstallPrompt() {
  rememberDismissal()
  setState({ kind: 'hidden' })
}

function rememberDismissal() {
  try {
    localStorage.setItem(DISMISSED_KEY, String(Date.now()))
  } catch {
    // storage unavailable (private mode); the banner may come back next visit
  }
}
