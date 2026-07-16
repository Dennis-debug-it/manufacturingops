import axios from 'axios'

/**
 * Friendly, user-facing fallback copy per HTTP status, used only when the
 * backend does not supply its own message. Never leak raw framework strings
 * (e.g. axios' "Request failed with status code 401") to users.
 */
const STATUS_FALLBACKS: Record<number, string> = {
  400: "Some of the information provided isn't valid. Please check it and try again.",
  401: "Your session has expired. Please sign in again.",
  403: "You don't have permission to do that.",
  404: "We couldn't find what you were looking for.",
  408: "The request timed out. Please try again.",
  409: "That conflicts with the current state. Please refresh and try again.",
  413: "That upload is too large.",
  422: "Some of the information provided isn't valid. Please check it and try again.",
  429: "Too many attempts. Please wait a moment and try again.",
}

const GENERIC_MESSAGE = "Something went wrong. Please try again."
const SERVER_MESSAGE = "Something went wrong on our end. Please try again in a moment."
const OFFLINE_MESSAGE = "Can't reach the server. Check your connection and try again."
const TIMEOUT_MESSAGE = "The request timed out. Please try again."

export interface ApiErrorOptions {
  status?: number | null
  errorCode?: string | null
  fieldErrors?: string[]
  isNetworkError?: boolean
}

/** Normalised error thrown by the API layer. Its `message` is always safe to show a user. */
export class ApiError extends Error {
  readonly status: number | null
  readonly errorCode: string | null
  readonly fieldErrors: string[]
  readonly isNetworkError: boolean

  constructor(message: string, options: ApiErrorOptions = {}) {
    super(message)
    this.name = 'ApiError'
    this.status = options.status ?? null
    this.errorCode = options.errorCode ?? null
    this.fieldErrors = options.fieldErrors ?? []
    this.isNetworkError = options.isNetworkError ?? false
  }
}

function messageFromBody(data: unknown): string | null {
  if (typeof data === 'string' && data.trim()) return data.trim()
  if (data && typeof data === 'object') {
    const record = data as Record<string, unknown>
    for (const key of ['message', 'detail', 'title'] as const) {
      const value = record[key]
      if (typeof value === 'string' && value.trim()) return value.trim()
    }
  }
  return null
}

function fieldErrorsFromBody(data: unknown): string[] {
  if (!data || typeof data !== 'object') return []
  const errors = (data as Record<string, unknown>).errors
  if (Array.isArray(errors)) {
    return errors.filter((entry): entry is string => typeof entry === 'string')
  }
  // ASP.NET ProblemDetails shape: { errors: { field: string[] } }
  if (errors && typeof errors === 'object') {
    return Object.values(errors as Record<string, unknown>).flatMap((value) =>
      Array.isArray(value) ? value.filter((item): item is string => typeof item === 'string') : [],
    )
  }
  return []
}

function codeFromBody(data: unknown): string | null {
  if (data && typeof data === 'object') {
    const code = (data as Record<string, unknown>).errorCode
    if (typeof code === 'string' && code.trim()) return code
  }
  return null
}

/** Convert any thrown value into an {@link ApiError} whose message is user-safe. */
export function normalizeError(error: unknown): ApiError {
  if (error instanceof ApiError) return error

  if (axios.isAxiosError(error)) {
    // No response at all → network failure or timeout.
    if (!error.response) {
      const isTimeout = error.code === 'ECONNABORTED' || /timeout/i.test(error.message)
      return new ApiError(isTimeout ? TIMEOUT_MESSAGE : OFFLINE_MESSAGE, { isNetworkError: true })
    }

    const status = error.response.status
    const data = error.response.data
    const backendMessage = messageFromBody(data)
    const fallback =
      STATUS_FALLBACKS[status] ?? (status >= 500 ? SERVER_MESSAGE : GENERIC_MESSAGE)

    return new ApiError(backendMessage ?? fallback, {
      status,
      errorCode: codeFromBody(data),
      fieldErrors: fieldErrorsFromBody(data),
    })
  }

  // Anything else (unexpected) — never surface its raw text.
  return new ApiError(GENERIC_MESSAGE)
}

/** The user-safe message for any thrown value. Use this at display sites. */
export function resolveErrorMessage(error: unknown): string {
  return normalizeError(error).message
}

/** The HTTP status for any thrown value, or null when unknown. */
export function getErrorStatus(error: unknown): number | null {
  return normalizeError(error).status
}
