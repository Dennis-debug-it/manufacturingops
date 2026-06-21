const DEFAULT_API_BASE_URL = import.meta.env.DEV ? 'http://localhost:5072' : 'https://app.ecosysdigital.co.ke'
const API_BASE_URL = (import.meta.env.VITE_API_BASE_URL || DEFAULT_API_BASE_URL).replace(/\/+$/, '')
const AUTH_STORAGE_KEY = 'ecosys.serviceops.auth'

type StorageMode = 'local' | 'session'

export class ApiError extends Error {
  status: number
  details?: unknown

  constructor(message: string, status = 500, details?: unknown) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.details = details
  }
}

export interface StoredAuth {
  token: string
  mode: StorageMode
}

type RequestOptions = {
  body?: BodyInit | object | null
  headers?: HeadersInit
  query?: Record<string, string | number | boolean | null | undefined>
  signal?: AbortSignal
}

type DownloadOptions = Omit<RequestOptions, 'body'> & {
  fallbackFileName?: string
}

let unauthorizedHandler: (() => void) | null = null

function hasWindow() {
  return typeof window !== 'undefined'
}

function getStorage(mode: StorageMode) {
  if (!hasWindow()) return null
  return mode === 'session' ? window.sessionStorage : window.localStorage
}

export function getStoredAuth(): StoredAuth | null {
  if (!hasWindow()) return null

  for (const mode of ['local', 'session'] as const) {
    const raw = getStorage(mode)?.getItem(AUTH_STORAGE_KEY)
    if (!raw) continue

    try {
      const parsed = JSON.parse(raw) as Partial<StoredAuth>
      if (typeof parsed.token === 'string' && parsed.token) {
        return { token: parsed.token, mode }
      }
    } catch {
      getStorage(mode)?.removeItem(AUTH_STORAGE_KEY)
    }
  }

  return null
}

export function persistAuthToken(token: string, mode: StorageMode = 'local') {
  if (!hasWindow()) return

  clearStoredAuth()
  getStorage(mode)?.setItem(AUTH_STORAGE_KEY, JSON.stringify({ token, mode }))
}

export function clearStoredAuth() {
  if (!hasWindow()) return
  window.localStorage.removeItem(AUTH_STORAGE_KEY)
  window.sessionStorage.removeItem(AUTH_STORAGE_KEY)
}

export function onUnauthorized(handler: (() => void) | null) {
  unauthorizedHandler = handler
}

function buildUrl(path: string, query?: RequestOptions['query']) {
  const normalizedPath = path.startsWith('/') ? path : `/${path}`
  const url = new URL(`${API_BASE_URL}${normalizedPath}`)

  if (query) {
    for (const [key, value] of Object.entries(query)) {
      if (value === undefined || value === null || value === '') continue
      url.searchParams.set(key, String(value))
    }
  }

  return url.toString()
}

async function parseResponse(response: Response) {
  if (response.status === 204) {
    return null
  }

  const contentType = response.headers.get('content-type') || ''

  if (contentType.includes('application/json')) {
    try {
      return await response.json()
    } catch {
      return null
    }
  }

  const text = await response.text()
  return text || null
}

function resolveErrorMessage(payload: unknown, fallback: string) {
  if (typeof payload === 'string' && payload.trim()) return payload
  if (payload && typeof payload === 'object') {
    const record = payload as Record<string, unknown>
    if (typeof record.message === 'string' && record.message.trim()) return record.message
    if (Array.isArray(record.errors)) {
      const firstError = record.errors.find((value) => typeof value === 'string' && value.trim())
      if (typeof firstError === 'string') return firstError
    }
    if (record.errors && typeof record.errors === 'object') {
      const firstError = Object.values(record.errors as Record<string, unknown[]>)
        .flat()
        .find((value) => typeof value === 'string')
      if (typeof firstError === 'string' && firstError.trim()) return firstError
    }
    if (typeof record.title === 'string' && record.title.trim()) return record.title
  }
  return fallback
}

function resolveStatusFallback(status: number) {
  return status === 400
    ? 'The request could not be processed. Please review the form and try again.'
    : status === 401
      ? 'Your session has expired. Please sign in again.'
      : status === 403
        ? 'You do not have permission to perform this action.'
        : status === 404
          ? 'The requested resource could not be found.'
          : status >= 500
            ? 'The server hit an unexpected problem. Please try again shortly.'
            : `Request failed with status ${status}.`
}

async function request<T>(method: string, path: string, options: RequestOptions = {}): Promise<T> {
  const auth = getStoredAuth()
  const headers = new Headers(options.headers)
  const isFormData = typeof FormData !== 'undefined' && options.body instanceof FormData

  if (!isFormData) {
    headers.set('Accept', 'application/json')
  }

  if (auth?.token) {
    headers.set('Authorization', `Bearer ${auth.token}`)
  }

  let body: BodyInit | undefined
  if (options.body !== undefined && options.body !== null) {
    if (isFormData || typeof options.body === 'string' || options.body instanceof Blob) {
      body = options.body as BodyInit
    } else {
      headers.set('Content-Type', 'application/json')
      body = JSON.stringify(options.body)
    }
  }

  let response: Response

  try {
    response = await fetch(buildUrl(path, options.query), {
      method,
      headers,
      body,
      signal: options.signal,
    })
  } catch (error) {
    if (options.signal?.aborted || (error instanceof Error && error.name === 'AbortError')) {
      throw error
    }

    throw new ApiError(
      'Unable to reach the ServiceOps API. Check that the backend is running and VITE_API_BASE_URL is correct.',
      0,
      error,
    )
  }

  const payload = await parseResponse(response)

  if (response.status === 401) {
    clearStoredAuth()
    unauthorizedHandler?.()
    if (hasWindow() && window.location.pathname !== '/login') {
      window.location.replace('/login')
    }
    throw new ApiError('Your session has expired. Please sign in again.', 401, payload)
  }

  if (!response.ok) {
    throw new ApiError(
      resolveErrorMessage(payload, resolveStatusFallback(response.status)),
      response.status,
      payload,
    )
  }

  return payload as T
}

function getDownloadFileName(response: Response, fallbackFileName: string) {
  const disposition = response.headers.get('content-disposition') || ''
  const utf8Match = disposition.match(/filename\*=UTF-8''([^;]+)/i)
  if (utf8Match?.[1]) {
    return decodeURIComponent(utf8Match[1])
  }

  const quotedMatch = disposition.match(/filename="([^"]+)"/i)
  if (quotedMatch?.[1]) {
    return quotedMatch[1]
  }

  const plainMatch = disposition.match(/filename=([^;]+)/i)
  if (plainMatch?.[1]) {
    return plainMatch[1].trim()
  }

  return fallbackFileName
}

async function requestBlob(path: string, options: DownloadOptions = {}) {
  const auth = getStoredAuth()
  const headers = new Headers(options.headers)
  headers.set('Accept', '*/*')

  if (auth?.token) {
    headers.set('Authorization', `Bearer ${auth.token}`)
  }

  let response: Response
  try {
    response = await fetch(buildUrl(path, options.query), {
      method: 'GET',
      headers,
      signal: options.signal,
    })
  } catch (error) {
    if (options.signal?.aborted || (error instanceof Error && error.name === 'AbortError')) {
      throw error
    }

    throw new ApiError(
      'Unable to reach the ServiceOps API. Check that the backend is running and VITE_API_BASE_URL is correct.',
      0,
      error,
    )
  }

  if (response.status === 401) {
    clearStoredAuth()
    unauthorizedHandler?.()
    if (hasWindow() && window.location.pathname !== '/login') {
      window.location.replace('/login')
    }
    throw new ApiError('Your session has expired. Please sign in again.', 401)
  }

  if (!response.ok) {
    const payload = await parseResponse(response)
    throw new ApiError(
      resolveErrorMessage(payload, resolveStatusFallback(response.status)),
      response.status,
      payload,
    )
  }

  return {
    blob: await response.blob(),
    fileName: getDownloadFileName(response, options.fallbackFileName || 'download'),
  }
}

export const api = {
  baseUrl: API_BASE_URL,
  get: <T>(path: string, options?: Omit<RequestOptions, 'body'>) => request<T>('GET', path, options),
  post: <T>(path: string, body?: RequestOptions['body'], options?: Omit<RequestOptions, 'body'>) =>
    request<T>('POST', path, { ...options, body }),
  postForm: <T>(path: string, formData: FormData, options?: Omit<RequestOptions, 'body'>) =>
    request<T>('POST', path, { ...options, body: formData }),
  put: <T>(path: string, body?: RequestOptions['body'], options?: Omit<RequestOptions, 'body'>) =>
    request<T>('PUT', path, { ...options, body }),
  patch: <T>(path: string, body?: RequestOptions['body'], options?: Omit<RequestOptions, 'body'>) =>
    request<T>('PATCH', path, { ...options, body }),
  delete: <T>(path: string, options?: Omit<RequestOptions, 'body'>) => request<T>('DELETE', path, options),
  download: (path: string, options?: DownloadOptions) => requestBlob(path, options),
}
