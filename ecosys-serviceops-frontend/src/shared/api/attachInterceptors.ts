import axios, { type AxiosInstance, type InternalAxiosRequestConfig } from 'axios'
import toast from 'react-hot-toast'
import { normalizeError } from './apiError'

export interface AuthInterceptorConfig {
  /** Returns the current bearer token, or null when signed out. */
  getToken: () => string | null
  /** Clears the stored session (called only on genuine session expiry). */
  clearSession: () => void
  /** Notifies the app that the session ended. */
  triggerLogout: () => void
  /** Where to send the user when their session expires. */
  loginPath: string
}

/** True for auth flows (sign-in, sign-up, password reset) that show errors inline themselves. */
function isAuthRequest(url: string | undefined): boolean {
  return Boolean(url && url.includes('/auth/'))
}

/**
 * Wires request auth + a single, consistent error pipeline onto an axios client:
 * every rejection becomes an {@link normalizeError ApiError} with a user-safe message,
 * a 401 on an *authenticated* request ends the session, and a 401 on a sign-in
 * attempt is handed back to the form to display inline (no bogus logout/redirect).
 */
export function attachAuthInterceptors(client: AxiosInstance, config: AuthInterceptorConfig) {
  client.interceptors.request.use((request: InternalAxiosRequestConfig) => {
    const token = config.getToken()
    if (token) {
      request.headers.Authorization = `Bearer ${token}`
    }
    return request
  })

  client.interceptors.response.use(
    (response) => response,
    (error: unknown) => {
      const apiError = normalizeError(error)
      const requestUrl = axios.isAxiosError(error) ? error.config?.url : undefined
      const authRequest = isAuthRequest(requestUrl)
      const hadToken = axios.isAxiosError(error) && Boolean(error.config?.headers?.Authorization)

      // Authenticated request rejected as 401 → the session is no longer valid.
      if (apiError.status === 401 && hadToken && !authRequest) {
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
