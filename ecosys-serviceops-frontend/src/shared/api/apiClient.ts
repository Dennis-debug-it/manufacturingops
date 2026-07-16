import axios from 'axios'
import { endpoints } from './endpoints'
import { clearSession, getAuthToken, triggerLogout } from './sessionStore'
import { attachAuthInterceptors } from './attachInterceptors'

const defaultBaseUrl = import.meta.env.VITE_API_BASE_URL || 'http://127.0.0.1:5072'

export const apiClient = axios.create({
  baseURL: defaultBaseUrl,
  headers: {
    Accept: 'application/json',
  },
})

attachAuthInterceptors(apiClient, {
  getToken: getAuthToken,
  clearSession,
  triggerLogout,
  loginPath: '/login',
})

export const api = {
  get: <T>(url: string, params?: Record<string, string | number | boolean | null | undefined>) =>
    apiClient.get<T>(url, { params }).then((response) => response.data),
  post: <T>(url: string, data?: unknown) =>
    apiClient.post<T>(url, data).then((response) => response.data),
  put: <T>(url: string, data?: unknown) =>
    apiClient.put<T>(url, data).then((response) => response.data),
  patch: <T>(url: string, data?: unknown) =>
    apiClient.patch<T>(url, data).then((response) => response.data),
  delete: <T>(url: string) =>
    apiClient.delete<T>(url).then((response) => response.data),
}

export const authEndpoints = endpoints.auth
