import axios, { type AxiosError, type AxiosInstance, type InternalAxiosRequestConfig } from 'axios'
import toast from 'react-hot-toast'
import { endpoints } from './endpoints'
import { clearSession, getAuthToken, triggerLogout } from './sessionStore'

const defaultBaseUrl = import.meta.env.VITE_API_BASE_URL || 'http://127.0.0.1:5072'

const resolveMessage = (error: unknown) => {
  if (axios.isAxiosError(error)) {
    const data = error.response?.data
    if (typeof data === 'string' && data.trim()) {
      return data
    }
    if (data && typeof data === 'object') {
      const record = data as Record<string, unknown>
      if (typeof record.message === 'string' && record.message.trim()) {
        return record.message
      }
      if (typeof record.title === 'string' && record.title.trim()) {
        return record.title
      }
    }
    if (error.message.trim()) {
      return error.message
    }
  }
  return 'Something went wrong. Please try again.'
}

const attachInterceptors = (client: AxiosInstance) => {
  client.interceptors.request.use((config: InternalAxiosRequestConfig) => {
    const token = getAuthToken()
    if (token) {
      config.headers.Authorization = `Bearer ${token}`
    }
    return config
  })

  client.interceptors.response.use(
    (response) => response,
    (error: AxiosError) => {
      if (error.response?.status === 401) {
        clearSession()
        triggerLogout()
        if (typeof window !== 'undefined' && window.location.pathname !== '/login') {
          window.location.assign('/login')
        }
        return Promise.reject(error)
      }

      toast.error(resolveMessage(error))
      return Promise.reject(error)
    },
  )
}

export const apiClient = axios.create({
  baseURL: defaultBaseUrl,
  headers: {
    Accept: 'application/json',
  },
})

attachInterceptors(apiClient)

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
