import axios, { type AxiosInstance, type AxiosResponse, type InternalAxiosRequestConfig } from 'axios'
import toast from 'react-hot-toast'
import { normalizeError, type ApiError } from './apiError'
import { isLicenseWarningExemptPath, setLicenseWarning } from './licenseWarningStore'
import { setConnectivityOffline } from './connectivityStore'
import { notifyOnce } from './notifyOnce'

declare module 'axios' {
  interface AxiosRequestConfig {
    /**
     * Skip the global background-failure toast for this one request. For an explicit user-action
     * request (a PDF download, a save, a test action) whose call site already shows its own
     * contextual message on failure — the interceptor's generic toast would otherwise double up
     * with it (Phase 22: choose the most useful single presentation, not several at once).
     */
    suppressGlobalErrorToast?: boolean
  }
}

/**
 * License failures already reach the user via the X-License-Warning response header (set by the
 * same middleware, on the same response, before it throws) → licenseWarningStore → the one global
 * banner. Toasting these too would double-present the identical fact. See
 * TenantLicenseEnforcementMiddleware / LicenseGuardService.EnsureTenantCanMutateAsync on the backend
 * for the only two codes it ever throws.
 */
const LICENSE_ERROR_CODES = new Set(['license_suspended', 'license_read_only'])

export interface AuthInterceptorConfig {
  /** Returns the current bearer token, or null when signed out. */
  getToken: () => string | null
  /** Clears the stored session (called only on genuine session expiry). */
  clearSession: () => void
  /** Notifies the app that the session ended. */
  triggerLogout: () => void
  /** Where to send the user when their session expires. */
  loginPath: string
  /**
   * Attempts a silent token refresh. Resolves to a new bearer token on success, or null when
   * refresh isn't possible (no refresh token, impersonation, or the refresh itself failed).
   */
  refresh?: () => Promise<string | null>
  /**
   * Compares this tab's session with the one other tabs share (see sessionStore.syncWithOtherTabs).
   * A 401 caused by another tab signing in as someone else must not refresh or clear that session.
   */
  syncWithOtherTabs?: () => 'current' | 'refreshed' | 'switched' | 'signedOut'
}

/** True for auth flows (sign-in, sign-up, password reset, token refresh) that handle errors themselves. */
function isSignInRequest(url: string | undefined): boolean {
  return Boolean(url && /\/auth\/(login|signup\/(start|verify))(\?|$)/.test(url))
}

function isAuthRequest(url: string | undefined): boolean {
  return Boolean(url && url.includes('/auth/'))
}

// Single-flight guard: many requests can 401 at once when the access token expires; they must all
// wait on ONE refresh call rather than stampeding the refresh endpoint.
let refreshInFlight: Promise<string | null> | null = null

function refreshOnce(refresh: () => Promise<string | null>): Promise<string | null> {
  refreshInFlight ??= refresh().finally(() => {
    refreshInFlight = null
  })
  return refreshInFlight
}

/**
 * Wires request auth + a single, consistent error pipeline onto an axios client:
 * every rejection becomes an {@link normalizeError ApiError} with a user-safe message,
 * a 401 on an *authenticated* request ends the session, and a 401 on a sign-in
 * attempt is handed back to the form to display inline (no bogus logout/redirect).
 */
/**
 * Tracks the license-health banner from whichever response carries it. Endpoints the backend's
 * license middleware never runs for (auth, platform-admin) carry no header either way and must
 * leave whatever warning is already showing alone — see isLicenseWarningExemptPath.
 */
function trackLicenseWarning(response: AxiosResponse | undefined) {
  if (!response || isLicenseWarningExemptPath(response.config?.url)) {
    return
  }
  setLicenseWarning(response.headers?.['x-license-warning'] as string | undefined)
}

export function attachAuthInterceptors(client: AxiosInstance, config: AuthInterceptorConfig) {
  client.interceptors.request.use((request: InternalAxiosRequestConfig) => {
    const token = config.getToken()
    // Signing in starts a new session: never send the old token with it. A temporary-password
    // token left over from the forced password change is refused for everything but that change,
    // so carrying it here sent the user straight back to the sign-in page.
    if (token && !isSignInRequest(request.url)) {
      request.headers.Authorization = `Bearer ${token}`
    }
    return request
  })

  client.interceptors.response.use(
    (response) => {
      trackLicenseWarning(response)
      // Any response at all, even one we're about to treat as an error elsewhere, proves the
      // server was reachable.
      setConnectivityOffline(false)
      return response
    },
    async (error: unknown) => {
      const axiosResponse = axios.isAxiosError(error) ? error.response : undefined
      trackLicenseWarning(axiosResponse)
      const apiError = normalizeError(error)
      // A transport failure (no response reached the server) is a systemic, single connectivity
      // problem, not a per-request one — see connectivityStore.ts / ConnectivityBanner.
      setConnectivityOffline(apiError.isNetworkError)
      const originalRequest = axios.isAxiosError(error) ? error.config : undefined
      const authRequest = isAuthRequest(originalRequest?.url)
      const hadToken = axios.isAxiosError(error) && Boolean(originalRequest?.headers?.Authorization)

      // Authenticated request rejected as 401 → the access token expired or the session ended.
      if (apiError.status === 401 && hadToken && !authRequest) {
        // Try a single silent refresh before ending the session. `_ecosysRetried` prevents an
        // infinite loop when the retried request 401s again (e.g. the session was revoked).
        const retriableRequest = originalRequest as (typeof originalRequest & { _ecosysRetried?: boolean }) | undefined
        const tabState = config.syncWithOtherTabs?.() ?? 'current'
        if (tabState === 'switched' || tabState === 'signedOut') {
          // Another tab signed in as someone else, or signed out: reload as whoever is signed in now.
          if (typeof window !== 'undefined') {
            window.location.reload()
          }
          return Promise.reject(apiError)
        }
        if (tabState === 'refreshed' && retriableRequest && !retriableRequest._ecosysRetried) {
          retriableRequest._ecosysRetried = true
          retriableRequest.headers = retriableRequest.headers ?? {}
          retriableRequest.headers.Authorization = `Bearer ${config.getToken()}`
          return client(retriableRequest)
        }
        if (config.refresh && retriableRequest && !retriableRequest._ecosysRetried) {
          const newToken = await refreshOnce(config.refresh)
          if (newToken) {
            retriableRequest._ecosysRetried = true
            retriableRequest.headers = retriableRequest.headers ?? {}
            retriableRequest.headers.Authorization = `Bearer ${newToken}`
            return client(retriableRequest)
          }
          // Two tabs refreshing at once: the refresh token rotates, so the slower tab's attempt fails
          // while the faster tab's new tokens are already stored. Use those rather than signing out.
          if (config.syncWithOtherTabs?.() === 'refreshed') {
            retriableRequest._ecosysRetried = true
            retriableRequest.headers = retriableRequest.headers ?? {}
            retriableRequest.headers.Authorization = `Bearer ${config.getToken()}`
            return client(retriableRequest)
          }
        }

        config.clearSession()
        config.triggerLogout()
        if (typeof window !== 'undefined' && window.location.pathname !== config.loginPath) {
          toast.error('Your session has expired. Please sign in again.')
          window.location.assign(config.loginPath)
        }
        return Promise.reject(apiError)
      }

      // Failed sign-in / any auth-flow error → let the form surface it inline.
      if (authRequest || apiError.status === 401) {
        return Promise.reject(apiError)
      }

      if (!originalRequest?.suppressGlobalErrorToast) {
        presentBackgroundFailure(apiError)
      }
      return Promise.reject(apiError)
    },
  )
}

/**
 * Unhandled/background failure → surface a friendly toast so nothing fails silently, but exactly
 * once per underlying cause rather than once per failed request:
 *  - A genuine connectivity failure already has its own single banner (see setConnectivityOffline
 *    above) — no toast at all, or every failing request in an outage would add another.
 *  - A suspended/read-only license already has its own single banner (see trackLicenseWarning
 *    above, fed from the same response) — no toast, so the fact isn't presented twice.
 *  - Everything else keeps a toast, keyed by cause so concurrent identical failures (e.g. a
 *    dashboard's parallel queries all hitting the same dead endpoint) collapse into one.
 */
export function presentBackgroundFailure(apiError: ApiError) {
  if (apiError.isNetworkError) {
    return
  }
  if (apiError.errorCode && LICENSE_ERROR_CODES.has(apiError.errorCode)) {
    return
  }

  const key = apiError.errorCode ?? (apiError.status ? String(apiError.status) : 'unknown')
  notifyOnce(key, () => toast.error(apiError.message, { id: key }))
}
