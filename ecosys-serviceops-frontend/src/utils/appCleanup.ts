const TRANSIENT_STORAGE_KEYS = [
  'ecosys.serviceops.auth',
  'ecosys.serviceops.session',
  'ecosys.serviceops.role',
  'ecosys.serviceops.tenant',
  'ecosys.serviceops.selected-module',
  'ecosys.serviceops.selectedModule',
  'ecosys.serviceops.selected-tenant',
  'ecosys.serviceops.selectedTenant',
  'ecosys-next-serviceops-store',
] as const

const TRANSIENT_BODY_CLASSES = ['overflow-hidden', 'pointer-events-none'] as const

export const UI_RESET_EVENT = 'ecosys:ui-reset'

function removeStorageKeys(storage: Storage | undefined) {
  if (!storage) return
  TRANSIENT_STORAGE_KEYS.forEach((key) => storage.removeItem(key))
}

export function cleanupBodyInteractivity() {
  if (typeof document === 'undefined') return

  document.body.style.pointerEvents = ''
  document.body.style.overflow = ''
  TRANSIENT_BODY_CLASSES.forEach((className) => document.body.classList.remove(className))
}

export function clearTransientAppState() {
  if (typeof window === 'undefined') return
  removeStorageKeys(window.localStorage)
  removeStorageKeys(window.sessionStorage)
}

export function dispatchUiReset() {
  if (typeof window === 'undefined') return
  window.dispatchEvent(new Event(UI_RESET_EVENT))
}

function describeElement(element: Element | null) {
  if (!(element instanceof HTMLElement)) return 'none'

  const testId = element.dataset.testid ? `[data-testid="${element.dataset.testid}"]` : ''
  const id = element.id ? `#${element.id}` : ''
  const classes = element.className && typeof element.className === 'string'
    ? `.${element.className.trim().split(/\s+/).filter(Boolean).slice(0, 4).join('.')}`
    : ''

  return `${element.tagName.toLowerCase()}${id}${classes}${testId}`
}

function looksLikeBlockingOverlay(element: HTMLElement) {
  const rect = element.getBoundingClientRect()
  const style = window.getComputedStyle(element)
  const coversViewport =
    rect.width >= window.innerWidth * 0.9 &&
    rect.height >= window.innerHeight * 0.9

  return coversViewport && (
    style.position === 'fixed' ||
    style.position === 'absolute' ||
    Number.parseInt(style.zIndex || '0', 10) >= 40
  )
}

export function installInteractionDebugLogger() {
  if (typeof document === 'undefined' || typeof window === 'undefined') {
    return () => undefined
  }

  const enabled =
    import.meta.env.DEV ||
    window.localStorage.getItem('ecosys.debug.interactions') === '1' ||
    window.sessionStorage.getItem('ecosys.debug.interactions') === '1'

  if (!enabled) {
    return () => undefined
  }

  const handlePointerDown = (event: PointerEvent) => {
    const topElement = document.elementFromPoint(event.clientX, event.clientY)
    if (!(topElement instanceof HTMLElement)) return

    const target = event.target instanceof HTMLElement ? event.target : null
    const overlayDetected = looksLikeBlockingOverlay(topElement)

    if (overlayDetected || topElement !== target) {
      console.info('[ecosys-ui-debug] pointer interaction', {
        x: event.clientX,
        y: event.clientY,
        target: describeElement(target),
        topElement: describeElement(topElement),
        targetPointerEvents: target ? window.getComputedStyle(target).pointerEvents : 'unknown',
        topPointerEvents: window.getComputedStyle(topElement).pointerEvents,
        bodyPointerEvents: document.body.style.pointerEvents || 'auto',
        bodyOverflow: document.body.style.overflow || 'auto',
      })
    }
  }

  document.addEventListener('pointerdown', handlePointerDown, true)
  return () => document.removeEventListener('pointerdown', handlePointerDown, true)
}
