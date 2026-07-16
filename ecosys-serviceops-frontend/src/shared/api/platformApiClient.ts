import axios from 'axios'
import { clearSession, getAuthToken, triggerLogout } from './sessionStore'
import { attachAuthInterceptors } from './attachInterceptors'

const defaultBaseUrl = import.meta.env.VITE_API_BASE_URL || 'http://127.0.0.1:5072'

export const platformApiClient = axios.create({
  baseURL: `${defaultBaseUrl.replace(/\/+$/, '')}/api/platform`,
  headers: {
    Accept: 'application/json',
  },
})

attachAuthInterceptors(platformApiClient, {
  getToken: getAuthToken,
  clearSession,
  triggerLogout,
  loginPath: '/login',
})
