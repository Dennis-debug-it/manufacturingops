// Tiny external store (not persisted) mirroring whether the last completed request reached the
// server at all. A genuine network failure (no HTTP response — see ApiError.isNetworkError) means
// every in-flight request is likely failing for the same reason, so this is a single connectivity
// state rather than one toast per failed request. Any response at all, success or error, proves the
// server was reached and clears it. Mirrors the shape of licenseWarningStore.ts.

type Listener = () => void

let offline = false
const listeners = new Set<Listener>()

export function setConnectivityOffline(next: boolean) {
  if (next === offline) return
  offline = next
  listeners.forEach((listener) => listener())
}

export function isConnectivityOffline(): boolean {
  return offline
}

export function subscribeConnectivity(listener: Listener): () => void {
  listeners.add(listener)
  return () => listeners.delete(listener)
}
