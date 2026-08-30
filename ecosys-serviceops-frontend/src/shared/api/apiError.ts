import axios from 'axios'
import toast from 'react-hot-toast'
import type { FieldValues, Path, UseFormSetError } from 'react-hook-form'

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
  fieldErrorMap?: Record<string, string[]>
  isNetworkError?: boolean
}

/** Normalised error thrown by the API layer. Its `message` is always safe to show a user. */
export class ApiError extends Error {
  readonly status: number | null
  readonly errorCode: string | null
  /** Every validation message the server returned, flattened and in order. */
  readonly fieldErrors: string[]
  /**
   * Validation messages still keyed by the field the server blamed, when it supplied keys at all.
   * See {@link applyApiFieldErrors} for which response shapes actually populate this.
   */
  readonly fieldErrorMap: Record<string, string[]>
  readonly isNetworkError: boolean

  constructor(message: string, options: ApiErrorOptions = {}) {
    super(message)
    this.name = 'ApiError'
    this.status = options.status ?? null
    this.errorCode = options.errorCode ?? null
    this.fieldErrors = options.fieldErrors ?? []
    this.fieldErrorMap = options.fieldErrorMap ?? {}
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

/**
 * The keyed half of the same `errors` payload. Only the ASP.NET `ValidationProblemDetails` shape
 * (`{ errors: { field: string[] } }`) carries keys; this API's own model-validation envelope sends a
 * flat `string[]`, which yields an empty map here and is recovered field-by-field in
 * {@link applyApiFieldErrors}.
 */
function fieldErrorMapFromBody(data: unknown): Record<string, string[]> {
  if (!data || typeof data !== 'object') return {}
  const errors = (data as Record<string, unknown>).errors
  if (!errors || typeof errors !== 'object' || Array.isArray(errors)) return {}

  const map: Record<string, string[]> = {}
  for (const [key, value] of Object.entries(errors as Record<string, unknown>)) {
    const messages = Array.isArray(value)
      ? value.filter((item): item is string => typeof item === 'string')
      : typeof value === 'string'
        ? [value]
        : []
    if (messages.length > 0) {
      map[key] = messages
    }
  }
  return map
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
      fieldErrorMap: fieldErrorMapFromBody(data),
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

/**
 * Default DataAnnotations message templates, reduced to the capture of `{0}` — the display name,
 * which for this API is always the DTO property name because no `[Required(ErrorMessage = …)]`
 * override exists anywhere in the backend. These recover the field from a message such as
 * "The ItemCode field is required." or "The field UnitCost must be between 0 and 100."
 */
const FIELD_NAME_PATTERNS: readonly RegExp[] = [
  /^the\s+(.+?)\s+field\s+is\b/i,
  /^the\s+field\s+(.+?)\s+must\b/i,
]

/** `Payload.ItemCode` / `$.itemCode` / `Item Code` all collapse to `itemcode`. */
function normalizeFieldKey(raw: string): string {
  const lastSegment = raw.split('.').pop() ?? raw
  return lastSegment.replace(/[^a-z0-9]/gi, '').toLowerCase()
}

function fieldNameFromMessage(message: string): string | null {
  for (const pattern of FIELD_NAME_PATTERNS) {
    const match = pattern.exec(message.trim())
    const captured = match?.[1]
    if (captured) {
      return captured.replace(/^['"]|['"]$/g, '')
    }
  }
  return null
}

export interface ApplyApiFieldErrorsOptions<TFieldValues extends FieldValues> {
  /** `setError` from the form's `useForm()`. */
  setError: UseFormSetError<TFieldValues>
  /**
   * The form's own field names. A server message is only attached when it names one of these —
   * anything else stays with the toast rather than being pinned to the wrong input.
   */
  fields: readonly Path<TFieldValues>[]
  /** Move focus to the first field the server rejected. Defaults to true. */
  shouldFocus?: boolean
}

/**
 * The message the API interceptor has already toasted for this error. Re-toasting it here would
 * double it up, so it is the one message the leftover sweep below deliberately skips.
 */
function alreadyToasted(apiError: ApiError, message: string): boolean {
  return message.trim() === apiError.message.trim()
}

/**
 * Applies a caught API error's per-field validation messages to a react-hook-form.
 *
 * Which envelope actually carries field-level detail, of the three this API returns:
 * - `{ type, title, status, traceId }` — RFC-9110 ProblemDetails on 404/500. **No field detail.**
 * - `{ message, errorCode }` — business-rule 400 (`BUSINESS_RULE_VIOLATION`). **No field detail.**
 * - `{ message, errorCode, errors[] }` — model-validation 400 (`VALIDATION_ERROR`). **This is the
 *   only one.** Note `errors` is a flat `string[]`: the server builds it from `ModelState.Values`
 *   and drops the keys, so the field survives only inside the message text and is recovered here
 *   from the default DataAnnotations templates. The keyed `ValidationProblemDetails` variant
 *   (`errors: { field: string[] }`) is handled first for robustness, but this backend never emits it.
 *
 * Returns true when at least one message was pinned to a field. Nothing is ever dropped: a message
 * naming a field this form does not have — or naming no field at all — is toasted instead, unless
 * it is the very message the API interceptor already toasted.
 */
export function applyApiFieldErrors<TFieldValues extends FieldValues>(
  error: unknown,
  { setError, fields, shouldFocus = true }: ApplyApiFieldErrorsOptions<TFieldValues>,
): boolean {
  const apiError = normalizeError(error)
  if (apiError.status !== 400 && apiError.status !== 422) {
    return false
  }

  const byNormalizedName = new Map<string, Path<TFieldValues>>()
  for (const field of fields) {
    byNormalizedName.set(normalizeFieldKey(field), field)
  }

  const keyedEntries = Object.entries(apiError.fieldErrorMap)
  // `[blamed field or null, message]` for every validation message the server sent.
  const entries: [string | null, string][] =
    keyedEntries.length > 0
      ? keyedEntries.flatMap(([key, messages]) => messages.map((message): [string | null, string] => [key, message]))
      : apiError.fieldErrors.map((message): [string | null, string] => [fieldNameFromMessage(message), message])

  const applied = new Set<string>()
  const unplaced: string[] = []
  for (const [name, message] of entries) {
    const field = name ? byNormalizedName.get(normalizeFieldKey(name)) : undefined
    if (!field || applied.has(field)) {
      // Either the server blamed something this form doesn't render, or it blamed the same field
      // twice. Both still have to reach the user rather than disappearing.
      if (!alreadyToasted(apiError, message) && !unplaced.includes(message)) {
        unplaced.push(message)
      }
      continue
    }
    setError(field, { type: 'server', message }, { shouldFocus: shouldFocus && applied.size === 0 })
    applied.add(field)
  }

  for (const message of unplaced) {
    toast.error(message)
  }

  return applied.size > 0
}
