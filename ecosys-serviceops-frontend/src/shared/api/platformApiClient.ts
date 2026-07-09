import axios, { type AxiosError, type AxiosInstance, type InternalAxiosRequestConfig } from 'axios'
import toast from 'react-hot-toast'
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

export const platformApiClient = axios.create({
  baseURL: `${defaultBaseUrl.replace(/\/+$/, '')}/api/platform`,
  headers: {
    Accept: 'application/json',
  },
})

attachInterceptors(platformApiClient)
