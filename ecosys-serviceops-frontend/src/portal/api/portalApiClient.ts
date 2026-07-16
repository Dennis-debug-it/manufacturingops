import axios from 'axios'
import { clearPortalSession, getPortalAuthToken, triggerPortalLogout } from './portalSessionStore'
import { attachAuthInterceptors } from '../../shared/api/attachInterceptors'

const defaultBaseUrl = import.meta.env.VITE_API_BASE_URL || 'http://127.0.0.1:5072'

export const portalApiClient = axios.create({
  baseURL: defaultBaseUrl,
  headers: { Accept: 'application/json' },
})

attachAuthInterceptors(portalApiClient, {
  getToken: getPortalAuthToken,
  clearSession: clearPortalSession,
  triggerLogout: triggerPortalLogout,
  loginPath: '/portal/login',
})

export const portalApi = {
  get: <T>(url: string, params?: Record<string, string | number | boolean | null | undefined>) =>
    portalApiClient.get<T>(url, { params }).then((response) => response.data),
  post: <T>(url: string, data?: unknown) =>
    portalApiClient.post<T>(url, data).then((response) => response.data),
  put: <T>(url: string, data?: unknown) =>
    portalApiClient.put<T>(url, data).then((response) => response.data),
}
