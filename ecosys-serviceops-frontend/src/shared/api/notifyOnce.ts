// Collapses repeated calls for the same underlying cause into one. Many parallel requests can fail
// for the same reason at once (a suspended license, a dead backend, a shared 500); without this,
// each one independently presents its own toast. Same single-flight-by-key shape as refreshOnce()
// in attachInterceptors.ts, just for presentation instead of a network call.

const lastFiredAt = new Map<string, number>()

/** Runs `fn` unless `key` already fired within `cooldownMs` (default 4s — matches the Toaster duration). */
export function notifyOnce(key: string, fn: () => void, cooldownMs = 4000) {
  const now = Date.now()
  const last = lastFiredAt.get(key)
  if (last !== undefined && now - last < cooldownMs) {
    return
  }
  lastFiredAt.set(key, now)
  fn()
}
