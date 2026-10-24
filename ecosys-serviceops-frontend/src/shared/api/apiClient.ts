import axios, { type AxiosRequestConfig } from 'axios'
import { endpoints } from './endpoints'
import {
  clearSession,
  getAuthToken,
  getImpersonationSession,
  getRefreshToken,
  getSession,
  setAuthToken,
  setRefreshToken,
  setSession,
  syncWithOtherTabs,
  triggerLogout,
} from './sessionStore'
import { attachAuthInterceptors } from './attachInterceptors'
import type { LoginResponse } from '../types/auth'

export const apiBaseUrl = import.meta.env.VITE_API_BASE_URL || 'http://127.0.0.1:5072'
const defaultBaseUrl = apiBaseUrl

export const apiClient = axios.create({
  baseURL: defaultBaseUrl,
  headers: {
    Accept: 'application/json',
  },
})

/**
 * Exchanges the stored refresh token for a fresh access token. Uses a bare axios call (not
 * apiClient) so it never re-enters the auth interceptors. Returns the new access token, or null
 * when refresh isn't possible — impersonation sessions have no refresh token, and a revoked or
 * expired refresh token yields a 401 that ends the session.
 */
async function refreshAccessToken(): Promise<string | null> {
  if (getImpersonationSession()) {
    return null
  }
  const refreshToken = getRefreshToken()
  if (!refreshToken) {
    return null
  }
  try {
    const { data } = await axios.post<LoginResponse>(
      `${defaultBaseUrl}${endpoints.auth.refresh}`,
      { refreshToken },
      { headers: { Accept: 'application/json' } },
    )
    setAuthToken(data.token)
    setRefreshToken(data.refreshToken)
    const stored = getSession()
    if (stored) {
      setSession({ ...stored, token: data.token })
    }
    return data.token
  } catch {
    return null
  }
}

attachAuthInterceptors(apiClient, {
  getToken: getAuthToken,
  clearSession,
  triggerLogout,
  loginPath: '/login',
  refresh: refreshAccessToken,
  syncWithOtherTabs,
})

export const api = {
  get: <T>(url: string, params?: Record<string, string | number | boolean | null | undefined>, config?: AxiosRequestConfig) =>
    apiClient.get<T>(url, { ...config, params }).then((response) => response.data),
  post: <T>(url: string, data?: unknown, config?: AxiosRequestConfig) =>
    apiClient.post<T>(url, data, config).then((response) => response.data),
  put: <T>(url: string, data?: unknown, config?: AxiosRequestConfig) =>
    apiClient.put<T>(url, data, config).then((response) => response.data),
  patch: <T>(url: string, data?: unknown, config?: AxiosRequestConfig) =>
    apiClient.patch<T>(url, data, config).then((response) => response.data),
  delete: <T>(url: string, config?: AxiosRequestConfig) =>
    apiClient.delete<T>(url, config).then((response) => response.data),
}

export const authEndpoints = endpoints.auth
