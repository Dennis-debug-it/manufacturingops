/**
 * Makes sure a phone never just shows a blank page. If the app has drawn nothing for a few
 * seconds (a script that failed to load, a stale copy of the app from an old service worker, an
 * error before anything rendered), a plain message appears with what went wrong and two ways out:
 * reload, or reset the app's cached files. Plain DOM on purpose, so it works when React doesn't.
 *
 * `?reset-app=1` in the address does the same reset on start-up, for when even this can't show.
 */

const RESET_PARAM = 'reset-app'
const CHECK_EVERY_MS = 2000
/** Consecutive empty checks before the message shows (so ~6s of nothing drawn). */
const EMPTY_CHECKS_BEFORE_MESSAGE = 3
/** IndexedDB databases that are only a cache. The outbox (unsynced work) is never deleted. */
const CACHE_DATABASES = ['ecosys-tech-cache']

let lastError: string | null = null

export function recordBootError(error: unknown) {
  lastError = error instanceof Error ? `${error.name}: ${error.message}` : String(error)
}

/** Clears the app's cached files and data (not unsynced work) and loads it fresh. */
export async function resetApp() {
  try {
    const registrations = await navigator.serviceWorker?.getRegistrations?.() ?? []
    await Promise.all(registrations.map((registration) => registration.unregister()))
  } catch {
    // no service worker support (e.g. served over plain http)
  }
  try {
    const keys = await caches.keys()
    await Promise.all(keys.map((key) => caches.delete(key)))
  } catch {
    // CacheStorage unavailable
  }
  for (const name of CACHE_DATABASES) {
    try {
      indexedDB.deleteDatabase(name)
    } catch {
      // ignore
    }
  }
  window.location.replace('/login')
}

function showMessage(root: HTMLElement) {
  if (document.getElementById('ecosys-boot-message')) return
  const box = document.createElement('div')
  box.id = 'ecosys-boot-message'
  box.setAttribute('role', 'alert')
  box.style.cssText = 'position:fixed;inset:0;display:flex;align-items:center;justify-content:center;padding:24px;background:#eef3f2;font-family:system-ui,sans-serif;z-index:2147483647'
  const card = document.createElement('div')
  card.style.cssText = 'max-width:420px;width:100%;background:#fff;border:1px solid #d8e2df;border-radius:16px;padding:24px;color:#0f172a'
  const title = document.createElement('h1')
  title.textContent = "Ecosys didn't load properly"
  title.style.cssText = 'margin:0 0 8px;font-size:20px'
  const text = document.createElement('p')
  text.textContent = 'Try Reload first. If the page stays blank, Reset app clears the copy of the app saved on this device and signs you in again. Changes waiting to sync are kept.'
  text.style.cssText = 'margin:0 0 16px;font-size:14px;line-height:1.5;color:#475569'
  card.append(title, text)
  if (lastError) {
    const detail = document.createElement('p')
    detail.textContent = lastError
    detail.style.cssText = 'margin:0 0 16px;font-size:12px;color:#b91c1c;word-break:break-word'
    card.append(detail)
  }
  const button = (label: string, primary: boolean, onClick: () => void) => {
    const el = document.createElement('button')
    el.type = 'button'
    el.textContent = label
    el.style.cssText = `height:44px;padding:0 16px;margin-right:8px;border-radius:10px;font-size:14px;font-weight:600;border:1px solid ${primary ? '#0f766e' : '#cbd5e1'};background:${primary ? '#0f766e' : '#fff'};color:${primary ? '#fff' : '#0f172a'}`
    el.addEventListener('click', onClick)
    return el
  }
  card.append(button('Reload', true, () => window.location.reload()), button('Reset app', false, () => void resetApp()))
  box.append(card)
  document.body.append(box)
  root.dataset.bootMessage = 'shown'
}

export function installBootGuard(root: HTMLElement): boolean {
  if (new URLSearchParams(window.location.search).has(RESET_PARAM)) {
    void resetApp()
    return false
  }

  window.addEventListener('error', (event) => recordBootError(event.error ?? event.message))
  window.addEventListener('unhandledrejection', (event) => recordBootError(event.reason))

  let emptyChecks = 0
  window.setInterval(() => {
    if (document.visibilityState !== 'visible') return
    if (root.childElementCount > 0) {
      emptyChecks = 0
      document.getElementById('ecosys-boot-message')?.remove()
      return
    }
    emptyChecks += 1
    if (emptyChecks >= EMPTY_CHECKS_BEFORE_MESSAGE) showMessage(root)
  }, CHECK_EVERY_MS)
  return true
}
