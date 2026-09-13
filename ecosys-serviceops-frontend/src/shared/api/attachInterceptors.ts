import axios, { type AxiosInstance, type AxiosResponse, type InternalAxiosRequestConfig } from 'axios'
import toast from 'react-hot-toast'
import { normalizeError } from './apiError'
import { isLicenseWarningExemptPath, setLicenseWarning } from './licenseWarningStore'

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
}

/** True for auth flows (sign-in, sign-up, password reset, token refresh) that handle errors themselves. */
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
    if (token) {
      request.headers.Authorization = `Bearer ${token}`
    }
    return request
  })

  client.interceptors.response.use(
    (response) => {
      trackLicenseWarning(response)
      return response
    },
    async (error: unknown) => {
      trackLicenseWarning(axios.isAxiosError(error) ? error.response : undefined)
      const apiError = normalizeError(error)
      const originalRequest = axios.isAxiosError(error) ? error.config : undefined
      const authRequest = isAuthRequest(originalRequest?.url)
      const hadToken = axios.isAxiosError(error) && Boolean(originalRequest?.headers?.Authorization)

      // Authenticated request rejected as 401 → the access token expired or the session ended.
      if (apiError.status === 401 && hadToken && !authRequest) {
        // Try a single silent refresh before ending the session. `_ecosysRetried` prevents an
        // infinite loop when the retried request 401s again (e.g. the session was revoked).
        const retriableRequest = originalRequest as (typeof originalRequest & { _ecosysRetried?: boolean }) | undefined
        if (config.refresh && retriableRequest && !retriableRequest._ecosysRetried) {
          const newToken = await refreshOnce(config.refresh)
          if (newToken) {
            retriableRequest._ecosysRetried = true
            retriableRequest.headers = retriableRequest.headers ?? {}
            retriableRequest.headers.Authorization = `Bearer ${newToken}`
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

      // Unhandled/background failure → surface a friendly toast so nothing fails silently.
      toast.error(apiError.message)
      return Promise.reject(apiError)
    },
  )
}
