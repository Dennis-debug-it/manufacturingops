import axios, { type AxiosError, type AxiosInstance, type InternalAxiosRequestConfig } from 'axios'
import toast from 'react-hot-toast'
import { clearPortalSession, getPortalAuthToken, triggerPortalLogout } from './portalSessionStore'

const defaultBaseUrl = import.meta.env.VITE_API_BASE_URL || 'http://127.0.0.1:5072'

const resolveMessage = (error: unknown) => {
  if (axios.isAxiosError(error)) {
    const data = error.response?.data
    if (typeof data === 'string' && data.trim()) return data
    if (data && typeof data === 'object') {
      const record = data as Record<string, unknown>
      if (typeof record.message === 'string' && record.message.trim()) return record.message
      if (typeof record.title === 'string' && record.title.trim()) return record.title
    }
    if (error.message.trim()) return error.message
  }
  return 'Something went wrong. Please try again.'
}

const attachInterceptors = (client: AxiosInstance) => {
  client.interceptors.request.use((config: InternalAxiosRequestConfig) => {
    const token = getPortalAuthToken()
    if (token) {
      config.headers.Authorization = `Bearer ${token}`
    }
    return config
  })

  client.interceptors.response.use(
    (response) => response,
    (error: AxiosError) => {
      if (error.response?.status === 401) {
        clearPortalSession()
        triggerPortalLogout()
        if (typeof window !== 'undefined' && window.location.pathname !== '/portal/login') {
          window.location.assign('/portal/login')
        }
        return Promise.reject(error)
      }

      toast.error(resolveMessage(error))
      return Promise.reject(error)
    },
  )
}

export const portalApiClient = axios.create({
  baseURL: defaultBaseUrl,
  headers: { Accept: 'application/json' },
})

attachInterceptors(portalApiClient)

export const portalApi = {
  get: <T>(url: string, params?: Record<string, string | number | boolean | null | undefined>) =>
    portalApiClient.get<T>(url, { params }).then((response) => response.data),
  post: <T>(url: string, data?: unknown) =>
    portalApiClient.post<T>(url, data).then((response) => response.data),
  put: <T>(url: string, data?: unknown) =>
    portalApiClient.put<T>(url, data).then((response) => response.data),
}
